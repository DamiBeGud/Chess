using System;
using System.Threading;
using System.Threading.Tasks;

namespace Chess.Online;

/// <summary>
/// IOnlineMatchRealtimeClient defines a contract within the Online module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include OnlineRealtimeLifecycleManager (Online), SignalROnlineMatchRealtimeClientFactory (Online), SignalROnlineMatchRealtimeClient (Online).
/// Key collaborators are Implementations include SignalROnlineMatchRealtimeClient (Online); related base contracts include IAsyncDisposable.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineRealtimeLifecycleManager (Online), SignalROnlineMatchRealtimeClientFactory (Online), SignalROnlineMatchRealtimeClient (Online), IOnlineMatchRealtimeClientFactory (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include SignalROnlineMatchRealtimeClient (Online); related base contracts include IAsyncDisposable.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public interface IOnlineMatchRealtimeClient : IAsyncDisposable
{
    event EventHandler<OnlineMatchSnapshotSyncEvent>? SnapshotReceived;
    event EventHandler<OnlineMatchUpdatedSyncEvent>? UpdatedReceived;
    event EventHandler<OnlineMatchPresenceChangedSyncEvent>? PresenceChangedReceived;
    event EventHandler<OnlineMatchEndedSyncEvent>? EndedReceived;
    event EventHandler<OnlineMatchErrorSyncEvent>? ErrorReceived;
    event EventHandler? Reconnected;
    event EventHandler<Exception?>? Disconnected;

    bool IsConnected { get; }

    Task ConnectAsync(string playerToken, CancellationToken cancellationToken = default);
    Task SubscribeMatchAsync(string matchId, string playerToken, CancellationToken cancellationToken = default);
    Task RequestResyncAsync(string matchId, string playerToken, CancellationToken cancellationToken = default);
    Task UnsubscribeMatchAsync(string matchId, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
}
