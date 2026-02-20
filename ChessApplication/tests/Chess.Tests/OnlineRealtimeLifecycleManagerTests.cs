using System;
using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;
using Chess.Online;
using Xunit;

namespace Chess.Tests;

public sealed class OnlineRealtimeLifecycleManagerTests
{
    [Fact]
    public async Task ResetAsync_UnsubscribesDisconnectsDisposesAndDetachesHandlers()
    {
        var fakeRealtimeClient = new FakeRealtimeClient();
        var manager = CreateManager(fakeRealtimeClient);
        var credentials = new OnlineMatchCredentials("match-1", "token-1", PieceColor.White);
        var forwardedUpdatedEvents = 0;
        manager.UpdatedReceived += (_, _) => forwardedUpdatedEvents++;

        var connectionError = await manager.EnsureConnectedAndSubscribedAsync(credentials);
        Assert.Null(connectionError);

        fakeRealtimeClient.EmitUpdated(
            sequence: 1,
            CreateSnapshot(matchId: "match-1", moveNumber: 1, sideToMove: OnlineMatchProtocolConstants.JoinerSeat));
        Assert.Equal(1, forwardedUpdatedEvents);

        await manager.ResetAsync(credentials);

        Assert.Equal(1, fakeRealtimeClient.UnsubscribeCalls);
        Assert.Equal(1, fakeRealtimeClient.DisconnectCalls);
        Assert.Equal(1, fakeRealtimeClient.DisposeCalls);

        fakeRealtimeClient.EmitUpdated(
            sequence: 2,
            CreateSnapshot(matchId: "match-1", moveNumber: 2, sideToMove: OnlineMatchProtocolConstants.CreatorSeat));
        Assert.Equal(1, forwardedUpdatedEvents);

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task EnsureConnectedAndSubscribedAsync_WhenConnectThrows_MapsDeterministicError()
    {
        var fakeRealtimeClient = new FakeRealtimeClient
        {
            ConnectException = new InvalidOperationException("failed to connect")
        };
        var manager = CreateManager(fakeRealtimeClient);

        var error = await manager.EnsureConnectedAndSubscribedAsync(
            new OnlineMatchCredentials("match-1", "token-1", PieceColor.White));

        Assert.NotNull(error);
        Assert.Equal("transport_error", error!.Code);
        Assert.Equal("failed to connect", error.Message);

        await manager.DisposeAsync();
    }

    private static OnlineRealtimeLifecycleManager CreateManager(FakeRealtimeClient fakeRealtimeClient)
    {
        return new OnlineRealtimeLifecycleManager(
            new StubRealtimeClientFactory(fakeRealtimeClient),
            new OnlineTransportErrorPolicy(new OnlineErrorMapper()));
    }

    private static OnlineMatchSnapshot CreateSnapshot(string matchId, int moveNumber, string sideToMove)
    {
        return new OnlineMatchSnapshot(
            MatchId: matchId,
            SideToMove: sideToMove,
            MoveNumber: moveNumber,
            Board:
            [
                "rnbqkbnr",
                "pppppppp",
                "........",
                "........",
                "........",
                "........",
                "PPPPPPPP",
                "RNBQKBNR"
            ]);
    }

    private sealed class StubRealtimeClientFactory : IOnlineMatchRealtimeClientFactory
    {
        private readonly IOnlineMatchRealtimeClient _realtimeClient;

        public StubRealtimeClientFactory(IOnlineMatchRealtimeClient realtimeClient)
        {
            _realtimeClient = realtimeClient;
        }

        public IOnlineMatchRealtimeClient CreateClient()
        {
            return _realtimeClient;
        }
    }

    private sealed class FakeRealtimeClient : IOnlineMatchRealtimeClient
    {
#pragma warning disable CS0067
        public event EventHandler<OnlineMatchSnapshotSyncEvent>? SnapshotReceived;
        public event EventHandler<OnlineMatchUpdatedSyncEvent>? UpdatedReceived;
        public event EventHandler<OnlineMatchPresenceChangedSyncEvent>? PresenceChangedReceived;
        public event EventHandler<OnlineMatchEndedSyncEvent>? EndedReceived;
        public event EventHandler<OnlineMatchErrorSyncEvent>? ErrorReceived;
        public event EventHandler? Reconnected;
        public event EventHandler<Exception?>? Disconnected;
#pragma warning restore CS0067

        public bool IsConnected { get; private set; }

        public int ConnectCalls { get; private set; }

        public int SubscribeCalls { get; private set; }

        public int RequestResyncCalls { get; private set; }

        public int UnsubscribeCalls { get; private set; }

        public int DisconnectCalls { get; private set; }

        public int DisposeCalls { get; private set; }

        public Exception? ConnectException { get; set; }

        public Task ConnectAsync(string playerToken, CancellationToken cancellationToken = default)
        {
            ConnectCalls++;

            if (ConnectException is not null)
            {
                throw ConnectException;
            }

            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task SubscribeMatchAsync(string matchId, string playerToken, CancellationToken cancellationToken = default)
        {
            SubscribeCalls++;
            return Task.CompletedTask;
        }

        public Task RequestResyncAsync(string matchId, string playerToken, CancellationToken cancellationToken = default)
        {
            RequestResyncCalls++;
            return Task.CompletedTask;
        }

        public Task UnsubscribeMatchAsync(string matchId, CancellationToken cancellationToken = default)
        {
            UnsubscribeCalls++;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            DisconnectCalls++;
            IsConnected = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCalls++;
            return ValueTask.CompletedTask;
        }

        public void EmitUpdated(long sequence, OnlineMatchSnapshot snapshot)
        {
            UpdatedReceived?.Invoke(
                this,
                new OnlineMatchUpdatedSyncEvent(
                    OnlineMatchProtocolConstants.EventMatchUpdated,
                    new OnlineMatchEventMetadata(snapshot.MatchId, Guid.NewGuid().ToString("N"), sequence, DateTimeOffset.UtcNow),
                    snapshot));
        }
    }
}
