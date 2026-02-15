using System.Collections.Generic;
using System.Linq;
using Chess.AI;
using Chess.Domain;
using Chess.Engine;
using Xunit;

namespace Chess.Tests;

public sealed class AiMoveSelectorTests
{
    [Fact]
    public void SelectBestMove_PrefersStrongMaterialCapture()
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

        var selectedMove = selector.SelectBestMove(state, PieceColor.White, searchDepth: 1);

        Assert.NotNull(selectedMove);
        Assert.Equal(new Square(3, 0), selectedMove!.From);
        Assert.Equal(new Square(3, 7), selectedMove.To);
        Assert.Equal(PieceType.Rook, selectedMove.CapturedPiece?.Type);
    }

    [Fact]
    public void SelectBestMove_UsesDeterministicTieBreak_WhenScoresAreEqual()
    {
        var engine = new ChessGameEngine();
        var selector = new NegamaxAiMoveSelector(engine, new ConstantEvaluator());
        var state = new GameState(
            Pieces:
            [
                new PiecePlacement(new Square(0, 0), new Piece(PieceType.King, PieceColor.White)),
                new PiecePlacement(new Square(7, 7), new Piece(PieceType.King, PieceColor.Black))
            ],
            SideToMove: PieceColor.White,
            CastlingRights: CastlingRights.None,
            EnPassantTarget: null,
            HalfmoveClock: 0,
            FullmoveNumber: 1,
            Status: GameStatus.InProgress,
            MoveHistory: []);

        var selectedMove = selector.SelectBestMove(state, PieceColor.White, searchDepth: 2);

        Assert.NotNull(selectedMove);
        Assert.Equal(new Square(0, 1), selectedMove!.To);
    }

    [Fact]
    public void SelectBestMove_ReturnsLegalMove()
    {
        var engine = new ChessGameEngine();
        var evaluator = new MaterialMobilityPositionEvaluator(engine);
        var selector = new NegamaxAiMoveSelector(engine, evaluator);
        var state = engine.CreateInitialGameState();
        var legalMoves = engine.GenerateLegalMoves(state)
            .Select(move => Normalize(move))
            .ToHashSet();

        var selectedMove = selector.SelectBestMove(state, PieceColor.White, searchDepth: 2);

        Assert.NotNull(selectedMove);
        Assert.Contains(Normalize(selectedMove!), legalMoves);
    }

    private static string Normalize(Move move)
    {
        return $"{move.From.File}:{move.From.Rank}->{move.To.File}:{move.To.Rank}:{(int?)move.PromotionPieceType ?? -1}";
    }

    private sealed class ConstantEvaluator : IAiPositionEvaluator
    {
        public int Evaluate(GameState gameState, PieceColor perspectiveColor)
        {
            return 0;
        }
    }
}
