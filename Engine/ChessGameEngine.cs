using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Chess.Domain;

namespace Chess.Engine;

public sealed class ChessGameEngine : IGameEngine
{
    private const string EventMoveRejected = "MoveRejected";
    private const string EventMoveAccepted = "MoveAccepted";
    private const string EventCastlingEvaluated = "CastlingEvaluated";
    private const string EventGameStatusEvaluated = "GameStatusEvaluated";

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

    private static readonly int[] PawnCaptureFileOffsets = [-1, 1];

    private readonly IChessEngineLogger _logger;
    private readonly EngineLogCategory _enabledLogCategories;
    private readonly bool _loggingEnabled;

    public ChessGameEngine()
        : this(null)
    {
    }

    public ChessGameEngine(ChessGameEngineOptions? options)
    {
        _logger = options?.Logger ?? NullChessEngineLogger.Instance;
        _enabledLogCategories = options?.LogCategories ?? EngineLogCategory.None;
        _loggingEnabled = _logger.IsEnabled && _enabledLogCategories != EngineLogCategory.None;
    }

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

        return GeneratePseudoLegalMovesForPiece(gameState, board, fromSquare, piece, logCastlingDecisions: true);
    }

    public IReadOnlyList<Move> GenerateLegalMoves(GameState gameState, Square fromSquare)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var board = BuildBoard(gameState);

        if (!board.TryGetValue(fromSquare, out var piece) || piece.Color != gameState.SideToMove)
        {
            return [];
        }

        return GenerateLegalMovesForPiece(gameState, board, fromSquare, piece, logCastlingDecisions: true);
    }

    public IReadOnlyList<Move> GenerateLegalMoves(GameState gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var board = BuildBoard(gameState);
        return GenerateLegalMoves(gameState, board);
    }

    public bool IsMoveLegal(GameState gameState, Square fromSquare, Square toSquare, PieceType? promotionPieceType = null)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var board = BuildBoard(gameState);
        var isLegal = TryFindRequestedLegalMove(
            gameState,
            board,
            fromSquare,
            toSquare,
            promotionPieceType,
            out _,
            out var failureReason,
            logCastlingDecisions: true);

        if (!isLegal)
        {
            LogMoveRejected(gameState, fromSquare, toSquare, promotionPieceType, failureReason);
        }

        return isLegal;
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
            LogMoveRejected(gameState, fromSquare, toSquare, promotionPieceType, MoveLookupFailureReason.GameAlreadyEnded);
            updatedGameState = gameState;
            return false;
        }

        var board = BuildBoard(gameState);
        if (!TryFindRequestedLegalMove(
                gameState,
                board,
                fromSquare,
                toSquare,
                promotionPieceType,
                out var legalMove,
                out var failureReason,
                logCastlingDecisions: true))
        {
            LogMoveRejected(gameState, fromSquare, toSquare, promotionPieceType, failureReason);
            updatedGameState = gameState;
            return false;
        }

        LogMoveAccepted(gameState, legalMove);
        updatedGameState = BuildNextGameState(gameState, legalMove);
        return true;
    }

    public bool IsKingInCheck(GameState gameState, PieceColor color)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var board = BuildBoard(gameState);
        return IsKingInCheck(board, color);
    }

    private enum MoveLookupFailureReason
    {
        None = 0,
        GameAlreadyEnded,
        SourceSquareIsEmpty,
        PieceBelongsToOpponent,
        DestinationNotLegal,
        PromotionSelectionInvalid
    }

    private IReadOnlyList<Move> GenerateLegalMoves(
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

    private bool TryFindRequestedLegalMove(
        GameState gameState,
        IReadOnlyDictionary<Square, Piece> board,
        Square fromSquare,
        Square toSquare,
        PieceType? promotionPieceType,
        [NotNullWhen(true)] out Move? requestedLegalMove,
        out MoveLookupFailureReason failureReason,
        bool logCastlingDecisions)
    {
        requestedLegalMove = null;

        if (!board.TryGetValue(fromSquare, out var movingPiece))
        {
            failureReason = MoveLookupFailureReason.SourceSquareIsEmpty;
            return false;
        }

        if (movingPiece.Color != gameState.SideToMove)
        {
            failureReason = MoveLookupFailureReason.PieceBelongsToOpponent;
            return false;
        }

        var resolvedPromotionPieceType = ResolvePromotionPieceType(movingPiece, toSquare, promotionPieceType);
        var legalMoves = GenerateLegalMovesForPiece(gameState, board, fromSquare, movingPiece, logCastlingDecisions);
        requestedLegalMove = legalMoves.FirstOrDefault(move =>
            move.To == toSquare && move.PromotionPieceType == resolvedPromotionPieceType);

        if (requestedLegalMove is not null)
        {
            failureReason = MoveLookupFailureReason.None;
            return true;
        }

        failureReason = promotionPieceType is not null && legalMoves.Any(move => move.To == toSquare)
            ? MoveLookupFailureReason.PromotionSelectionInvalid
            : MoveLookupFailureReason.DestinationNotLegal;
        return false;
    }

    private IReadOnlyList<Move> GenerateLegalMovesForPiece(
        GameState gameState,
        IReadOnlyDictionary<Square, Piece> board,
        Square fromSquare,
        Piece piece,
        bool logCastlingDecisions)
    {
        var pseudoMoves = GeneratePseudoLegalMovesForPiece(gameState, board, fromSquare, piece, logCastlingDecisions);
        var legalMoves = new List<Move>(pseudoMoves.Count);
        var kingSquare = piece.Type == PieceType.King ? fromSquare : FindKingSquare(board, piece.Color);

        foreach (var move in pseudoMoves)
        {
            var kingSquareAfterMove = piece.Type == PieceType.King ? move.To : kingSquare;
            if (!MoveLeavesKingInCheck(board, move, kingSquareAfterMove, piece.Color))
            {
                legalMoves.Add(move);
            }
        }

        return legalMoves;
    }

    private IReadOnlyList<Move> GeneratePseudoLegalMovesForPiece(
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

        foreach (var fileOffset in PawnCaptureFileOffsets)
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
            LogCastlingDecision(king.Color, fromSquare, null, false, "king has already moved", logCastlingDecisions);
            return;
        }

        var homeRank = king.Color == PieceColor.White ? 0 : 7;
        var kingStartSquare = new Square(4, homeRank);
        if (fromSquare != kingStartSquare)
        {
            LogCastlingDecision(king.Color, fromSquare, null, false, "king is not on the start square", logCastlingDecisions);
            return;
        }

        var opponentColor = GetOpponentColor(king.Color);
        if (IsSquareAttacked(board, kingStartSquare, opponentColor))
        {
            LogCastlingDecision(king.Color, fromSquare, null, false, "king is currently in check", logCastlingDecisions);
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
            LogCastlingDecision(
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
            LogCastlingDecision(
                king.Color,
                fromSquare,
                destination,
                false,
                $"ineligible {sideLabel} castling: {reason}",
                logCastlingDecisions);
            return;
        }

        moves.Add(new Move(fromSquare, destination, king, IsCastling: true));
        LogCastlingDecision(king.Color, fromSquare, destination, true, $"eligible {sideLabel} castling", logCastlingDecisions);
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

        var opponentColor = GetOpponentColor(kingColor);
        var transitFile = isKingSide ? 5 : 3;
        var destinationFile = isKingSide ? 6 : 2;
        if (IsSquareAttacked(board, new Square(transitFile, homeRank), opponentColor)
            || IsSquareAttacked(board, new Square(destinationFile, homeRank), opponentColor))
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
        (int File, int Rank)[] offsets)
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
        (int File, int Rank)[] directions)
    {
        foreach (var (fileStep, rankStep) in directions)
        {
            var currentFile = fromSquare.File + fileStep;
            var currentRank = fromSquare.Rank + rankStep;

            while (IsWithinBoard(currentFile, currentRank))
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

    private static bool MoveLeavesKingInCheck(
        IReadOnlyDictionary<Square, Piece> board,
        Move move,
        Square kingSquareAfterMove,
        PieceColor movingColor)
    {
        var boardAfterMove = ApplyMoveOnBoard(board, move);
        return IsSquareAttacked(boardAfterMove, kingSquareAfterMove, GetOpponentColor(movingColor));
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

    private static bool IsKingInCheck(IReadOnlyDictionary<Square, Piece> board, PieceColor color)
    {
        var kingSquare = FindKingSquare(board, color);
        return IsSquareAttacked(board, kingSquare, GetOpponentColor(color));
    }

    private static bool IsSquareAttacked(
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

        foreach (var (fileOffset, rankOffset) in KnightOffsets)
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

        foreach (var (fileOffset, rankOffset) in KingOffsets)
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

        if (IsAttackedBySlidingPiece(board, targetSquare, attackerColor, BishopDirections, PieceType.Bishop)
            || IsAttackedBySlidingPiece(board, targetSquare, attackerColor, RookDirections, PieceType.Rook))
        {
            return true;
        }

        return false;
    }

    private static bool IsAttackedBySlidingPiece(
        IReadOnlyDictionary<Square, Piece> board,
        Square targetSquare,
        PieceColor attackerColor,
        (int File, int Rank)[] directions,
        PieceType primaryPieceType)
    {
        foreach (var (fileStep, rankStep) in directions)
        {
            var currentFile = targetSquare.File + fileStep;
            var currentRank = targetSquare.Rank + rankStep;

            while (IsWithinBoard(currentFile, currentRank))
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
        if (!IsWithinBoard(file, rank))
        {
            return false;
        }

        return board.TryGetValue(new Square(file, rank), out var piece)
            && piece.Color == color
            && piece.Type == pieceType;
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
        var nextStatus = DetermineGameStatus(nextState, boardAfterMove, nextPositionSignature, positionHistory);

        return nextState with
        {
            Status = nextStatus,
            PositionHistory = positionHistory
        };
    }

    private GameStatus DetermineGameStatus(
        GameState state,
        IReadOnlyDictionary<Square, Piece> board,
        string currentPositionSignature,
        IReadOnlyList<string> positionHistory)
    {
        var legalMoves = GenerateLegalMoves(state, board);
        if (legalMoves.Count == 0)
        {
            if (IsKingInCheck(board, state.SideToMove))
            {
                var winningStatus = state.SideToMove == PieceColor.White ? GameStatus.BlackWin : GameStatus.WhiteWin;
                LogGameStatusDecision(
                    state.SideToMove,
                    legalMoves.Count,
                    winningStatus,
                    "checkmate");
                return winningStatus;
            }

            LogGameStatusDecision(state.SideToMove, legalMoves.Count, GameStatus.Draw, "stalemate");
            return GameStatus.Draw;
        }

        if (state.HalfmoveClock >= 100)
        {
            LogGameStatusDecision(state.SideToMove, legalMoves.Count, GameStatus.Draw, "fifty-move rule");
            return GameStatus.Draw;
        }

        if (IsThreefoldRepetition(currentPositionSignature, positionHistory))
        {
            LogGameStatusDecision(state.SideToMove, legalMoves.Count, GameStatus.Draw, "threefold repetition");
            return GameStatus.Draw;
        }

        if (IsInsufficientMaterial(state))
        {
            LogGameStatusDecision(state.SideToMove, legalMoves.Count, GameStatus.Draw, "insufficient material");
            return GameStatus.Draw;
        }

        LogGameStatusDecision(state.SideToMove, legalMoves.Count, GameStatus.InProgress, "legal moves remain");
        return GameStatus.InProgress;
    }

    private void LogMoveRejected(
        GameState gameState,
        Square fromSquare,
        Square toSquare,
        PieceType? promotionPieceType,
        MoveLookupFailureReason failureReason)
    {
        LogDecision(
            EngineLogCategory.MoveValidation,
            new EngineLogEntry(
                Category: EngineLogCategory.MoveValidation,
                EventName: EventMoveRejected,
                Message: DescribeMoveLookupFailureReason(failureReason),
                From: fromSquare,
                To: toSquare,
                SideToMove: gameState.SideToMove,
                PromotionPieceType: promotionPieceType,
                GameStatus: gameState.Status));
    }

    private void LogMoveAccepted(GameState gameState, Move move)
    {
        LogDecision(
            EngineLogCategory.MoveValidation,
            new EngineLogEntry(
                Category: EngineLogCategory.MoveValidation,
                EventName: EventMoveAccepted,
                Message: "Move accepted.",
                From: move.From,
                To: move.To,
                SideToMove: gameState.SideToMove,
                PieceType: move.MovedPiece.Type,
                PromotionPieceType: move.PromotionPieceType,
                GameStatus: gameState.Status));
    }

    private void LogCastlingDecision(
        PieceColor sideToMove,
        Square fromSquare,
        Square? toSquare,
        bool isEligible,
        string reason,
        bool emitLog)
    {
        if (!emitLog)
        {
            return;
        }

        var message = isEligible
            ? $"Castling allowed: {reason}"
            : $"Castling rejected: {reason}";

        LogDecision(
            EngineLogCategory.SpecialMoves,
            new EngineLogEntry(
                Category: EngineLogCategory.SpecialMoves,
                EventName: EventCastlingEvaluated,
                Message: message,
                From: fromSquare,
                To: toSquare,
                SideToMove: sideToMove));
    }

    private void LogGameStatusDecision(
        PieceColor sideToMove,
        int legalMoveCount,
        GameStatus gameStatus,
        string reason)
    {
        LogDecision(
            EngineLogCategory.GameStatus,
            new EngineLogEntry(
                Category: EngineLogCategory.GameStatus,
                EventName: EventGameStatusEvaluated,
                Message: $"{reason}; legalMoves={legalMoveCount}",
                SideToMove: sideToMove,
                GameStatus: gameStatus));
    }

    private void LogDecision(EngineLogCategory category, in EngineLogEntry entry)
    {
        if (!_loggingEnabled || (_enabledLogCategories & category) == 0)
        {
            return;
        }

        _logger.Log(in entry);
    }

    private static string DescribeMoveLookupFailureReason(MoveLookupFailureReason reason)
    {
        return reason switch
        {
            MoveLookupFailureReason.GameAlreadyEnded => "Game is not in progress.",
            MoveLookupFailureReason.SourceSquareIsEmpty => "No piece exists on source square.",
            MoveLookupFailureReason.PieceBelongsToOpponent => "Source piece does not belong to side to move.",
            MoveLookupFailureReason.DestinationNotLegal => "Destination is not a legal move.",
            MoveLookupFailureReason.PromotionSelectionInvalid => "Promotion selection is incompatible with legal promotion moves.",
            _ => "Move rejected."
        };
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
