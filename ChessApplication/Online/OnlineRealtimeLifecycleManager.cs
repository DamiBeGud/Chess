using System;
using System.Threading;
using System.Threading.Tasks;

namespace Chess.Online;

internal sealed class OnlineRealtimeLifecycleManager : IOnlineRealtimeLifecycleManager
{
    private readonly IOnlineMatchRealtimeClientFactory _realtimeClientFactory;
    private readonly IOnlineTransportErrorPolicy _transportErrorPolicy;

    private IOnlineMatchRealtimeClient? _realtimeClient;

    internal OnlineRealtimeLifecycleManager(
        IOnlineMatchRealtimeClientFactory realtimeClientFactory,
        IOnlineTransportErrorPolicy transportErrorPolicy)
    {
        _realtimeClientFactory = realtimeClientFactory ?? throw new ArgumentNullException(nameof(realtimeClientFactory));
        _transportErrorPolicy = transportErrorPolicy ?? throw new ArgumentNullException(nameof(transportErrorPolicy));
    }

    public event EventHandler<OnlineMatchSnapshotSyncEvent>? SnapshotReceived;
    public event EventHandler<OnlineMatchUpdatedSyncEvent>? UpdatedReceived;
    public event EventHandler<OnlineMatchPresenceChangedSyncEvent>? PresenceChangedReceived;
    public event EventHandler<OnlineMatchEndedSyncEvent>? EndedReceived;
    public event EventHandler<OnlineMatchErrorSyncEvent>? ErrorReceived;
    public event EventHandler? Reconnected;
    public event EventHandler<Exception?>? Disconnected;

    public bool IsConnected => _realtimeClient?.IsConnected is true;

    public async Task<OnlineUserError?> EnsureConnectedAndSubscribedAsync(
        OnlineMatchCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        var realtimeClient = GetOrCreateRealtimeClient();

        try
        {
            await realtimeClient.ConnectAsync(credentials.PlayerToken, cancellationToken);
        }
        catch (Exception ex)
        {
            return _transportErrorPolicy.Map(ex, "Unable to connect to realtime match updates.");
        }

        try
        {
            await realtimeClient.SubscribeMatchAsync(credentials.MatchId, credentials.PlayerToken, cancellationToken);
        }
        catch (Exception ex)
        {
            return _transportErrorPolicy.Map(ex, "Unable to subscribe to realtime match updates.");
        }

        try
        {
            await realtimeClient.RequestResyncAsync(credentials.MatchId, credentials.PlayerToken, cancellationToken);
        }
        catch (Exception ex)
        {
            return _transportErrorPolicy.Map(ex, "Unable to request realtime resync.");
        }

        return null;
    }

    public async Task<OnlineUserError?> ResubscribeAfterReconnectAsync(
        OnlineMatchCredentials credentials,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        if (_realtimeClient is null)
        {
            return null;
        }

        try
        {
            await _realtimeClient.SubscribeMatchAsync(
                credentials.MatchId,
                credentials.PlayerToken,
                cancellationToken);
            await _realtimeClient.RequestResyncAsync(
                credentials.MatchId,
                credentials.PlayerToken,
                cancellationToken);
            return null;
        }
        catch (Exception ex)
        {
            return _transportErrorPolicy.Map(ex, "Unable to resubscribe after reconnect.");
        }
    }

    public async Task SuspendAsync(CancellationToken cancellationToken = default)
    {
        if (_realtimeClient is null)
        {
            return;
        }

        await _realtimeClient.DisconnectAsync(cancellationToken);
    }

    public async Task ResetAsync(OnlineMatchCredentials? credentials, CancellationToken cancellationToken = default)
    {
        if (_realtimeClient is null)
        {
            return;
        }

        var realtimeClient = _realtimeClient;
        DetachRealtimeHandlers(realtimeClient);

        try
        {
            if (credentials is not null && realtimeClient.IsConnected)
            {
                await realtimeClient.UnsubscribeMatchAsync(credentials.MatchId, cancellationToken);
            }
        }
        catch (Exception)
        {
        }

        try
        {
            await realtimeClient.DisconnectAsync(cancellationToken);
        }
        catch (Exception)
        {
        }

        await realtimeClient.DisposeAsync();
        _realtimeClient = null;
    }

    public async ValueTask DisposeAsync()
    {
        await ResetAsync(credentials: null, CancellationToken.None);
    }

    private IOnlineMatchRealtimeClient GetOrCreateRealtimeClient()
    {
        if (_realtimeClient is not null)
        {
            return _realtimeClient;
        }

        _realtimeClient = _realtimeClientFactory.CreateClient();
        AttachRealtimeHandlers(_realtimeClient);
        return _realtimeClient;
    }

    private void AttachRealtimeHandlers(IOnlineMatchRealtimeClient realtimeClient)
    {
        realtimeClient.SnapshotReceived += OnSnapshotReceived;
        realtimeClient.UpdatedReceived += OnUpdatedReceived;
        realtimeClient.PresenceChangedReceived += OnPresenceChangedReceived;
        realtimeClient.EndedReceived += OnEndedReceived;
        realtimeClient.ErrorReceived += OnErrorReceived;
        realtimeClient.Reconnected += OnReconnected;
        realtimeClient.Disconnected += OnDisconnected;
    }

    private void DetachRealtimeHandlers(IOnlineMatchRealtimeClient realtimeClient)
    {
        realtimeClient.SnapshotReceived -= OnSnapshotReceived;
        realtimeClient.UpdatedReceived -= OnUpdatedReceived;
        realtimeClient.PresenceChangedReceived -= OnPresenceChangedReceived;
        realtimeClient.EndedReceived -= OnEndedReceived;
        realtimeClient.ErrorReceived -= OnErrorReceived;
        realtimeClient.Reconnected -= OnReconnected;
        realtimeClient.Disconnected -= OnDisconnected;
    }

    private void OnSnapshotReceived(object? sender, OnlineMatchSnapshotSyncEvent payload)
    {
        SnapshotReceived?.Invoke(this, payload);
    }

    private void OnUpdatedReceived(object? sender, OnlineMatchUpdatedSyncEvent payload)
    {
        UpdatedReceived?.Invoke(this, payload);
    }

    private void OnPresenceChangedReceived(object? sender, OnlineMatchPresenceChangedSyncEvent payload)
    {
        PresenceChangedReceived?.Invoke(this, payload);
    }

    private void OnEndedReceived(object? sender, OnlineMatchEndedSyncEvent payload)
    {
        EndedReceived?.Invoke(this, payload);
    }

    private void OnErrorReceived(object? sender, OnlineMatchErrorSyncEvent payload)
    {
        ErrorReceived?.Invoke(this, payload);
    }

    private void OnReconnected(object? sender, EventArgs e)
    {
        Reconnected?.Invoke(this, EventArgs.Empty);
    }

    private void OnDisconnected(object? sender, Exception? exception)
    {
        Disconnected?.Invoke(this, exception);
    }
}
