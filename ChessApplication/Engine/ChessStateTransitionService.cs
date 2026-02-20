using System.Collections.Generic;
using System.Linq;
using Chess.Domain;

namespace Chess.Engine;

/// <summary>
/// ChessStateTransitionService is a concrete type within the Engine module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include ChessGameEngine (Engine).
/// Key collaborators are ChessGameStatusEvaluator.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> ChessGameEngine (Engine)</para>
/// <para><b>Usage pattern:</b> Callers invoke it during legal move generation, move application, attack evaluation, and game-status checks inside the engine pipeline.</para>
/// <para><b>Dependencies/Collaborators:</b> ChessGameStatusEvaluator.</para>
/// <para><b>Boundary:</b> This type sits in the rules engine boundary and participates in move evaluation or state transition logic.</para>
/// </remarks>
internal sealed class ChessStateTransitionService
{
    private readonly ChessGameStatusEvaluator _statusEvaluator;

    internal ChessStateTransitionService(ChessGameStatusEvaluator statusEvaluator)
    {
        _statusEvaluator = statusEvaluator;
    }

    internal GameState BuildNextGameState(GameState currentState, Move legalMove)
    {
        var board = ChessEngineBoard.BuildBoard(currentState);
        var boardAfterMove = ChessMoveApplication.ApplyMoveOnBoard(board, legalMove);
        var movedPieceAfterMove = ChessMoveApplication.BuildMovedPieceAfterMove(legalMove);

        var moveHistory = currentState.MoveHistory.ToList();
        moveHistory.Add(legalMove with { MovedPiece = movedPieceAfterMove });

        var halfmoveClock = legalMove.CapturedPiece is not null || legalMove.MovedPiece.Type == PieceType.Pawn
            ? 0
            : currentState.HalfmoveClock + 1;

        var fullmoveNumber = currentState.SideToMove == PieceColor.Black
            ? currentState.FullmoveNumber + 1
            : currentState.FullmoveNumber;

        var updatedCastlingRights = ChessMoveApplication.UpdateCastlingRights(currentState.CastlingRights, legalMove);
        var enPassantTarget = ChessMoveApplication.DetermineEnPassantTarget(legalMove);
        var nextSideToMove = ChessEngineBoard.GetOpponentColor(currentState.SideToMove);

        var nextState = currentState with
        {
            Pieces = ChessEngineBoard.ToPlacements(boardAfterMove),
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
            positionHistory.Add(ChessGameStatusEvaluator.BuildPositionSignature(currentState));
        }

        var nextPositionSignature = ChessGameStatusEvaluator.BuildPositionSignature(nextState);
        positionHistory.Add(nextPositionSignature);
        var nextStatus = _statusEvaluator.DetermineGameStatus(nextState, boardAfterMove, nextPositionSignature, positionHistory);

        return nextState with
        {
            Status = nextStatus,
            PositionHistory = positionHistory
        };
    }
}
