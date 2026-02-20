using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;

namespace Chess.Online;

/// <summary>
/// SignalROnlineMatchRealtimeClient is a concrete type within the Online module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include SignalROnlineMatchRealtimeClientFactory (Online).
/// Key collaborators are Uri, HttpConnectionOptions, IOnlineMatchRealtimeClient.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> SignalROnlineMatchRealtimeClientFactory (Online)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> Uri, HttpConnectionOptions, IOnlineMatchRealtimeClient.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed class SignalROnlineMatchRealtimeClient : IOnlineMatchRealtimeClient
{
    private readonly Uri _hubUri;
    private readonly Action<HttpConnectionOptions>? _configureConnection;
    private HubConnection? _connection;
    private string? _activeToken;

    public SignalROnlineMatchRealtimeClient(
        Uri baseUri,
        Action<HttpConnectionOptions>? configureConnection = null)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        _hubUri = new Uri(baseUri, OnlineMatchProtocolConstants.MatchHubRoute);
        _configureConnection = configureConnection;
    }

    public event EventHandler<OnlineMatchSnapshotSyncEvent>? SnapshotReceived;
    public event EventHandler<OnlineMatchUpdatedSyncEvent>? UpdatedReceived;
    public event EventHandler<OnlineMatchPresenceChangedSyncEvent>? PresenceChangedReceived;
    public event EventHandler<OnlineMatchEndedSyncEvent>? EndedReceived;
    public event EventHandler<OnlineMatchErrorSyncEvent>? ErrorReceived;
    public event EventHandler? Reconnected;
    public event EventHandler<Exception?>? Disconnected;

    public bool IsConnected => _connection?.State is HubConnectionState.Connected;

    public async Task ConnectAsync(string playerToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(playerToken))
        {
            throw new ArgumentException("Player token is required.", nameof(playerToken));
        }

        var normalizedToken = playerToken.Trim();
        if (_connection is not null &&
            string.Equals(_activeToken, normalizedToken, StringComparison.Ordinal))
        {
            if (_connection.State is HubConnectionState.Connected)
            {
                return;
            }

            if (_connection.State is HubConnectionState.Disconnected)
            {
                await _connection.StartAsync(cancellationToken);
                return;
            }
        }

        await DisposeConnectionAsync();

        _activeToken = normalizedToken;
        _connection = BuildConnection(normalizedToken);
        RegisterHandlers(_connection);
        await _connection.StartAsync(cancellationToken);
    }

    public Task SubscribeMatchAsync(string matchId, string playerToken, CancellationToken cancellationToken = default)
    {
        return RequireConnection().InvokeCoreAsync(
            "SubscribeMatch",
            new object?[] { matchId, playerToken },
            cancellationToken);
    }

    public Task RequestResyncAsync(string matchId, string playerToken, CancellationToken cancellationToken = default)
    {
        return RequireConnection().InvokeCoreAsync(
            "RequestResync",
            new object?[] { matchId, playerToken },
            cancellationToken);
    }

    public Task UnsubscribeMatchAsync(string matchId, CancellationToken cancellationToken = default)
    {
        return RequireConnection().InvokeCoreAsync(
            "UnsubscribeMatch",
            new object?[] { matchId },
            cancellationToken);
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is null || _connection.State is HubConnectionState.Disconnected)
        {
            return;
        }

        await _connection.StopAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeConnectionAsync();
    }

    private HubConnection BuildConnection(string accessToken)
    {
        return new HubConnectionBuilder()
            .WithUrl(
                _hubUri,
                options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                    _configureConnection?.Invoke(options);
                })
            .WithAutomaticReconnect()
            .Build();
    }

    private void RegisterHandlers(HubConnection connection)
    {
        connection.On<OnlineMatchSnapshotSyncEvent>(
            OnlineMatchProtocolConstants.EventMatchSnapshot,
            payload => SnapshotReceived?.Invoke(this, payload));
        connection.On<OnlineMatchUpdatedSyncEvent>(
            OnlineMatchProtocolConstants.EventMatchUpdated,
            payload => UpdatedReceived?.Invoke(this, payload));
        connection.On<OnlineMatchPresenceChangedSyncEvent>(
            OnlineMatchProtocolConstants.EventMatchPresenceChanged,
            payload => PresenceChangedReceived?.Invoke(this, payload));
        connection.On<OnlineMatchEndedSyncEvent>(
            OnlineMatchProtocolConstants.EventMatchEnded,
            payload => EndedReceived?.Invoke(this, payload));
        connection.On<OnlineMatchErrorSyncEvent>(
            OnlineMatchProtocolConstants.EventMatchError,
            payload => ErrorReceived?.Invoke(this, payload));

        connection.Reconnected += _ =>
        {
            Reconnected?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        };

        connection.Closed += error =>
        {
            Disconnected?.Invoke(this, error);
            return Task.CompletedTask;
        };
    }

    private HubConnection RequireConnection()
    {
        if (_connection is null)
        {
            throw new InvalidOperationException("Realtime connection is not initialized.");
        }

        return _connection;
    }

    private async Task DisposeConnectionAsync()
    {
        if (_connection is null)
        {
            return;
        }

        var connection = _connection;
        _connection = null;
        _activeToken = null;

        try
        {
            if (connection.State is not HubConnectionState.Disconnected)
            {
                await connection.StopAsync(CancellationToken.None);
            }
        }
        catch (InvalidOperationException)
        {
        }

        await connection.DisposeAsync();
    }
}
