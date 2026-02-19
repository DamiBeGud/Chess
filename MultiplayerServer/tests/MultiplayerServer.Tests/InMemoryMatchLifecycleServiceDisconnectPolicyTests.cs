using Microsoft.Extensions.Options;
using MultiplayerServer.Application.Matches;

namespace MultiplayerServer.Tests;

public sealed class InMemoryMatchLifecycleServiceDisconnectPolicyTests
{
    [Fact]
    public void DisconnectMatch_MarksSeatDisconnectedAndKeepsSeatReserved()
    {
        var clock = new AdjustableMatchClock(new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero));
        var scheduler = new ManualDisconnectGraceScheduler();
        var publisher = new CapturingLifecycleEventPublisher();
        var service = CreateService(clock, scheduler, publisher);

        var created = service.CreateMatch();
        _ = JoinMatch(service, created.JoinCode);

        var disconnected = service.DisconnectMatch(created.MatchId, created.CreatorToken);

        var success = Assert.IsType<DisconnectMatchSucceeded>(disconnected);
        Assert.True(success.Response.PresenceChanged);
        Assert.Equal(MatchSeats.Creator, success.Response.Seat);
        Assert.False(success.Response.Snapshot.Presence.Creator.IsConnected);
        Assert.True(success.Response.Snapshot.Presence.Creator.IsReserved);
        Assert.True(success.Response.Snapshot.Presence.Joiner.IsReserved);
        Assert.Equal(clock.UtcNow.AddSeconds(30), success.Response.GraceExpiresUtc);
        Assert.True(scheduler.HasScheduledSeat(created.MatchId, MatchSeats.Creator));
    }

    [Fact]
    public void ReconnectMatch_WithinGrace_SucceedsAndCancelsTimeout()
    {
        var clock = new AdjustableMatchClock(new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero));
        var scheduler = new ManualDisconnectGraceScheduler();
        var service = CreateService(clock, scheduler, new CapturingLifecycleEventPublisher());

        var created = service.CreateMatch();
        _ = JoinMatch(service, created.JoinCode);
        _ = Assert.IsType<DisconnectMatchSucceeded>(service.DisconnectMatch(created.MatchId, created.CreatorToken));

        clock.Advance(TimeSpan.FromSeconds(10));

        var reconnected = service.ReconnectMatch(created.MatchId, created.CreatorToken);

        var success = Assert.IsType<ReconnectMatchSucceeded>(reconnected);
        Assert.True(success.Response.PresenceChanged);
        Assert.Equal(MatchSeats.Creator, success.Response.Seat);
        Assert.True(success.Response.Snapshot.Presence.Creator.IsConnected);
        Assert.Null(success.Response.Snapshot.Presence.Creator.GraceExpiresUtc);
        Assert.False(scheduler.HasScheduledSeat(created.MatchId, MatchSeats.Creator));
    }

    [Fact]
    public void ReconnectMatch_AfterGrace_FailsAndEndsMatch()
    {
        var clock = new AdjustableMatchClock(new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero));
        var scheduler = new ManualDisconnectGraceScheduler();
        var service = CreateService(clock, scheduler, new CapturingLifecycleEventPublisher());

        var created = service.CreateMatch();
        _ = JoinMatch(service, created.JoinCode);
        _ = Assert.IsType<DisconnectMatchSucceeded>(service.DisconnectMatch(created.MatchId, created.CreatorToken));

        clock.Advance(TimeSpan.FromSeconds(31));

        var reconnect = service.ReconnectMatch(created.MatchId, created.CreatorToken);

        var failed = Assert.IsType<ReconnectMatchFailed>(reconnect);
        Assert.Equal(MatchErrorCodes.GraceExpired, failed.Error.Code);

        var snapshot = Assert.IsType<GetMatchSnapshotSucceeded>(
            service.GetMatchSnapshot(created.MatchId, created.CreatorToken)).Response.Snapshot;
        Assert.Equal(MatchStatuses.Ended, snapshot.Status);
        Assert.Equal(MatchResolutions.Forfeit, snapshot.Resolution);
        Assert.Equal(MatchSeats.Joiner, snapshot.WinnerSeat);
    }

    [Theory]
    [InlineData(MatchAbandonmentResolutionMode.Forfeit)]
    [InlineData(MatchAbandonmentResolutionMode.Draw)]
    public async Task GraceTimeout_ResolvesUsingConfiguredPolicy(MatchAbandonmentResolutionMode policy)
    {
        var clock = new AdjustableMatchClock(new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero));
        var scheduler = new ManualDisconnectGraceScheduler();
        var publisher = new CapturingLifecycleEventPublisher();
        var service = CreateService(
            clock,
            scheduler,
            publisher,
            new MatchDisconnectPolicyOptions
            {
                DisconnectGracePeriodSeconds = 5,
                AbandonmentResolution = policy
            });

        var created = service.CreateMatch();
        _ = JoinMatch(service, created.JoinCode);
        _ = Assert.IsType<DisconnectMatchSucceeded>(service.DisconnectMatch(created.MatchId, created.CreatorToken));

        clock.Advance(TimeSpan.FromSeconds(6));
        await scheduler.TriggerSeatTimeoutAsync(created.MatchId, MatchSeats.Creator);

        var snapshot = Assert.IsType<GetMatchSnapshotSucceeded>(
            service.GetMatchSnapshot(created.MatchId, created.CreatorToken)).Response.Snapshot;
        Assert.Equal(MatchStatuses.Ended, snapshot.Status);

        if (policy == MatchAbandonmentResolutionMode.Forfeit)
        {
            Assert.Equal(MatchResolutions.Forfeit, snapshot.Resolution);
            Assert.Equal(MatchSeats.Joiner, snapshot.WinnerSeat);
        }
        else
        {
            Assert.Equal(MatchResolutions.Draw, snapshot.Resolution);
            Assert.Null(snapshot.WinnerSeat);
        }

        var published = Assert.Single(publisher.MatchEndedSnapshots);
        Assert.Equal(created.MatchId, published.MatchId);
        Assert.Equal(MatchStatuses.Ended, published.Status);
    }

    [Fact]
    public void DisconnectAndReconnect_DuplicateOperations_AreIdempotent()
    {
        var clock = new AdjustableMatchClock(new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero));
        var scheduler = new ManualDisconnectGraceScheduler();
        var service = CreateService(clock, scheduler, new CapturingLifecycleEventPublisher());

        var created = service.CreateMatch();
        _ = JoinMatch(service, created.JoinCode);

        var firstDisconnect = Assert.IsType<DisconnectMatchSucceeded>(
            service.DisconnectMatch(created.MatchId, created.CreatorToken));
        var duplicateDisconnect = Assert.IsType<DisconnectMatchSucceeded>(
            service.DisconnectMatch(created.MatchId, created.CreatorToken));

        Assert.True(firstDisconnect.Response.PresenceChanged);
        Assert.False(duplicateDisconnect.Response.PresenceChanged);
        Assert.Equal(1, scheduler.GetScheduleCount(created.MatchId, MatchSeats.Creator));

        var firstReconnect = Assert.IsType<ReconnectMatchSucceeded>(
            service.ReconnectMatch(created.MatchId, created.CreatorToken));
        var duplicateReconnect = Assert.IsType<ReconnectMatchSucceeded>(
            service.ReconnectMatch(created.MatchId, created.CreatorToken));

        Assert.True(firstReconnect.Response.PresenceChanged);
        Assert.False(duplicateReconnect.Response.PresenceChanged);
    }

    private static JoinMatchSuccess JoinMatch(InMemoryMatchLifecycleService service, string joinCode)
    {
        return Assert.IsType<JoinMatchSucceeded>(service.JoinMatch(joinCode)).Response;
    }

    private static InMemoryMatchLifecycleService CreateService(
        IMatchClock clock,
        ManualDisconnectGraceScheduler scheduler,
        CapturingLifecycleEventPublisher publisher,
        MatchDisconnectPolicyOptions? options = null)
    {
        return new InMemoryMatchLifecycleService(
            new InMemoryMatchRepository(),
            new GuidMatchIdGenerator(),
            new RandomJoinCodeGenerator(),
            new RandomPlayerTokenGenerator(),
            new ClassicChessRulesEngine(),
            new MatchSnapshotFactory(),
            Options.Create(options ?? new MatchDisconnectPolicyOptions
            {
                DisconnectGracePeriodSeconds = 30,
                AbandonmentResolution = MatchAbandonmentResolutionMode.Forfeit
            }),
            clock,
            scheduler,
            publisher);
    }

    private sealed class AdjustableMatchClock(DateTimeOffset utcNow) : IMatchClock
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;

        public void Advance(TimeSpan by)
        {
            UtcNow = UtcNow.Add(by);
        }
    }

    private sealed class ManualDisconnectGraceScheduler : IDisconnectGraceScheduler
    {
        private readonly Dictionary<string, ScheduledSeatTimeout> _timeouts =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, int> _scheduleCounts =
            new(StringComparer.OrdinalIgnoreCase);

        public void ScheduleSeatGraceTimeout(
            string matchId,
            string seat,
            DateTimeOffset dueUtc,
            Func<CancellationToken, Task> onTimeoutAsync)
        {
            var key = BuildKey(matchId, seat);
            _timeouts[key] = new ScheduledSeatTimeout(dueUtc, onTimeoutAsync);
            _scheduleCounts[key] = _scheduleCounts.TryGetValue(key, out var count)
                ? count + 1
                : 1;
        }

        public void CancelSeatGraceTimeout(string matchId, string seat)
        {
            _timeouts.Remove(BuildKey(matchId, seat));
        }

        public void CancelMatchGraceTimeouts(string matchId)
        {
            foreach (var key in _timeouts.Keys.Where(key => key.StartsWith($"{matchId}|", StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                _timeouts.Remove(key);
            }
        }

        public bool HasScheduledSeat(string matchId, string seat)
        {
            return _timeouts.ContainsKey(BuildKey(matchId, seat));
        }

        public int GetScheduleCount(string matchId, string seat)
        {
            return _scheduleCounts.TryGetValue(BuildKey(matchId, seat), out var count)
                ? count
                : 0;
        }

        public async Task TriggerSeatTimeoutAsync(string matchId, string seat)
        {
            var key = BuildKey(matchId, seat);
            if (!_timeouts.TryGetValue(key, out var timeout))
            {
                throw new InvalidOperationException($"No timeout scheduled for {key}.");
            }

            _timeouts.Remove(key);
            await timeout.Callback(CancellationToken.None);
        }

        public void Dispose()
        {
            _timeouts.Clear();
            _scheduleCounts.Clear();
        }

        private static string BuildKey(string matchId, string seat)
        {
            return $"{matchId}|{seat}";
        }

        private sealed record ScheduledSeatTimeout(
            DateTimeOffset DueUtc,
            Func<CancellationToken, Task> Callback);
    }

    private sealed class CapturingLifecycleEventPublisher : IMatchLifecycleEventPublisher
    {
        public List<MatchSnapshot> MatchEndedSnapshots { get; } = [];

        public Task PublishMatchEndedAsync(MatchSnapshot snapshot, CancellationToken cancellationToken)
        {
            MatchEndedSnapshots.Add(snapshot);
            return Task.CompletedTask;
        }
    }
}
