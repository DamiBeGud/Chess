using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace MultiplayerServer.Application.Matches;

public sealed class InMemoryDisconnectGraceScheduler(IMatchClock clock, ILogger<InMemoryDisconnectGraceScheduler> logger)
    : IDisconnectGraceScheduler
{
    private readonly ConcurrentDictionary<string, ScheduledTimeout> _timeouts =
        new(StringComparer.OrdinalIgnoreCase);

    public void ScheduleSeatGraceTimeout(
        string matchId,
        string seat,
        DateTimeOffset dueUtc,
        Func<CancellationToken, Task> onTimeoutAsync)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        ArgumentException.ThrowIfNullOrWhiteSpace(seat);
        ArgumentNullException.ThrowIfNull(onTimeoutAsync);

        var key = BuildKey(matchId, seat);
        CancelSeatGraceTimeout(matchId, seat);

        var cancellation = new CancellationTokenSource();
        var timeout = new ScheduledTimeout(key, cancellation);
        _timeouts[key] = timeout;

        _ = RunTimeoutAsync(timeout, dueUtc, onTimeoutAsync);
    }

    public void CancelSeatGraceTimeout(string matchId, string seat)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        ArgumentException.ThrowIfNullOrWhiteSpace(seat);

        var key = BuildKey(matchId, seat);
        if (_timeouts.TryRemove(key, out var timeout))
        {
            timeout.Cancellation.Cancel();
            timeout.Cancellation.Dispose();
        }
    }

    public void CancelMatchGraceTimeouts(string matchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);

        foreach (var key in _timeouts.Keys)
        {
            if (!key.StartsWith(matchId, StringComparison.OrdinalIgnoreCase) ||
                key.Length <= matchId.Length ||
                key[matchId.Length] != '|')
            {
                continue;
            }

            if (_timeouts.TryRemove(key, out var timeout))
            {
                timeout.Cancellation.Cancel();
                timeout.Cancellation.Dispose();
            }
        }
    }

    public void Dispose()
    {
        foreach (var timeout in _timeouts.Values)
        {
            timeout.Cancellation.Cancel();
            timeout.Cancellation.Dispose();
        }

        _timeouts.Clear();
    }

    private async Task RunTimeoutAsync(
        ScheduledTimeout timeout,
        DateTimeOffset dueUtc,
        Func<CancellationToken, Task> onTimeoutAsync)
    {
        try
        {
            var delay = dueUtc - clock.UtcNow;
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, timeout.Cancellation.Token);
            }

            if (timeout.Cancellation.Token.IsCancellationRequested)
            {
                return;
            }

            if (!_timeouts.TryRemove(timeout.Key, out var removed) || !ReferenceEquals(removed, timeout))
            {
                return;
            }

            await onTimeoutAsync(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // Expected when a seat reconnects or the process shuts down.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Disconnect grace timeout callback failed for {TimeoutKey}", timeout.Key);
        }
        finally
        {
            timeout.Cancellation.Dispose();
        }
    }

    private static string BuildKey(string matchId, string seat)
    {
        return $"{matchId}|{seat}";
    }

    private sealed record ScheduledTimeout(string Key, CancellationTokenSource Cancellation);
}
