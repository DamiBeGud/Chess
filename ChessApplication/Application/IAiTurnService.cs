using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;

namespace Chess.AppCore;

public interface IAiTurnService
{
    bool IsAvailable => true;

    bool CanRequestMove(PieceColor aiColor);

    Task<Move?> TryPlayTurnAsync(PieceColor aiColor, int searchDepth, CancellationToken cancellationToken = default);
}
