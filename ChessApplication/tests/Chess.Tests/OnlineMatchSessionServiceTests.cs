using System;
using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;
using Chess.Online;
using Xunit;

namespace Chess.Tests;

public sealed class OnlineMatchSessionServiceTests
{
    [Fact]
    public async Task RecoverAsync_ReconnectsAndResubscribesAfterSuspend()
    {
        var snapshot = CreateSnapshot(moveNumber: 1, sideToMove: OnlineMatchProtocolConstants.CreatorSeat);
        var httpClient = new StubOnlineMatchHttpClient(
            getSnapshot: (_, _) => Task.FromResult(OnlineOperationResult<OnlineMatchSnapshot>.Success(snapshot)));
        var realtimeClient = new FakeRealtimeClient();
        await using var session = CreateSession(httpClient, realtimeClient);

        var resumeResult = await session.ResumeMatchAsync(
            matchId: snapshot.MatchId,
            playerToken: "e2ef01fdb8517a608fcf4862ef35f6a1",
            seat: PieceColor.White);
        Assert.True(resumeResult.IsSuccess);
        Assert.Equal(1, realtimeClient.ConnectCalls);
        Assert.Equal(1, realtimeClient.SubscribeCalls);
        Assert.Equal(1, realtimeClient.RequestResyncCalls);

        await session.SuspendRealtimeAsync();
        Assert.False(realtimeClient.IsConnected);

        var recoverResult = await session.RecoverAsync();
        Assert.True(recoverResult.IsSuccess);
        Assert.Equal(2, realtimeClient.ConnectCalls);
        Assert.Equal(2, realtimeClient.SubscribeCalls);
        Assert.Equal(2, realtimeClient.RequestResyncCalls);
    }

    [Fact]
    public async Task RealtimeSequenceGap_TriggersBackgroundResync()
    {
        var snapshot = CreateSnapshot(moveNumber: 1, sideToMove: OnlineMatchProtocolConstants.CreatorSeat);
        var httpClient = new StubOnlineMatchHttpClient(
            getSnapshot: (_, _) => Task.FromResult(OnlineOperationResult<OnlineMatchSnapshot>.Success(snapshot)));
        var realtimeClient = new FakeRealtimeClient();
        await using var session = CreateSession(httpClient, realtimeClient);

        var resumeResult = await session.ResumeMatchAsync(
            matchId: snapshot.MatchId,
            playerToken: "e2ef01fdb8517a608fcf4862ef35f6a1",
            seat: PieceColor.White);
        Assert.True(resumeResult.IsSuccess);
        var baselineResyncCalls = realtimeClient.RequestResyncCalls;

        realtimeClient.EmitUpdated(sequence: 1, snapshot with { MoveNumber = 2 });
        realtimeClient.EmitUpdated(sequence: 3, snapshot with { MoveNumber = 3 });

        await WaitForConditionAsync(
            () => realtimeClient.RequestResyncCalls > baselineResyncCalls,
            timeout: TimeSpan.FromSeconds(3));
    }

    private static OnlineMatchSessionService CreateSession(
        StubOnlineMatchHttpClient httpClient,
        FakeRealtimeClient realtimeClient)
    {
        var factory = new StubRealtimeClientFactory(realtimeClient);
        return new OnlineMatchSessionService(
            httpClient,
            new OnlineErrorMapper(),
            factory,
            new OnlineSnapshotGameStateMapper(),
            new OnlineRealtimeEventReducer());
    }

