using System;
using System.Threading;
using System.Threading.Tasks;

namespace Chess.Online;

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
