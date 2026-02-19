using System.Threading;
using Chess.Domain;

namespace Chess.AI;

public interface IAiMoveSelector
{
    Move? SelectBestMove(
        GameState gameState,
        PieceColor aiColor,
        int searchDepth,
        CancellationToken cancellationToken = default);
}
