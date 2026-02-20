using System;
using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;

namespace Chess.UI.Services;

internal interface IMainWindowPersistenceContext
{
    bool IsOnlineMatchActive { get; }

    void SetFeedback(string feedback);

    void SetLastAction(string lastAction);

    void CancelInFlightAiTurn();

    void ClearSelection();

    void SetFocusedSquare(Square square);

    void RefreshBoardFromCurrentState();

    void QueueAiTurnIfNeeded();
}

internal interface IMainWindowPersistenceCoordinator
{
    Task SaveGameAsync(
        string? persistenceFilePath,
        IMainWindowPersistenceContext context,
        CancellationToken cancellationToken = default);

    Task LoadGameAsync(
        string? persistenceFilePath,
        Square defaultFocusSquare,
        IMainWindowPersistenceContext context,
        CancellationToken cancellationToken = default);
}
