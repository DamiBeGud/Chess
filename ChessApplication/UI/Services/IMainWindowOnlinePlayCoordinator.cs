using System;
using System.Threading.Tasks;
using Chess.Domain;

namespace Chess.UI.Services;

/// <summary>
/// IMainWindowOnlinePlayContext defines a contract within the UI/Services module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include OnlineOperation (UI/ViewModels), IMainWindowOnlinePlayCoordinator (UI/Services), MainWindowOnlinePlayCoordinator (UI/Services).
/// Key collaborators are Implementations include MainWindowViewModel (UI/ViewModels).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> OnlineOperation (UI/ViewModels), IMainWindowOnlinePlayCoordinator (UI/Services), MainWindowOnlinePlayCoordinator (UI/Services), MainWindowViewModel (UI/ViewModels)</para>
/// <para><b>Usage pattern:</b> View models delegate focused interaction steps to this type, which applies deterministic UI workflow logic for the given context.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include MainWindowViewModel (UI/ViewModels).</para>
/// <para><b>Boundary:</b> This type sits in the UI/Services UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal interface IMainWindowOnlinePlayContext
{
    bool IsOnlineOperationInProgress { get; }

    GameState? GetOnlineGameState();

    void SetFocusedSquare(Square square);

    void SetFeedback(string feedback);

    void SetLastAction(string lastAction);

    void ClearSelection();

    void UpdateSquareHighlights();

    void RefreshBoardFromCurrentState();

    Task RunOnlineOperationWithBusyStateAsync(Func<Task> operationAsync);

    void CancelInFlightAiTurn();

    void DisablePlayVsAi();

    void StartNewLocalGame();

    void SetFocusedSquareToDefault();
}

/// <summary>
/// IMainWindowOnlinePlayCoordinator defines a contract within the UI/Services module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include MainWindowViewModel (UI/ViewModels), MainWindowOnlinePlayCoordinator (UI/Services).
/// Key collaborators are Implementations include MainWindowOnlinePlayCoordinator (UI/Services).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindowViewModel (UI/ViewModels), MainWindowOnlinePlayCoordinator (UI/Services)</para>
/// <para><b>Usage pattern:</b> View models delegate focused interaction steps to this type, which applies deterministic UI workflow logic for the given context.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include MainWindowOnlinePlayCoordinator (UI/Services).</para>
/// <para><b>Boundary:</b> This type sits in the UI/Services UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal interface IMainWindowOnlinePlayCoordinator
{
    Task HandleSquareClickedAsync(Square square, IMainWindowOnlinePlayContext context);

    Task CreateOnlineMatchAsync(IMainWindowOnlinePlayContext context);

    Task JoinOnlineMatchAsync(string? onlineJoinCode, IMainWindowOnlinePlayContext context);

    Task LeaveOnlineMatchAsync(IMainWindowOnlinePlayContext context);

    Task ResyncOnlineMatchAsync(IMainWindowOnlinePlayContext context);
}
