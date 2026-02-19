using Microsoft.AspNetCore.SignalR;
using MultiplayerServer.Application.Matches;
using MultiplayerServer.Contracts.V1;
using MultiplayerServer.Transport.V1;

namespace MultiplayerServer.Hubs.V1;

public sealed class V1MatchSyncPublisher : IMatchSyncPublisher
{
    private readonly IHubContext<MatchHub> _hubContext;
    private readonly IMatchSyncSequencer _sequencer;
    private readonly ILogger<V1MatchSyncPublisher> _logger;

    public V1MatchSyncPublisher(
        IHubContext<MatchHub> hubContext,
        IMatchSyncSequencer sequencer,
        ILogger<V1MatchSyncPublisher> logger)
    {
        _hubContext = hubContext;
        _sequencer = sequencer;
        _logger = logger;
    }

    public async Task PublishMatchUpdatedAsync(
        MatchSnapshot snapshot,
        string eventId,
        CancellationToken cancellationToken)
    {
        if (!TryCreateMetadata(snapshot.MatchId, eventId, out var metadata))
        {
            return;
        }

        var payload = new MatchUpdatedSyncEvent(
            MatchProtocolConstants.EventMatchUpdated,
            metadata,
            MatchContractMapper.ToContractSnapshot(snapshot));

        await _hubContext.Clients
            .Group(MatchHubGroupNames.ForMatch(snapshot.MatchId))
            .SendAsync(MatchProtocolConstants.EventMatchUpdated, payload, cancellationToken);
    }

    public async Task PublishMatchSnapshotToConnectionAsync(
        string connectionId,
        MatchSnapshot snapshot,
        string eventId,
        CancellationToken cancellationToken)
    {
        if (!TryCreateMetadata(snapshot.MatchId, eventId, out var metadata))
        {
            return;
        }

        var payload = new MatchSnapshotSyncEvent(
            MatchProtocolConstants.EventMatchSnapshot,
            metadata,
            MatchContractMapper.ToContractSnapshot(snapshot));

        await _hubContext.Clients
            .Client(connectionId)
            .SendAsync(MatchProtocolConstants.EventMatchSnapshot, payload, cancellationToken);
    }

    public async Task PublishTransportErrorToConnectionAsync(
        string connectionId,
        string matchId,
        string eventId,
        string code,
        string message,
        CancellationToken cancellationToken)
    {
        if (!TryCreateMetadata(matchId, eventId, out var metadata))
        {
            return;
        }

        var payload = new MatchErrorSyncEvent(
            MatchProtocolConstants.EventMatchError,
            metadata,
            code,
            message);

        await _hubContext.Clients
            .Client(connectionId)
            .SendAsync(MatchProtocolConstants.EventMatchError, payload, cancellationToken);
    }

    private bool TryCreateMetadata(
        string matchId,
        string eventId,
        out MatchSyncEventMetadata metadata)
    {
        metadata = default!;
        if (!_sequencer.TryReserve(matchId, eventId, out var sequence))
        {
            _logger.LogInformation(
                "Duplicate transport event suppressed for match {MatchId}; event {EventId}",
                matchId,
                eventId);
            return false;
        }

        metadata = new MatchSyncEventMetadata(
            matchId,
            eventId,
            sequence,
            DateTimeOffset.UtcNow);
        return true;
    }
}
