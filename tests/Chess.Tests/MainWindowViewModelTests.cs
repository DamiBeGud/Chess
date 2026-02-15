using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Media;
using Chess.AppCore;
using Chess.Domain;
using Chess.Engine;
using Chess.Persistence;
using Chess.UI.Assets;
using Chess.UI.ViewModels;
using Xunit;

namespace Chess.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_InitializesBoardStateAndStatus()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(64, viewModel.BoardSquares.Count);
        Assert.Equal("Status: In progress. Side to move: White.", viewModel.GameStatusText);
        Assert.Empty(viewModel.MoveHistoryEntries);
        Assert.True(viewModel.IsMoveHistoryEmpty);

        var e2 = FindSquare(viewModel, 4, 1);
        var e7 = FindSquare(viewModel, 4, 6);
        AssertSquareHasPieceAsset(e2, "white-pawn");
        AssertSquareHasPieceAsset(e7, "black-pawn");
    }

    [Fact]
    public void SelectingPiece_HighlightsLegalDestinations()
    {
        var viewModel = CreateViewModel();
        var e2 = FindSquare(viewModel, 4, 1);
        var e3 = FindSquare(viewModel, 4, 2);
        var e4 = FindSquare(viewModel, 4, 3);

        var e3BeforeSelection = e3.Background;
        var e4BeforeSelection = e4.Background;

        e2.ClickCommand.Execute(null);

        Assert.NotEqual(e3BeforeSelection, e3.Background);
        Assert.NotEqual(e4BeforeSelection, e4.Background);
    }

    [Fact]
    public void ClickingIllegalDestination_ShowsInvalidMoveFeedback()
    {
        var viewModel = CreateViewModel();
        var e2 = FindSquare(viewModel, 4, 1);
        var e5 = FindSquare(viewModel, 4, 4);

        e2.ClickCommand.Execute(null);
        e5.ClickCommand.Execute(null);

        Assert.Equal("Status: In progress. Side to move: White.", viewModel.GameStatusText);
        Assert.Equal("Invalid move target: e5. Legal destinations from e2: e3, e4.", viewModel.FeedbackText);
    }

    [Fact]
    public void ClickingEmptySquare_ShowsSelectionGuidance()
    {
        var viewModel = CreateViewModel();
        var e3 = FindSquare(viewModel, 4, 2);

        e3.ClickCommand.Execute(null);

        Assert.Equal("No piece at e3. Select one of your White pieces.", viewModel.FeedbackText);
    }

    [Fact]
    public void ClickingOpponentPieceOnWrongTurn_ShowsTurnFeedback()
    {
        var viewModel = CreateViewModel();
        var e7 = FindSquare(viewModel, 4, 6);

        e7.ClickCommand.Execute(null);

        Assert.Equal("Cannot select Black piece at e7. It is White to move.", viewModel.FeedbackText);
    }

    [Fact]
    public void ClickingPieceWithNoLegalMoves_ShowsNoLegalMovesFeedback()
    {
        var viewModel = CreateViewModel();
        var e1 = FindSquare(viewModel, 4, 0);

        e1.ClickCommand.Execute(null);

        Assert.Equal("Selected square e1 has no legal moves.", viewModel.FeedbackText);
    }

    [Fact]
    public void ClickingLegalDestination_AppliesMoveThroughSessionService()
    {
        var viewModel = CreateViewModel();
        var e2 = FindSquare(viewModel, 4, 1);
        var e4 = FindSquare(viewModel, 4, 3);

        e2.ClickCommand.Execute(null);
        e4.ClickCommand.Execute(null);

        Assert.Equal("Status: In progress. Side to move: Black.", viewModel.GameStatusText);
        Assert.Equal(string.Empty, viewModel.FeedbackText);
        Assert.Equal("Last action: White moved Pawn from e2 to e4.", viewModel.LastActionText);
        AssertSquareHasNoPieceAsset(FindSquare(viewModel, 4, 1));
        AssertSquareHasPieceAsset(FindSquare(viewModel, 4, 3), "white-pawn");
        Assert.Equal(new[] { "1. e2-e4" }, viewModel.MoveHistoryEntries);
        Assert.False(viewModel.IsMoveHistoryEmpty);
    }

    [Fact]
    public void ConsecutiveLegalMoves_UpdateMoveHistoryWithMoveNumbers()
    {
        var viewModel = CreateViewModel();
        var e2 = FindSquare(viewModel, 4, 1);
        var e4 = FindSquare(viewModel, 4, 3);
        var e7 = FindSquare(viewModel, 4, 6);
        var e5 = FindSquare(viewModel, 4, 4);

        e2.ClickCommand.Execute(null);
        e4.ClickCommand.Execute(null);
        e7.ClickCommand.Execute(null);
        e5.ClickCommand.Execute(null);

        Assert.Equal(
            new[] { "1. e2-e4", "1... e7-e5" },
            viewModel.MoveHistoryEntries);
        Assert.False(viewModel.IsMoveHistoryEmpty);
    }

    [Fact]
    public void Constructor_WithPreloadedMoveHistory_FormatsSpecialNotation()
    {
        var preloadedState = CreateState(
            GameStatus.InProgress,
            PieceColor.White,
            new List<Move>
            {
                new(
                    From: new Square(4, 0),
                    To: new Square(6, 0),
                    MovedPiece: new Piece(PieceType.King, PieceColor.White, HasMoved: true),
                    IsCastling: true),
                new(
                    From: new Square(4, 7),
                    To: new Square(2, 7),
                    MovedPiece: new Piece(PieceType.King, PieceColor.Black, HasMoved: true),
                    IsCastling: true),
                new(
                    From: new Square(4, 3),
                    To: new Square(3, 4),
                    MovedPiece: new Piece(PieceType.Pawn, PieceColor.White, HasMoved: true),
                    CapturedPiece: new Piece(PieceType.Pawn, PieceColor.Black, HasMoved: true)),
                new(
                    From: new Square(1, 1),
                    To: new Square(0, 0),
                    MovedPiece: new Piece(PieceType.Pawn, PieceColor.Black, HasMoved: true),
                    CapturedPiece: new Piece(PieceType.Rook, PieceColor.White, HasMoved: true),
                    PromotionPieceType: PieceType.Queen)
            },
            new PiecePlacement(new Square(6, 0), new Piece(PieceType.King, PieceColor.White, HasMoved: true)),
            new PiecePlacement(new Square(2, 7), new Piece(PieceType.King, PieceColor.Black, HasMoved: true)));

        var session = new StubGameSessionService(preloadedState);
        var viewModel = new MainWindowViewModel(session, CreateTestAssetResolver());

        Assert.Equal(
            new[] { "1. O-O", "1... O-O-O", "2. e4xd5", "2... b2xa1=Q" },
            viewModel.MoveHistoryEntries);
        Assert.False(viewModel.IsMoveHistoryEmpty);
    }

    [Fact]
    public void Constructor_WithFinishedGameState_ShowsResultStatusImmediately()
    {
        var finishedState = CreateState(
            GameStatus.WhiteWin,
            PieceColor.Black,
            new PiecePlacement(new Square(4, 0), new Piece(PieceType.King, PieceColor.White)),
            new PiecePlacement(new Square(4, 7), new Piece(PieceType.King, PieceColor.Black)));

        var session = new StubGameSessionService(finishedState);
        var viewModel = new MainWindowViewModel(session, CreateTestAssetResolver());

        Assert.Equal("Status: White wins.", viewModel.GameStatusText);
    }

    [Fact]
    public void ClickingSquare_WhenGameFinished_ShowsFeedbackAndDoesNotQueryLegalMoves()
    {
        var finishedState = CreateState(
            GameStatus.Draw,
            PieceColor.White,
            new PiecePlacement(new Square(4, 0), new Piece(PieceType.King, PieceColor.White)),
            new PiecePlacement(new Square(4, 7), new Piece(PieceType.King, PieceColor.Black)));

        var session = new StubGameSessionService(finishedState);
        var viewModel = new MainWindowViewModel(session, CreateTestAssetResolver());

        var e1 = FindSquare(viewModel, 4, 0);
        e1.ClickCommand.Execute(null);

        Assert.Equal("Status: Draw.", viewModel.GameStatusText);
        Assert.Equal("Game is finished (Draw). Start a new game to continue.", viewModel.FeedbackText);
        Assert.Equal(0, session.GetLegalMovesFromCallCount);
    }

    [Fact]
    public void HandleKeyboardInput_ArrowAndEnter_AppliesMove()
    {
        var viewModel = CreateViewModel();

        Assert.Equal("Keyboard focus: e2.", viewModel.FocusedSquareText);

        Assert.True(viewModel.HandleKeyboardInput(Key.Enter));
        Assert.True(viewModel.HandleKeyboardInput(Key.Up));
        Assert.True(viewModel.HandleKeyboardInput(Key.Up));
        Assert.True(viewModel.HandleKeyboardInput(Key.Enter));

        Assert.Equal("Status: In progress. Side to move: Black.", viewModel.GameStatusText);
        Assert.Equal(string.Empty, viewModel.FeedbackText);
        Assert.Equal("Last action: White moved Pawn from e2 to e4.", viewModel.LastActionText);
        Assert.Equal("Keyboard focus: e4.", viewModel.FocusedSquareText);
    }

    [Fact]
    public void HandleKeyboardInput_WasdAndSpace_AppliesMoveFromFocusedSquare()
    {
        var viewModel = CreateViewModel();

        Assert.True(viewModel.HandleKeyboardInput(Key.D));
        Assert.Equal("Keyboard focus: f2.", viewModel.FocusedSquareText);

        Assert.True(viewModel.HandleKeyboardInput(Key.Space));
        Assert.True(viewModel.HandleKeyboardInput(Key.W));
        Assert.Equal("Keyboard focus: f3.", viewModel.FocusedSquareText);
        Assert.True(viewModel.HandleKeyboardInput(Key.Space));

        Assert.Equal("Status: In progress. Side to move: Black.", viewModel.GameStatusText);
        Assert.Equal(string.Empty, viewModel.FeedbackText);
        Assert.Equal("Last action: White moved Pawn from f2 to f3.", viewModel.LastActionText);
        AssertSquareHasNoPieceAsset(FindSquare(viewModel, 5, 1));
        AssertSquareHasPieceAsset(FindSquare(viewModel, 5, 2), "white-pawn");
    }

    [Fact]
    public void HandleKeyboardInput_SelectionAndFocusStaySynchronized_AcrossReselectInvalidAndEscape()
    {
        var viewModel = CreateViewModel();
        var g3 = FindSquare(viewModel, 6, 2);
        var h3 = FindSquare(viewModel, 7, 2);
        var g3BeforeSelection = g3.Background;
        var h3BeforeSelection = h3.Background;

        Assert.True(viewModel.HandleKeyboardInput(Key.Enter));
        Assert.True(viewModel.HandleKeyboardInput(Key.Right));
        Assert.True(viewModel.HandleKeyboardInput(Key.Enter));
        Assert.True(viewModel.HandleKeyboardInput(Key.Right));
        Assert.True(viewModel.HandleKeyboardInput(Key.Enter));
        Assert.True(viewModel.HandleKeyboardInput(Key.Right));
        Assert.True(viewModel.HandleKeyboardInput(Key.Enter));

        Assert.True(viewModel.HandleKeyboardInput(Key.Left));
        Assert.True(viewModel.HandleKeyboardInput(Key.Up));
        Assert.Equal("Keyboard focus: g3.", viewModel.FocusedSquareText);
        Assert.True(viewModel.HandleKeyboardInput(Key.Enter));

        Assert.Equal("Invalid move target: g3. Legal destinations from h2: h3, h4.", viewModel.FeedbackText);
        Assert.Equal(g3BeforeSelection, g3.Background);
        Assert.NotEqual(h3BeforeSelection, h3.Background);

        Assert.True(viewModel.HandleKeyboardInput(Key.Escape));
        Assert.Equal("Selection cleared.", viewModel.FeedbackText);
        Assert.Equal("Keyboard focus: g3.", viewModel.FocusedSquareText);
        Assert.Equal(h3BeforeSelection, h3.Background);
    }

    [Fact]
    public void HandleKeyboardInput_Escape_ClearsSelection()
    {
        var viewModel = CreateViewModel();
        var e3 = FindSquare(viewModel, 4, 2);
        var e3BeforeSelection = e3.Background;

        Assert.True(viewModel.HandleKeyboardInput(Key.Enter));
        Assert.NotEqual(e3BeforeSelection, e3.Background);
        Assert.True(viewModel.HandleKeyboardInput(Key.Escape));

        Assert.Equal(e3BeforeSelection, e3.Background);
        Assert.Equal("Selection cleared.", viewModel.FeedbackText);
    }

    [Fact]
    public async Task SaveAndLoadCommands_RoundTripStateAndUpdateFeedback()
    {
        var viewModel = CreateViewModel();
        var savePath = Path.Combine(Path.GetTempPath(), $"chess-viewmodel-save-{Path.GetRandomFileName()}.json");
        viewModel.PersistenceFilePath = savePath;

        try
        {
            FindSquare(viewModel, 4, 1).ClickCommand.Execute(null);
            FindSquare(viewModel, 4, 3).ClickCommand.Execute(null);

            await viewModel.SaveGameAsync();
            Assert.True(File.Exists(savePath));
            Assert.Equal($"Last action: Saved game to {savePath}.", viewModel.LastActionText);
            Assert.Equal(string.Empty, viewModel.FeedbackText);

            viewModel.StartNewGame();
            Assert.Equal("Status: In progress. Side to move: White.", viewModel.GameStatusText);

            await viewModel.LoadGameAsync();
            Assert.Equal("Status: In progress. Side to move: Black.", viewModel.GameStatusText);
            Assert.Equal($"Last action: Loaded game from {savePath}.", viewModel.LastActionText);
            Assert.Equal(string.Empty, viewModel.FeedbackText);
            AssertSquareHasNoPieceAsset(FindSquare(viewModel, 4, 1));
            AssertSquareHasPieceAsset(FindSquare(viewModel, 4, 3), "white-pawn");
        }
        finally
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }
    }

    [Fact]
    public async Task LoadGameAsync_WithCorruptedFile_ShowsGracefulFeedback()
    {
        var viewModel = CreateViewModel();
        var savePath = Path.Combine(Path.GetTempPath(), $"chess-viewmodel-save-{Path.GetRandomFileName()}.json");
        viewModel.PersistenceFilePath = savePath;

        try
        {
            await File.WriteAllTextAsync(savePath, "{ invalid json");
            await viewModel.LoadGameAsync();

            Assert.StartsWith("Unable to load game: ", viewModel.FeedbackText, StringComparison.Ordinal);
            Assert.Equal("Status: In progress. Side to move: White.", viewModel.GameStatusText);
        }
        finally
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
        }
    }

    private static MainWindowViewModel CreateViewModel()
    {
        var session = new GameSessionService(new ChessGameEngine(), new JsonGameStateStore());
        return new MainWindowViewModel(session, CreateTestAssetResolver());
    }

    private static IPieceAssetResolver CreateTestAssetResolver()
    {
        return new PieceAssetResolver(_ => CreateTestImage());
    }

    private static IImage CreateTestImage()
    {
        return new TestImage();
    }

    private static GameState CreateState(GameStatus status, PieceColor sideToMove, params PiecePlacement[] pieces)
    {
        return new GameState(
            Pieces: pieces,
            SideToMove: sideToMove,
            CastlingRights: CastlingRights.None,
            EnPassantTarget: null,
            HalfmoveClock: 0,
            FullmoveNumber: 1,
            Status: status,
            MoveHistory: [],
            PositionHistory: []);
    }

    private static GameState CreateState(
        GameStatus status,
        PieceColor sideToMove,
        IReadOnlyList<Move> moveHistory,
        params PiecePlacement[] pieces)
    {
        return new GameState(
            Pieces: pieces,
            SideToMove: sideToMove,
            CastlingRights: CastlingRights.None,
            EnPassantTarget: null,
            HalfmoveClock: 0,
            FullmoveNumber: 1,
            Status: status,
            MoveHistory: moveHistory,
            PositionHistory: []);
    }

    private static BoardSquareViewModel FindSquare(MainWindowViewModel viewModel, int file, int rank)
    {
        return viewModel.BoardSquares.Single(square => square.Square == new Square(file, rank));
    }

    private static void AssertSquareHasNoPieceAsset(BoardSquareViewModel squareViewModel)
    {
        Assert.Null(squareViewModel.PieceImage);
        Assert.Equal(string.Empty, squareViewModel.PieceAssetUri);
        Assert.False(squareViewModel.IsUsingFallbackAsset);
    }

    private static void AssertSquareHasPieceAsset(BoardSquareViewModel squareViewModel, string expectedAssetStem)
    {
        Assert.NotNull(squareViewModel.PieceImage);
        Assert.Contains($"/{expectedAssetStem}.", squareViewModel.PieceAssetUri, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubGameSessionService : IGameSessionService
    {
        private GameState _currentGameState;

        public StubGameSessionService(GameState initialState)
        {
            _currentGameState = initialState;
        }

        public int GetLegalMovesFromCallCount { get; private set; }

        public GameState CurrentGameState => _currentGameState;

        public GameState StartNewGame()
        {
            return _currentGameState;
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
            return Task.FromResult(_currentGameState);
        }
    }
}
