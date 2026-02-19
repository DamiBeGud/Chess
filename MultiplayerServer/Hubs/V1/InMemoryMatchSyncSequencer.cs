namespace MultiplayerServer.Hubs.V1;

public sealed class InMemoryMatchSyncSequencer : IMatchSyncSequencer
{
    private const int MaxRememberedEventIds = 2048;

    private readonly object _sync = new();
    private readonly Dictionary<string, MatchEventStreamState> _stateByMatchId =
        new(StringComparer.OrdinalIgnoreCase);

    public bool TryReserve(string matchId, string eventId, out long sequence)
    {
        if (string.IsNullOrWhiteSpace(matchId))
        {
            throw new ArgumentException("matchId is required.", nameof(matchId));
        }

        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new ArgumentException("eventId is required.", nameof(eventId));
        }

        lock (_sync)
        {
            if (!_stateByMatchId.TryGetValue(matchId, out var state))
            {
                state = new MatchEventStreamState();
                _stateByMatchId[matchId] = state;
            }

            if (state.RecentEventIdSet.Contains(eventId))
            {
                sequence = state.LatestSequence;
                return false;
            }

            state.LatestSequence += 1;
            sequence = state.LatestSequence;

            state.RecentEventIdSet.Add(eventId);
            state.RecentEventIds.Enqueue(eventId);
            if (state.RecentEventIds.Count > MaxRememberedEventIds)
            {
                var removed = state.RecentEventIds.Dequeue();
                state.RecentEventIdSet.Remove(removed);
            }

            return true;
        }
    }

    private sealed class MatchEventStreamState
    {
        public long LatestSequence { get; set; }
        public Queue<string> RecentEventIds { get; } = new();
        public HashSet<string> RecentEventIdSet { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
