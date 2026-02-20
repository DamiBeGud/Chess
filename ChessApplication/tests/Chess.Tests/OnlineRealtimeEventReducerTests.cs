using Chess.Online;
using Xunit;

namespace Chess.Tests;

public sealed class OnlineRealtimeEventReducerTests
{
    private readonly OnlineRealtimeEventReducer _reducer = new();

    [Fact]
    public void ReduceSnapshot_AppliesNewerSequence()
    {
        var current = CreateSnapshot(OnlineMatchProtocolConstants.MatchStatusInProgress);
        var incoming = CreateSnapshot(OnlineMatchProtocolConstants.MatchStatusInProgress);

        var result = _reducer.ReduceSnapshot(
            currentSnapshot: current,
            currentSequence: 4,
            incomingSequence: 5,
            incomingSnapshot: incoming);

        Assert.Equal(OnlineRealtimeApplyStatus.Applied, result.Status);
        Assert.Equal(5, result.UpdatedSequence);
        Assert.False(result.RequiresResync);
    }

    [Fact]
    public void ReduceSnapshot_IgnoresOutOfOrderSequence()
    {
        var current = CreateSnapshot(OnlineMatchProtocolConstants.MatchStatusInProgress);
        var incoming = CreateSnapshot(OnlineMatchProtocolConstants.MatchStatusInProgress);

        var result = _reducer.ReduceSnapshot(
            currentSnapshot: current,
            currentSequence: 7,
            incomingSequence: 6,
            incomingSnapshot: incoming);

        Assert.Equal(OnlineRealtimeApplyStatus.IgnoredOutOfOrder, result.Status);
        Assert.Equal(7, result.UpdatedSequence);
    }

    [Fact]
    public void ReduceSnapshot_TerminalRegressionTriggersResync()
    {
        var terminal = CreateSnapshot(OnlineMatchProtocolConstants.MatchStatusEnded);
        var stale = CreateSnapshot(OnlineMatchProtocolConstants.MatchStatusInProgress);

        var result = _reducer.ReduceSnapshot(
            currentSnapshot: terminal,
            currentSequence: 10,
            incomingSequence: 11,
            incomingSnapshot: stale);

        Assert.Equal(OnlineRealtimeApplyStatus.IgnoredTerminalRegression, result.Status);
        Assert.Equal(10, result.UpdatedSequence);
        Assert.True(result.RequiresResync);
    }

    [Fact]
    public void ReduceMetadataOnly_DetectsSequenceGap()
    {
        var result = _reducer.ReduceMetadataOnly(currentSequence: 2, incomingSequence: 5);

        Assert.Equal(OnlineRealtimeApplyStatus.Applied, result.Status);
        Assert.Equal(5, result.UpdatedSequence);
        Assert.True(result.RequiresResync);
    }

    private static OnlineMatchSnapshot CreateSnapshot(string status)
    {
        return new OnlineMatchSnapshot(
            MatchId: "58b02d5d9d5c43cc9a0314a8f1f4a14c",
            SideToMove: OnlineMatchProtocolConstants.CreatorSeat,
            MoveNumber: 1,
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
            Status: status,
            Resolution: status == OnlineMatchProtocolConstants.MatchStatusEnded
                ? OnlineMatchProtocolConstants.MatchResolutionDraw
                : null,
            WinnerSeat: null,
            Presence: new OnlineMatchPresence(
                new OnlineSeatPresence(OnlineMatchProtocolConstants.CreatorSeat, true, true, null, null),
                new OnlineSeatPresence(OnlineMatchProtocolConstants.JoinerSeat, true, true, null, null)));
    }
}
