using System.Collections.Generic;
using System.Linq;
using Chess.Domain;
using Chess.Engine;
using Xunit;

namespace Chess.Tests;

public sealed class MoveGenerationTests
{
    private readonly ChessGameEngine _engine = new();

    [Fact]
    public void Pawn_MovesFollowForwardDoubleBlockedAndDiagonalCaptureRules()
    {
        var openBoardState = CreateState(
            PieceColor.White,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.King, PieceColor.Black, 4, 7),
            Placement(PieceType.Pawn, PieceColor.White, 3, 1),
            Placement(PieceType.Pawn, PieceColor.Black, 4, 2));

        var openMoves = _engine.GeneratePseudoLegalMoves(openBoardState, new Square(3, 1));

        AssertContainsDestinations(openMoves, "3,2", "3,3", "4,2");
        Assert.DoesNotContain(openMoves, move => move.To == new Square(2, 2));

        var blockedState = CreateState(
            PieceColor.White,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.King, PieceColor.Black, 4, 7),
            Placement(PieceType.Pawn, PieceColor.White, 3, 1),
            Placement(PieceType.Bishop, PieceColor.Black, 3, 2),
            Placement(PieceType.Knight, PieceColor.Black, 2, 2));

        var blockedMoves = _engine.GeneratePseudoLegalMoves(blockedState, new Square(3, 1));

        AssertContainsDestinations(blockedMoves, "2,2");
        Assert.DoesNotContain(blockedMoves, move => move.To == new Square(3, 2));
        Assert.DoesNotContain(blockedMoves, move => move.To == new Square(3, 3));
    }

    [Fact]
    public void Knight_MovesInLShapeAndIgnoresInterveningPieces()
    {
        var state = CreateState(
            PieceColor.White,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.King, PieceColor.Black, 4, 7),
            Placement(PieceType.Knight, PieceColor.White, 3, 3),
            Placement(PieceType.Pawn, PieceColor.White, 3, 4),
            Placement(PieceType.Pawn, PieceColor.White, 4, 3),
            Placement(PieceType.Bishop, PieceColor.White, 4, 5),
            Placement(PieceType.Pawn, PieceColor.Black, 2, 5));

        var moves = _engine.GeneratePseudoLegalMoves(state, new Square(3, 3));

        AssertContainsDestinations(moves, "1,2", "1,4", "2,1", "2,5", "4,1", "5,2", "5,4");
        Assert.DoesNotContain(moves, move => move.To == new Square(4, 5));
    }

    [Fact]
    public void Bishop_MovesDiagonallyAndStopsOnBlockers()
    {
        var state = CreateState(
            PieceColor.White,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.King, PieceColor.Black, 4, 7),
            Placement(PieceType.Bishop, PieceColor.White, 3, 3),
            Placement(PieceType.Pawn, PieceColor.White, 4, 4),
            Placement(PieceType.Pawn, PieceColor.Black, 1, 5),
            Placement(PieceType.Pawn, PieceColor.White, 2, 2));

        var moves = _engine.GeneratePseudoLegalMoves(state, new Square(3, 3));

        AssertContainsDestinations(moves, "2,4", "1,5", "4,2", "5,1", "6,0");
        Assert.DoesNotContain(moves, move => move.To == new Square(4, 4));
        Assert.DoesNotContain(moves, move => move.To == new Square(0, 6));
    }

    [Fact]
    public void Rook_MovesOrthogonallyAndStopsOnBlockers()
    {
        var state = CreateState(
            PieceColor.White,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.King, PieceColor.Black, 4, 7),
            Placement(PieceType.Rook, PieceColor.White, 3, 3),
            Placement(PieceType.Pawn, PieceColor.White, 3, 5),
            Placement(PieceType.Pawn, PieceColor.Black, 3, 1),
            Placement(PieceType.Pawn, PieceColor.White, 5, 3));

        var moves = _engine.GeneratePseudoLegalMoves(state, new Square(3, 3));

        AssertContainsDestinations(moves, "3,4", "3,2", "3,1", "2,3", "1,3", "0,3", "4,3");
        Assert.DoesNotContain(moves, move => move.To == new Square(3, 5));
        Assert.DoesNotContain(moves, move => move.To == new Square(6, 3));
    }

    [Fact]
    public void Queen_MovesAsCombinedRookAndBishop()
    {
        var state = CreateState(
            PieceColor.White,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.King, PieceColor.Black, 4, 7),
            Placement(PieceType.Queen, PieceColor.White, 3, 3));

        var moves = _engine.GeneratePseudoLegalMoves(state, new Square(3, 3));

        Assert.Equal(27, moves.Count);
        AssertContainsDestinations(moves, "3,7", "7,3", "0,0", "6,6");
    }

    [Fact]
    public void King_MovesOneSquareAndCannotLandOnOwnPiece()
    {
        var state = CreateState(
            PieceColor.White,
            Placement(PieceType.King, PieceColor.White, 3, 3),
            Placement(PieceType.King, PieceColor.Black, 7, 7),
            Placement(PieceType.Pawn, PieceColor.White, 2, 4),
            Placement(PieceType.Pawn, PieceColor.Black, 4, 4));

        var moves = _engine.GeneratePseudoLegalMoves(state, new Square(3, 3));

        Assert.Equal(7, moves.Count);
        AssertContainsDestinations(moves, "2,2", "2,3", "3,2", "3,4", "4,2", "4,3", "4,4");
        Assert.DoesNotContain(moves, move => move.To == new Square(2, 4));
    }

    [Fact]
    public void IsMoveLegal_RejectsMoveFromEmptySquare()
    {
        var state = _engine.CreateInitialGameState();
        var isLegal = _engine.IsMoveLegal(state, new Square(3, 3), new Square(3, 4));
        Assert.False(isLegal);
    }

    [Fact]
    public void IsMoveLegal_RejectsMoveOfOpponentPiece()
    {
        var state = _engine.CreateInitialGameState();
        var isLegal = _engine.IsMoveLegal(state, new Square(0, 6), new Square(0, 5));
        Assert.False(isLegal);
    }

    [Fact]
    public void IsMoveLegal_RejectsMoveThatLeavesOwnKingInCheck()
    {
        var state = CreateState(
            PieceColor.White,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.Rook, PieceColor.White, 4, 1),
            Placement(PieceType.King, PieceColor.Black, 0, 7),
            Placement(PieceType.Rook, PieceColor.Black, 4, 7));

        var isLegal = _engine.IsMoveLegal(state, new Square(4, 1), new Square(3, 1));
        Assert.False(isLegal);
    }

    [Fact]
    public void IsMoveLegal_AcceptsLegalEvasionFromCheck()
    {
        var state = CreateState(
            PieceColor.White,
            Placement(PieceType.King, PieceColor.White, 4, 0),
            Placement(PieceType.Rook, PieceColor.White, 7, 0),
            Placement(PieceType.King, PieceColor.Black, 0, 7),
            Placement(PieceType.Rook, PieceColor.Black, 4, 7));

        var kingEscapeMoveIsLegal = _engine.IsMoveLegal(state, new Square(4, 0), new Square(5, 0));
        var irrelevantMoveIsLegal = _engine.IsMoveLegal(state, new Square(7, 0), new Square(7, 1));

        Assert.True(kingEscapeMoveIsLegal);
        Assert.False(irrelevantMoveIsLegal);
    }

    private static void AssertContainsDestinations(IReadOnlyCollection<Move> moves, params string[] expectedDestinations)
    {
        var actualDestinations = moves
            .Select(move => $"{move.To.File},{move.To.Rank}")
            .OrderBy(destination => destination)
            .ToArray();

        foreach (var destination in expectedDestinations)
        {
            Assert.Contains(destination, actualDestinations);
        }
    }

    private static PiecePlacement Placement(PieceType pieceType, PieceColor color, int file, int rank)
    {
        return new PiecePlacement(new Square(file, rank), new Piece(pieceType, color));
    }

    private static GameState CreateState(PieceColor sideToMove, params PiecePlacement[] pieces)
    {
        return new GameState(
            Pieces: pieces,
            SideToMove: sideToMove,
            CastlingRights: CastlingRights.None,
            EnPassantTarget: null,
            HalfmoveClock: 0,
            FullmoveNumber: 1,
            Status: GameStatus.InProgress,
            MoveHistory: []);
    }
}
