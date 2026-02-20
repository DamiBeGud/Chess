using System;

namespace Chess.Online;

public enum OnlineRealtimeApplyStatus
{
    Applied = 0,
    IgnoredOutOfOrder = 1,
    IgnoredTerminalRegression = 2
}

public sealed record OnlineRealtimeApplyResult(
    OnlineRealtimeApplyStatus Status,
    long UpdatedSequence,
    bool RequiresResync);

public sealed class OnlineRealtimeEventReducer
{
    public OnlineRealtimeApplyResult ReduceSnapshot(
        OnlineMatchSnapshot? currentSnapshot,
        long currentSequence,
        long incomingSequence,
        OnlineMatchSnapshot incomingSnapshot)
    {
        ArgumentNullException.ThrowIfNull(incomingSnapshot);

        if (incomingSequence <= currentSequence)
        {
            return new OnlineRealtimeApplyResult(
                OnlineRealtimeApplyStatus.IgnoredOutOfOrder,
                currentSequence,
                RequiresResync: false);
        }

        var requiresResync = currentSequence > 0 && incomingSequence > currentSequence + 1;
        if (currentSnapshot is not null &&
            IsTerminal(currentSnapshot) &&
            !IsTerminal(incomingSnapshot))
        {
            return new OnlineRealtimeApplyResult(
                OnlineRealtimeApplyStatus.IgnoredTerminalRegression,
                currentSequence,
                RequiresResync: true);
        }

        return new OnlineRealtimeApplyResult(
            OnlineRealtimeApplyStatus.Applied,
            incomingSequence,
            requiresResync);
    }

    public OnlineRealtimeApplyResult ReduceMetadataOnly(long currentSequence, long incomingSequence)
    {
        if (incomingSequence <= currentSequence)
        {
            return new OnlineRealtimeApplyResult(
                OnlineRealtimeApplyStatus.IgnoredOutOfOrder,
                currentSequence,
                RequiresResync: false);
        }

        var requiresResync = currentSequence > 0 && incomingSequence > currentSequence + 1;
        return new OnlineRealtimeApplyResult(
            OnlineRealtimeApplyStatus.Applied,
            incomingSequence,
            requiresResync);
    }

    private static bool IsTerminal(OnlineMatchSnapshot snapshot)
    {
        return string.Equals(
            snapshot.Status,
            OnlineMatchProtocolConstants.MatchStatusEnded,
            StringComparison.Ordinal);
    }
}
