using System;

namespace Chess.Online;

/// <summary>
/// OnlineRealtimeApplyStatus is an enumeration within the Online module.
/// Its named values model a bounded set of states, options, or outcomes used by collaborators.
/// Primary production consumers include OnlineRealtimeEventReducer (Online), OnlineSessionStateCoordinator (Online), OnlineRealtimeApplyResult (Online).
/// Its values are interpreted by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineRealtimeEventReducer (Online), OnlineSessionStateCoordinator (Online), OnlineRealtimeApplyResult (Online), OnlineMatchSessionService (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> Its values are interpreted by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public enum OnlineRealtimeApplyStatus
{
    Applied = 0,
    IgnoredOutOfOrder = 1,
    IgnoredTerminalRegression = 2
}

/// <summary>
/// OnlineRealtimeApplyResult is a record type within the Online module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include OnlineRealtimeEventReducer (Online), OnlineSessionStateCoordinator (Online).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineRealtimeEventReducer (Online), OnlineSessionStateCoordinator (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed record OnlineRealtimeApplyResult(
    OnlineRealtimeApplyStatus Status,
    long UpdatedSequence,
    bool RequiresResync);

/// <summary>
/// OnlineRealtimeEventReducer is a concrete type within the Online module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include OnlineSessionStateCoordinator (Online), OnlineMatchSessionService (Online), App (AppShell).
/// Key collaborators are Online, transport, session, Applied, IgnoredOutOfOrder, incomingSnapshot.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineSessionStateCoordinator (Online), OnlineMatchSessionService (Online), App (AppShell)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> Online, transport, session, Applied, IgnoredOutOfOrder, incomingSnapshot.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
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
