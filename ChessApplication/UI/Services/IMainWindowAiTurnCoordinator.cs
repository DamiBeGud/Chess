using System;
using Chess.Domain;

namespace Chess.UI.Services;

/// <summary>
/// IMainWindowAiTurnCoordinator defines a contract within the UI/Services module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include MainWindowViewModel (UI/ViewModels), MainWindowAiTurnCoordinator (UI/Services).
/// Key collaborators are Implementations include MainWindowAiTurnCoordinator (UI/Services).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindowViewModel (UI/ViewModels), MainWindowAiTurnCoordinator (UI/Services)</para>
/// <para><b>Usage pattern:</b> View models delegate focused interaction steps to this type, which applies deterministic UI workflow logic for the given context.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include MainWindowAiTurnCoordinator (UI/Services).</para>
/// <para><b>Boundary:</b> This type sits in the UI/Services UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
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
