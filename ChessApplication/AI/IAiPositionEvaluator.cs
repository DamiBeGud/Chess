using Chess.Domain;

namespace Chess.AI;

/// <summary>
/// IAiPositionEvaluator defines a contract within the AI module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include NegamaxAiMoveSelector (AI), MaterialMobilityPositionEvaluator (AI).
/// Key collaborators are Implementations include MaterialMobilityPositionEvaluator (AI).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> NegamaxAiMoveSelector (AI), MaterialMobilityPositionEvaluator (AI)</para>
/// <para><b>Usage pattern:</b> Startup code constructs and wires this type during application initialization and desktop-lifetime setup.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include MaterialMobilityPositionEvaluator (AI).</para>
/// <para><b>Boundary:</b> This type sits in the application shell boundary and participates in startup or desktop lifetime wiring.</para>
/// </remarks>
public interface IAiPositionEvaluator
{
    int Evaluate(GameState gameState, PieceColor perspectiveColor);
}
