using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MultiplayerServer.Application.Matches;

public sealed class InMemoryMatchLifecycleService : IMatchLifecycleService
{
    private readonly IMatchRepository _repository;
    private readonly IMatchIdGenerator _matchIdGenerator;
    private readonly IJoinCodeGenerator _joinCodeGenerator;
    private readonly IPlayerTokenGenerator _playerTokenGenerator;
    private readonly IChessRulesEngine _rulesEngine;
    private readonly IMatchSnapshotFactory _snapshotFactory;
    private readonly ILogger<InMemoryMatchLifecycleService> _logger;

    public InMemoryMatchLifecycleService()
        : this(
            new InMemoryMatchRepository(),
            new GuidMatchIdGenerator(),
            new RandomJoinCodeGenerator(),
            new RandomPlayerTokenGenerator(),
            new ClassicChessRulesEngine(),
            new MatchSnapshotFactory(),
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
        ILogger<InMemoryMatchLifecycleService>? logger = null)
    {
        _repository = repository;
        _matchIdGenerator = matchIdGenerator;
        _joinCodeGenerator = joinCodeGenerator;
        _playerTokenGenerator = playerTokenGenerator;
        _rulesEngine = rulesEngine;
        _snapshotFactory = snapshotFactory;
        _logger = logger ?? NullLogger<InMemoryMatchLifecycleService>.Instance;
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
                EnPassantTarget = null
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

                var joinerToken = _playerTokenGenerator.Generate();
                match.JoinerToken = joinerToken;
                _logger.LogInformation(
                    "Join accepted for {MatchId}; assigned {Seat}",
                    match.MatchId,
                    MatchSeats.Joiner);

                // Deterministic seat assignment (MS-002): creator is White, first joiner is Black.
                return new JoinMatchSucceeded(new JoinMatchSuccess(match.MatchId, MatchSeats.Joiner, joinerToken));
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
        var boardRows = new[]
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
