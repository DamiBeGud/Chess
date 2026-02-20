using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;

namespace Chess.AppCore;

/// <summary>
/// IGameSessionService defines a contract within the Application module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include MainWindowViewModel (UI/ViewModels), AiTurnService (Application), MainWindowLocalPlayCoordinator (UI/Services).
/// Key collaborators are Implementations include GameSessionService (Application).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindowViewModel (UI/ViewModels), AiTurnService (Application), MainWindowLocalPlayCoordinator (UI/Services), MainWindowPersistenceCoordinator (UI/Services)</para>
/// <para><b>Usage pattern:</b> The UI layer triggers this service per user action, and the service delegates to engine/persistence collaborators to complete the operation.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include GameSessionService (Application).</para>
/// <para><b>Boundary:</b> This type sits in the application-service boundary and orchestrates domain operations for callers.</para>
/// </remarks>
public interface IGameSessionService
{
    GameState StartNewGame();
    IReadOnlyList<Move> GetLegalMovesFrom(Square fromSquare);
    bool TryMakeMove(Square fromSquare, Square toSquare, PieceType? promotionPieceType = null);
    Task SaveAsync(string filePath, CancellationToken cancellationToken = default);
    Task<GameState> LoadAsync(string filePath, CancellationToken cancellationToken = default);
    GameState CurrentGameState { get; }
}
