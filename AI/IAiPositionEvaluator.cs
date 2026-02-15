using Chess.Domain;

namespace Chess.AI;

public interface IAiPositionEvaluator
{
    int Evaluate(GameState gameState, PieceColor perspectiveColor);
}
