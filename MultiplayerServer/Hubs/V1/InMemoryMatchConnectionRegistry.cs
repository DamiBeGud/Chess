namespace MultiplayerServer.Hubs.V1;

public sealed class InMemoryMatchConnectionRegistry : IMatchConnectionRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<string, HashSet<string>> _matchIdsByConnection = new(StringComparer.Ordinal);

    public void AddSubscription(string connectionId, string matchId)
    {
        lock (_sync)
        {
            if (!_matchIdsByConnection.TryGetValue(connectionId, out var matchIds))
            {
                matchIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _matchIdsByConnection[connectionId] = matchIds;
            }

            matchIds.Add(matchId);
        }
    }

    public bool RemoveSubscription(string connectionId, string matchId)
    {
        lock (_sync)
        {
            if (!_matchIdsByConnection.TryGetValue(connectionId, out var matchIds))
            {
                return false;
            }

            var removed = matchIds.Remove(matchId);
            if (matchIds.Count == 0)
            {
                _matchIdsByConnection.Remove(connectionId);
            }

            return removed;
        }
    }

    public bool IsSubscribed(string connectionId, string matchId)
    {
        lock (_sync)
        {
            return _matchIdsByConnection.TryGetValue(connectionId, out var matchIds) &&
                   matchIds.Contains(matchId);
        }
    }

    public IReadOnlyList<string> RemoveConnection(string connectionId)
    {
        lock (_sync)
        {
            if (!_matchIdsByConnection.TryGetValue(connectionId, out var matchIds))
            {
                return Array.Empty<string>();
            }

            _matchIdsByConnection.Remove(connectionId);
            return matchIds.ToArray();
        }
    }
}
