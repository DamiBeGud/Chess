using Chess.Domain;
using Chess.Online;
using Xunit;

namespace Chess.Tests;

public sealed class OnlineSessionStateCoordinatorTests
{
    [Fact]
    public void ApplyRealtimeSnapshot_AppliesSnapshotAndUpdatesSequence()
    {
        var coordinator = new OnlineSessionStateCoordinator(
            new OnlineSnapshotGameStateMapper(),
            new OnlineRealtimeEventReducer());
        var snapshot = CreateSnapshot(moveNumber: 1, sideToMove: OnlineMatchProtocolConstants.CreatorSeat);

        var reduction = coordinator.ApplyRealtimeSnapshot(1, snapshot, out var applyError);

        Assert.Equal(OnlineRealtimeApplyStatus.Applied, reduction.Status);
        Assert.Equal(1, coordinator.LastSequence);
        Assert.False(reduction.RequiresResync);
        Assert.Null(applyError);
        Assert.NotNull(coordinator.CurrentSnapshot);
        Assert.NotNull(coordinator.CurrentGameState);
    }

    [Fact]
    public void ApplyRealtimeSnapshot_WithSequenceGap_RequestsResyncAndSupportsNonReentrantGate()
    {
        var coordinator = new OnlineSessionStateCoordinator(
            new OnlineSnapshotGameStateMapper(),
            new OnlineRealtimeEventReducer());
        var snapshot = CreateSnapshot(moveNumber: 1, sideToMove: OnlineMatchProtocolConstants.CreatorSeat);

        coordinator.ApplyRealtimeSnapshot(1, snapshot, out _);
        var reduction = coordinator.ApplyRealtimeSnapshot(3, snapshot with { MoveNumber = 2 }, out _);

        Assert.True(reduction.RequiresResync);
        Assert.True(coordinator.TryBeginResync());
        Assert.False(coordinator.TryBeginResync());
        coordinator.EndResync();
        Assert.True(coordinator.TryBeginResync());
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
}
