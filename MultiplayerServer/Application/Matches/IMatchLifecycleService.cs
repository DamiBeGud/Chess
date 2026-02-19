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

public interface IGetMatchSnapshotUseCase
{
    GetMatchSnapshotOutcome GetMatchSnapshot(string? matchId, string? playerToken);
}

public interface IReconnectMatchUseCase
{
    ReconnectMatchOutcome ReconnectMatch(string? matchId, string? playerToken);
}

public interface IDisconnectMatchUseCase
{
    DisconnectMatchOutcome DisconnectMatch(string? matchId, string? playerToken);
}

public interface IMatchLifecycleService :
    ICreateMatchUseCase,
    IJoinMatchUseCase,
    ISubmitMoveUseCase,
    IGetMatchSnapshotUseCase,
    IReconnectMatchUseCase,
    IDisconnectMatchUseCase;

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
    IReadOnlyList<string> Board,
    string Status,
    string? Resolution,
    string? WinnerSeat,
    MatchPresenceSnapshot Presence)
{
    public MatchSnapshot(
        string MatchId,
        string SideToMove,
        int MoveNumber,
        IReadOnlyList<string> Board)
        : this(
            MatchId,
            SideToMove,
            MoveNumber,
            Board,
            MatchStatuses.InProgress,
            null,
            null,
            MatchPresenceSnapshot.Empty)
    {
    }
}

public sealed record MatchPresenceSnapshot(
    MatchSeatPresenceSnapshot Creator,
    MatchSeatPresenceSnapshot Joiner)
{
    public static readonly MatchPresenceSnapshot Empty = new(
        new MatchSeatPresenceSnapshot(
            MatchSeats.Creator,
            false,
            true,
            null,
            null),
        new MatchSeatPresenceSnapshot(
            MatchSeats.Joiner,
            false,
            false,
            null,
            null));
}

public sealed record MatchSeatPresenceSnapshot(
    string Seat,
    bool IsConnected,
    bool IsReserved,
    DateTimeOffset? DisconnectedUtc,
    DateTimeOffset? GraceExpiresUtc);

public abstract record GetMatchSnapshotOutcome;

public sealed record GetMatchSnapshotSucceeded(GetMatchSnapshotSuccess Response) : GetMatchSnapshotOutcome;

public sealed record GetMatchSnapshotSuccess(MatchSnapshot Snapshot);

public sealed record GetMatchSnapshotFailed(GetMatchSnapshotFailure Error) : GetMatchSnapshotOutcome;

public sealed record GetMatchSnapshotFailure(string Code, string Message);

public sealed record SubmitMoveFailed(SubmitMoveFailure Error) : SubmitMoveOutcome;

public sealed record SubmitMoveFailure(string Code, string Message);

public abstract record ReconnectMatchOutcome;

public sealed record ReconnectMatchSucceeded(ReconnectMatchSuccess Response) : ReconnectMatchOutcome;

public sealed record ReconnectMatchSuccess(
    MatchSnapshot Snapshot,
    string Seat,
    bool PresenceChanged);

public sealed record ReconnectMatchFailed(ReconnectMatchFailure Error) : ReconnectMatchOutcome;

public sealed record ReconnectMatchFailure(string Code, string Message);

public abstract record DisconnectMatchOutcome;

public sealed record DisconnectMatchSucceeded(DisconnectMatchSuccess Response) : DisconnectMatchOutcome;

public sealed record DisconnectMatchSuccess(
    MatchSnapshot Snapshot,
    string Seat,
    bool PresenceChanged,
    DateTimeOffset? GraceExpiresUtc);

public sealed record DisconnectMatchFailed(DisconnectMatchFailure Error) : DisconnectMatchOutcome;

public sealed record DisconnectMatchFailure(string Code, string Message);

public static class MatchSeats
{
    public const string Creator = "White";
    public const string Joiner = "Black";
}

public static class MatchStatuses
{
    public const string InProgress = "in_progress";
    public const string Ended = "ended";
}

public static class MatchResolutions
{
    public const string Forfeit = "forfeit";
    public const string Draw = "draw";
}

public static class MatchErrorCodes
{
    public const string JoinCodeRequired = "join_code_required";
    public const string MatchIdRequired = "match_id_required";
    public const string InvalidMatchIdFormat = "invalid_match_id_format";
    public const string PlayerTokenRequired = "player_token_required";
    public const string InvalidPlayerTokenFormat = "invalid_player_token_format";
    public const string MoveCoordinatesRequired = "move_coordinates_required";
    public const string MatchNotFound = "match_not_found";
    public const string MatchNotReady = "match_not_ready";
    public const string MatchFull = "match_full";
    public const string InvalidPlayerToken = "invalid_player_token";
    public const string InvalidPromotion = "invalid_promotion";
    public const string OutOfTurn = "out_of_turn";
    public const string IllegalMove = "illegal_move";
    public const string GraceExpired = "grace_expired";
    public const string UnauthorizedResume = "unauthorized_resume";
    public const string SeatNotReconnectable = "seat_not_reconnectable";
    public const string MatchAlreadyEnded = "match_already_ended";
}
