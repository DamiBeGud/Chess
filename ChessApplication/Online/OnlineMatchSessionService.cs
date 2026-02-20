using System;
using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;

namespace Chess.Online;

/// <summary>
/// OnlineMatchSessionService is a concrete type within the Online module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include App (AppShell).
/// Key collaborators are IOnlineMatchHttpClient, IOnlineErrorMapper, IOnlineMatchRealtimeClientFactory, IOnlineSnapshotGameStateMapper, OnlineRealtimeEventReducer, IOnlineMatchTransportAdapter.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> App (AppShell)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> IOnlineMatchHttpClient, IOnlineErrorMapper, IOnlineMatchRealtimeClientFactory, IOnlineSnapshotGameStateMapper, OnlineRealtimeEventReducer, IOnlineMatchTransportAdapter.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed class OnlineMatchSessionService : IOnlineMatchSessionService
{
    private const string BackgroundFailureCode = "transport_error";
    private const string BackgroundFailureMessage = "Realtime background synchronization failed.";

    private readonly IOnlineMatchTransportAdapter _transport;
    private readonly IOnlineErrorMapper _errorMapper;
    private readonly IOnlineRealtimeLifecycleManager _realtimeLifecycleManager;
    private readonly OnlineSessionStateCoordinator _sessionState;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public OnlineMatchSessionService(
        IOnlineMatchHttpClient httpClient,
        IOnlineErrorMapper errorMapper,
        IOnlineMatchRealtimeClientFactory realtimeClientFactory,
        IOnlineSnapshotGameStateMapper snapshotMapper,
        OnlineRealtimeEventReducer reducer)
        : this(
            new OnlineMatchTransportAdapter(httpClient),
            errorMapper,
            new OnlineRealtimeLifecycleManager(realtimeClientFactory, new OnlineTransportErrorPolicy(errorMapper)),
            new OnlineSessionStateCoordinator(snapshotMapper, reducer))
    {
    }

    internal OnlineMatchSessionService(
        IOnlineMatchTransportAdapter transport,
        IOnlineErrorMapper errorMapper,
        IOnlineRealtimeLifecycleManager realtimeLifecycleManager,
        OnlineSessionStateCoordinator sessionState)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _errorMapper = errorMapper ?? throw new ArgumentNullException(nameof(errorMapper));
        _realtimeLifecycleManager = realtimeLifecycleManager ?? throw new ArgumentNullException(nameof(realtimeLifecycleManager));
        _sessionState = sessionState ?? throw new ArgumentNullException(nameof(sessionState));

        AttachRealtimeHandlers();
    }

    public event EventHandler? SessionStateChanged;
    public event EventHandler<OnlineUserError>? SessionError;

    public bool IsInMatch => _sessionState.Credentials is not null;
    public bool IsConnected => _realtimeLifecycleManager.IsConnected;
    public string? MatchId => _sessionState.Credentials?.MatchId;
    public string? JoinCode => _sessionState.JoinCode;
    public PieceColor? Seat => _sessionState.Credentials?.Seat;
    public OnlineMatchSnapshot? CurrentSnapshot => _sessionState.CurrentSnapshot;
    public GameState? CurrentGameState => _sessionState.CurrentGameState;
    public long LastSequence => _sessionState.LastSequence;

    public async Task<OnlineOperationResult<OnlineCreatedMatch>> CreateMatchAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await ResetSessionInternalAsync(cancellationToken);

            var createResult = await _transport.CreateMatchAsync(cancellationToken);
            if (!createResult.IsSuccess)
            {
                return OnlineOperationResult<OnlineCreatedMatch>.Failure(createResult.Error!);
            }

            var created = createResult.Value!;
            _sessionState.SetCreatedCredentials(created);

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

            var joinResult = await _transport.JoinMatchAsync(joinCode, cancellationToken);
            if (!joinResult.IsSuccess)
            {
                return OnlineOperationResult<OnlineJoinedMatch>.Failure(joinResult.Error!);
            }

            var joined = joinResult.Value!;
            if (!_sessionState.TrySetJoinedCredentials(joined, out var seat))
            {
                return OnlineOperationResult<OnlineJoinedMatch>.Failure(
                    new OnlineUserError(
                        "invalid_seat",
                        "Server returned an unsupported seat value.",
                        OnlineUserAction.StartNewMatch,
                        IsTerminal: true));
            }

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

            _sessionState.SetResumedCredentials(matchId.Trim(), playerToken.Trim(), seat);

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
            if (_sessionState.Credentials is not OnlineMatchCredentials credentials)
            {
                return OnlineOperationResult<OnlineMatchSnapshot>.Failure(CreateSessionNotStartedError());
            }

            var moveResult = await _transport.SubmitMoveAsync(
                credentials,
                fromSquare,
                toSquare,
                promotionPieceType,
                cancellationToken);
            if (!moveResult.IsSuccess)
            {
                return OnlineOperationResult<OnlineMatchSnapshot>.Failure(moveResult.Error!);
            }

            var snapshot = moveResult.Value!.Snapshot;
            var applyError = _sessionState.TryApplySnapshot(snapshot);
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
            await _realtimeLifecycleManager.SuspendAsync(cancellationToken);
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
            DetachRealtimeHandlers();
            await _realtimeLifecycleManager.DisposeAsync();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private async Task<OnlineOperationResult<OnlineMatchSnapshot>> RecoverInternalAsync(CancellationToken cancellationToken)
    {
        if (_sessionState.Credentials is not OnlineMatchCredentials credentials)
        {
            return OnlineOperationResult<OnlineMatchSnapshot>.Failure(CreateSessionNotStartedError());
        }

        var snapshotResult = await _transport.GetSnapshotAsync(credentials, cancellationToken);
        if (!snapshotResult.IsSuccess)
        {
            return OnlineOperationResult<OnlineMatchSnapshot>.Failure(snapshotResult.Error!);
        }

        var snapshot = snapshotResult.Value!;
        var applyError = _sessionState.TryApplySnapshot(snapshot);
        if (applyError is not null)
        {
            return OnlineOperationResult<OnlineMatchSnapshot>.Failure(applyError);
        }

        var connectionError = await _realtimeLifecycleManager.EnsureConnectedAndSubscribedAsync(credentials, cancellationToken);
        NotifyStateChanged();

        if (connectionError is not null)
        {
            NotifyError(connectionError);
            return OnlineOperationResult<OnlineMatchSnapshot>.Failure(connectionError);
        }

        return OnlineOperationResult<OnlineMatchSnapshot>.Success(snapshot);
    }

    private async Task ResetSessionInternalAsync(CancellationToken cancellationToken)
    {
        await _realtimeLifecycleManager.ResetAsync(_sessionState.Credentials, cancellationToken);
        _sessionState.Reset();
    }

    private void AttachRealtimeHandlers()
    {
        _realtimeLifecycleManager.SnapshotReceived += OnSnapshotReceived;
        _realtimeLifecycleManager.UpdatedReceived += OnUpdatedReceived;
        _realtimeLifecycleManager.PresenceChangedReceived += OnPresenceChangedReceived;
        _realtimeLifecycleManager.EndedReceived += OnEndedReceived;
        _realtimeLifecycleManager.ErrorReceived += OnErrorReceived;
        _realtimeLifecycleManager.Reconnected += OnReconnected;
        _realtimeLifecycleManager.Disconnected += OnDisconnected;
    }

    private void DetachRealtimeHandlers()
    {
        _realtimeLifecycleManager.SnapshotReceived -= OnSnapshotReceived;
        _realtimeLifecycleManager.UpdatedReceived -= OnUpdatedReceived;
        _realtimeLifecycleManager.PresenceChangedReceived -= OnPresenceChangedReceived;
        _realtimeLifecycleManager.EndedReceived -= OnEndedReceived;
        _realtimeLifecycleManager.ErrorReceived -= OnErrorReceived;
        _realtimeLifecycleManager.Reconnected -= OnReconnected;
        _realtimeLifecycleManager.Disconnected -= OnDisconnected;
    }

    private void OnSnapshotReceived(object? sender, OnlineMatchSnapshotSyncEvent payload)
    {
        RunSafeBackground(() => HandleSnapshotEventAsync(payload.Metadata.Sequence, payload.Snapshot));
    }

    private void OnUpdatedReceived(object? sender, OnlineMatchUpdatedSyncEvent payload)
    {
        RunSafeBackground(() => HandleSnapshotEventAsync(payload.Metadata.Sequence, payload.Snapshot));
    }

    private void OnPresenceChangedReceived(object? sender, OnlineMatchPresenceChangedSyncEvent payload)
    {
        RunSafeBackground(() => HandleSnapshotEventAsync(payload.Metadata.Sequence, payload.Snapshot));
    }

    private void OnEndedReceived(object? sender, OnlineMatchEndedSyncEvent payload)
    {
        RunSafeBackground(() => HandleSnapshotEventAsync(payload.Metadata.Sequence, payload.Snapshot));
    }

    private void OnErrorReceived(object? sender, OnlineMatchErrorSyncEvent payload)
    {
        RunSafeBackground(() => HandleErrorEventAsync(payload.Metadata.Sequence, payload.Code, payload.Message));
    }

    private void OnReconnected(object? sender, EventArgs e)
    {
        RunSafeBackground(HandleReconnectedAsync);
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
            var reduction = _sessionState.ApplyRealtimeSnapshot(sequence, snapshot, out var applyError);
            if (reduction.Status is OnlineRealtimeApplyStatus.Applied)
            {
                if (applyError is not null)
                {
                    NotifyError(applyError);
                    return;
                }

                NotifyStateChanged();
            }

            if (reduction.RequiresResync && _sessionState.TryBeginResync())
            {
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
            var reduction = _sessionState.ApplyRealtimeErrorMetadata(sequence);
            if (reduction.RequiresResync && _sessionState.TryBeginResync())
            {
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
            if (_sessionState.Credentials is not OnlineMatchCredentials credentials)
            {
                return;
            }

            reconnectError = await _realtimeLifecycleManager.ResubscribeAfterReconnectAsync(
                credentials,
                CancellationToken.None);
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
                _sessionState.EndResync();
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    private void RunSafeBackground(Func<Task> work)
    {
        _ = ExecuteSafeBackgroundAsync(work);
    }

    private async Task ExecuteSafeBackgroundAsync(Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (OperationCanceledException)
        {
            // Realtime teardown may cancel background callbacks.
        }
        catch (ObjectDisposedException)
        {
            // Service teardown may race with in-flight background callbacks.
        }
        catch (Exception)
        {
            try
            {
                NotifyError(CreateBackgroundFailureError());
            }
            catch (Exception)
            {
            }
        }
    }

    private OnlineUserError CreateBackgroundFailureError()
    {
        return _errorMapper.Map(new OnlineTransportError(BackgroundFailureCode, BackgroundFailureMessage));
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
