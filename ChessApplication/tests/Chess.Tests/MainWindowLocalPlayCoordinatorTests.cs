using Chess.AppCore;
using Chess.Domain;
using Chess.Engine;
using Chess.Persistence;
using Chess.UI.Assets;
using Chess.UI.Services;
using Xunit;

namespace Chess.Tests;

public sealed class MainWindowLocalPlayCoordinatorTests
{
    [Fact]
    public void HandleSquareClicked_LegalMove_AppliesMoveAndQueuesAiCheck()
    {
        var session = new GameSessionService(new ChessGameEngine(), new JsonGameStateStore());
        var selectionState = new MainWindowSelectionState(new Square(4, 1));
        var textFormatter = new MainWindowTextFormatter(new PieceAssetResolver(_ => null));
        var coordinator = new MainWindowLocalPlayCoordinator(session, selectionState, textFormatter);
        var context = new FakeLocalPlayContext(selectionState);
        var e2 = new Square(4, 1);
        var e4 = new Square(4, 3);

        coordinator.HandleSquareClicked(e2, context);
        coordinator.HandleSquareClicked(e4, context);

        Assert.Equal(PieceColor.Black, session.CurrentGameState.SideToMove);
        Assert.Equal("Last action: White moved Pawn from e2 to e4.", context.LastAction);
        Assert.Equal(string.Empty, context.Feedback);
        Assert.Equal(1, context.RefreshBoardCallCount);
        Assert.Equal(1, context.QueueAiTurnCallCount);
    }

    [Fact]
    public void HandleSquareClicked_WhenAiTurnBlocksInput_SetsThinkingFeedback()
    {
        var session = new GameSessionService(new ChessGameEngine(), new JsonGameStateStore());
        var selectionState = new MainWindowSelectionState(new Square(4, 1));
        var textFormatter = new MainWindowTextFormatter(new PieceAssetResolver(_ => null));
        var coordinator = new MainWindowLocalPlayCoordinator(session, selectionState, textFormatter);
        var context = new FakeLocalPlayContext(selectionState)
        {
            IsBlockedByAiTurn = true,
            AiThinkingFeedback = "AI (Black) is thinking..."
        };

        coordinator.HandleSquareClicked(new Square(4, 1), context);

        Assert.Equal("AI (Black) is thinking...", context.Feedback);
        Assert.Equal(1, context.QueueAiTurnCallCount);
        Assert.Equal(PieceColor.White, session.CurrentGameState.SideToMove);
    }

    private sealed class FakeLocalPlayContext : IMainWindowLocalPlayContext
    {
        private readonly IMainWindowSelectionState _selectionState;

        public FakeLocalPlayContext(IMainWindowSelectionState selectionState)
        {
            _selectionState = selectionState;
        }

        public bool IsBlockedByAiTurn { get; set; }

        public string AiThinkingFeedback { get; set; } = string.Empty;

        public string Feedback { get; private set; } = string.Empty;

        public string LastAction { get; private set; } = string.Empty;

        public int QueueAiTurnCallCount { get; private set; }

        public int UpdateSquareHighlightsCallCount { get; private set; }

        public int RefreshBoardCallCount { get; private set; }

        public bool IsHumanInputBlockedByAiTurn()
        {
            return IsBlockedByAiTurn;
        }

        public string BuildAiThinkingFeedback()
        {
            return AiThinkingFeedback;
        }

        public void QueueAiTurnIfNeeded()
        {
            QueueAiTurnCallCount++;
        }

        public void SetFocusedSquare(Square square)
        {
            _selectionState.SetFocusedSquare(square);
        }

        public void SetFeedback(string feedback)
        {
            Feedback = feedback;
        }

        public void SetLastAction(string lastAction)
        {
            LastAction = lastAction;
        }

        public void ClearSelection()
        {
            _selectionState.ClearSelection();
        }

        public void UpdateSquareHighlights()
        {
            UpdateSquareHighlightsCallCount++;
        }

        public void RefreshBoardFromCurrentState()
        {
            RefreshBoardCallCount++;
        }
    }
}
