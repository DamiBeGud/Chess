using MultiplayerServer.Contracts.V1;

namespace MultiplayerServer.Application.Matches;

public interface ICreateMatchUseCase
{
    CreateMatchResponse CreateMatch();
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

public abstract record JoinMatchOutcome;

public sealed record JoinMatchSucceeded(JoinMatchResponse Response) : JoinMatchOutcome;

public sealed record JoinMatchFailed(JoinMatchFailure Error) : JoinMatchOutcome;

public sealed record JoinMatchFailure(string Code, string Message);

public abstract record SubmitMoveOutcome;

public sealed record SubmitMoveSucceeded(SubmitMoveResponse Response) : SubmitMoveOutcome;

public sealed record SubmitMoveFailed(SubmitMoveFailure Error) : SubmitMoveOutcome;

public sealed record SubmitMoveFailure(string Code, string Message);
