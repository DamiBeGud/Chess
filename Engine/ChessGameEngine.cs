using System;
using System.Collections.Generic;
using System.Linq;
using Chess.Domain;

namespace Chess.Engine;

public sealed class ChessGameEngine : IGameEngine
{
    private static readonly (int File, int Rank)[] KnightOffsets =
    [
        (-2, -1), (-2, 1), (-1, -2), (-1, 2),
        (1, -2), (1, 2), (2, -1), (2, 1)
    ];

    private static readonly (int File, int Rank)[] KingOffsets =
    [
        (-1, -1), (-1, 0), (-1, 1),
        (0, -1),           (0, 1),
        (1, -1),  (1, 0),  (1, 1)
    ];

    private static readonly (int File, int Rank)[] BishopDirections =
    [
        (-1, -1), (-1, 1), (1, -1), (1, 1)
    ];

    private static readonly (int File, int Rank)[] RookDirections =
    [
        (-1, 0), (1, 0), (0, -1), (0, 1)
    ];

    private static readonly (int File, int Rank)[] QueenDirections =
    [
        (-1, -1), (-1, 1), (1, -1), (1, 1),
        (-1, 0), (1, 0), (0, -1), (0, 1)
    ];

    public GameState CreateInitialGameState()
    {
        return InitialPositionBuilder.CreateInitialState();
    }

    public IReadOnlyList<Move> GeneratePseudoLegalMoves(GameState gameState, Square fromSquare)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var board = BuildBoard(gameState);

        if (!board.TryGetValue(fromSquare, out var piece))
        {
            return [];
        }

