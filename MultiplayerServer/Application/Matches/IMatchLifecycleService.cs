using MultiplayerServer.Contracts.V1;

namespace MultiplayerServer.Application.Matches;

public interface IMatchLifecycleService
{
    CreateMatchResponse CreateMatch();
    JoinMatchResult JoinMatch(string? joinCode);
    SubmitMoveResult SubmitMove(string? matchId, string? playerToken, string? from, string? to, string? promotion);
}

public sealed record JoinMatchResult(
    bool IsSuccess,
    JoinMatchResponse? Response,
    JoinMatchFailure? Failure)
{
    public static JoinMatchResult Success(JoinMatchResponse response)
    {
        return new JoinMatchResult(true, response, null);
    }

    public static JoinMatchResult Failed(JoinMatchFailure failure)
    {
        return new JoinMatchResult(false, null, failure);
    }
}

public sealed record JoinMatchFailure(
    string Code,
    string Message);

public sealed record SubmitMoveResult(
    bool IsSuccess,
    SubmitMoveResponse? Response,
    SubmitMoveFailure? Failure)
{
    public static SubmitMoveResult Success(SubmitMoveResponse response)
    {
        return new SubmitMoveResult(true, response, null);
    }

    public static SubmitMoveResult Failed(SubmitMoveFailure failure)
    {
        return new SubmitMoveResult(false, null, failure);
    }
}

public sealed record SubmitMoveFailure(
    string Code,
    string Message);
