using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;

namespace Chess.AppCore;

/// <summary>
/// IAiTurnService defines a contract within the Application module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include MainWindowViewModel (UI/ViewModels), MainWindowAiTurnCoordinator (UI/Services), AiTurnService (Application).
/// Key collaborators are Implementations include AiTurnService (Application), NoOpAiTurnService (UI/ViewModels).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindowViewModel (UI/ViewModels), MainWindowAiTurnCoordinator (UI/Services), AiTurnService (Application), NoOpAiTurnService (UI/ViewModels)</para>
/// <para><b>Usage pattern:</b> The UI layer triggers this service per user action, and the service delegates to engine/persistence collaborators to complete the operation.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include AiTurnService (Application), NoOpAiTurnService (UI/ViewModels).</para>
/// <para><b>Boundary:</b> This type sits in the application-service boundary and orchestrates domain operations for callers.</para>
/// </remarks>
public interface IAiTurnService
{
    bool IsAvailable => true;

    bool CanRequestMove(PieceColor aiColor);

    Task<Move?> TryPlayTurnAsync(PieceColor aiColor, int searchDepth, CancellationToken cancellationToken = default);
}
