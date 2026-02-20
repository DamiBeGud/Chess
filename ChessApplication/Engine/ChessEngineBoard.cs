using System;
using System.Collections.Generic;
using System.Linq;
using Chess.Domain;

namespace Chess.Engine;

internal static class ChessEngineBoard
{
    internal static Dictionary<Square, Piece> BuildBoard(GameState gameState)
    {
        var board = new Dictionary<Square, Piece>(gameState.Pieces.Count);

        foreach (var placement in gameState.Pieces)
        {
            if (!board.TryAdd(placement.Square, placement.Piece))
            {
                throw new InvalidOperationException("Invalid game state: multiple pieces on the same square.");
            }
        }

        return board;
    }

    internal static IReadOnlyList<PiecePlacement> ToPlacements(IReadOnlyDictionary<Square, Piece> board)
    {
        return board
            .Select(entry => new PiecePlacement(entry.Key, entry.Value))
            .OrderBy(placement => placement.Square.Rank)
            .ThenBy(placement => placement.Square.File)
            .ToArray();
    }

    internal static Square FindKingSquare(IReadOnlyDictionary<Square, Piece> board, PieceColor color)
    {
        foreach (var (square, piece) in board)
        {
            if (piece.Type == PieceType.King && piece.Color == color)
            {
                return square;
            }
        }

        throw new InvalidOperationException($"No {color} king found in game state.");
    }

    internal static PieceColor GetOpponentColor(PieceColor color)
    {
        return color == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }

    internal static bool IsWithinBoard(int file, int rank)
    {
        return BoardGeometry.IsWithinBoard(file, rank);
    }
}
