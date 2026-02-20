using System;
using System.Threading.Tasks;
using Chess.Domain;

namespace Chess.UI.Services;

internal interface IMainWindowOnlinePlayContext
{
    bool IsOnlineOperationInProgress { get; }

    GameState? GetDisplayGameState();

    void SetFocusedSquare(Square square);

    void SetFeedback(string feedback);

    void SetLastAction(string lastAction);

    void ClearSelection();

    void UpdateSquareHighlights();

    void RefreshBoardFromCurrentState();

    Task RunOnlineOperationWithBusyStateAsync(Func<Task> operationAsync);

    void CancelInFlightAiTurn();

    void DisablePlayVsAi();

    void UpdateOnlineSessionText();

    void NotifyAiAvailabilityChanged();

    void NotifyOnlineMatchActiveChanged();

    void StartNewLocalGame();

    void SetFocusedSquareToDefault();
}

internal interface IMainWindowOnlinePlayCoordinator
{
    Task HandleSquareClickedAsync(Square square, IMainWindowOnlinePlayContext context);

    Task CreateOnlineMatchAsync(IMainWindowOnlinePlayContext context);

    Task JoinOnlineMatchAsync(string? onlineJoinCode, IMainWindowOnlinePlayContext context);

    Task LeaveOnlineMatchAsync(IMainWindowOnlinePlayContext context);

    Task ResyncOnlineMatchAsync(IMainWindowOnlinePlayContext context);
}
