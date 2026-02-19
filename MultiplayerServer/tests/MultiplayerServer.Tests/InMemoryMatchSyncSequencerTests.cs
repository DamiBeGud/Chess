using MultiplayerServer.Hubs.V1;

namespace MultiplayerServer.Tests;

public sealed class InMemoryMatchSyncSequencerTests
{
    [Fact]
    public void TryReserve_AssignsMonotonicSequencePerMatch()
    {
        var sequencer = new InMemoryMatchSyncSequencer();

        var firstAccepted = sequencer.TryReserve("match-1", "event-1", out var firstSequence);
        var secondAccepted = sequencer.TryReserve("match-1", "event-2", out var secondSequence);

        Assert.True(firstAccepted);
        Assert.True(secondAccepted);
        Assert.Equal(1, firstSequence);
        Assert.Equal(2, secondSequence);
    }

    [Fact]
    public void TryReserve_DuplicateEventId_IsRejectedWithoutAdvancingSequence()
    {
        var sequencer = new InMemoryMatchSyncSequencer();

        var firstAccepted = sequencer.TryReserve("match-1", "event-1", out var firstSequence);
        var duplicateAccepted = sequencer.TryReserve("match-1", "event-1", out var duplicateSequence);
        var nextAccepted = sequencer.TryReserve("match-1", "event-2", out var nextSequence);

        Assert.True(firstAccepted);
        Assert.False(duplicateAccepted);
        Assert.True(nextAccepted);
        Assert.Equal(1, firstSequence);
        Assert.Equal(1, duplicateSequence);
        Assert.Equal(2, nextSequence);
    }

    [Fact]
    public void TryReserve_TracksSequenceIndependentlyPerMatch()
    {
        var sequencer = new InMemoryMatchSyncSequencer();

        sequencer.TryReserve("match-1", "event-1", out var matchOneSequence);
        sequencer.TryReserve("match-2", "event-1", out var matchTwoSequence);

        Assert.Equal(1, matchOneSequence);
        Assert.Equal(1, matchTwoSequence);
    }
}
