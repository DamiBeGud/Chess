using System;
using System.Linq;
using Chess.Domain;
using Chess.Engine;
using Xunit;

namespace Chess.Tests;

public sealed class BoardGeometryTests
{
    [Fact]
    public void Coordinate_RoundTripsAcrossEntireBoard()
    {
        for (var file = 0; file < BoardGeometry.BoardSize; file++)
        {
            for (var rank = 0; rank < BoardGeometry.BoardSize; rank++)
            {
                var square = new Square(file, rank);
                var coordinate = BoardGeometry.ToCoordinate(square);

                var parsed = BoardGeometry.ParseCoordinate(coordinate);

                Assert.Equal(square, parsed);
                Assert.True(BoardGeometry.TryParseCoordinate(coordinate, out var parsedTry));
                Assert.Equal(square, parsedTry);
            }
        }

        Assert.True(BoardGeometry.TryParseCoordinate("E2", out var upperCased));
        Assert.Equal(new Square(4, 1), upperCased);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a0")]
    [InlineData("i1")]
    [InlineData("e9")]
    [InlineData("11")]
    [InlineData("abc")]
    public void TryParseCoordinate_InvalidInput_ReturnsFalse(string value)
    {
        Assert.False(BoardGeometry.TryParseCoordinate(value, out _));
    }

    [Fact]
    public void KnightMoveGeneration_UsesSharedGeometryOffsets()
    {
        var gameState = new GameState(
            Pieces:
            [
                new PiecePlacement(new Square(4, 0), new Piece(PieceType.King, PieceColor.White)),
                new PiecePlacement(new Square(4, 7), new Piece(PieceType.King, PieceColor.Black)),
                new PiecePlacement(new Square(3, 3), new Piece(PieceType.Knight, PieceColor.White))
            ],
            SideToMove: PieceColor.White,
            CastlingRights: CastlingRights.None,
            EnPassantTarget: null,
            HalfmoveClock: 0,
            FullmoveNumber: 1,
            Status: GameStatus.InProgress,
            MoveHistory: []);

        var board = ChessEngineBoard.BuildBoard(gameState);
        var generator = new ChessMoveGenerator((_, _, _, _, _, _) => { });
        var knight = board[new Square(3, 3)];

        var moves = generator.GeneratePseudoLegalMovesForPiece(
            gameState,
            board,
            fromSquare: new Square(3, 3),
            piece: knight,
            logCastlingDecisions: false);

        var expectedDestinations = BoardGeometry.KnightOffsets
            .Select(offset => (File: 3 + offset.File, Rank: 3 + offset.Rank))
            .Where(offset => BoardGeometry.IsWithinBoard(offset.File, offset.Rank))
            .Select(offset => new Square(offset.File, offset.Rank))
            .OrderBy(square => square.Rank)
            .ThenBy(square => square.File)
            .ToArray();

        var actualDestinations = moves
            .Select(move => move.To)
            .OrderBy(square => square.Rank)
            .ThenBy(square => square.File)
            .ToArray();

        Assert.Equal(expectedDestinations, actualDestinations);
    }
}
