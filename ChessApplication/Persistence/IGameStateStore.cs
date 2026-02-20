using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;

namespace Chess.Persistence;

/// <summary>
/// IGameStateStore defines a contract within the Persistence module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include GameSessionService (Application), JsonGameStateStore (Persistence).
/// Key collaborators are Implementations include JsonGameStateStore (Persistence).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> GameSessionService (Application), JsonGameStateStore (Persistence)</para>
/// <para><b>Usage pattern:</b> Application services call it on save/load paths to convert between persisted payloads and runtime game state.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include JsonGameStateStore (Persistence).</para>
/// <para><b>Boundary:</b> This type sits in the persistence boundary and handles serialization or storage-oriented contracts.</para>
/// </remarks>
public interface IGameStateStore
{
    Task SaveAsync(string filePath, GameState gameState, CancellationToken cancellationToken = default);
    Task<GameState> LoadAsync(string filePath, CancellationToken cancellationToken = default);
}
