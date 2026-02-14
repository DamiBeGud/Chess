using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;

namespace Chess.AppCore;

public interface IGameSessionService
{
    GameState StartNewGame();
    bool TryMakeMove(Square fromSquare, Square toSquare, PieceType? promotionPieceType = null);
    Task SaveAsync(string filePath, CancellationToken cancellationToken = default);
    Task<GameState> LoadAsync(string filePath, CancellationToken cancellationToken = default);
    GameState CurrentGameState { get; }
}
