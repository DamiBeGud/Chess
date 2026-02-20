using System.Collections.Generic;
using Chess.Domain;

namespace Chess.Engine;

/// <summary>
/// IGameEngine defines a contract within the Engine module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include GameSessionService (Application), MaterialMobilityPositionEvaluator (AI), NegamaxAiMoveSelector (AI).
/// Key collaborators are Implementations include ChessGameEngine (Engine).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> GameSessionService (Application), MaterialMobilityPositionEvaluator (AI), NegamaxAiMoveSelector (AI), ChessGameEngine (Engine)</para>
/// <para><b>Usage pattern:</b> Callers invoke it during legal move generation, move application, attack evaluation, and game-status checks inside the engine pipeline.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include ChessGameEngine (Engine).</para>
/// <para><b>Boundary:</b> This type sits in the rules engine boundary and participates in move evaluation or state transition logic.</para>
/// </remarks>
public interface IGameEngine
{
    GameState CreateInitialGameState();
    IReadOnlyList<Move> GeneratePseudoLegalMoves(GameState gameState, Square fromSquare);
    IReadOnlyList<Move> GenerateLegalMoves(GameState gameState, Square fromSquare);
    IReadOnlyList<Move> GenerateLegalMoves(GameState gameState);
    bool IsMoveLegal(GameState gameState, Square fromSquare, Square toSquare, PieceType? promotionPieceType = null);
    bool TryApplyMove(
        GameState gameState,
        Square fromSquare,
        Square toSquare,
        out GameState updatedGameState,
        PieceType? promotionPieceType = null);
    bool IsKingInCheck(GameState gameState, PieceColor color);
}
