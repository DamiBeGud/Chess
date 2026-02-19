using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MultiplayerServer.Application.Matches;

public sealed class InMemoryMatchLifecycleService : IMatchLifecycleService, IDisposable
{
    private readonly IMatchRepository _repository;
    private readonly IMatchIdGenerator _matchIdGenerator;
    private readonly IJoinCodeGenerator _joinCodeGenerator;
    private readonly IPlayerTokenGenerator _playerTokenGenerator;
    private readonly IChessRulesEngine _rulesEngine;
    private readonly IMatchSnapshotFactory _snapshotFactory;
    private readonly IMatchClock _clock;
    private readonly IDisconnectGraceScheduler _disconnectGraceScheduler;
    private readonly IMatchLifecycleEventPublisher _lifecycleEventPublisher;
    private readonly MatchDisconnectPolicyOptions _disconnectPolicy;
    private readonly ILogger<InMemoryMatchLifecycleService> _logger;

    public InMemoryMatchLifecycleService()
        : this(
            new InMemoryMatchRepository(),
            new GuidMatchIdGenerator(),
            new RandomJoinCodeGenerator(),
            new RandomPlayerTokenGenerator(),
            new ClassicChessRulesEngine(),
            new MatchSnapshotFactory(),
            Options.Create(new MatchDisconnectPolicyOptions()),
            new SystemMatchClock(),
            new InMemoryDisconnectGraceScheduler(
                new SystemMatchClock(),
                NullLogger<InMemoryDisconnectGraceScheduler>.Instance),
            new NullMatchLifecycleEventPublisher(),
            null)
    {
    }

