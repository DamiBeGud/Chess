using System;
using Chess.Domain;

namespace Chess.UI.Services;

internal interface IMainWindowAiTurnCoordinator
{
    bool IsAiTurnInProgress { get; }

    bool IsHumanInputBlockedByAiTurn(
        bool isPlayVsAiEnabled,
        PieceColor aiControlledColor,
        GameState currentState);

    void QueueAiTurnIfNeeded(
        Func<bool> isPlayVsAiEnabledProvider,
        Func<PieceColor> aiControlledColorProvider,
        Func<int> aiSearchDepthProvider,
        Func<string> aiThinkingFeedbackProvider,
        Action<string> setFeedback,
        Action<Move> setLastActionFromAiMove,
        Action clearSelectionAndRefreshBoard,
        Action notifyIsAiThinkingChanged);

    void CancelInFlightAiTurn();
}
