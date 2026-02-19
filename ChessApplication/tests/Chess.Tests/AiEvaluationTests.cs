using Chess.AI;
using Chess.Domain;
using Chess.Engine;
using Xunit;

namespace Chess.Tests;

public sealed class AiEvaluationTests
{
    [Fact]
    public void Evaluate_MaterialAdvantage_FavorsSideWithExtraMaterial()
    {
        var evaluator = new MaterialMobilityPositionEvaluator(new ChessGameEngine());
        var state = CreateState(
            new PiecePlacement(new Square(4, 0), new Piece(PieceType.King, PieceColor.White)),
            new PiecePlacement(new Square(4, 7), new Piece(PieceType.King, PieceColor.Black)),
            new PiecePlacement(new Square(3, 3), new Piece(PieceType.Queen, PieceColor.White)));

        var whiteScore = evaluator.Evaluate(state, PieceColor.White);
        var blackScore = evaluator.Evaluate(state, PieceColor.Black);

        Assert.True(whiteScore > 0);
        Assert.True(blackScore < 0);
    }

    [Fact]
    public void Evaluate_MobilityDifference_InfluencesScoreWhenMaterialIsEqual()
    {
        var evaluator = new MaterialMobilityPositionEvaluator(new ChessGameEngine());
        var whiteMobileState = CreateState(
            new PiecePlacement(new Square(4, 3), new Piece(PieceType.King, PieceColor.White)),
            new PiecePlacement(new Square(0, 7), new Piece(PieceType.King, PieceColor.Black)));
        var blackMobileState = CreateState(
            new PiecePlacement(new Square(0, 0), new Piece(PieceType.King, PieceColor.White)),
            new PiecePlacement(new Square(4, 4), new Piece(PieceType.King, PieceColor.Black)));

        var whiteMobileScore = evaluator.Evaluate(whiteMobileState, PieceColor.White);
        var blackMobileScore = evaluator.Evaluate(blackMobileState, PieceColor.White);

        Assert.True(whiteMobileScore > blackMobileScore);
    }

    private static GameState CreateState(params PiecePlacement[] pieces)
    {
        return new GameState(
            Pieces: pieces,
            SideToMove: PieceColor.White,
            CastlingRights: CastlingRights.None,
            EnPassantTarget: null,
            HalfmoveClock: 0,
            FullmoveNumber: 1,
            Status: GameStatus.InProgress,
            MoveHistory: []);
    }
}