    private static OnlineMatchSnapshot CreateSnapshot(int moveNumber, string sideToMove)
    {
        return new OnlineMatchSnapshot(
            MatchId: "58b02d5d9d5c43cc9a0314a8f1f4a14c",
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
            ],
            Status: OnlineMatchProtocolConstants.MatchStatusInProgress,
            Resolution: null,
            WinnerSeat: null,
            Presence: new OnlineMatchPresence(
                new OnlineSeatPresence(OnlineMatchProtocolConstants.CreatorSeat, true, true, null, null),
                new OnlineSeatPresence(OnlineMatchProtocolConstants.JoinerSeat, true, true, null, null)));
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        var until = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < until)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.True(condition());
    }

    private sealed class StubOnlineMatchHttpClient : IOnlineMatchHttpClient
    {
        private readonly Func<CancellationToken, Task<OnlineOperationResult<OnlineCreateMatchResponse>>> _createMatch;
        private readonly Func<string, CancellationToken, Task<OnlineOperationResult<OnlineJoinMatchResponse>>> _joinMatch;
        private readonly Func<OnlineSubmitMoveRequest, CancellationToken, Task<OnlineOperationResult<OnlineSubmitMoveResponse>>> _submitMove;
        private readonly Func<OnlineSnapshotRequest, CancellationToken, Task<OnlineOperationResult<OnlineMatchSnapshot>>> _getSnapshot;

        public StubOnlineMatchHttpClient(
            Func<CancellationToken, Task<OnlineOperationResult<OnlineCreateMatchResponse>>>? createMatch = null,
            Func<string, CancellationToken, Task<OnlineOperationResult<OnlineJoinMatchResponse>>>? joinMatch = null,
            Func<OnlineSubmitMoveRequest, CancellationToken, Task<OnlineOperationResult<OnlineSubmitMoveResponse>>>? submitMove = null,
            Func<OnlineSnapshotRequest, CancellationToken, Task<OnlineOperationResult<OnlineMatchSnapshot>>>? getSnapshot = null)
        {
            _createMatch = createMatch ?? (_ => Task.FromResult(OnlineOperationResult<OnlineCreateMatchResponse>.Failure(new OnlineUserError("unsupported", "Unsupported in test.", OnlineUserAction.None))));
            _joinMatch = joinMatch ?? ((_, _) => Task.FromResult(OnlineOperationResult<OnlineJoinMatchResponse>.Failure(new OnlineUserError("unsupported", "Unsupported in test.", OnlineUserAction.None))));
            _submitMove = submitMove ?? ((_, _) => Task.FromResult(OnlineOperationResult<OnlineSubmitMoveResponse>.Failure(new OnlineUserError("unsupported", "Unsupported in test.", OnlineUserAction.None))));
            _getSnapshot = getSnapshot ?? ((_, _) => Task.FromResult(OnlineOperationResult<OnlineMatchSnapshot>.Failure(new OnlineUserError("unsupported", "Unsupported in test.", OnlineUserAction.None))));
        }

        public Task<OnlineOperationResult<OnlineCreateMatchResponse>> CreateMatchAsync(CancellationToken cancellationToken = default)
        {
            return _createMatch(cancellationToken);
        }

        public Task<OnlineOperationResult<OnlineJoinMatchResponse>> JoinMatchAsync(
            string joinCode,
            CancellationToken cancellationToken = default)
        {
            return _joinMatch(joinCode, cancellationToken);
        }

        public Task<OnlineOperationResult<OnlineSubmitMoveResponse>> SubmitMoveAsync(
            OnlineSubmitMoveRequest request,
            CancellationToken cancellationToken = default)
        {
            return _submitMove(request, cancellationToken);
        }

        public Task<OnlineOperationResult<OnlineMatchSnapshot>> GetSnapshotAsync(
            OnlineSnapshotRequest request,
            CancellationToken cancellationToken = default)
        {
            return _getSnapshot(request, cancellationToken);
        }
    }

    private sealed class StubRealtimeClientFactory : IOnlineMatchRealtimeClientFactory
    {
        private readonly IOnlineMatchRealtimeClient _client;

        public StubRealtimeClientFactory(IOnlineMatchRealtimeClient client)
        {
            _client = client;
        }

        public IOnlineMatchRealtimeClient CreateClient()
        {
            return _client;
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

        public Task ConnectAsync(string playerToken, CancellationToken cancellationToken = default)
        {
            ConnectCalls++;
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
            Disconnected?.Invoke(this, null);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
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

        public void EmitSnapshot(long sequence, OnlineMatchSnapshot snapshot)
        {
            SnapshotReceived?.Invoke(
                this,
                new OnlineMatchSnapshotSyncEvent(
                    OnlineMatchProtocolConstants.EventMatchSnapshot,
                    new OnlineMatchEventMetadata(snapshot.MatchId, Guid.NewGuid().ToString("N"), sequence, DateTimeOffset.UtcNow),
                    snapshot));
        }

        public void EmitEnded(long sequence, OnlineMatchSnapshot snapshot)
        {
            EndedReceived?.Invoke(
                this,
                new OnlineMatchEndedSyncEvent(
                    OnlineMatchProtocolConstants.EventMatchEnded,
                    new OnlineMatchEventMetadata(snapshot.MatchId, Guid.NewGuid().ToString("N"), sequence, DateTimeOffset.UtcNow),
                    snapshot));
        }

        public void EmitError(long sequence, string matchId, string code, string message)
        {
            ErrorReceived?.Invoke(
                this,
                new OnlineMatchErrorSyncEvent(
                    OnlineMatchProtocolConstants.EventMatchError,
                    new OnlineMatchEventMetadata(matchId, Guid.NewGuid().ToString("N"), sequence, DateTimeOffset.UtcNow),
                    code,
                    message));
        }

        public void EmitReconnected()
        {
            Reconnected?.Invoke(this, EventArgs.Empty);
        }
    }
}
