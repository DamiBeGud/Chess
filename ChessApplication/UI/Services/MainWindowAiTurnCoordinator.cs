using System;
using System.Threading;
using System.Threading.Tasks;
using Chess.AppCore;
using Chess.Domain;

namespace Chess.UI.Services;

internal sealed class MainWindowAiTurnCoordinator : IMainWindowAiTurnCoordinator
{
    private readonly IAiTurnService _aiTurnService;
    private readonly object _syncRoot = new();
    private CancellationTokenSource? _aiTurnCancellationSource;

    internal MainWindowAiTurnCoordinator(IAiTurnService aiTurnService)
    {
        _aiTurnService = aiTurnService ?? throw new ArgumentNullException(nameof(aiTurnService));
    }

    public bool IsAiTurnInProgress { get; private set; }

    public bool IsHumanInputBlockedByAiTurn(
        bool isPlayVsAiEnabled,
        PieceColor aiControlledColor,
        GameState currentState)
    {
        if (!isPlayVsAiEnabled)
        {
            return false;
        }

        if (IsAiTurnInProgress)
        {
            return true;
        }

        return currentState.Status == GameStatus.InProgress && currentState.SideToMove == aiControlledColor;
    }

    public void QueueAiTurnIfNeeded(
        Func<bool> isPlayVsAiEnabledProvider,
        Func<PieceColor> aiControlledColorProvider,
        Func<int> aiSearchDepthProvider,
        Func<string> aiThinkingFeedbackProvider,
        Action<string> setFeedback,
        Action<Move> setLastActionFromAiMove,
        Action clearSelectionAndRefreshBoard,
        Action notifyIsAiThinkingChanged)
    {
        if (!isPlayVsAiEnabledProvider() || IsAiTurnInProgress)
        {
            return;
        }

        if (!_aiTurnService.CanRequestMove(aiControlledColorProvider()))
        {
            return;
        }

        var aiTurnCancellationSource = TryBeginAiTurn();
        if (aiTurnCancellationSource is null)
        {
            return;
        }

        _ = PlayAiTurnAsync(
            aiTurnCancellationSource,
            isPlayVsAiEnabledProvider,
            aiControlledColorProvider,
            aiSearchDepthProvider,
            aiThinkingFeedbackProvider,
            setFeedback,
            setLastActionFromAiMove,
            clearSelectionAndRefreshBoard,
            notifyIsAiThinkingChanged);
    }

    public void CancelInFlightAiTurn()
    {
        CancellationTokenSource? cancellationSource;
        lock (_syncRoot)
        {
            cancellationSource = _aiTurnCancellationSource;
            _aiTurnCancellationSource = null;
        }

        if (cancellationSource is null)
        {
            return;
        }

        try
        {
            cancellationSource.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            cancellationSource.Dispose();
        }
    }

    private async Task PlayAiTurnAsync(
        CancellationTokenSource aiTurnCancellationSource,
        Func<bool> isPlayVsAiEnabledProvider,
        Func<PieceColor> aiControlledColorProvider,
        Func<int> aiSearchDepthProvider,
        Func<string> aiThinkingFeedbackProvider,
        Action<string> setFeedback,
        Action<Move> setLastActionFromAiMove,
        Action clearSelectionAndRefreshBoard,
        Action notifyIsAiThinkingChanged)
    {
        if (IsAiTurnInProgress)
        {
            CompleteAiTurn(aiTurnCancellationSource);
            return;
        }

        IsAiTurnInProgress = true;
        notifyIsAiThinkingChanged();
        setFeedback(aiThinkingFeedbackProvider());
        var shouldRequeueAiTurn = false;

        try
        {
            var aiMove = await _aiTurnService.TryPlayTurnAsync(
                aiControlledColorProvider(),
                aiSearchDepthProvider(),
                aiTurnCancellationSource.Token);
            if (aiMove is not null)
            {
                setLastActionFromAiMove(aiMove);
            }
            else if (isPlayVsAiEnabledProvider() && _aiTurnService.CanRequestMove(aiControlledColorProvider()))
            {
                shouldRequeueAiTurn = true;
            }

            clearSelectionAndRefreshBoard();
            setFeedback(string.Empty);
        }
        catch (OperationCanceledException)
        {
            setFeedback(isPlayVsAiEnabledProvider() ? "AI move canceled." : string.Empty);
            shouldRequeueAiTurn = isPlayVsAiEnabledProvider() && _aiTurnService.CanRequestMove(aiControlledColorProvider());
        }
        catch (Exception exception)
        {
            setFeedback($"AI move failed: {exception.Message}");
        }
        finally
        {
            IsAiTurnInProgress = false;
            notifyIsAiThinkingChanged();
            CompleteAiTurn(aiTurnCancellationSource);
            if (shouldRequeueAiTurn)
            {
                QueueAiTurnIfNeeded(
                    isPlayVsAiEnabledProvider,
                    aiControlledColorProvider,
                    aiSearchDepthProvider,
                    aiThinkingFeedbackProvider,
                    setFeedback,
                    setLastActionFromAiMove,
                    clearSelectionAndRefreshBoard,
                    notifyIsAiThinkingChanged);
            }
        }
    }

    private CancellationTokenSource? TryBeginAiTurn()
    {
        lock (_syncRoot)
        {
            if (_aiTurnCancellationSource is not null)
            {
                return null;
            }

            _aiTurnCancellationSource = new CancellationTokenSource();
            return _aiTurnCancellationSource;
        }
    }

    private void CompleteAiTurn(CancellationTokenSource cancellationSource)
    {
        var shouldDispose = false;
        lock (_syncRoot)
        {
            if (ReferenceEquals(_aiTurnCancellationSource, cancellationSource))
            {
                _aiTurnCancellationSource = null;
                shouldDispose = true;
            }
        }

        if (!shouldDispose)
        {
            return;
        }

        cancellationSource.Dispose();
    }
}
