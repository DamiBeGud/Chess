using System.Collections.Generic;
using Chess.Domain;

namespace Chess.Engine;

internal static class ChessAttackDetector
{
    internal static bool MoveLeavesKingInCheck(
        IReadOnlyDictionary<Square, Piece> board,
        Move move,
        Square kingSquareAfterMove,
        PieceColor movingColor)
    {
        var boardAfterMove = ChessMoveApplication.ApplyMoveOnBoard(board, move);
        return IsSquareAttacked(boardAfterMove, kingSquareAfterMove, ChessEngineBoard.GetOpponentColor(movingColor));
    }

    internal static bool IsKingInCheck(IReadOnlyDictionary<Square, Piece> board, PieceColor color)
    {
        var kingSquare = ChessEngineBoard.FindKingSquare(board, color);
        return IsSquareAttacked(board, kingSquare, ChessEngineBoard.GetOpponentColor(color));
    }

    internal static bool IsSquareAttacked(
        IReadOnlyDictionary<Square, Piece> board,
        Square targetSquare,
        PieceColor attackerColor)
    {
        var pawnSourceRank = targetSquare.Rank - (attackerColor == PieceColor.White ? 1 : -1);
        if (IsPieceAt(board, targetSquare.File - 1, pawnSourceRank, attackerColor, PieceType.Pawn)
            || IsPieceAt(board, targetSquare.File + 1, pawnSourceRank, attackerColor, PieceType.Pawn))
        {
            return true;
        }

        foreach (var (fileOffset, rankOffset) in BoardGeometry.KnightOffsets)
        {
            if (IsPieceAt(
                    board,
                    targetSquare.File + fileOffset,
                    targetSquare.Rank + rankOffset,
                    attackerColor,
                    PieceType.Knight))
            {
                return true;
            }
        }

        foreach (var (fileOffset, rankOffset) in BoardGeometry.KingOffsets)
        {
            if (IsPieceAt(
                    board,
                    targetSquare.File + fileOffset,
                    targetSquare.Rank + rankOffset,
                    attackerColor,
                    PieceType.King))
            {
                return true;
            }
        }

        if (IsAttackedBySlidingPiece(board, targetSquare, attackerColor, BoardGeometry.DiagonalDirections, PieceType.Bishop)
            || IsAttackedBySlidingPiece(board, targetSquare, attackerColor, BoardGeometry.OrthogonalDirections, PieceType.Rook))
        {
            return true;
        }

        return false;
    }

    private static bool IsAttackedBySlidingPiece(
        IReadOnlyDictionary<Square, Piece> board,
        Square targetSquare,
        PieceColor attackerColor,
        IReadOnlyList<(int File, int Rank)> directions,
        PieceType primaryPieceType)
    {
        foreach (var (fileStep, rankStep) in directions)
        {
            var currentFile = targetSquare.File + fileStep;
            var currentRank = targetSquare.Rank + rankStep;

            while (ChessEngineBoard.IsWithinBoard(currentFile, currentRank))
            {
                var currentSquare = new Square(currentFile, currentRank);
                if (!board.TryGetValue(currentSquare, out var piece))
                {
                    currentFile += fileStep;
                    currentRank += rankStep;
                    continue;
                }

                if (piece.Color == attackerColor
                    && (piece.Type == primaryPieceType || piece.Type == PieceType.Queen))
                {
                    return true;
                }

                break;
            }
        }

        return false;
    }

    private static bool IsPieceAt(
        IReadOnlyDictionary<Square, Piece> board,
        int file,
        int rank,
        PieceColor color,
        PieceType pieceType)
    {
        if (!ChessEngineBoard.IsWithinBoard(file, rank))
        {
            return false;
        }

        return board.TryGetValue(new Square(file, rank), out var piece)
            && piece.Color == color
            && piece.Type == pieceType;
    }
}
