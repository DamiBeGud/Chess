using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Chess.Domain;

namespace Chess.Engine;

/// <summary>
/// ChessMoveGenerator is a concrete type within the Engine module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include ChessGameEngine (Engine).
/// Key collaborators are bool.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> ChessGameEngine (Engine)</para>
/// <para><b>Usage pattern:</b> Callers invoke it during legal move generation, move application, attack evaluation, and game-status checks inside the engine pipeline.</para>
/// <para><b>Dependencies/Collaborators:</b> bool.</para>
/// <para><b>Boundary:</b> This type sits in the rules engine boundary and participates in move evaluation or state transition logic.</para>
/// </remarks>
internal sealed class ChessMoveGenerator
{
    private readonly Action<PieceColor, Square, Square?, bool, string, bool> _logCastlingDecision;

    internal ChessMoveGenerator(Action<PieceColor, Square, Square?, bool, string, bool> logCastlingDecision)
    {
        _logCastlingDecision = logCastlingDecision ?? throw new ArgumentNullException(nameof(logCastlingDecision));
    }

    internal IReadOnlyList<Move> GenerateLegalMovesForActiveSide(
        GameState gameState,
        IReadOnlyDictionary<Square, Piece> board)
    {
        var legalMoves = new List<Move>();

        foreach (var placement in gameState.Pieces)
        {
            if (placement.Piece.Color != gameState.SideToMove)
            {
                continue;
            }

            legalMoves.AddRange(GenerateLegalMovesForPiece(
                gameState,
                board,
                placement.Square,
                placement.Piece,
                logCastlingDecisions: false));
        }

        return legalMoves;
    }

    internal IReadOnlyList<Move> GenerateLegalMovesForPiece(
        GameState gameState,
        IReadOnlyDictionary<Square, Piece> board,
        Square fromSquare,
        Piece piece,
        bool logCastlingDecisions)
    {
        var pseudoMoves = GeneratePseudoLegalMovesForPiece(gameState, board, fromSquare, piece, logCastlingDecisions);
        var legalMoves = new List<Move>(pseudoMoves.Count);
        var kingSquare = piece.Type == PieceType.King ? fromSquare : ChessEngineBoard.FindKingSquare(board, piece.Color);

        foreach (var move in pseudoMoves)
        {
            var kingSquareAfterMove = piece.Type == PieceType.King ? move.To : kingSquare;
            if (!ChessAttackDetector.MoveLeavesKingInCheck(board, move, kingSquareAfterMove, piece.Color))
            {
                legalMoves.Add(move);
            }
        }

        return legalMoves;
    }

