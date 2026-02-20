using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Chess.Domain;

namespace Chess.Engine;

/// <summary>
/// ChessGameEngine is a concrete type within the Engine module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include App (AppShell).
/// Key collaborators are ChessGameEngineOptions, generation, move, attack, None, GameAlreadyEnded.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> App (AppShell)</para>
/// <para><b>Usage pattern:</b> Callers invoke it during legal move generation, move application, attack evaluation, and game-status checks inside the engine pipeline.</para>
/// <para><b>Dependencies/Collaborators:</b> ChessGameEngineOptions, generation, move, attack, None, GameAlreadyEnded.</para>
/// <para><b>Boundary:</b> This type sits in the rules engine boundary and participates in move evaluation or state transition logic.</para>
/// </remarks>
public sealed class ChessGameEngine : IGameEngine
{
    private const string EventMoveRejected = "MoveRejected";
    private const string EventMoveAccepted = "MoveAccepted";
    private const string EventCastlingEvaluated = "CastlingEvaluated";
    private const string EventGameStatusEvaluated = "GameStatusEvaluated";

    private readonly IChessEngineLogger _logger;
    private readonly EngineLogCategory _enabledLogCategories;
    private readonly bool _loggingEnabled;
    private readonly ChessMoveGenerator _moveGenerator;
    private readonly ChessStateTransitionService _stateTransitionService;

    public ChessGameEngine()
        : this(null)
    {
    }

    public ChessGameEngine(ChessGameEngineOptions? options)
    {
        _logger = options?.Logger ?? NullChessEngineLogger.Instance;
        _enabledLogCategories = options?.LogCategories ?? EngineLogCategory.None;
        _loggingEnabled = _logger.IsEnabled && _enabledLogCategories != EngineLogCategory.None;

        _moveGenerator = new ChessMoveGenerator(LogCastlingDecision);

        var gameStatusEvaluator = new ChessGameStatusEvaluator(
            (state, board) => _moveGenerator.GenerateLegalMovesForActiveSide(state, board),
            LogGameStatusDecision);
        _stateTransitionService = new ChessStateTransitionService(gameStatusEvaluator);
    }

    public GameState CreateInitialGameState()
    {
        return InitialPositionBuilder.CreateInitialState();
    }

    public IReadOnlyList<Move> GeneratePseudoLegalMoves(GameState gameState, Square fromSquare)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var board = ChessEngineBoard.BuildBoard(gameState);

        if (!board.TryGetValue(fromSquare, out var piece))
        {
            return [];
        }

        return _moveGenerator.GeneratePseudoLegalMovesForPiece(
            gameState,
            board,
            fromSquare,
            piece,
            logCastlingDecisions: true);
    }

    public IReadOnlyList<Move> GenerateLegalMoves(GameState gameState, Square fromSquare)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var board = ChessEngineBoard.BuildBoard(gameState);

        if (!board.TryGetValue(fromSquare, out var piece) || piece.Color != gameState.SideToMove)
        {
            return [];
        }

        return _moveGenerator.GenerateLegalMovesForPiece(
            gameState,
            board,
            fromSquare,
            piece,
            logCastlingDecisions: true);
    }

    public IReadOnlyList<Move> GenerateLegalMoves(GameState gameState)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var board = ChessEngineBoard.BuildBoard(gameState);
        return _moveGenerator.GenerateLegalMovesForActiveSide(gameState, board);
    }

    public bool IsMoveLegal(GameState gameState, Square fromSquare, Square toSquare, PieceType? promotionPieceType = null)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var board = ChessEngineBoard.BuildBoard(gameState);
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

        var board = ChessEngineBoard.BuildBoard(gameState);
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
        updatedGameState = _stateTransitionService.BuildNextGameState(gameState, legalMove);
        return true;
    }

    public bool IsKingInCheck(GameState gameState, PieceColor color)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var board = ChessEngineBoard.BuildBoard(gameState);
        return ChessAttackDetector.IsKingInCheck(board, color);
    }

    /// <summary>
    /// MoveLookupFailureReason is an enumeration within the Engine module.
    /// Its named values model a bounded set of states, options, or outcomes used by collaborators.
    /// Primary production consumers include ChessGameEngine (Engine).
    /// Its values are interpreted by the services and models listed in the Used by section.
    /// </summary>
    /// <remarks>
    /// <para><b>Used by:</b> ChessGameEngine (Engine)</para>
    /// <para><b>Usage pattern:</b> Callers invoke it during legal move generation, move application, attack evaluation, and game-status checks inside the engine pipeline.</para>
    /// <para><b>Dependencies/Collaborators:</b> Its values are interpreted by the services and models listed in the Used by section.</para>
    /// <para><b>Boundary:</b> This type sits in the rules engine boundary and participates in move evaluation or state transition logic.</para>
    /// </remarks>
    private enum MoveLookupFailureReason
    {
        None = 0,
        GameAlreadyEnded,
        SourceSquareIsEmpty,
        PieceBelongsToOpponent,
        DestinationNotLegal,
        PromotionSelectionInvalid
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

        var resolvedPromotionPieceType = ChessMoveApplication.ResolvePromotionPieceType(movingPiece, toSquare, promotionPieceType);
        var legalMoves = _moveGenerator.GenerateLegalMovesForPiece(
            gameState,
            board,
            fromSquare,
            movingPiece,
            logCastlingDecisions);
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
}
