using Chess.Domain;

namespace Chess.UI.Services;

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

internal interface IMainWindowLocalPlayCoordinator
{
    void HandleSquareClicked(Square square, IMainWindowLocalPlayContext context);
}
