using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;

namespace Chess.Persistence;

public interface IGameStateStore
{
    Task SaveAsync(string filePath, GameState gameState, CancellationToken cancellationToken = default);
    Task<GameState> LoadAsync(string filePath, CancellationToken cancellationToken = default);
}
