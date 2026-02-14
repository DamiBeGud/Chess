using System.Collections.Generic;
using Chess.Domain;

namespace Chess.Engine;

public interface IGameEngine
{
    GameState CreateInitialGameState();
    IReadOnlyList<Move> GeneratePseudoLegalMoves(GameState gameState, Square fromSquare);
    IReadOnlyList<Move> GenerateLegalMoves(GameState gameState, Square fromSquare);
    IReadOnlyList<Move> GenerateLegalMoves(GameState gameState);
    bool IsMoveLegal(GameState gameState, Square fromSquare, Square toSquare, PieceType? promotionPieceType = null);
    bool IsKingInCheck(GameState gameState, PieceColor color);
}
