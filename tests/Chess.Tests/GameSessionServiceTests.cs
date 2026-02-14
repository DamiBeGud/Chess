using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chess.AppCore;
using Chess.Domain;
using Chess.Engine;
using Chess.Persistence;
using Xunit;

namespace Chess.Tests;

public sealed class GameSessionServiceTests
{
    [Fact]
    public async Task StartNewGame_ResetsStateAfterLoadingAnotherState()
    {
        var engine = new ChessGameEngine();
        var alteredState = engine.CreateInitialGameState() with
        {
            SideToMove = PieceColor.Black,
            FullmoveNumber = 18,
            HalfmoveClock = 7,
            MoveHistory = new List<Move>
            {
                new(
                    new Square(4, 1),
                    new Square(4, 3),
                    new Piece(PieceType.Pawn, PieceColor.White, HasMoved: true))
            }
        };

        var store = new InMemoryGameStateStore(alteredState);
        var service = new GameSessionService(engine, store);

        await service.LoadAsync("ignored-path");
        var resetState = service.StartNewGame();
        var expectedInitialState = engine.CreateInitialGameState();

        Assert.Equal(expectedInitialState.SideToMove, resetState.SideToMove);
        Assert.Equal(expectedInitialState.Pieces.Count, resetState.Pieces.Count);
        Assert.Equal(expectedInitialState.CastlingRights, resetState.CastlingRights);
        Assert.Equal(expectedInitialState.EnPassantTarget, resetState.EnPassantTarget);
        Assert.Equal(expectedInitialState.HalfmoveClock, resetState.HalfmoveClock);
        Assert.Equal(expectedInitialState.FullmoveNumber, resetState.FullmoveNumber);
        Assert.Equal(expectedInitialState.Status, resetState.Status);
        Assert.Equal(expectedInitialState.SchemaVersion, resetState.SchemaVersion);
        Assert.Empty(resetState.MoveHistory);
        Assert.Equal(NormalizePieces(expectedInitialState.Pieces), NormalizePieces(resetState.Pieces));
        Assert.Equal(resetState, service.CurrentGameState);
    }

    private static IReadOnlyList<string> NormalizePieces(IReadOnlyList<PiecePlacement> pieces)
    {
        return pieces
            .Select(p => $"{p.Piece.Color}:{p.Piece.Type}:{p.Piece.HasMoved}:{p.Square.File}:{p.Square.Rank}")
            .OrderBy(p => p)
            .ToArray();
    }

    private sealed class InMemoryGameStateStore : IGameStateStore
    {
        private readonly GameState _loadResult;

        public InMemoryGameStateStore(GameState loadResult)
        {
            _loadResult = loadResult;
        }

        public Task SaveAsync(string filePath, GameState gameState, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<GameState> LoadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_loadResult);
        }
    }
}
