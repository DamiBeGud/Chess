namespace MultiplayerServer.Hubs.V1;

public sealed class InMemoryMatchConnectionRegistry : IMatchConnectionRegistry
{
    private readonly object _sync = new();

    private readonly Dictionary<string, Dictionary<string, MatchConnectionSubscription>> _subscriptionsByConnection =
        new(StringComparer.Ordinal);

    public void AddSubscription(string connectionId, string matchId, string playerToken)
    {
        lock (_sync)
        {
            if (!_subscriptionsByConnection.TryGetValue(connectionId, out var subscriptions))
            {
                subscriptions = new Dictionary<string, MatchConnectionSubscription>(StringComparer.OrdinalIgnoreCase);
                _subscriptionsByConnection[connectionId] = subscriptions;
            }

            subscriptions[matchId] = new MatchConnectionSubscription(matchId, playerToken);
        }
    }

    public MatchConnectionSubscription? RemoveSubscription(string connectionId, string matchId)
    {
        lock (_sync)
        {
            if (!_subscriptionsByConnection.TryGetValue(connectionId, out var subscriptions) ||
                !subscriptions.TryGetValue(matchId, out var removed))
            {
                return null;
            }

            subscriptions.Remove(matchId);
            if (subscriptions.Count == 0)
            {
                _subscriptionsByConnection.Remove(connectionId);
            }

            return removed;
        }
    }

    public bool IsSubscribed(string connectionId, string matchId)
    {
        lock (_sync)
        {
            return _subscriptionsByConnection.TryGetValue(connectionId, out var subscriptions) &&
                   subscriptions.ContainsKey(matchId);
        }
    }

    public IReadOnlyList<MatchConnectionSubscription> RemoveConnection(string connectionId)
    {
        lock (_sync)
        {
            if (!_subscriptionsByConnection.TryGetValue(connectionId, out var subscriptions))
            {
                return Array.Empty<MatchConnectionSubscription>();
            }

            _subscriptionsByConnection.Remove(connectionId);
            return subscriptions.Values.ToArray();
        }
    }
}
