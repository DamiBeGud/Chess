using System;
using System.Collections.Generic;
using System.Linq;
using Chess.Domain;

namespace Chess.Engine;

/// <summary>
/// ChessEngineBoard is a concrete type within the Engine module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include ChessMoveGenerator (Engine), ChessGameEngine (Engine), ChessAttackDetector (Engine).
/// No constructor-injected collaborators were detected in this declaration.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> ChessMoveGenerator (Engine), ChessGameEngine (Engine), ChessAttackDetector (Engine), ChessStateTransitionService (Engine)</para>
/// <para><b>Usage pattern:</b> Callers invoke it during legal move generation, move application, attack evaluation, and game-status checks inside the engine pipeline.</para>
/// <para><b>Dependencies/Collaborators:</b> No constructor-injected collaborators were detected in this declaration.</para>
/// <para><b>Boundary:</b> This type sits in the rules engine boundary and participates in move evaluation or state transition logic.</para>
/// </remarks>
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
