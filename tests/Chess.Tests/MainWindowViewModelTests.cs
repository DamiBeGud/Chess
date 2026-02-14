using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chess.AppCore;
using Chess.Domain;
using Chess.Engine;
using Chess.Persistence;
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

        var e2 = FindSquare(viewModel, 4, 1);
        var e7 = FindSquare(viewModel, 4, 6);
        Assert.Equal("\u2659", e2.PieceGlyph);
        Assert.Equal("\u265F", e7.PieceGlyph);
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
        Assert.Equal("Invalid move target: e5.", viewModel.FeedbackText);
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
        Assert.Equal(string.Empty, FindSquare(viewModel, 4, 1).PieceGlyph);
        Assert.Equal("\u2659", FindSquare(viewModel, 4, 3).PieceGlyph);
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
        var viewModel = new MainWindowViewModel(session);

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
        var viewModel = new MainWindowViewModel(session);

        var e1 = FindSquare(viewModel, 4, 0);
        e1.ClickCommand.Execute(null);

        Assert.Equal("Status: Draw.", viewModel.GameStatusText);
        Assert.Equal("Game is finished (Draw). Start a new game to continue.", viewModel.FeedbackText);
        Assert.Equal(0, session.GetLegalMovesFromCallCount);
    }

    private static MainWindowViewModel CreateViewModel()
    {
        var session = new GameSessionService(new ChessGameEngine(), new JsonGameStateStore());
        return new MainWindowViewModel(session);
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
            MoveHistory: []);
    }

    private static BoardSquareViewModel FindSquare(MainWindowViewModel viewModel, int file, int rank)
    {
        return viewModel.BoardSquares.Single(square => square.Square == new Square(file, rank));
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
