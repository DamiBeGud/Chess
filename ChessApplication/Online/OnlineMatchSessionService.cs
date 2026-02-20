using System;
using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;
using Microsoft.AspNetCore.SignalR;

namespace Chess.Online;

public sealed class OnlineMatchSessionService : IOnlineMatchSessionService
{
    private readonly IOnlineMatchHttpClient _httpClient;
    private readonly IOnlineErrorMapper _errorMapper;
    private readonly IOnlineMatchRealtimeClientFactory _realtimeClientFactory;
    private readonly IOnlineSnapshotGameStateMapper _snapshotMapper;
    private readonly OnlineRealtimeEventReducer _reducer;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private IOnlineMatchRealtimeClient? _realtimeClient;
    private OnlineMatchCredentials? _credentials;
    private string? _joinCode;
    private bool _isResyncInFlight;
    private long _lastSequence;

    public OnlineMatchSessionService(
        IOnlineMatchHttpClient httpClient,
        IOnlineErrorMapper errorMapper,
        IOnlineMatchRealtimeClientFactory realtimeClientFactory,
        IOnlineSnapshotGameStateMapper snapshotMapper,
        OnlineRealtimeEventReducer reducer)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _errorMapper = errorMapper ?? throw new ArgumentNullException(nameof(errorMapper));
        _realtimeClientFactory = realtimeClientFactory ?? throw new ArgumentNullException(nameof(realtimeClientFactory));
        _snapshotMapper = snapshotMapper ?? throw new ArgumentNullException(nameof(snapshotMapper));
        _reducer = reducer ?? throw new ArgumentNullException(nameof(reducer));
    }

    public event EventHandler? SessionStateChanged;
    public event EventHandler<OnlineUserError>? SessionError;

    public bool IsInMatch => _credentials is not null;
    public bool IsConnected => _realtimeClient?.IsConnected is true;
    public string? MatchId => _credentials?.MatchId;
    public string? JoinCode => _joinCode;
    public PieceColor? Seat => _credentials?.Seat;
    public OnlineMatchSnapshot? CurrentSnapshot { get; private set; }
    public GameState? CurrentGameState { get; private set; }
    public long LastSequence => _lastSequence;

    public async Task<OnlineOperationResult<OnlineCreatedMatch>> CreateMatchAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await ResetSessionInternalAsync(cancellationToken);

            var createResult = await _httpClient.CreateMatchAsync(cancellationToken);
            if (!createResult.IsSuccess)
            {
                return OnlineOperationResult<OnlineCreatedMatch>.Failure(createResult.Error!);
            }

            var created = createResult.Value!;
            _credentials = new OnlineMatchCredentials(created.MatchId, created.CreatorToken, PieceColor.White);
            _joinCode = created.JoinCode;

            var recoverResult = await RecoverInternalAsync(cancellationToken);
            if (!recoverResult.IsSuccess)
            {
                return OnlineOperationResult<OnlineCreatedMatch>.Failure(recoverResult.Error!);
            }

            NotifyStateChanged();
            return OnlineOperationResult<OnlineCreatedMatch>.Success(
                new OnlineCreatedMatch(created.MatchId, created.JoinCode, PieceColor.White));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OnlineOperationResult<OnlineJoinedMatch>> JoinMatchAsync(
        string joinCode,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await ResetSessionInternalAsync(cancellationToken);

            var joinResult = await _httpClient.JoinMatchAsync(joinCode, cancellationToken);
            if (!joinResult.IsSuccess)
            {
                return OnlineOperationResult<OnlineJoinedMatch>.Failure(joinResult.Error!);
            }

            var joined = joinResult.Value!;
            if (!TryParseSeat(joined.Seat, out var seat))
            {
                return OnlineOperationResult<OnlineJoinedMatch>.Failure(
                    new OnlineUserError(
                        "invalid_seat",
                        "Server returned an unsupported seat value.",
                        OnlineUserAction.StartNewMatch,
                        IsTerminal: true));
            }

            _credentials = new OnlineMatchCredentials(joined.MatchId, joined.PlayerToken, seat);
            _joinCode = null;

            var recoverResult = await RecoverInternalAsync(cancellationToken);
            if (!recoverResult.IsSuccess)
            {
                return OnlineOperationResult<OnlineJoinedMatch>.Failure(recoverResult.Error!);
            }

            NotifyStateChanged();
            return OnlineOperationResult<OnlineJoinedMatch>.Success(new OnlineJoinedMatch(joined.MatchId, seat));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OnlineOperationResult<OnlineResumedMatch>> ResumeMatchAsync(
        string matchId,
        string playerToken,
        PieceColor seat,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await ResetSessionInternalAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(matchId) || string.IsNullOrWhiteSpace(playerToken))
            {
                return OnlineOperationResult<OnlineResumedMatch>.Failure(
                    new OnlineUserError(
                        "resume_credentials_required",
                        "Match ID and player token are required.",
                        OnlineUserAction.Retry));
            }

            _credentials = new OnlineMatchCredentials(matchId.Trim(), playerToken.Trim(), seat);
            _joinCode = null;

            var recoverResult = await RecoverInternalAsync(cancellationToken);
            if (!recoverResult.IsSuccess)
            {
                return OnlineOperationResult<OnlineResumedMatch>.Failure(recoverResult.Error!);
            }

            NotifyStateChanged();
            return OnlineOperationResult<OnlineResumedMatch>.Success(new OnlineResumedMatch(matchId.Trim(), seat));
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OnlineOperationResult<OnlineMatchSnapshot>> SubmitMoveAsync(
        Square fromSquare,
        Square toSquare,
        PieceType? promotionPieceType = null,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_credentials is null)
            {
                return OnlineOperationResult<OnlineMatchSnapshot>.Failure(CreateSessionNotStartedError());
            }

            var moveResult = await _httpClient.SubmitMoveAsync(
                new OnlineSubmitMoveRequest(
                    _credentials.MatchId,
                    _credentials.PlayerToken,
                    ToCoordinate(fromSquare),
                    ToCoordinate(toSquare),
                    ToPromotionToken(promotionPieceType)),
                cancellationToken);
            if (!moveResult.IsSuccess)
            {
                return OnlineOperationResult<OnlineMatchSnapshot>.Failure(moveResult.Error!);
            }

            var snapshot = moveResult.Value!.Snapshot;
            var applyError = TryApplySnapshot(snapshot);
            if (applyError is not null)
            {
                return OnlineOperationResult<OnlineMatchSnapshot>.Failure(applyError);
            }

            NotifyStateChanged();
            return OnlineOperationResult<OnlineMatchSnapshot>.Success(snapshot);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OnlineOperationResult<OnlineMatchSnapshot>> RecoverAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await RecoverInternalAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OnlineOperationResult<OnlineMatchSnapshot>> RequestResyncAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await RecoverInternalAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SuspendRealtimeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_realtimeClient is null)
            {
                return;
            }

            await _realtimeClient.DisconnectAsync(cancellationToken);
            NotifyStateChanged();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task LeaveMatchAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await ResetSessionInternalAsync(cancellationToken);
            NotifyStateChanged();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            await ResetSessionInternalAsync(CancellationToken.None);
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private async Task<OnlineOperationResult<OnlineMatchSnapshot>> RecoverInternalAsync(CancellationToken cancellationToken)
    {
        if (_credentials is null)
        {
            return OnlineOperationResult<OnlineMatchSnapshot>.Failure(CreateSessionNotStartedError());
        }

        var snapshotResult = await _httpClient.GetSnapshotAsync(
            new OnlineSnapshotRequest(_credentials.MatchId, _credentials.PlayerToken),
            cancellationToken);
        if (!snapshotResult.IsSuccess)
        {
            return OnlineOperationResult<OnlineMatchSnapshot>.Failure(snapshotResult.Error!);
        }

        var snapshot = snapshotResult.Value!;
        var applyError = TryApplySnapshot(snapshot);
        if (applyError is not null)
        {
            return OnlineOperationResult<OnlineMatchSnapshot>.Failure(applyError);
        }

        var connectionError = await EnsureConnectedAndSubscribedInternalAsync(_credentials, cancellationToken);
        NotifyStateChanged();

        if (connectionError is not null)
        {
            NotifyError(connectionError);
            return OnlineOperationResult<OnlineMatchSnapshot>.Failure(connectionError);
        }

        return OnlineOperationResult<OnlineMatchSnapshot>.Success(snapshot);
    }

    private async Task<OnlineUserError?> EnsureConnectedAndSubscribedInternalAsync(
        OnlineMatchCredentials credentials,
        CancellationToken cancellationToken)
    {
        var realtimeClient = GetOrCreateRealtimeClient();

        try
        {
            await realtimeClient.ConnectAsync(credentials.PlayerToken, cancellationToken);
        }
        catch (Exception ex)
        {
            return MapTransportException(ex, "Unable to connect to realtime match updates.");
        }

        try
        {
            await realtimeClient.SubscribeMatchAsync(credentials.MatchId, credentials.PlayerToken, cancellationToken);
        }
        catch (Exception ex)
        {
            return MapTransportException(ex, "Unable to subscribe to realtime match updates.");
        }

        try
        {
            await realtimeClient.RequestResyncAsync(credentials.MatchId, credentials.PlayerToken, cancellationToken);
        }
        catch (Exception ex)
        {
            return MapTransportException(ex, "Unable to request realtime resync.");
        }

        return null;
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

    private async Task ResetSessionInternalAsync(CancellationToken cancellationToken)
    {
        if (_realtimeClient is not null)
        {
            var realtimeClient = _realtimeClient;
            DetachRealtimeHandlers(realtimeClient);

            try
            {
                if (_credentials is not null && realtimeClient.IsConnected)
                {
                    await realtimeClient.UnsubscribeMatchAsync(_credentials.MatchId, cancellationToken);
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

        _credentials = null;
        _joinCode = null;
        _lastSequence = 0;
        _isResyncInFlight = false;
        CurrentSnapshot = null;
        CurrentGameState = null;
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
        _ = HandleSnapshotEventAsync(payload.Metadata.Sequence, payload.Snapshot);
    }

    private void OnUpdatedReceived(object? sender, OnlineMatchUpdatedSyncEvent payload)
    {
        _ = HandleSnapshotEventAsync(payload.Metadata.Sequence, payload.Snapshot);
    }

    private void OnPresenceChangedReceived(object? sender, OnlineMatchPresenceChangedSyncEvent payload)
    {
        _ = HandleSnapshotEventAsync(payload.Metadata.Sequence, payload.Snapshot);
    }

    private void OnEndedReceived(object? sender, OnlineMatchEndedSyncEvent payload)
    {
        _ = HandleSnapshotEventAsync(payload.Metadata.Sequence, payload.Snapshot);
    }

    private void OnErrorReceived(object? sender, OnlineMatchErrorSyncEvent payload)
    {
        _ = HandleErrorEventAsync(payload.Metadata.Sequence, payload.Code, payload.Message);
    }

    private void OnReconnected(object? sender, EventArgs e)
    {
        _ = HandleReconnectedAsync();
    }

    private void OnDisconnected(object? sender, Exception? exception)
    {
        NotifyStateChanged();
    }

    private async Task HandleSnapshotEventAsync(long sequence, OnlineMatchSnapshot snapshot)
    {
        var shouldRunResync = false;
        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            var reduction = _reducer.ReduceSnapshot(CurrentSnapshot, _lastSequence, sequence, snapshot);
            if (reduction.Status is OnlineRealtimeApplyStatus.Applied)
            {
                _lastSequence = reduction.UpdatedSequence;
                var applyError = TryApplySnapshot(snapshot);
                if (applyError is not null)
                {
                    NotifyError(applyError);
                    return;
                }

                NotifyStateChanged();
            }

            if (reduction.RequiresResync && !_isResyncInFlight)
            {
                _isResyncInFlight = true;
                shouldRunResync = true;
            }
        }
        finally
        {
            _gate.Release();
        }

        if (shouldRunResync)
        {
            await RunBackgroundResyncAsync();
        }
    }

    private async Task HandleErrorEventAsync(long sequence, string code, string message)
    {
        var shouldRunResync = false;
        OnlineUserError mappedError;

        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            var reduction = _reducer.ReduceMetadataOnly(_lastSequence, sequence);
            if (reduction.Status is OnlineRealtimeApplyStatus.Applied)
            {
                _lastSequence = reduction.UpdatedSequence;
            }

            if (reduction.RequiresResync && !_isResyncInFlight)
            {
                _isResyncInFlight = true;
                shouldRunResync = true;
            }

            mappedError = _errorMapper.Map(new OnlineTransportError(code, message));
        }
        finally
        {
            _gate.Release();
        }

        NotifyError(mappedError);

        if (shouldRunResync)
        {
            await RunBackgroundResyncAsync();
        }
    }

    private async Task HandleReconnectedAsync()
    {
        OnlineUserError? reconnectError = null;

        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            if (_credentials is null || _realtimeClient is null)
            {
                return;
            }

            try
            {
                await _realtimeClient.SubscribeMatchAsync(
                    _credentials.MatchId,
                    _credentials.PlayerToken,
                    CancellationToken.None);
                await _realtimeClient.RequestResyncAsync(
                    _credentials.MatchId,
                    _credentials.PlayerToken,
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                reconnectError = MapTransportException(ex, "Unable to resubscribe after reconnect.");
            }
        }
        finally
        {
            _gate.Release();
        }

        if (reconnectError is not null)
        {
            NotifyError(reconnectError);
        }
    }

    private async Task RunBackgroundResyncAsync()
    {
        try
        {
            var result = await RequestResyncAsync(CancellationToken.None);
            if (!result.IsSuccess && result.Error is not null)
            {
                NotifyError(result.Error);
            }
        }
        finally
        {
            await _gate.WaitAsync(CancellationToken.None);
            try
            {
                _isResyncInFlight = false;
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    private OnlineUserError? TryApplySnapshot(OnlineMatchSnapshot snapshot)
    {
        try
        {
            CurrentSnapshot = snapshot;
            CurrentGameState = _snapshotMapper.Map(snapshot);
            return null;
        }
        catch (Exception)
        {
            return new OnlineUserError(
                "invalid_snapshot",
                "Received an invalid snapshot from the server.",
                OnlineUserAction.RequestResync);
        }
    }

    private OnlineUserError MapTransportException(Exception exception, string fallbackMessage)
    {
        var transportError = ToTransportError(exception, fallbackMessage);
        return _errorMapper.Map(transportError);
    }

    private static OnlineTransportError ToTransportError(Exception exception, string fallbackMessage)
    {
        if (exception is HubException hubException &&
            TryParseHubExceptionMessage(hubException.Message, out var code, out var message))
        {
            return new OnlineTransportError(code, message);
        }

        var messageValue = string.IsNullOrWhiteSpace(exception.Message)
            ? fallbackMessage
            : exception.Message;
        return new OnlineTransportError("transport_error", messageValue);
    }

    private static bool TryParseHubExceptionMessage(
        string? hubMessage,
        out string code,
        out string message)
    {
        code = string.Empty;
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(hubMessage))
        {
            return false;
        }

        var separatorIndex = hubMessage.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == hubMessage.Length - 1)
        {
            return false;
        }

        code = hubMessage[..separatorIndex].Trim();
        message = hubMessage[(separatorIndex + 1)..].Trim();
        return !string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(message);
    }

    private static bool TryParseSeat(string seatValue, out PieceColor seat)
    {
        if (string.Equals(seatValue, OnlineMatchProtocolConstants.CreatorSeat, StringComparison.Ordinal))
        {
            seat = PieceColor.White;
            return true;
        }

        if (string.Equals(seatValue, OnlineMatchProtocolConstants.JoinerSeat, StringComparison.Ordinal))
        {
            seat = PieceColor.Black;
            return true;
        }

        seat = default;
        return false;
    }

    private static string ToCoordinate(Square square)
    {
        return $"{(char)('a' + square.File)}{square.Rank + 1}";
    }

    private static string? ToPromotionToken(PieceType? promotionPieceType)
    {
        return promotionPieceType switch
        {
            null => null,
            PieceType.Queen => "Q",
            PieceType.Rook => "R",
            PieceType.Bishop => "B",
            PieceType.Knight => "N",
            _ => null
        };
    }

    private static OnlineUserError CreateSessionNotStartedError()
    {
        return new OnlineUserError(
            "session_not_started",
            "Join or create an online match first.",
            OnlineUserAction.Retry);
    }

    private void NotifyStateChanged()
    {
        SessionStateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NotifyError(OnlineUserError error)
    {
        SessionError?.Invoke(this, error);
    }
}
