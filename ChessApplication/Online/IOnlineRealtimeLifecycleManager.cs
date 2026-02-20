using System;
using System.Threading;
using System.Threading.Tasks;

namespace Chess.Online;

/// <summary>
/// IOnlineRealtimeLifecycleManager defines a contract within the Online module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include OnlineMatchSessionService (Online), OnlineRealtimeLifecycleManager (Online).
/// Key collaborators are Implementations include OnlineRealtimeLifecycleManager (Online); related base contracts include IAsyncDisposable.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineMatchSessionService (Online), OnlineRealtimeLifecycleManager (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include OnlineRealtimeLifecycleManager (Online); related base contracts include IAsyncDisposable.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
internal interface IOnlineRealtimeLifecycleManager : IAsyncDisposable
{
    event EventHandler<OnlineMatchSnapshotSyncEvent>? SnapshotReceived;
    event EventHandler<OnlineMatchUpdatedSyncEvent>? UpdatedReceived;
    event EventHandler<OnlineMatchPresenceChangedSyncEvent>? PresenceChangedReceived;
    event EventHandler<OnlineMatchEndedSyncEvent>? EndedReceived;
    event EventHandler<OnlineMatchErrorSyncEvent>? ErrorReceived;
    event EventHandler? Reconnected;
    event EventHandler<Exception?>? Disconnected;

    bool IsConnected { get; }

    Task<OnlineUserError?> EnsureConnectedAndSubscribedAsync(
        OnlineMatchCredentials credentials,
        CancellationToken cancellationToken = default);

    Task<OnlineUserError?> ResubscribeAfterReconnectAsync(
        OnlineMatchCredentials credentials,
        CancellationToken cancellationToken = default);

    Task SuspendAsync(CancellationToken cancellationToken = default);

    Task ResetAsync(OnlineMatchCredentials? credentials, CancellationToken cancellationToken = default);
}
