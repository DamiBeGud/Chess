using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Chess.AI;
using Chess.AppCore;
using Chess.Domain;
using Chess.Engine;
using Xunit;

namespace Chess.Tests;

public sealed class AiTurnServiceTests
{
    [Fact]
    public async Task TryPlayTurnAsync_InProgressState_AppliesAiMove()
    {
        var engine = new ChessGameEngine();
        var evaluator = new MaterialMobilityPositionEvaluator(engine);
        var selector = new NegamaxAiMoveSelector(engine, evaluator);
        var state = new GameState(
            Pieces:
            [
                new PiecePlacement(new Square(4, 0), new Piece(PieceType.King, PieceColor.White)),
                new PiecePlacement(new Square(3, 0), new Piece(PieceType.Queen, PieceColor.White)),
                new PiecePlacement(new Square(4, 7), new Piece(PieceType.King, PieceColor.Black)),
                new PiecePlacement(new Square(3, 7), new Piece(PieceType.Rook, PieceColor.Black))
            ],
            SideToMove: PieceColor.White,
            CastlingRights: CastlingRights.None,
            EnPassantTarget: null,
            HalfmoveClock: 0,
            FullmoveNumber: 1,
            Status: GameStatus.InProgress,
            MoveHistory: []);
        var session = new MutableGameSessionService(state, engine);
        var aiTurnService = new AiTurnService(session, selector);

        var aiMove = await aiTurnService.TryPlayTurnAsync(PieceColor.White, searchDepth: 1);

        Assert.NotNull(aiMove);
        Assert.True(session.TryMakeMoveCallCount > 0);
        Assert.Equal(PieceColor.Black, session.CurrentGameState.SideToMove);
    }

    [Fact]
    public async Task TryPlayTurnAsync_FinishedState_DoesNotApplyMove()
    {
        var engine = new ChessGameEngine();
        var evaluator = new MaterialMobilityPositionEvaluator(engine);
        var selector = new NegamaxAiMoveSelector(engine, evaluator);
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
        var session = new MutableGameSessionService(finishedState, engine);
        var aiTurnService = new AiTurnService(session, selector);

        var aiMove = await aiTurnService.TryPlayTurnAsync(PieceColor.White, searchDepth: 1);

        Assert.Null(aiMove);
        Assert.Equal(0, session.TryMakeMoveCallCount);
        Assert.Equal(GameStatus.Draw, session.CurrentGameState.Status);
    }

    [Fact]
    public async Task TryPlayTurnAsync_Canceled_DoesNotApplyMove()
    {
        var engine = new ChessGameEngine();
        var state = engine.CreateInitialGameState();
        var selector = new BlockingCancellableSelector();
        var session = new MutableGameSessionService(state, engine);
        var aiTurnService = new AiTurnService(session, selector);
        using var cancellation = new CancellationTokenSource();

        var task = aiTurnService.TryPlayTurnAsync(PieceColor.White, searchDepth: 2, cancellation.Token);
        await selector.Started;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await task);
        Assert.Equal(0, session.TryMakeMoveCallCount);
    }

    private sealed class MutableGameSessionService : IGameSessionService
    {
        private readonly IGameEngine _gameEngine;
        private GameState _currentState;

        public MutableGameSessionService(GameState currentState, IGameEngine gameEngine)
        {
            _currentState = currentState;
            _gameEngine = gameEngine;
        }

        public int TryMakeMoveCallCount { get; private set; }

        public GameState StartNewGame()
        {
            return _currentState;
        }

        public IReadOnlyList<Move> GetLegalMovesFrom(Square fromSquare)
        {
            return _gameEngine.GenerateLegalMoves(_currentState, fromSquare);
        }

        public bool TryMakeMove(Square fromSquare, Square toSquare, PieceType? promotionPieceType = null)
        {
            TryMakeMoveCallCount++;
            if (!_gameEngine.TryApplyMove(_currentState, fromSquare, toSquare, out var updatedState, promotionPieceType))
            {
                return false;
            }

            _currentState = updatedState;
            return true;
        }

        public Task SaveAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<GameState> LoadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_currentState);
        }

        public GameState CurrentGameState => _currentState;
    }

    private sealed class BlockingCancellableSelector : IAiMoveSelector
    {
        private readonly TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public Move? SelectBestMove(
            GameState gameState,
            PieceColor aiColor,
            int searchDepth,
            CancellationToken cancellationToken = default)
        {
            _started.TrySetResult(true);
            cancellationToken.WaitHandle.WaitOne();
            cancellationToken.ThrowIfCancellationRequested();
            return null;
        }
    }
}
