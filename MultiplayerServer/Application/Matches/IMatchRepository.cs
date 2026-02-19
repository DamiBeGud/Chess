namespace MultiplayerServer.Application.Matches;

public interface IMatchRepository
{
    void Add(MatchState matchState);
    bool JoinCodeExists(string joinCode);

    TResult WithMatchByJoinCode<TResult>(
        string joinCode,
        Func<MatchState?, TResult> action);

    TResult WithMatchById<TResult>(
        string matchId,
        Func<MatchState?, TResult> action);
}
