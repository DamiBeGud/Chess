using System;
using System.Threading;
using System.Threading.Tasks;

namespace Chess.Online;

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