    public InMemoryMatchLifecycleService(
        IMatchRepository repository,
        IMatchIdGenerator matchIdGenerator,
        IJoinCodeGenerator joinCodeGenerator,
        IPlayerTokenGenerator playerTokenGenerator,
        IChessRulesEngine rulesEngine,
        IMatchSnapshotFactory snapshotFactory,
        IOptions<MatchDisconnectPolicyOptions>? disconnectPolicyOptions = null,
        IMatchClock? clock = null,
        IDisconnectGraceScheduler? disconnectGraceScheduler = null,
        IMatchLifecycleEventPublisher? lifecycleEventPublisher = null,
        ILogger<InMemoryMatchLifecycleService>? logger = null)
    {
        _repository = repository;
        _matchIdGenerator = matchIdGenerator;
        _joinCodeGenerator = joinCodeGenerator;
        _playerTokenGenerator = playerTokenGenerator;
        _rulesEngine = rulesEngine;
        _snapshotFactory = snapshotFactory;
        _disconnectPolicy = disconnectPolicyOptions?.Value ?? new MatchDisconnectPolicyOptions();
        _clock = clock ?? new SystemMatchClock();
        _disconnectGraceScheduler = disconnectGraceScheduler ?? new InMemoryDisconnectGraceScheduler(
            _clock,
            NullLogger<InMemoryDisconnectGraceScheduler>.Instance);
        _lifecycleEventPublisher = lifecycleEventPublisher ?? new NullMatchLifecycleEventPublisher();
        _logger = logger ?? NullLogger<InMemoryMatchLifecycleService>.Instance;

        if (_disconnectPolicy.DisconnectGracePeriodSeconds < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(disconnectPolicyOptions),
                "DisconnectGracePeriodSeconds must be at least 1 second.");
        }
    }

    public CreateMatchResult CreateMatch()
    {
        var matchId = _matchIdGenerator.Generate();
        var joinCode = GenerateUniqueJoinCode();
        var creatorToken = _playerTokenGenerator.Generate();

        _repository.Add(
            new MatchState
            {
                MatchId = matchId,
                JoinCode = joinCode,
                CreatorToken = creatorToken,
                SideToMove = MatchSeats.Creator,
                MoveNumber = 1,
                Board = CreateInitialBoard(),
                WhiteCanCastleKingSide = true,
                WhiteCanCastleQueenSide = true,
                BlackCanCastleKingSide = true,
                BlackCanCastleQueenSide = true,
                EnPassantTarget = null,
                CreatorConnected = true,
                JoinerConnected = false,
                CreatorDisconnectedUtc = null,
                JoinerDisconnectedUtc = null,
                CreatorGraceExpiresUtc = null,
                JoinerGraceExpiresUtc = null,
                Status = MatchStatuses.InProgress,
                Resolution = null,
                WinnerSeat = null,
                EndedUtc = null
            });

        _logger.LogInformation("Match created {MatchId}", matchId);
        return new CreateMatchResult(matchId, joinCode, creatorToken);
    }

    public JoinMatchOutcome JoinMatch(string? joinCode)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            _logger.LogWarning("Join rejected with {ErrorCode}", MatchErrorCodes.JoinCodeRequired);
            return new JoinMatchFailed(new JoinMatchFailure(MatchErrorCodes.JoinCodeRequired, "joinCode is required."));
        }

        var normalizedJoinCode = joinCode.Trim().ToUpperInvariant();
        return _repository.WithMatchByJoinCode<JoinMatchOutcome>(
            normalizedJoinCode,
            match =>
            {
                if (match is null)
                {
                    _logger.LogWarning("Join rejected with {ErrorCode}", MatchErrorCodes.MatchNotFound);
                    return new JoinMatchFailed(new JoinMatchFailure(MatchErrorCodes.MatchNotFound, "Match was not found."));
                }

                if (match.JoinerToken is not null)
                {
                    _logger.LogWarning(
                        "Join rejected for {MatchId} with {ErrorCode}",
                        match.MatchId,
                        MatchErrorCodes.MatchFull);
                    return new JoinMatchFailed(new JoinMatchFailure(MatchErrorCodes.MatchFull, "Match already has two players."));
                }

                if (string.Equals(match.Status, MatchStatuses.Ended, StringComparison.Ordinal))
                {
                    return new JoinMatchFailed(
                        new JoinMatchFailure(
                            MatchErrorCodes.MatchAlreadyEnded,
                            "Match has already ended."));
                }

                var joinerToken = _playerTokenGenerator.Generate();
                match.JoinerToken = joinerToken;
                match.JoinerConnected = true;
                match.JoinerDisconnectedUtc = null;
                match.JoinerGraceExpiresUtc = null;

                _logger.LogInformation(
                    "Join accepted for {MatchId}; assigned {Seat}",
                    match.MatchId,
                    MatchSeats.Joiner);

                // Deterministic seat assignment (MS-002): creator is White, first joiner is Black.
                return new JoinMatchSucceeded(new JoinMatchSuccess(match.MatchId, MatchSeats.Joiner, joinerToken));
            });
    }

    public GetMatchSnapshotOutcome GetMatchSnapshot(string? matchId, string? playerToken)
    {
        if (string.IsNullOrWhiteSpace(matchId))
        {
            return new GetMatchSnapshotFailed(
                new GetMatchSnapshotFailure(
                    MatchErrorCodes.MatchIdRequired,
                    "matchId is required."));
        }

        if (string.IsNullOrWhiteSpace(playerToken))
        {
            return new GetMatchSnapshotFailed(
                new GetMatchSnapshotFailure(
                    MatchErrorCodes.PlayerTokenRequired,
                    "playerToken is required."));
        }

        var normalizedMatchId = matchId.Trim();
        var normalizedPlayerToken = playerToken.Trim();

        return _repository.WithMatchById<GetMatchSnapshotOutcome>(
            normalizedMatchId,
            match =>
            {
                if (match is null)
                {
                    _logger.LogWarning(
                        "Snapshot rejected with {ErrorCode}; match {MatchId} not found",
                        MatchErrorCodes.MatchNotFound,
                        normalizedMatchId);
                    return new GetMatchSnapshotFailed(
                        new GetMatchSnapshotFailure(
                            MatchErrorCodes.MatchNotFound,
                            "Match was not found."));
                }

                var seat = ResolveSeat(match, normalizedPlayerToken);
                if (seat is null)
                {
                    _logger.LogWarning(
                        "Snapshot rejected for {MatchId} with {ErrorCode}",
                        match.MatchId,
                        MatchErrorCodes.InvalidPlayerToken);
                    return new GetMatchSnapshotFailed(
                        new GetMatchSnapshotFailure(
                            MatchErrorCodes.InvalidPlayerToken,
                            "playerToken is not valid for this match."));
                }

                return new GetMatchSnapshotSucceeded(
                    new GetMatchSnapshotSuccess(_snapshotFactory.Create(match)));
            });
    }

    public SubmitMoveOutcome SubmitMove(string? matchId, string? playerToken, string? from, string? to, string? promotion)
    {
        if (string.IsNullOrWhiteSpace(matchId))
        {
            return new SubmitMoveFailed(new SubmitMoveFailure(MatchErrorCodes.MatchIdRequired, "matchId is required."));
        }

        if (string.IsNullOrWhiteSpace(playerToken))
        {
            return new SubmitMoveFailed(new SubmitMoveFailure(MatchErrorCodes.PlayerTokenRequired, "playerToken is required."));
        }

        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        {
            return new SubmitMoveFailed(
                new SubmitMoveFailure(
                    MatchErrorCodes.MoveCoordinatesRequired,
                    "from and to are required."));
        }

        var normalizedMatchId = matchId.Trim();
        var normalizedPlayerToken = playerToken.Trim();
        var normalizedFrom = from.Trim().ToLowerInvariant();
        var normalizedTo = to.Trim().ToLowerInvariant();
        var normalizedPromotion = string.IsNullOrWhiteSpace(promotion) ? null : promotion.Trim();

        return _repository.WithMatchById<SubmitMoveOutcome>(
            normalizedMatchId,
            match =>
            {
                if (match is null)
                {
                    _logger.LogWarning(
                        "Move rejected with {ErrorCode}; match {MatchId} not found",
                        MatchErrorCodes.MatchNotFound,
                        normalizedMatchId);
                    return new SubmitMoveFailed(new SubmitMoveFailure(MatchErrorCodes.MatchNotFound, "Match was not found."));
                }

                if (string.Equals(match.Status, MatchStatuses.Ended, StringComparison.Ordinal))
                {
                    return new SubmitMoveFailed(
                        new SubmitMoveFailure(
                            MatchErrorCodes.MatchAlreadyEnded,
                            "Match has already ended."));
                }

                if (match.JoinerToken is null)
                {
                    _logger.LogWarning(
                        "Move rejected for {MatchId} with {ErrorCode}",
                        match.MatchId,
                        MatchErrorCodes.MatchNotReady);
                    return new SubmitMoveFailed(
                        new SubmitMoveFailure(
                            MatchErrorCodes.MatchNotReady,
                            "Match is waiting for the second player."));
                }

                var seat = ResolveSeat(match, normalizedPlayerToken);
                if (seat is null)
                {
                    _logger.LogWarning(
                        "Move rejected for {MatchId} with {ErrorCode}",
                        match.MatchId,
                        MatchErrorCodes.InvalidPlayerToken);
                    return new SubmitMoveFailed(
                        new SubmitMoveFailure(
                            MatchErrorCodes.InvalidPlayerToken,
                            "playerToken is not valid for this match."));
                }

                if (!IsSeatConnected(match, seat))
                {
                    return new SubmitMoveFailed(
                        new SubmitMoveFailure(
                            MatchErrorCodes.SeatNotReconnectable,
                            "Reconnect the seat before submitting a move."));
                }

                if (!AreBothJoinedSeatsConnected(match))
                {
                    return new SubmitMoveFailed(
                        new SubmitMoveFailure(
                            MatchErrorCodes.SeatNotReconnectable,
                            "Match is paused while a player is disconnected."));
                }

                if (_disconnectPolicy.RequireBothPlayersConnectedToStart &&
                    match.MoveNumber == 1 &&
                    (!match.CreatorConnected || !match.JoinerConnected))
                {
                    return new SubmitMoveFailed(
                        new SubmitMoveFailure(
                            MatchErrorCodes.MatchNotReady,
                            "Both players must be connected before the first move."));
                }

                if (!string.Equals(match.SideToMove, seat, StringComparison.Ordinal))
                {
                    _logger.LogWarning(
                        "Move rejected for {MatchId} with {ErrorCode}; expected {ExpectedSeat}, got {Seat}",
                        match.MatchId,
                        MatchErrorCodes.OutOfTurn,
                        match.SideToMove,
                        seat);
                    return new SubmitMoveFailed(
                        new SubmitMoveFailure(
                            MatchErrorCodes.OutOfTurn,
                            "It is not this player's turn."));
                }

                var moveOutcome = _rulesEngine.TryApplyMove(
                    match,
                    seat,
                    normalizedFrom,
                    normalizedTo,
                    normalizedPromotion);

                switch (moveOutcome)
                {
                    case MoveRejectedOutcome { Reason: MoveRejectionReason.InvalidPromotion }:
                        _logger.LogWarning(
                            "Move rejected with {ErrorCode}; invalid promotion value {Promotion}",
                            MatchErrorCodes.InvalidPromotion,
                            normalizedPromotion);
                        return new SubmitMoveFailed(
                            new SubmitMoveFailure(
                                MatchErrorCodes.InvalidPromotion,
                                "promotion must be one of Q, R, B, or N."));
                    case MoveRejectedOutcome { Reason: MoveRejectionReason.InvalidCoordinates }:
                        return new SubmitMoveFailed(
                            new SubmitMoveFailure(
                                MatchErrorCodes.IllegalMove,
                                "Move format is invalid. Use coordinates like e2 and e4."));
                    case MoveRejectedOutcome:
                        _logger.LogWarning(
                            "Move rejected for {MatchId} with {ErrorCode}; {From}->{To}",
                            match.MatchId,
                            MatchErrorCodes.IllegalMove,
                            normalizedFrom,
                            normalizedTo);
                        return new SubmitMoveFailed(
                            new SubmitMoveFailure(
                                MatchErrorCodes.IllegalMove,
                                "Move is not legal."));
                }

                match.SideToMove = seat == MatchSeats.Creator
                    ? MatchSeats.Joiner
                    : MatchSeats.Creator;
                match.MoveNumber += 1;

                _logger.LogInformation(
                    "Move accepted for {MatchId}; {From}->{To}; next turn {SideToMove}",
                    match.MatchId,
                    normalizedFrom,
                    normalizedTo,
                    match.SideToMove);

                var snapshot = _snapshotFactory.Create(match);
                return new SubmitMoveSucceeded(new SubmitMoveSuccess(snapshot));
            });
    }

    public ReconnectMatchOutcome ReconnectMatch(string? matchId, string? playerToken)
    {
        if (string.IsNullOrWhiteSpace(matchId))
        {
            return new ReconnectMatchFailed(
                new ReconnectMatchFailure(
                    MatchErrorCodes.MatchIdRequired,
                    "matchId is required."));
        }

        if (string.IsNullOrWhiteSpace(playerToken))
        {
            return new ReconnectMatchFailed(
                new ReconnectMatchFailure(
                    MatchErrorCodes.PlayerTokenRequired,
                    "playerToken is required."));
        }

        var normalizedMatchId = matchId.Trim();
        var normalizedPlayerToken = playerToken.Trim();
        MatchSnapshot? endedSnapshot = null;

        var outcome = _repository.WithMatchById<ReconnectMatchOutcome>(
            normalizedMatchId,
            match =>
            {
                if (match is null)
                {
                    return new ReconnectMatchFailed(
                        new ReconnectMatchFailure(
                            MatchErrorCodes.MatchNotFound,
                            "Match was not found."));
                }

                if (string.Equals(match.Status, MatchStatuses.Ended, StringComparison.Ordinal))
                {
                    return new ReconnectMatchFailed(
                        new ReconnectMatchFailure(
                            MatchErrorCodes.MatchAlreadyEnded,
                            "Match has already ended."));
                }

                var seat = ResolveSeat(match, normalizedPlayerToken);
                if (seat is null)
                {
                    return new ReconnectMatchFailed(
                        new ReconnectMatchFailure(
                            MatchErrorCodes.UnauthorizedResume,
                            "Seat cannot be resumed with this token."));
                }

                if (IsSeatConnected(match, seat))
                {
                    return new ReconnectMatchSucceeded(
                        new ReconnectMatchSuccess(
                            _snapshotFactory.Create(match),
                            seat,
                            false));
                }

                var graceExpiresUtc = GetSeatGraceExpiresUtc(match, seat);
                if (graceExpiresUtc is null)
                {
                    return new ReconnectMatchFailed(
                        new ReconnectMatchFailure(
                            MatchErrorCodes.SeatNotReconnectable,
                            "Seat is not reconnectable."));
                }

                var now = _clock.UtcNow;
                if (now >= graceExpiresUtc.Value)
                {
                    ResolveDisconnectedSeatAbandonment(match, seat, now);
                    endedSnapshot = _snapshotFactory.Create(match);
                    return new ReconnectMatchFailed(
                        new ReconnectMatchFailure(
                            MatchErrorCodes.GraceExpired,
                            "Reconnect grace period has expired."));
                }

                MarkSeatConnected(match, seat);
                return new ReconnectMatchSucceeded(
                    new ReconnectMatchSuccess(
                        _snapshotFactory.Create(match),
                        seat,
                        true));
            });

        if (outcome is ReconnectMatchSucceeded { Response: { PresenceChanged: true, Seat: var seat } })
        {
            _disconnectGraceScheduler.CancelSeatGraceTimeout(normalizedMatchId, seat);
        }

        if (endedSnapshot is not null)
        {
            _disconnectGraceScheduler.CancelMatchGraceTimeouts(normalizedMatchId);
            _ = PublishMatchEndedAsync(endedSnapshot, CancellationToken.None);
        }

        return outcome;
    }

    public DisconnectMatchOutcome DisconnectMatch(string? matchId, string? playerToken)
    {
        if (string.IsNullOrWhiteSpace(matchId))
        {
            return new DisconnectMatchFailed(
                new DisconnectMatchFailure(
                    MatchErrorCodes.MatchIdRequired,
                    "matchId is required."));
        }

        if (string.IsNullOrWhiteSpace(playerToken))
        {
            return new DisconnectMatchFailed(
                new DisconnectMatchFailure(
                    MatchErrorCodes.PlayerTokenRequired,
                    "playerToken is required."));
        }

        var normalizedMatchId = matchId.Trim();
        var normalizedPlayerToken = playerToken.Trim();

        var outcome = _repository.WithMatchById<DisconnectMatchOutcome>(
            normalizedMatchId,
            match =>
            {
                if (match is null)
                {
                    return new DisconnectMatchFailed(
                        new DisconnectMatchFailure(
                            MatchErrorCodes.MatchNotFound,
                            "Match was not found."));
                }

                if (string.Equals(match.Status, MatchStatuses.Ended, StringComparison.Ordinal))
                {
                    return new DisconnectMatchFailed(
                        new DisconnectMatchFailure(
                            MatchErrorCodes.MatchAlreadyEnded,
                            "Match has already ended."));
                }

                var seat = ResolveSeat(match, normalizedPlayerToken);
                if (seat is null)
                {
                    return new DisconnectMatchFailed(
                        new DisconnectMatchFailure(
                            MatchErrorCodes.UnauthorizedResume,
                            "Seat cannot be disconnected with this token."));
                }

                if (!IsSeatConnected(match, seat))
                {
                    return new DisconnectMatchSucceeded(
                        new DisconnectMatchSuccess(
                            _snapshotFactory.Create(match),
                            seat,
                            false,
                            GetSeatGraceExpiresUtc(match, seat)));
                }

                var disconnectedUtc = _clock.UtcNow;
                var graceExpiresUtc = disconnectedUtc.AddSeconds(_disconnectPolicy.DisconnectGracePeriodSeconds);
                MarkSeatDisconnected(match, seat, disconnectedUtc, graceExpiresUtc);

                return new DisconnectMatchSucceeded(
                    new DisconnectMatchSuccess(
                        _snapshotFactory.Create(match),
                        seat,
                        true,
                        graceExpiresUtc));
            });

        if (outcome is DisconnectMatchSucceeded
            {
                Response:
                {
                    PresenceChanged: true,
                    GraceExpiresUtc: { } graceExpiresUtc,
                    Seat: var seat
                }
            })
        {
            _disconnectGraceScheduler.ScheduleSeatGraceTimeout(
                normalizedMatchId,
                seat,
                graceExpiresUtc,
                cancellationToken => HandleSeatGraceTimeoutAsync(normalizedMatchId, seat, cancellationToken));
        }

        return outcome;
    }

    public void Dispose()
    {
        _disconnectGraceScheduler.Dispose();
    }

    private async Task HandleSeatGraceTimeoutAsync(string matchId, string seat, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        MatchSnapshot? endedSnapshot = null;
        DateTimeOffset? retryDueUtc = null;

        var resolved = _repository.WithMatchById(
            matchId,
            match =>
            {
                if (match is null || string.Equals(match.Status, MatchStatuses.Ended, StringComparison.Ordinal))
                {
                    return false;
                }

                if (IsSeatConnected(match, seat))
                {
                    return false;
                }

                var graceExpiresUtc = GetSeatGraceExpiresUtc(match, seat);
                if (graceExpiresUtc is null)
                {
                    return false;
                }

                if (now < graceExpiresUtc.Value)
                {
                    retryDueUtc = graceExpiresUtc;
                    return false;
                }

                ResolveDisconnectedSeatAbandonment(match, seat, now);
                endedSnapshot = _snapshotFactory.Create(match);
                return true;
            });

        if (!resolved || endedSnapshot is null)
        {
            if (retryDueUtc.HasValue)
            {
                _disconnectGraceScheduler.ScheduleSeatGraceTimeout(
                    matchId,
                    seat,
                    retryDueUtc.Value,
                    token => HandleSeatGraceTimeoutAsync(matchId, seat, token));
            }

            return;
        }

        _disconnectGraceScheduler.CancelMatchGraceTimeouts(matchId);
        await PublishMatchEndedAsync(endedSnapshot, cancellationToken);
    }

    private async Task PublishMatchEndedAsync(MatchSnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            await _lifecycleEventPublisher.PublishMatchEndedAsync(snapshot, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to publish match-ended event for {MatchId}", snapshot.MatchId);
        }
    }

    private void ResolveDisconnectedSeatAbandonment(MatchState match, string disconnectedSeat, DateTimeOffset endedUtc)
    {
        match.Status = MatchStatuses.Ended;
        match.EndedUtc = endedUtc;

        if (_disconnectPolicy.AbandonmentResolution == MatchAbandonmentResolutionMode.Draw)
        {
            match.Resolution = MatchResolutions.Draw;
            match.WinnerSeat = null;
            return;
        }

        match.Resolution = MatchResolutions.Forfeit;
        match.WinnerSeat = disconnectedSeat == MatchSeats.Creator
            ? MatchSeats.Joiner
            : MatchSeats.Creator;
    }

    private static bool IsSeatConnected(MatchState match, string seat)
    {
        return seat == MatchSeats.Creator
            ? match.CreatorConnected
            : match.JoinerToken is not null && match.JoinerConnected;
    }

    private static DateTimeOffset? GetSeatGraceExpiresUtc(MatchState match, string seat)
    {
        return seat == MatchSeats.Creator
            ? match.CreatorGraceExpiresUtc
            : match.JoinerGraceExpiresUtc;
    }

    private static void MarkSeatConnected(MatchState match, string seat)
    {
        if (seat == MatchSeats.Creator)
        {
            match.CreatorConnected = true;
            match.CreatorDisconnectedUtc = null;
            match.CreatorGraceExpiresUtc = null;
            return;
        }

        match.JoinerConnected = true;
        match.JoinerDisconnectedUtc = null;
        match.JoinerGraceExpiresUtc = null;
    }

    private static void MarkSeatDisconnected(
        MatchState match,
        string seat,
        DateTimeOffset disconnectedUtc,
        DateTimeOffset graceExpiresUtc)
    {
        if (seat == MatchSeats.Creator)
        {
            match.CreatorConnected = false;
            match.CreatorDisconnectedUtc = disconnectedUtc;
            match.CreatorGraceExpiresUtc = graceExpiresUtc;
            return;
        }

        match.JoinerConnected = false;
        match.JoinerDisconnectedUtc = disconnectedUtc;
        match.JoinerGraceExpiresUtc = graceExpiresUtc;
    }

    private static bool AreBothJoinedSeatsConnected(MatchState match)
    {
        return match.CreatorConnected && (match.JoinerToken is null || match.JoinerConnected);
    }

    private string GenerateUniqueJoinCode()
    {
        while (true)
        {
            var candidate = _joinCodeGenerator.Generate();
            if (_repository.JoinCodeExists(candidate))
            {
                continue;
            }

            return candidate;
        }
    }

    private static string? ResolveSeat(MatchState match, string playerToken)
    {
        if (string.Equals(match.CreatorToken, playerToken, StringComparison.OrdinalIgnoreCase))
        {
            return MatchSeats.Creator;
        }

        if (match.JoinerToken is not null &&
            string.Equals(match.JoinerToken, playerToken, StringComparison.OrdinalIgnoreCase))
        {
            return MatchSeats.Joiner;
        }

        return null;
    }

    private static char[] CreateInitialBoard()
    {
        var boardRows =
            new[]
            {
                "rnbqkbnr",
                "pppppppp",
                "........",
                "........",
                "........",
                "........",
                "PPPPPPPP",
                "RNBQKBNR"
            };

        var board = new char[64];
        for (var row = 0; row < 8; row++)
        {
            for (var col = 0; col < 8; col++)
            {
                board[(row * 8) + col] = boardRows[row][col];
            }
        }

        return board;
    }
}
