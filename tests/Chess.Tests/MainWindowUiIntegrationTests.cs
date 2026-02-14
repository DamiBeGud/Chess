using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Chess.AppCore;
using Chess.Domain;
using Chess.Engine;
using Chess.Persistence;
using Chess.UI.ViewModels;
using Xunit;

namespace Chess.Tests;

public sealed class MainWindowUiIntegrationTests
{
    [AvaloniaFact]
    public void MainWindow_RendersBoardAndInitialStatus()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();

            var boardButtons = GetBoardSquareButtons(window);
            Assert.Equal(64, boardButtons.Count);
            Assert.Equal("Status: In progress. Side to move: White.", GetGameStatusText(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SelectingOwnPiece_HighlightsLegalDestinations()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();

            var e2 = FindSquareButton(window, "e2");
            var e3 = FindSquareButton(window, "e3");
            var e4 = FindSquareButton(window, "e4");

            var e3Before = e3.Background;
            var e4Before = e4.Background;

            Click(e2);

            Assert.NotEqual(e3Before, e3.Background);
            Assert.NotEqual(e4Before, e4.Background);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void IllegalDestination_IsRejectedAndStatusRemainsUnchanged()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();

            Click(FindSquareButton(window, "e2"));
            Click(FindSquareButton(window, "e5"));

            Assert.Equal("Status: In progress. Side to move: White.", GetGameStatusText(window));
            Assert.Equal("Invalid move target: e5.", GetFeedbackText(window));
            Assert.Equal("Last action: Started a new game.", GetLastActionText(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void LegalMove_UpdatesBoardAndStatus()
    {
        var window = CreateWindow(CreateSessionService());

        try
        {
            window.Show();

            var e2 = FindSquareButton(window, "e2");
            var e4 = FindSquareButton(window, "e4");

            Click(e2);
            Click(e4);

            Assert.Equal("Status: In progress. Side to move: Black.", GetGameStatusText(window));
            Assert.Equal("Last action: White moved Pawn from e2 to e4.", GetLastActionText(window));
            Assert.Equal(string.Empty, GetFeedbackText(window));
            Assert.Equal(string.Empty, GetSquareViewModel(e2).PieceGlyph);
            Assert.Equal("\u2659", GetSquareViewModel(e4).PieceGlyph);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void FinishedGameState_ShowsResultAndBlocksMoveInteraction()
    {
        var finishedState = new GameState(
            Pieces:
            [
                new PiecePlacement(new Square(4, 0), new Piece(PieceType.King, PieceColor.White)),
                new PiecePlacement(new Square(4, 7), new Piece(PieceType.King, PieceColor.Black))
            ],
            SideToMove: PieceColor.White,
            CastlingRights: CastlingRights.None,
            EnPassantTarget: null,
            HalfmoveClock: 0,
            FullmoveNumber: 1,
            Status: GameStatus.Draw,
            MoveHistory: []);

        var stubSessionService = new StubGameSessionService(finishedState);
        var window = CreateWindow(stubSessionService);

        try
        {
            window.Show();

            Assert.Equal("Status: Draw.", GetGameStatusText(window));

            Click(FindSquareButton(window, "e1"));

            Assert.Equal("Game is finished (Draw). Start a new game to continue.", GetFeedbackText(window));
            Assert.Equal(0, stubSessionService.GetLegalMovesFromCallCount);
        }
        finally
        {
            window.Close();
        }
    }

    private static MainWindow CreateWindow(IGameSessionService sessionService)
    {
        var viewModel = new MainWindowViewModel(sessionService);
        return new MainWindow(viewModel);
    }

    private static GameSessionService CreateSessionService()
    {
        return new GameSessionService(new ChessGameEngine(), new JsonGameStateStore());
    }

    private static IReadOnlyList<Button> GetBoardSquareButtons(Window window)
    {
        return window.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.Classes.Contains("board-square"))
            .ToArray();
    }

    private static Button FindSquareButton(Window window, string coordinate)
    {
        return GetBoardSquareButtons(window)
            .Single(button => string.Equals(button.Tag?.ToString(), coordinate, StringComparison.OrdinalIgnoreCase));
    }

    private static BoardSquareViewModel GetSquareViewModel(Button squareButton)
    {
        return Assert.IsType<BoardSquareViewModel>(squareButton.DataContext);
    }

    private static void Click(Button button)
    {
        Assert.NotNull(button.Command);
        Assert.True(button.Command!.CanExecute(button.CommandParameter));
        button.Command.Execute(button.CommandParameter);
    }

    private static string GetGameStatusText(Window window)
    {
        var textBlock = window.FindControl<TextBlock>("GameStatusTextBlock");
        return textBlock?.Text ?? string.Empty;
    }

    private static string GetFeedbackText(Window window)
    {
        var textBlock = window.FindControl<TextBlock>("FeedbackTextBlock");
        return textBlock?.Text ?? string.Empty;
    }

    private static string GetLastActionText(Window window)
    {
        var textBlock = window.FindControl<TextBlock>("LastActionTextBlock");
        return textBlock?.Text ?? string.Empty;
    }

    private sealed class StubGameSessionService : IGameSessionService
    {
        private readonly GameState _gameState;

        public StubGameSessionService(GameState gameState)
        {
            _gameState = gameState;
        }

        public int GetLegalMovesFromCallCount { get; private set; }

        public GameState StartNewGame()
        {
            return _gameState;
        }

        public IReadOnlyList<Move> GetLegalMovesFrom(Square fromSquare)
        {
            GetLegalMovesFromCallCount++;
            return [];
        }

        public bool TryMakeMove(Square fromSquare, Square toSquare, PieceType? promotionPieceType = null)
        {
            return false;
        }

        public Task SaveAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<GameState> LoadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_gameState);
        }

        public GameState CurrentGameState => _gameState;
    }
}
