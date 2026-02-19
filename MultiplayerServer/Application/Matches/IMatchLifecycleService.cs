namespace MultiplayerServer.Application.Matches;

public interface ICreateMatchUseCase
{
    CreateMatchResult CreateMatch();
}

public interface IJoinMatchUseCase
{
    JoinMatchOutcome JoinMatch(string? joinCode);
}

public interface ISubmitMoveUseCase
{
    SubmitMoveOutcome SubmitMove(string? matchId, string? playerToken, string? from, string? to, string? promotion);
}

public interface IMatchLifecycleService : ICreateMatchUseCase, IJoinMatchUseCase, ISubmitMoveUseCase;

public sealed record CreateMatchResult(
    string MatchId,
    string JoinCode,
    string CreatorToken);

public abstract record JoinMatchOutcome;

public sealed record JoinMatchSucceeded(JoinMatchSuccess Response) : JoinMatchOutcome;

public sealed record JoinMatchSuccess(
    string MatchId,
    string Seat,
    string PlayerToken);

public sealed record JoinMatchFailed(JoinMatchFailure Error) : JoinMatchOutcome;

public sealed record JoinMatchFailure(string Code, string Message);

public abstract record SubmitMoveOutcome;

public sealed record SubmitMoveSucceeded(SubmitMoveSuccess Response) : SubmitMoveOutcome;

public sealed record SubmitMoveSuccess(MatchSnapshot Snapshot);

public sealed record MatchSnapshot(
    string MatchId,
    string SideToMove,
    int MoveNumber,
    IReadOnlyList<string> Board);

public sealed record SubmitMoveFailed(SubmitMoveFailure Error) : SubmitMoveOutcome;

public sealed record SubmitMoveFailure(string Code, string Message);

public static class MatchSeats
{
    public const string Creator = "White";
    public const string Joiner = "Black";
}

public static class MatchErrorCodes
{
    public const string JoinCodeRequired = "join_code_required";
    public const string MatchIdRequired = "match_id_required";
    public const string PlayerTokenRequired = "player_token_required";
    public const string MoveCoordinatesRequired = "move_coordinates_required";
    public const string MatchNotFound = "match_not_found";
    public const string MatchNotReady = "match_not_ready";
    public const string MatchFull = "match_full";
    public const string InvalidPlayerToken = "invalid_player_token";
    public const string InvalidPromotion = "invalid_promotion";
    public const string OutOfTurn = "out_of_turn";
    public const string IllegalMove = "illegal_move";
}
