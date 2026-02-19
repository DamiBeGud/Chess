using System;
using System.Collections.Generic;
using Chess.Domain;

namespace Chess.Engine;

internal static class ChessMoveApplication
{
    internal static readonly PieceType[] PromotionPieceTypes =
    [
        PieceType.Queen,
        PieceType.Rook,
        PieceType.Bishop,
        PieceType.Knight
    ];

    internal static Dictionary<Square, Piece> ApplyMoveOnBoard(IReadOnlyDictionary<Square, Piece> board, Move move)
    {
        var updatedBoard = new Dictionary<Square, Piece>(board);
        updatedBoard.Remove(move.From);

        if (move.IsEnPassant)
        {
            var capturedPawnSquare = new Square(move.To.File, move.From.Rank);
            updatedBoard.Remove(capturedPawnSquare);
        }
        else
        {
            updatedBoard.Remove(move.To);
        }

        var movedPieceAfterMove = BuildMovedPieceAfterMove(move);
        updatedBoard[move.To] = movedPieceAfterMove;

        if (move.IsCastling)
        {
            MoveRookForCastling(updatedBoard, move, movedPieceAfterMove.Color);
        }

        return updatedBoard;
    }

    internal static Piece BuildMovedPieceAfterMove(Move move)
    {
        if (move.PromotionPieceType is PieceType promotionPieceType)
        {
            return new Piece(promotionPieceType, move.MovedPiece.Color, HasMoved: true);
        }

        return move.MovedPiece with { HasMoved = true };
    }

    internal static CastlingRights UpdateCastlingRights(CastlingRights currentRights, Move move)
    {
        var updatedRights = currentRights;

        if (move.MovedPiece.Type == PieceType.King)
        {
            updatedRights = move.MovedPiece.Color == PieceColor.White
                ? updatedRights & ~(CastlingRights.WhiteKingSide | CastlingRights.WhiteQueenSide)
                : updatedRights & ~(CastlingRights.BlackKingSide | CastlingRights.BlackQueenSide);
        }

        if (move.MovedPiece.Type == PieceType.Rook)
        {
            updatedRights = RemoveCastlingRightForRookSquare(updatedRights, move.MovedPiece.Color, move.From);
        }

        if (move.CapturedPiece is { Type: PieceType.Rook } capturedRook && !move.IsEnPassant)
        {
            updatedRights = RemoveCastlingRightForRookSquare(updatedRights, capturedRook.Color, move.To);
        }

        return updatedRights;
    }

    internal static Square? DetermineEnPassantTarget(Move move)
    {
        if (move.MovedPiece.Type != PieceType.Pawn)
        {
            return null;
        }

        if (Math.Abs(move.To.Rank - move.From.Rank) != 2)
        {
            return null;
        }

        var midRank = (move.From.Rank + move.To.Rank) / 2;
        return new Square(move.From.File, midRank);
    }

    internal static PieceType? ResolvePromotionPieceType(Piece? movingPiece, Square toSquare, PieceType? promotionPieceType)
    {
        if (movingPiece is null || movingPiece.Type != PieceType.Pawn)
        {
            return promotionPieceType;
        }

        if (!IsPromotionRank(movingPiece.Color, toSquare.Rank))
        {
            return promotionPieceType;
        }

        return promotionPieceType ?? PieceType.Queen;
    }

    internal static bool IsPromotionRank(PieceColor color, int rank)
    {
        return (color == PieceColor.White && rank == 7) || (color == PieceColor.Black && rank == 0);
    }

    private static CastlingRights RemoveCastlingRightForRookSquare(
        CastlingRights currentRights,
        PieceColor rookColor,
        Square rookSquare)
    {
        if (rookColor == PieceColor.White)
        {
            if (rookSquare == new Square(0, 0))
            {
                return currentRights & ~CastlingRights.WhiteQueenSide;
            }

            if (rookSquare == new Square(7, 0))
            {
                return currentRights & ~CastlingRights.WhiteKingSide;
            }
        }
        else
        {
            if (rookSquare == new Square(0, 7))
            {
                return currentRights & ~CastlingRights.BlackQueenSide;
            }

            if (rookSquare == new Square(7, 7))
            {
                return currentRights & ~CastlingRights.BlackKingSide;
            }
        }

        return currentRights;
    }

    private static void MoveRookForCastling(
        IDictionary<Square, Piece> board,
        Move move,
        PieceColor kingColor)
    {
        var homeRank = move.From.Rank;
        Square rookFromSquare;
        Square rookToSquare;

        if (move.To.File == 6)
        {
            rookFromSquare = new Square(7, homeRank);
            rookToSquare = new Square(5, homeRank);
        }
        else if (move.To.File == 2)
        {
            rookFromSquare = new Square(0, homeRank);
            rookToSquare = new Square(3, homeRank);
        }
        else
        {
            throw new InvalidOperationException("Invalid castling destination square.");
        }

        if (!board.TryGetValue(rookFromSquare, out var rook)
            || rook.Type != PieceType.Rook
            || rook.Color != kingColor)
        {
            throw new InvalidOperationException("Invalid castling move: expected rook not found.");
        }

        board.Remove(rookFromSquare);
        board[rookToSquare] = rook with { HasMoved = true };
    }
}
