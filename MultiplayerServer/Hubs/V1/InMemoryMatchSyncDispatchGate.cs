using System.Collections.Concurrent;

namespace MultiplayerServer.Hubs.V1;

public sealed class InMemoryMatchSyncDispatchGate : IMatchSyncDispatchGate
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates =
        new(StringComparer.OrdinalIgnoreCase);

    public async ValueTask<IAsyncDisposable> AcquireAsync(string matchId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(matchId))
        {
            throw new ArgumentException("matchId is required.", nameof(matchId));
        }

        var gate = _gates.GetOrAdd(matchId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new Releaser(gate);
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private readonly SemaphoreSlim _gate;
        private bool _released;

        public Releaser(SemaphoreSlim gate)
        {
            _gate = gate;
        }

        public ValueTask DisposeAsync()
        {
            if (_released)
            {
                return ValueTask.CompletedTask;
            }

            _released = true;
            _gate.Release();
            return ValueTask.CompletedTask;
        }
    }
}
