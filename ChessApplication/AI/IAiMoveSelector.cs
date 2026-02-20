using System.Threading;
using Chess.Domain;

namespace Chess.AI;

/// <summary>
/// IAiMoveSelector defines a contract within the AI module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include AiTurnService (Application), NegamaxAiMoveSelector (AI).
/// Key collaborators are Implementations include NegamaxAiMoveSelector (AI).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> AiTurnService (Application), NegamaxAiMoveSelector (AI)</para>
/// <para><b>Usage pattern:</b> Startup code constructs and wires this type during application initialization and desktop-lifetime setup.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include NegamaxAiMoveSelector (AI).</para>
/// <para><b>Boundary:</b> This type sits in the application shell boundary and participates in startup or desktop lifetime wiring.</para>
/// </remarks>
public interface IAiMoveSelector
{
    Move? SelectBestMove(
        GameState gameState,
        PieceColor aiColor,
        int searchDepth,
        CancellationToken cancellationToken = default);
}
