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

    private static readonly PieceType[] PromotionPieceTypes =
    [
        PieceType.Queen,
        PieceType.Rook,
        PieceType.Bishop,
        PieceType.Knight
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

        return GeneratePseudoLegalMovesForPiece(gameState, board, fromSquare, piece);
    }

    public IReadOnlyList<Move> GenerateLegalMoves(GameState gameState, Square fromSquare)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var board = BuildBoard(gameState);

        if (!board.TryGetValue(fromSquare, out var piece) || piece.Color != gameState.SideToMove)
        {
            return [];
        }

        return GenerateLegalMovesForPiece(gameState, board, fromSquare, piece);
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

            legalMoves.AddRange(GenerateLegalMovesForPiece(gameState, board, placement.Square, placement.Piece));
        }

        return legalMoves;
    }

    public bool IsMoveLegal(GameState gameState, Square fromSquare, Square toSquare, PieceType? promotionPieceType = null)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var board = BuildBoard(gameState);
        board.TryGetValue(fromSquare, out var movingPiece);
        var resolvedPromotionPieceType = ResolvePromotionPieceType(movingPiece, toSquare, promotionPieceType);

        return GenerateLegalMoves(gameState, fromSquare)
            .Any(move => move.To == toSquare && move.PromotionPieceType == resolvedPromotionPieceType);
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

        var board = BuildBoard(gameState);
        board.TryGetValue(fromSquare, out var movingPiece);
        var resolvedPromotionPieceType = ResolvePromotionPieceType(movingPiece, toSquare, promotionPieceType);

        Move? legalMove = GenerateLegalMoves(gameState, fromSquare)
            .FirstOrDefault(move => move.To == toSquare && move.PromotionPieceType == resolvedPromotionPieceType);

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
        GameState gameState,
        IReadOnlyDictionary<Square, Piece> board,
        Square fromSquare,
        Piece piece)
    {
        var pseudoMoves = GeneratePseudoLegalMovesForPiece(gameState, board, fromSquare, piece);
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
        GameState gameState,
        IReadOnlyDictionary<Square, Piece> board,
        Square fromSquare,
        Piece piece)
    {
        var moves = new List<Move>();

        switch (piece.Type)
        {
            case PieceType.Pawn:
                AddPawnMoves(gameState, board, moves, fromSquare, piece);
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
                AddCastlingMoves(gameState, board, moves, fromSquare, piece);
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

        if (IsWithinBoard(fromSquare.File, nextRank))
        {
            var oneForward = new Square(fromSquare.File, nextRank);
            if (!board.ContainsKey(oneForward))
            {
                AddPawnMove(moves, fromSquare, oneForward, pawn);

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
                if (gameState.EnPassantTarget is null || gameState.EnPassantTarget.Value != targetSquare)
                {
                    continue;
                }

                var capturedPawnSquare = new Square(targetFile, fromSquare.Rank);
                if (!board.TryGetValue(capturedPawnSquare, out var enPassantPawn))
                {
                    continue;
                }

                if (enPassantPawn.Type != PieceType.Pawn || enPassantPawn.Color == pawn.Color)
                {
                    continue;
                }

                AddPawnMove(moves, fromSquare, targetSquare, pawn, enPassantPawn, isEnPassant: true);
                continue;
            }

            if (targetPiece.Color != pawn.Color && targetPiece.Type != PieceType.King)
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
        if (IsPromotionRank(pawn.Color, toSquare.Rank))
        {
            foreach (var promotionPieceType in PromotionPieceTypes)
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

    private static void AddCastlingMoves(
        GameState gameState,
        IReadOnlyDictionary<Square, Piece> board,
        ICollection<Move> moves,
        Square fromSquare,
        Piece king)
    {
        if (king.HasMoved)
        {
            return;
        }

        var homeRank = king.Color == PieceColor.White ? 0 : 7;
        var kingStartSquare = new Square(4, homeRank);
        if (fromSquare != kingStartSquare)
        {
            return;
        }

        var opponentColor = GetOpponentColor(king.Color);
        if (IsSquareAttacked(board, kingStartSquare, opponentColor))
        {
            return;
        }

        if (HasCastlingRight(gameState.CastlingRights, king.Color, isKingSide: true)
            && CanCastle(board, king.Color, homeRank, isKingSide: true))
        {
            moves.Add(new Move(fromSquare, new Square(6, homeRank), king, IsCastling: true));
        }

        if (HasCastlingRight(gameState.CastlingRights, king.Color, isKingSide: false)
            && CanCastle(board, king.Color, homeRank, isKingSide: false))
        {
            moves.Add(new Move(fromSquare, new Square(2, homeRank), king, IsCastling: true));
        }
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
        bool isKingSide)
    {
        var rookFromSquare = new Square(isKingSide ? 7 : 0, homeRank);
        if (!board.TryGetValue(rookFromSquare, out var rook)
            || rook.Type != PieceType.Rook
            || rook.Color != kingColor
            || rook.HasMoved)
        {
            return false;
        }

        var betweenSquares = isKingSide
            ? new[] { new Square(5, homeRank), new Square(6, homeRank) }
            : new[] { new Square(1, homeRank), new Square(2, homeRank), new Square(3, homeRank) };

        foreach (var square in betweenSquares)
        {
            if (board.ContainsKey(square))
            {
                return false;
            }
        }

        var opponentColor = GetOpponentColor(kingColor);
        var transitSquares = isKingSide
            ? new[] { new Square(5, homeRank), new Square(6, homeRank) }
            : new[] { new Square(3, homeRank), new Square(2, homeRank) };

        foreach (var square in transitSquares)
        {
            if (IsSquareAttacked(board, square, opponentColor))
            {
                return false;
            }
        }

        return true;
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

    private GameState BuildNextGameState(GameState currentState, Move legalMove)
    {
        var board = BuildBoard(currentState);
        var boardAfterMove = ApplyMoveOnBoard(board, legalMove);
        var movedPieceAfterMove = BuildMovedPieceAfterMove(legalMove);

        var moveHistory = currentState.MoveHistory.ToList();
        moveHistory.Add(legalMove with { MovedPiece = movedPieceAfterMove });

        var halfmoveClock = legalMove.CapturedPiece is not null || legalMove.MovedPiece.Type == PieceType.Pawn
            ? 0
            : currentState.HalfmoveClock + 1;

        var fullmoveNumber = currentState.SideToMove == PieceColor.Black
            ? currentState.FullmoveNumber + 1
            : currentState.FullmoveNumber;

        var updatedCastlingRights = UpdateCastlingRights(currentState.CastlingRights, legalMove);
        var enPassantTarget = DetermineEnPassantTarget(legalMove);
        var nextSideToMove = GetOpponentColor(currentState.SideToMove);

        var nextState = currentState with
        {
            Pieces = ToPlacements(boardAfterMove),
            SideToMove = nextSideToMove,
            HalfmoveClock = halfmoveClock,
            FullmoveNumber = fullmoveNumber,
            CastlingRights = updatedCastlingRights,
            EnPassantTarget = enPassantTarget,
            MoveHistory = moveHistory,
            Status = GameStatus.InProgress
        };

        var positionHistory = currentState.PositionHistory?.ToList() ?? [];
        if (positionHistory.Count == 0)
        {
            positionHistory.Add(BuildPositionSignature(currentState));
        }

        var nextPositionSignature = BuildPositionSignature(nextState);
        positionHistory.Add(nextPositionSignature);
        var nextStatus = DetermineGameStatus(nextState, nextPositionSignature, positionHistory);

        return nextState with
        {
            Status = nextStatus,
            PositionHistory = positionHistory
        };
    }

    private GameStatus DetermineGameStatus(
        GameState state,
        string currentPositionSignature,
        IReadOnlyList<string> positionHistory)
    {
        var legalMoves = GenerateLegalMoves(state);
        if (legalMoves.Count == 0)
        {
            if (IsKingInCheck(state, state.SideToMove))
            {
                return state.SideToMove == PieceColor.White ? GameStatus.BlackWin : GameStatus.WhiteWin;
            }

            return GameStatus.Draw;
        }

        if (state.HalfmoveClock >= 100)
        {
            return GameStatus.Draw;
        }

        if (IsThreefoldRepetition(currentPositionSignature, positionHistory))
        {
            return GameStatus.Draw;
        }

        if (IsInsufficientMaterial(state))
        {
            return GameStatus.Draw;
        }

        return GameStatus.InProgress;
    }

    private static Piece BuildMovedPieceAfterMove(Move move)
    {
        if (move.PromotionPieceType is PieceType promotionPieceType)
        {
            return new Piece(promotionPieceType, move.MovedPiece.Color, HasMoved: true);
        }

        return move.MovedPiece with { HasMoved = true };
    }

    private static CastlingRights UpdateCastlingRights(CastlingRights currentRights, Move move)
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

    private static Square? DetermineEnPassantTarget(Move move)
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

    private static PieceType? ResolvePromotionPieceType(Piece? movingPiece, Square toSquare, PieceType? promotionPieceType)
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

    private static bool IsPromotionRank(PieceColor color, int rank)
    {
        return (color == PieceColor.White && rank == 7) || (color == PieceColor.Black && rank == 0);
    }

    private static bool IsThreefoldRepetition(string currentPositionSignature, IReadOnlyList<string> positionHistory)
    {
        var occurrences = 0;

        foreach (var positionSignature in positionHistory)
        {
            if (positionSignature != currentPositionSignature)
            {
                continue;
            }

            occurrences++;
            if (occurrences >= 3)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsufficientMaterial(GameState gameState)
    {
        var nonKingPlacements = gameState.Pieces
            .Where(placement => placement.Piece.Type != PieceType.King)
            .ToArray();

        if (nonKingPlacements.Length == 0)
        {
            return true;
        }

        if (nonKingPlacements.Any(placement =>
                placement.Piece.Type is PieceType.Pawn or PieceType.Rook or PieceType.Queen))
        {
            return false;
        }

        if (nonKingPlacements.Length == 1)
        {
            return true;
        }

        var whiteMinorPlacements = nonKingPlacements
            .Where(placement => placement.Piece.Color == PieceColor.White)
            .ToArray();
        var blackMinorPlacements = nonKingPlacements
            .Where(placement => placement.Piece.Color == PieceColor.Black)
            .ToArray();

        if (whiteMinorPlacements.Length == 2
            && blackMinorPlacements.Length == 0
            && whiteMinorPlacements.All(placement => placement.Piece.Type == PieceType.Knight))
        {
            return true;
        }

        if (blackMinorPlacements.Length == 2
            && whiteMinorPlacements.Length == 0
            && blackMinorPlacements.All(placement => placement.Piece.Type == PieceType.Knight))
        {
            return true;
        }

        if (!nonKingPlacements.All(placement => placement.Piece.Type == PieceType.Bishop))
        {
            return false;
        }

        var hasLightSquareBishop = nonKingPlacements.Any(placement => IsLightSquare(placement.Square));
        var hasDarkSquareBishop = nonKingPlacements.Any(placement => !IsLightSquare(placement.Square));
        return !(hasLightSquareBishop && hasDarkSquareBishop);
    }

    private static string BuildPositionSignature(GameState gameState)
    {
        var pieceLayout = string.Join(
            ",",
            gameState.Pieces
                .OrderBy(placement => placement.Square.Rank)
                .ThenBy(placement => placement.Square.File)
                .Select(placement =>
                    $"{ToPieceSymbol(placement.Piece)}{placement.Square.File}{placement.Square.Rank}"));

        var enPassant = gameState.EnPassantTarget is null
            ? "-"
            : $"{gameState.EnPassantTarget.Value.File}{gameState.EnPassantTarget.Value.Rank}";

        return $"{gameState.SideToMove}|{(int)gameState.CastlingRights}|{enPassant}|{pieceLayout}";
    }

    private static char ToPieceSymbol(Piece piece)
    {
        var symbol = piece.Type switch
        {
            PieceType.Pawn => 'p',
            PieceType.Knight => 'n',
            PieceType.Bishop => 'b',
            PieceType.Rook => 'r',
            PieceType.Queen => 'q',
            PieceType.King => 'k',
            _ => throw new ArgumentOutOfRangeException(nameof(piece))
        };

        return piece.Color == PieceColor.White ? char.ToUpperInvariant(symbol) : symbol;
    }

    private static bool IsLightSquare(Square square)
    {
        return (square.File + square.Rank) % 2 != 0;
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