        return GeneratePseudoLegalMovesForPiece(board, fromSquare, piece);
    }

    public IReadOnlyList<Move> GenerateLegalMoves(GameState gameState, Square fromSquare)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var board = BuildBoard(gameState);

        if (!board.TryGetValue(fromSquare, out var piece) || piece.Color != gameState.SideToMove)
        {
            return [];
        }

        return GenerateLegalMovesForPiece(board, fromSquare, piece);
    }

    public IReadOnlyList<Move> GenerateLegalMoves(GameState gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var board = BuildBoard(gameState);
        var legalMoves = new List<Move>();

        foreach (var placement in gameState.Pieces)
        {
            if (placement.Piece.Color != gameState.SideToMove)
            {
                continue;
            }

            legalMoves.AddRange(GenerateLegalMovesForPiece(board, placement.Square, placement.Piece));
        }

        return legalMoves;
    }

    public bool IsMoveLegal(GameState gameState, Square fromSquare, Square toSquare, PieceType? promotionPieceType = null)
    {
        return GenerateLegalMoves(gameState, fromSquare)
            .Any(move => move.To == toSquare && move.PromotionPieceType == promotionPieceType);
    }

    public bool TryApplyMove(
        GameState gameState,
        Square fromSquare,
        Square toSquare,
        out GameState updatedGameState,
        PieceType? promotionPieceType = null)
    {
        ArgumentNullException.ThrowIfNull(gameState);

        if (gameState.Status != GameStatus.InProgress)
        {
            updatedGameState = gameState;
            return false;
        }

        Move? legalMove = GenerateLegalMoves(gameState, fromSquare)
            .FirstOrDefault(move => move.To == toSquare && move.PromotionPieceType == promotionPieceType);

        if (legalMove is null)
        {
            updatedGameState = gameState;
            return false;
        }

        updatedGameState = BuildNextGameState(gameState, legalMove);
        return true;
    }

    public bool IsKingInCheck(GameState gameState, PieceColor color)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var board = BuildBoard(gameState);
        var kingSquare = FindKingSquare(board, color);
        return IsSquareAttacked(board, kingSquare, GetOpponentColor(color));
    }

    private static IReadOnlyList<Move> GenerateLegalMovesForPiece(
        IReadOnlyDictionary<Square, Piece> board,
        Square fromSquare,
        Piece piece)
    {
        var pseudoMoves = GeneratePseudoLegalMovesForPiece(board, fromSquare, piece);
        var legalMoves = new List<Move>(pseudoMoves.Count);

        foreach (var move in pseudoMoves)
        {
            if (!MoveLeavesKingInCheck(board, move, piece.Color))
            {
                legalMoves.Add(move);
            }
        }

        return legalMoves;
    }

    private static IReadOnlyList<Move> GeneratePseudoLegalMovesForPiece(
        IReadOnlyDictionary<Square, Piece> board,
        Square fromSquare,
        Piece piece)
    {
        var moves = new List<Move>();

        switch (piece.Type)
        {
            case PieceType.Pawn:
                AddPawnMoves(board, moves, fromSquare, piece);
                break;
            case PieceType.Knight:
                AddJumpingMoves(board, moves, fromSquare, piece, KnightOffsets);
                break;
            case PieceType.Bishop:
                AddSlidingMoves(board, moves, fromSquare, piece, BishopDirections);
                break;
            case PieceType.Rook:
                AddSlidingMoves(board, moves, fromSquare, piece, RookDirections);
                break;
            case PieceType.Queen:
                AddSlidingMoves(board, moves, fromSquare, piece, QueenDirections);
                break;
            case PieceType.King:
                AddJumpingMoves(board, moves, fromSquare, piece, KingOffsets);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return moves;
    }

    private static void AddPawnMoves(
        IReadOnlyDictionary<Square, Piece> board,
        ICollection<Move> moves,
        Square fromSquare,
        Piece pawn)
    {
        var direction = pawn.Color == PieceColor.White ? 1 : -1;
        var startRank = pawn.Color == PieceColor.White ? 1 : 6;
        var nextRank = fromSquare.Rank + direction;

        if (IsWithinBoard(fromSquare.File, nextRank))
        {
            var oneForward = new Square(fromSquare.File, nextRank);
            if (!board.ContainsKey(oneForward))
            {
                moves.Add(new Move(fromSquare, oneForward, pawn));

                var twoForwardRank = fromSquare.Rank + (2 * direction);
                if (fromSquare.Rank == startRank && IsWithinBoard(fromSquare.File, twoForwardRank))
                {
                    var twoForward = new Square(fromSquare.File, twoForwardRank);
                    if (!board.ContainsKey(twoForward))
                    {
                        moves.Add(new Move(fromSquare, twoForward, pawn));
                    }
                }
            }
        }

        foreach (var fileOffset in new[] { -1, 1 })
        {
            var targetFile = fromSquare.File + fileOffset;
            var targetRank = fromSquare.Rank + direction;

            if (!IsWithinBoard(targetFile, targetRank))
            {
                continue;
            }

            var targetSquare = new Square(targetFile, targetRank);
            if (!board.TryGetValue(targetSquare, out var targetPiece))
            {
                continue;
            }

            if (targetPiece.Color != pawn.Color && targetPiece.Type != PieceType.King)
            {
                moves.Add(new Move(fromSquare, targetSquare, pawn, targetPiece));
            }
        }
    }

    private static void AddJumpingMoves(
        IReadOnlyDictionary<Square, Piece> board,
        ICollection<Move> moves,
        Square fromSquare,
        Piece piece,
        IEnumerable<(int File, int Rank)> offsets)
    {
        foreach (var (fileOffset, rankOffset) in offsets)
        {
            var targetFile = fromSquare.File + fileOffset;
            var targetRank = fromSquare.Rank + rankOffset;

            if (!IsWithinBoard(targetFile, targetRank))
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
        IEnumerable<(int File, int Rank)> directions)
    {
        foreach (var (fileStep, rankStep) in directions)
        {
            var currentFile = fromSquare.File + fileStep;
            var currentRank = fromSquare.Rank + rankStep;

            while (IsWithinBoard(currentFile, currentRank))
            {
                var targetSquare = new Square(currentFile, currentRank);

                if (!board.TryGetValue(targetSquare, out var targetPiece))
                {
                    moves.Add(new Move(fromSquare, targetSquare, piece));
                    currentFile += fileStep;
                    currentRank += rankStep;
                    continue;
                }

                if (targetPiece.Color != piece.Color && targetPiece.Type != PieceType.King)
                {
                    moves.Add(new Move(fromSquare, targetSquare, piece, targetPiece));
                }

                break;
            }
        }
    }

    private static void AddMoveIfValid(
        IReadOnlyDictionary<Square, Piece> board,
        ICollection<Move> moves,
        Square fromSquare,
        Square toSquare,
        Piece movingPiece)
    {
        if (!board.TryGetValue(toSquare, out var targetPiece))
        {
            moves.Add(new Move(fromSquare, toSquare, movingPiece));
            return;
        }

        if (targetPiece.Color != movingPiece.Color && targetPiece.Type != PieceType.King)
        {
            moves.Add(new Move(fromSquare, toSquare, movingPiece, targetPiece));
        }
    }

    private static bool MoveLeavesKingInCheck(
        IReadOnlyDictionary<Square, Piece> board,
        Move move,
        PieceColor movingColor)
    {
        var boardAfterMove = ApplyMoveOnBoard(board, move);
        var kingSquare = move.MovedPiece.Type == PieceType.King
            ? move.To
            : FindKingSquare(boardAfterMove, movingColor);

        return IsSquareAttacked(boardAfterMove, kingSquare, GetOpponentColor(movingColor));
    }

    private static Dictionary<Square, Piece> ApplyMoveOnBoard(IReadOnlyDictionary<Square, Piece> board, Move move)
    {
        var updatedBoard = new Dictionary<Square, Piece>(board);
        updatedBoard.Remove(move.From);
        updatedBoard.Remove(move.To);
        updatedBoard[move.To] = move.MovedPiece with { HasMoved = true };
        return updatedBoard;
    }

    private static bool IsSquareAttacked(
        IReadOnlyDictionary<Square, Piece> board,
        Square targetSquare,
        PieceColor attackerColor)
    {
        foreach (var (sourceSquare, piece) in board)
        {
            if (piece.Color != attackerColor)
            {
                continue;
            }

            if (CanPieceAttackSquare(board, sourceSquare, piece, targetSquare))
            {
                return true;
            }
        }

        return false;
    }

    private static bool CanPieceAttackSquare(
        IReadOnlyDictionary<Square, Piece> board,
        Square sourceSquare,
        Piece piece,
        Square targetSquare)
    {
        return piece.Type switch
        {
            PieceType.Pawn => PawnAttacksSquare(sourceSquare, piece.Color, targetSquare),
            PieceType.Knight => KnightAttacksSquare(sourceSquare, targetSquare),
            PieceType.Bishop => SlidingPieceAttacksSquare(board, sourceSquare, targetSquare, BishopDirections),
            PieceType.Rook => SlidingPieceAttacksSquare(board, sourceSquare, targetSquare, RookDirections),
            PieceType.Queen => SlidingPieceAttacksSquare(board, sourceSquare, targetSquare, QueenDirections),
            PieceType.King => KingAttacksSquare(sourceSquare, targetSquare),
            _ => false
        };
    }

    private static bool PawnAttacksSquare(Square sourceSquare, PieceColor color, Square targetSquare)
    {
        var direction = color == PieceColor.White ? 1 : -1;
        var targetRank = sourceSquare.Rank + direction;
        return targetSquare.Rank == targetRank
               && (targetSquare.File == sourceSquare.File - 1 || targetSquare.File == sourceSquare.File + 1);
    }

    private static bool KnightAttacksSquare(Square sourceSquare, Square targetSquare)
    {
        var fileDelta = Math.Abs(sourceSquare.File - targetSquare.File);
        var rankDelta = Math.Abs(sourceSquare.Rank - targetSquare.Rank);
        return (fileDelta == 1 && rankDelta == 2) || (fileDelta == 2 && rankDelta == 1);
    }

    private static bool KingAttacksSquare(Square sourceSquare, Square targetSquare)
    {
        var fileDelta = Math.Abs(sourceSquare.File - targetSquare.File);
        var rankDelta = Math.Abs(sourceSquare.Rank - targetSquare.Rank);
        return fileDelta <= 1 && rankDelta <= 1 && (fileDelta != 0 || rankDelta != 0);
    }

    private static bool SlidingPieceAttacksSquare(
        IReadOnlyDictionary<Square, Piece> board,
        Square sourceSquare,
        Square targetSquare,
        IEnumerable<(int File, int Rank)> directions)
    {
        foreach (var (fileStep, rankStep) in directions)
        {
            var currentFile = sourceSquare.File + fileStep;
            var currentRank = sourceSquare.Rank + rankStep;

            while (IsWithinBoard(currentFile, currentRank))
            {
                var currentSquare = new Square(currentFile, currentRank);

                if (currentSquare == targetSquare)
                {
                    return true;
                }

                if (board.ContainsKey(currentSquare))
                {
                    break;
                }

                currentFile += fileStep;
                currentRank += rankStep;
            }
        }

        return false;
    }

    private static Square FindKingSquare(IReadOnlyDictionary<Square, Piece> board, PieceColor color)
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

    private static Dictionary<Square, Piece> BuildBoard(GameState gameState)
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

    private static GameState BuildNextGameState(GameState currentState, Move legalMove)
    {
        var board = BuildBoard(currentState);
        board.Remove(legalMove.From);
        board.Remove(legalMove.To);

        var movedPieceAfterMove = legalMove.MovedPiece with { HasMoved = true };
        board[legalMove.To] = movedPieceAfterMove;

        var moveHistory = currentState.MoveHistory.ToList();
        moveHistory.Add(legalMove with { MovedPiece = movedPieceAfterMove });

        var halfmoveClock = legalMove.CapturedPiece is not null || legalMove.MovedPiece.Type == PieceType.Pawn
            ? 0
            : currentState.HalfmoveClock + 1;

        var fullmoveNumber = currentState.SideToMove == PieceColor.Black
            ? currentState.FullmoveNumber + 1
            : currentState.FullmoveNumber;

        return currentState with
        {
            Pieces = ToPlacements(board),
            SideToMove = GetOpponentColor(currentState.SideToMove),
            HalfmoveClock = halfmoveClock,
            FullmoveNumber = fullmoveNumber,
            EnPassantTarget = null,
            MoveHistory = moveHistory
        };
    }

    private static IReadOnlyList<PiecePlacement> ToPlacements(IReadOnlyDictionary<Square, Piece> board)
    {
        return board
            .Select(entry => new PiecePlacement(entry.Key, entry.Value))
            .OrderBy(placement => placement.Square.Rank)
            .ThenBy(placement => placement.Square.File)
            .ToArray();
    }

    private static PieceColor GetOpponentColor(PieceColor color)
    {
        return color == PieceColor.White ? PieceColor.Black : PieceColor.White;
    }

    private static bool IsWithinBoard(int file, int rank)
    {
        return file is >= 0 and <= 7 && rank is >= 0 and <= 7;
    }
}
