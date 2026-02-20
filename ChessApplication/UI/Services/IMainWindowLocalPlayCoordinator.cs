using Chess.Domain;

namespace Chess.UI.Services;

/// <summary>
/// IMainWindowLocalPlayContext defines a contract within the UI/Services module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include OnlineOperation (UI/ViewModels), MainWindowLocalPlayCoordinator (UI/Services), IMainWindowLocalPlayCoordinator (UI/Services).
/// Key collaborators are Implementations include MainWindowViewModel (UI/ViewModels).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineOperation (UI/ViewModels), MainWindowLocalPlayCoordinator (UI/Services), IMainWindowLocalPlayCoordinator (UI/Services), MainWindowViewModel (UI/ViewModels)</para>
/// <para><b>Usage pattern:</b> View models delegate focused interaction steps to this type, which applies deterministic UI workflow logic for the given context.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include MainWindowViewModel (UI/ViewModels).</para>
/// <para><b>Boundary:</b> This type sits in the UI/Services UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal interface IMainWindowLocalPlayContext
{
    bool IsHumanInputBlockedByAiTurn();

    string BuildAiThinkingFeedback();

    void QueueAiTurnIfNeeded();

    void SetFocusedSquare(Square square);

    void SetFeedback(string feedback);

    void SetLastAction(string lastAction);

    void ClearSelection();

    void UpdateSquareHighlights();

    void RefreshBoardFromCurrentState();
}

/// <summary>
/// IMainWindowLocalPlayCoordinator defines a contract within the UI/Services module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include MainWindowViewModel (UI/ViewModels), MainWindowLocalPlayCoordinator (UI/Services).
/// Key collaborators are Implementations include MainWindowLocalPlayCoordinator (UI/Services).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindowViewModel (UI/ViewModels), MainWindowLocalPlayCoordinator (UI/Services)</para>
/// <para><b>Usage pattern:</b> View models delegate focused interaction steps to this type, which applies deterministic UI workflow logic for the given context.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include MainWindowLocalPlayCoordinator (UI/Services).</para>
/// <para><b>Boundary:</b> This type sits in the UI/Services UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal interface IMainWindowLocalPlayCoordinator
{
    void HandleSquareClicked(Square square, IMainWindowLocalPlayContext context);
}
