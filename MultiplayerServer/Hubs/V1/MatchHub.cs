using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MultiplayerServer.Application.Matches;
using MultiplayerServer.Contracts.V1;

namespace MultiplayerServer.Hubs.V1;

// Reserved v1 hub route. Match events are introduced in MS-004.
[Authorize]
public sealed class MatchHub : Hub
{
    private readonly IGetMatchSnapshotUseCase _snapshotUseCase;
    private readonly IReconnectMatchUseCase _reconnectUseCase;
    private readonly IDisconnectMatchUseCase _disconnectUseCase;
    private readonly IMatchConnectionRegistry _connectionRegistry;
    private readonly IMatchSyncDispatchGate _dispatchGate;
    private readonly IMatchSyncPublisher _syncPublisher;
    private readonly IMatchSyncEventIdGenerator _eventIdGenerator;
    private readonly ILogger<MatchHub> _logger;

    public MatchHub(
        IGetMatchSnapshotUseCase snapshotUseCase,
        IReconnectMatchUseCase reconnectUseCase,
        IDisconnectMatchUseCase disconnectUseCase,
        IMatchConnectionRegistry connectionRegistry,
        IMatchSyncDispatchGate dispatchGate,
        IMatchSyncPublisher syncPublisher,
        IMatchSyncEventIdGenerator eventIdGenerator,
        ILogger<MatchHub> logger)
    {
        _snapshotUseCase = snapshotUseCase;
        _reconnectUseCase = reconnectUseCase;
        _disconnectUseCase = disconnectUseCase;
        _connectionRegistry = connectionRegistry;
        _dispatchGate = dispatchGate;
        _syncPublisher = syncPublisher;
        _eventIdGenerator = eventIdGenerator;
        _logger = logger;
    }

