using System.Collections.Generic;
using Chess.Domain;

namespace Chess.Engine;

internal static class InitialPositionBuilder
{
    public static GameState CreateInitialState()
    {
        return new GameState(
            Pieces: CreateStandardPiecePlacement(),
            SideToMove: PieceColor.White,
            CastlingRights: CastlingRights.WhiteKingSide
                            | CastlingRights.WhiteQueenSide
                            | CastlingRights.BlackKingSide
                            | CastlingRights.BlackQueenSide,
            EnPassantTarget: null,
            HalfmoveClock: 0,
            FullmoveNumber: 1,
            Status: GameStatus.InProgress,
            MoveHistory: [],
            PositionHistory: []);
    }

    private static IReadOnlyList<PiecePlacement> CreateStandardPiecePlacement()
    {
        var pieces = new List<PiecePlacement>(32);

        AddBackRank(pieces, PieceColor.White, rank: 0);
        AddPawns(pieces, PieceColor.White, rank: 1);
        AddPawns(pieces, PieceColor.Black, rank: 6);
        AddBackRank(pieces, PieceColor.Black, rank: 7);

        return pieces;
    }

    private static void AddPawns(ICollection<PiecePlacement> pieces, PieceColor color, int rank)
    {
        for (var file = 0; file < 8; file++)
        {
            pieces.Add(new PiecePlacement(new Square(file, rank), new Piece(PieceType.Pawn, color)));
        }
    }

    private static void AddBackRank(ICollection<PiecePlacement> pieces, PieceColor color, int rank)
    {
        pieces.Add(new PiecePlacement(new Square(0, rank), new Piece(PieceType.Rook, color)));
        pieces.Add(new PiecePlacement(new Square(1, rank), new Piece(PieceType.Knight, color)));
        pieces.Add(new PiecePlacement(new Square(2, rank), new Piece(PieceType.Bishop, color)));
        pieces.Add(new PiecePlacement(new Square(3, rank), new Piece(PieceType.Queen, color)));
        pieces.Add(new PiecePlacement(new Square(4, rank), new Piece(PieceType.King, color)));
        pieces.Add(new PiecePlacement(new Square(5, rank), new Piece(PieceType.Bishop, color)));
        pieces.Add(new PiecePlacement(new Square(6, rank), new Piece(PieceType.Knight, color)));
        pieces.Add(new PiecePlacement(new Square(7, rank), new Piece(PieceType.Rook, color)));
    }
}