    internal IReadOnlyList<Move> GeneratePseudoLegalMovesForPiece(
        GameState gameState,
        IReadOnlyDictionary<Square, Piece> board,
        Square fromSquare,
        Piece piece,
        bool logCastlingDecisions)
    {
        var moves = new List<Move>();

        switch (piece.Type)
        {
            case PieceType.Pawn:
                AddPawnMoves(gameState, board, moves, fromSquare, piece);
                break;
            case PieceType.Knight:
                AddJumpingMoves(board, moves, fromSquare, piece, BoardGeometry.KnightOffsets);
                break;
            case PieceType.Bishop:
                AddSlidingMoves(board, moves, fromSquare, piece, BoardGeometry.DiagonalDirections);
                break;
            case PieceType.Rook:
                AddSlidingMoves(board, moves, fromSquare, piece, BoardGeometry.OrthogonalDirections);
                break;
            case PieceType.Queen:
                AddSlidingMoves(board, moves, fromSquare, piece, BoardGeometry.QueenDirections);
                break;
            case PieceType.King:
                AddJumpingMoves(board, moves, fromSquare, piece, BoardGeometry.KingOffsets);
                AddCastlingMoves(gameState, board, moves, fromSquare, piece, logCastlingDecisions);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return moves;
    }

    private static void AddPawnMoves(
        GameState gameState,
        IReadOnlyDictionary<Square, Piece> board,
        ICollection<Move> moves,
        Square fromSquare,
        Piece pawn)
    {
        var direction = pawn.Color == PieceColor.White ? 1 : -1;
        var startRank = pawn.Color == PieceColor.White ? 1 : 6;
        var nextRank = fromSquare.Rank + direction;

        if (ChessEngineBoard.IsWithinBoard(fromSquare.File, nextRank))
        {
            var oneForward = new Square(fromSquare.File, nextRank);
            if (!board.ContainsKey(oneForward))
            {
                AddPawnMove(moves, fromSquare, oneForward, pawn);

                var twoForwardRank = fromSquare.Rank + (2 * direction);
                if (fromSquare.Rank == startRank && ChessEngineBoard.IsWithinBoard(fromSquare.File, twoForwardRank))
                {
                    var twoForward = new Square(fromSquare.File, twoForwardRank);
                    if (!board.ContainsKey(twoForward))
                    {
                        moves.Add(new Move(fromSquare, twoForward, pawn));
                    }
                }
            }
        }

        foreach (var fileOffset in BoardGeometry.PawnCaptureFileOffsets)
        {
            var targetFile = fromSquare.File + fileOffset;
            var targetRank = fromSquare.Rank + direction;

            if (!ChessEngineBoard.IsWithinBoard(targetFile, targetRank))
            {
                continue;
            }

            var targetSquare = new Square(targetFile, targetRank);
            if (!board.TryGetValue(targetSquare, out var targetPiece))
            {
                if (gameState.EnPassantTarget is null || gameState.EnPassantTarget.Value != targetSquare)
                {
                    continue;
                }

                var capturedPawnSquare = new Square(targetFile, fromSquare.Rank);
                if (!board.TryGetValue(capturedPawnSquare, out var enPassantPawn))
                {
                    continue;
                }

                if (enPassantPawn.Type != PieceType.Pawn || !CanCaptureTarget(pawn, enPassantPawn))
                {
                    continue;
                }

                AddPawnMove(moves, fromSquare, targetSquare, pawn, enPassantPawn, isEnPassant: true);
                continue;
            }

            if (CanCaptureTarget(pawn, targetPiece))
            {
                AddPawnMove(moves, fromSquare, targetSquare, pawn, targetPiece);
            }
        }
    }

    private static void AddPawnMove(
        ICollection<Move> moves,
        Square fromSquare,
        Square toSquare,
        Piece pawn,
        Piece? capturedPiece = null,
        bool isEnPassant = false)
    {
        if (ChessMoveApplication.IsPromotionRank(pawn.Color, toSquare.Rank))
        {
            foreach (var promotionPieceType in ChessMoveApplication.PromotionPieceTypes)
            {
                moves.Add(new Move(
                    fromSquare,
                    toSquare,
                    pawn,
                    capturedPiece,
                    IsEnPassant: isEnPassant,
                    PromotionPieceType: promotionPieceType));
            }

            return;
        }

        moves.Add(new Move(
            fromSquare,
            toSquare,
            pawn,
            capturedPiece,
            IsEnPassant: isEnPassant));
    }

    private void AddCastlingMoves(
        GameState gameState,
        IReadOnlyDictionary<Square, Piece> board,
        ICollection<Move> moves,
        Square fromSquare,
        Piece king,
        bool logCastlingDecisions)
    {
        if (king.HasMoved)
        {
            _logCastlingDecision(king.Color, fromSquare, null, false, "king has already moved", logCastlingDecisions);
            return;
        }

        var homeRank = king.Color == PieceColor.White ? 0 : 7;
        var kingStartSquare = new Square(4, homeRank);
        if (fromSquare != kingStartSquare)
        {
            _logCastlingDecision(king.Color, fromSquare, null, false, "king is not on the start square", logCastlingDecisions);
            return;
        }

        var opponentColor = ChessEngineBoard.GetOpponentColor(king.Color);
        if (ChessAttackDetector.IsSquareAttacked(board, kingStartSquare, opponentColor))
        {
            _logCastlingDecision(king.Color, fromSquare, null, false, "king is currently in check", logCastlingDecisions);
            return;
        }

        TryAddCastlingMove(gameState, board, moves, fromSquare, king, homeRank, isKingSide: true, logCastlingDecisions);
        TryAddCastlingMove(gameState, board, moves, fromSquare, king, homeRank, isKingSide: false, logCastlingDecisions);
    }

    private void TryAddCastlingMove(
        GameState gameState,
        IReadOnlyDictionary<Square, Piece> board,
        ICollection<Move> moves,
        Square fromSquare,
        Piece king,
        int homeRank,
        bool isKingSide,
        bool logCastlingDecisions)
    {
        var destination = new Square(isKingSide ? 6 : 2, homeRank);
        var sideLabel = isKingSide ? "king-side" : "queen-side";

        if (!HasCastlingRight(gameState.CastlingRights, king.Color, isKingSide))
        {
            _logCastlingDecision(
                king.Color,
                fromSquare,
                destination,
                false,
                $"missing {sideLabel} castling right",
                logCastlingDecisions);
            return;
        }

        if (!CanCastle(board, king.Color, homeRank, isKingSide, out var reason))
        {
            _logCastlingDecision(
                king.Color,
                fromSquare,
                destination,
                false,
                $"ineligible {sideLabel} castling: {reason}",
                logCastlingDecisions);
            return;
        }

        moves.Add(new Move(fromSquare, destination, king, IsCastling: true));
        _logCastlingDecision(king.Color, fromSquare, destination, true, $"eligible {sideLabel} castling", logCastlingDecisions);
    }

    private static bool HasCastlingRight(CastlingRights castlingRights, PieceColor color, bool isKingSide)
    {
        var requiredRight = (color, isKingSide) switch
        {
            (PieceColor.White, true) => CastlingRights.WhiteKingSide,
            (PieceColor.White, false) => CastlingRights.WhiteQueenSide,
            (PieceColor.Black, true) => CastlingRights.BlackKingSide,
            (PieceColor.Black, false) => CastlingRights.BlackQueenSide,
            _ => CastlingRights.None
        };

        return (castlingRights & requiredRight) != 0;
    }

    private static bool CanCastle(
        IReadOnlyDictionary<Square, Piece> board,
        PieceColor kingColor,
        int homeRank,
        bool isKingSide,
        [NotNullWhen(false)] out string? failureReason)
    {
        var rookFromSquare = new Square(isKingSide ? 7 : 0, homeRank);
        if (!board.TryGetValue(rookFromSquare, out var rook)
            || rook.Type != PieceType.Rook
            || rook.Color != kingColor
            || rook.HasMoved)
        {
            failureReason = "rook is missing or has moved";
            return false;
        }

        var fromFile = isKingSide ? 5 : 1;
        var toFile = isKingSide ? 6 : 3;
        for (var file = fromFile; file <= toFile; file++)
        {
            var square = new Square(file, homeRank);
            if (board.ContainsKey(square))
            {
                failureReason = "path squares are occupied";
                return false;
            }
        }

        var opponentColor = ChessEngineBoard.GetOpponentColor(kingColor);
        var transitFile = isKingSide ? 5 : 3;
        var destinationFile = isKingSide ? 6 : 2;
        if (ChessAttackDetector.IsSquareAttacked(board, new Square(transitFile, homeRank), opponentColor)
            || ChessAttackDetector.IsSquareAttacked(board, new Square(destinationFile, homeRank), opponentColor))
        {
            failureReason = "transit or destination square is attacked";
            return false;
        }

        failureReason = null;
        return true;
    }

    private static void AddJumpingMoves(
        IReadOnlyDictionary<Square, Piece> board,
        ICollection<Move> moves,
        Square fromSquare,
        Piece piece,
        IReadOnlyList<(int File, int Rank)> offsets)
    {
        foreach (var (fileOffset, rankOffset) in offsets)
        {
            var targetFile = fromSquare.File + fileOffset;
            var targetRank = fromSquare.Rank + rankOffset;

            if (!ChessEngineBoard.IsWithinBoard(targetFile, targetRank))
            {
                continue;
            }

            var targetSquare = new Square(targetFile, targetRank);
            AddMoveIfValid(board, moves, fromSquare, targetSquare, piece);
        }
    }

    private static void AddSlidingMoves(
        IReadOnlyDictionary<Square, Piece> board,
        ICollection<Move> moves,
        Square fromSquare,
        Piece piece,
        IReadOnlyList<(int File, int Rank)> directions)
    {
        foreach (var (fileStep, rankStep) in directions)
        {
            var currentFile = fromSquare.File + fileStep;
            var currentRank = fromSquare.Rank + rankStep;

            while (ChessEngineBoard.IsWithinBoard(currentFile, currentRank))
            {
                var targetSquare = new Square(currentFile, currentRank);
                if (AddMoveIfValid(board, moves, fromSquare, targetSquare, piece))
                {
                    currentFile += fileStep;
                    currentRank += rankStep;
                    continue;
                }

                break;
            }
        }
    }

    private static bool AddMoveIfValid(
        IReadOnlyDictionary<Square, Piece> board,
        ICollection<Move> moves,
        Square fromSquare,
        Square toSquare,
        Piece movingPiece)
    {
        if (!board.TryGetValue(toSquare, out var targetPiece))
        {
            moves.Add(new Move(fromSquare, toSquare, movingPiece));
            return true;
        }

        if (CanCaptureTarget(movingPiece, targetPiece))
        {
            moves.Add(new Move(fromSquare, toSquare, movingPiece, targetPiece));
        }

        return false;
    }

    private static bool CanCaptureTarget(Piece movingPiece, Piece targetPiece)
    {
        return targetPiece.Color != movingPiece.Color && targetPiece.Type != PieceType.King;
    }
}