    public async Task SubscribeMatch(string? matchId, string? playerToken)
    {
        var normalizedMatchId = NormalizeRequiredMatchId(matchId);
        var normalizedPlayerToken = NormalizeRequiredPlayerToken(playerToken);
        await EnsureConnectionTokenMatchesRequestedTokenAsync(normalizedPlayerToken, normalizedMatchId);

        await using var dispatchLease = await _dispatchGate.AcquireAsync(normalizedMatchId, Context.ConnectionAborted);
        var reconnectOutcome = _reconnectUseCase.ReconnectMatch(normalizedMatchId, normalizedPlayerToken);
        switch (reconnectOutcome)
        {
            case ReconnectMatchSucceeded { Response: { Snapshot: { } snapshot } response }:
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    MatchHubGroupNames.ForMatch(normalizedMatchId),
                    Context.ConnectionAborted);
                _connectionRegistry.AddSubscription(Context.ConnectionId, normalizedMatchId, normalizedPlayerToken);
                await _syncPublisher.PublishMatchSnapshotToConnectionAsync(
                    Context.ConnectionId,
                    snapshot,
                    _eventIdGenerator.Generate(),
                    Context.ConnectionAborted);

                if (response.PresenceChanged)
                {
                    await _syncPublisher.PublishMatchPresenceChangedAsync(
                        snapshot,
                        response.Seat,
                        _eventIdGenerator.Generate(),
                        Context.ConnectionAborted);
                }

                _logger.LogInformation(
                    "Connection {ConnectionId} subscribed to realtime match {MatchId}",
                    Context.ConnectionId,
                    normalizedMatchId);
                return;
            case ReconnectMatchSucceeded:
                throw new InvalidOperationException("Reconnect success outcome must include a snapshot payload.");
            case ReconnectMatchFailed { Error: { } error }:
                await PublishTransportErrorAsync(normalizedMatchId, error.Code, error.Message);
                throw new HubException(BuildHubExceptionMessage(error.Code, error.Message));
            case ReconnectMatchFailed:
                throw new InvalidOperationException("Reconnect failure outcome must include an error payload.");
            default:
                throw new InvalidOperationException($"Unsupported reconnect outcome type: {reconnectOutcome.GetType().Name}");
        }
    }

    public async Task UnsubscribeMatch(string? matchId)
    {
        var normalizedMatchId = NormalizeRequiredMatchId(matchId);
        await using var dispatchLease = await _dispatchGate.AcquireAsync(normalizedMatchId, Context.ConnectionAborted);

        var removed = _connectionRegistry.RemoveSubscription(Context.ConnectionId, normalizedMatchId);
        if (removed is null)
        {
            await PublishTransportErrorAsync(
                normalizedMatchId,
                MatchProtocolConstants.ErrorTransportNotSubscribed,
                "Connection is not subscribed to this match.");
            throw new HubException(
                BuildHubExceptionMessage(
                    MatchProtocolConstants.ErrorTransportNotSubscribed,
                    "Connection is not subscribed to this match."));
        }

        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            MatchHubGroupNames.ForMatch(normalizedMatchId),
            Context.ConnectionAborted);

        _logger.LogInformation(
            "Connection {ConnectionId} unsubscribed from realtime match {MatchId}",
            Context.ConnectionId,
            normalizedMatchId);
    }

    public async Task RequestResync(string? matchId, string? playerToken)
    {
        var normalizedMatchId = NormalizeRequiredMatchId(matchId);
        var normalizedPlayerToken = NormalizeRequiredPlayerToken(playerToken);
        await EnsureConnectionTokenMatchesRequestedTokenAsync(normalizedPlayerToken, normalizedMatchId);

        if (!_connectionRegistry.IsSubscribed(Context.ConnectionId, normalizedMatchId))
        {
            await PublishTransportErrorAsync(
                normalizedMatchId,
                MatchProtocolConstants.ErrorTransportNotSubscribed,
                "Subscribe to the match before requesting a resync.");
            throw new HubException(
                BuildHubExceptionMessage(
                    MatchProtocolConstants.ErrorTransportNotSubscribed,
                    "Subscribe to the match before requesting a resync."));
        }

        await using var dispatchLease = await _dispatchGate.AcquireAsync(normalizedMatchId, Context.ConnectionAborted);
        var snapshotOutcome = _snapshotUseCase.GetMatchSnapshot(normalizedMatchId, normalizedPlayerToken);
        switch (snapshotOutcome)
        {
            case GetMatchSnapshotSucceeded { Response: { Snapshot: { } snapshot } }:
                await _syncPublisher.PublishMatchSnapshotToConnectionAsync(
                    Context.ConnectionId,
                    snapshot,
                    _eventIdGenerator.Generate(),
                    Context.ConnectionAborted);
                _logger.LogInformation(
                    "Connection {ConnectionId} requested resync for {MatchId}",
                    Context.ConnectionId,
                    normalizedMatchId);
                return;
            case GetMatchSnapshotSucceeded:
                throw new InvalidOperationException("Snapshot success outcome must include a snapshot payload.");
            case GetMatchSnapshotFailed { Error: { } error }:
                await PublishTransportErrorAsync(normalizedMatchId, error.Code, error.Message);
                throw new HubException(BuildHubExceptionMessage(error.Code, error.Message));
            case GetMatchSnapshotFailed:
                throw new InvalidOperationException("Snapshot failure outcome must include an error payload.");
            default:
                throw new InvalidOperationException($"Unsupported snapshot outcome type: {snapshotOutcome.GetType().Name}");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var subscriptions = _connectionRegistry.RemoveConnection(Context.ConnectionId);
        foreach (var subscription in subscriptions)
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                MatchHubGroupNames.ForMatch(subscription.MatchId),
                CancellationToken.None);

            await using var dispatchLease = await _dispatchGate.AcquireAsync(subscription.MatchId, CancellationToken.None);
            var disconnectOutcome = _disconnectUseCase.DisconnectMatch(subscription.MatchId, subscription.PlayerToken);
            await PublishPresenceChangeIfNeededAsync(disconnectOutcome, CancellationToken.None);
        }

        if (exception is not null)
        {
            _logger.LogWarning(
                exception,
                "Connection {ConnectionId} disconnected from realtime transport with an error.",
                Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation(
                "Connection {ConnectionId} disconnected from realtime transport.",
                Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private async Task PublishPresenceChangeIfNeededAsync(DisconnectMatchOutcome outcome, CancellationToken cancellationToken)
    {
        switch (outcome)
        {
            case DisconnectMatchSucceeded { Response: { PresenceChanged: true, Snapshot: { } snapshot, Seat: { } seat } }:
                await _syncPublisher.PublishMatchPresenceChangedAsync(
                    snapshot,
                    seat,
                    _eventIdGenerator.Generate(),
                    cancellationToken);
                return;
            case DisconnectMatchFailed { Error: { } error }:
                _logger.LogInformation(
                    "Disconnect registration did not change presence: {Code}",
                    error.Code);
                return;
            default:
                return;
        }
    }

    private async Task PublishTransportErrorAsync(string matchId, string code, string message)
    {
        await _syncPublisher.PublishTransportErrorToConnectionAsync(
            Context.ConnectionId,
            matchId,
            _eventIdGenerator.Generate(),
            code,
            message,
            Context.ConnectionAborted);
    }

    private string NormalizeRequiredMatchId(string? matchId)
    {
        if (string.IsNullOrWhiteSpace(matchId))
        {
            throw new HubException(
                BuildHubExceptionMessage(
                    MatchErrorCodes.MatchIdRequired,
                    "matchId is required."));
        }

        var normalized = matchId.Trim();
        if (!IsHex32(normalized))
        {
            throw new HubException(
                BuildHubExceptionMessage(
                    MatchErrorCodes.InvalidMatchIdFormat,
                    "matchId has invalid format."));
        }

        return normalized;
    }

    private string NormalizeRequiredPlayerToken(string? playerToken)
    {
        if (string.IsNullOrWhiteSpace(playerToken))
        {
            throw new HubException(
                BuildHubExceptionMessage(
                    MatchErrorCodes.PlayerTokenRequired,
                    "playerToken is required."));
        }

        var normalized = playerToken.Trim();
        if (!IsHex32(normalized))
        {
            throw new HubException(
                BuildHubExceptionMessage(
                    MatchErrorCodes.InvalidPlayerTokenFormat,
                    "playerToken has invalid format."));
        }

        return normalized;
    }

    private async Task EnsureConnectionTokenMatchesRequestedTokenAsync(string requestedPlayerToken, string matchId)
    {
        var authenticatedPlayerToken = Context.User?.FindFirst(PlayerTokenAuthenticationDefaults.PlayerTokenClaimType)?.Value;
        if (string.IsNullOrWhiteSpace(authenticatedPlayerToken) ||
            !string.Equals(authenticatedPlayerToken.Trim(), requestedPlayerToken, StringComparison.OrdinalIgnoreCase))
        {
            await PublishTransportErrorAsync(
                matchId,
                MatchProtocolConstants.ErrorUnauthorizedResume,
                "Connection token does not match requested playerToken.");
            throw new HubException(
                BuildHubExceptionMessage(
                    MatchProtocolConstants.ErrorUnauthorizedResume,
                    $"Connection is not authorized for realtime match access to {matchId}."));
        }
    }

    private static bool IsHex32(string value)
    {
        if (value.Length != 32)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!Uri.IsHexDigit(character))
            {
                return false;
            }
        }

        return true;
    }

    private static string BuildHubExceptionMessage(string code, string message)
    {
        return $"{code}:{message}";
    }
}
