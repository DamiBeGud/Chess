namespace MultiplayerServer.Application.Matches;

public sealed class InMemoryMatchRepository : IMatchRepository
{
    private readonly object _sync = new();
    private readonly Dictionary<string, MatchState> _matchesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _matchIdByJoinCode = new(StringComparer.Ordinal);

    public void Add(MatchState matchState)
    {
        lock (_sync)
        {
            _matchesById[matchState.MatchId] = matchState;
            _matchIdByJoinCode[matchState.JoinCode] = matchState.MatchId;
        }
    }

    public bool JoinCodeExists(string joinCode)
    {
        lock (_sync)
        {
            return _matchIdByJoinCode.ContainsKey(joinCode);
        }
    }

    public TResult WithMatchByJoinCode<TResult>(
        string joinCode,
        Func<MatchState?, TResult> action)
    {
        lock (_sync)
        {
            if (!_matchIdByJoinCode.TryGetValue(joinCode, out var matchId) ||
                !_matchesById.TryGetValue(matchId, out var match))
            {
                return action(null);
            }

            return action(match);
        }
    }

    public TResult WithMatchById<TResult>(
        string matchId,
        Func<MatchState?, TResult> action)
    {
        lock (_sync)
        {
            if (!_matchesById.TryGetValue(matchId, out var match))
            {
                return action(null);
            }

            return action(match);
        }
    }
}
