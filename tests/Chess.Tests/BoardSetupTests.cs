using System.Collections.Generic;
using System.Linq;
using Chess.Domain;
using Chess.Engine;
using Xunit;

namespace Chess.Tests;

public sealed class BoardSetupTests
{
    [Fact]
    public void InitialState_HasExpectedPieceCountsAndKingPositions()
    {
        var engine = new ChessGameEngine();

        var state = engine.CreateInitialGameState();

        Assert.Equal(PieceColor.White, state.SideToMove);
        Assert.Equal(32, state.Pieces.Count);
        Assert.Equal(16, state.Pieces.Count(p => p.Piece.Color == PieceColor.White));
        Assert.Equal(16, state.Pieces.Count(p => p.Piece.Color == PieceColor.Black));
        Assert.Equal(8, CountPieces(state, PieceColor.White, PieceType.Pawn));
        Assert.Equal(2, CountPieces(state, PieceColor.White, PieceType.Rook));
        Assert.Equal(2, CountPieces(state, PieceColor.White, PieceType.Knight));
        Assert.Equal(2, CountPieces(state, PieceColor.White, PieceType.Bishop));
        Assert.Equal(1, CountPieces(state, PieceColor.White, PieceType.Queen));
        Assert.Equal(1, CountPieces(state, PieceColor.White, PieceType.King));
        Assert.Equal(8, CountPieces(state, PieceColor.Black, PieceType.Pawn));
        Assert.Equal(2, CountPieces(state, PieceColor.Black, PieceType.Rook));
        Assert.Equal(2, CountPieces(state, PieceColor.Black, PieceType.Knight));
        Assert.Equal(2, CountPieces(state, PieceColor.Black, PieceType.Bishop));
        Assert.Equal(1, CountPieces(state, PieceColor.Black, PieceType.Queen));
        Assert.Equal(1, CountPieces(state, PieceColor.Black, PieceType.King));
        Assert.Contains(state.Pieces, p => p.Piece.Color == PieceColor.White && p.Piece.Type == PieceType.King && p.Square == new Square(4, 0));
        Assert.Contains(state.Pieces, p => p.Piece.Color == PieceColor.Black && p.Piece.Type == PieceType.King && p.Square == new Square(4, 7));
    }

    [Fact]
    public void InitialState_PiecesAreWithinBoardAndHaveUniqueSquares()
    {
        var engine = new ChessGameEngine();

        var state = engine.CreateInitialGameState();
        var uniqueSquares = new HashSet<Square>();

        foreach (var placement in state.Pieces)
        {
            Assert.InRange(placement.Square.File, 0, 7);
            Assert.InRange(placement.Square.Rank, 0, 7);
            Assert.True(uniqueSquares.Add(placement.Square), "Each piece must occupy a unique square.");
        }
    }

    private static int CountPieces(GameState state, PieceColor color, PieceType pieceType)
    {
        return state.Pieces.Count(p => p.Piece.Color == color && p.Piece.Type == pieceType);
    }
}
