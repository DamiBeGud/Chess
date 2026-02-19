namespace MultiplayerServer.Application.Matches;

public interface IChessRulesEngine
{
    MoveApplicationOutcome TryApplyMove(
        MatchState matchState,
        string seat,
        string from,
        string to,
        string? promotion);
}

public abstract record MoveApplicationOutcome;

public sealed record MoveAppliedOutcome : MoveApplicationOutcome;

public sealed record MoveRejectedOutcome(MoveRejectionReason Reason) : MoveApplicationOutcome;

public enum MoveRejectionReason
{
    InvalidPromotion,
    InvalidCoordinates,
    IllegalMove
}
