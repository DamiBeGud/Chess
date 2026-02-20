using System;
using System.Threading;
using System.Threading.Tasks;
using Chess.AI;
using Chess.Domain;

namespace Chess.AppCore;

/// <summary>
/// AiTurnService is a concrete type within the Application module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include App (AppShell).
/// Key collaborators are IGameSessionService, IAiMoveSelector, IAiTurnService.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> App (AppShell)</para>
/// <para><b>Usage pattern:</b> The UI layer triggers this service per user action, and the service delegates to engine/persistence collaborators to complete the operation.</para>
/// <para><b>Dependencies/Collaborators:</b> IGameSessionService, IAiMoveSelector, IAiTurnService.</para>
/// <para><b>Boundary:</b> This type sits in the application-service boundary and orchestrates domain operations for callers.</para>
/// </remarks>
public sealed class AiTurnService : IAiTurnService
{
    private readonly IGameSessionService _gameSessionService;
    private readonly IAiMoveSelector _aiMoveSelector;

    public AiTurnService(IGameSessionService gameSessionService, IAiMoveSelector aiMoveSelector)
    {
        _gameSessionService = gameSessionService ?? throw new ArgumentNullException(nameof(gameSessionService));
        _aiMoveSelector = aiMoveSelector ?? throw new ArgumentNullException(nameof(aiMoveSelector));
    }

    public bool CanRequestMove(PieceColor aiColor)
    {
        var state = _gameSessionService.CurrentGameState;
        return state.Status == GameStatus.InProgress && state.SideToMove == aiColor;
    }

    public async Task<Move?> TryPlayTurnAsync(
        PieceColor aiColor,
        int searchDepth,
        CancellationToken cancellationToken = default)
    {
        var snapshot = _gameSessionService.CurrentGameState;
        if (snapshot.Status != GameStatus.InProgress || snapshot.SideToMove != aiColor)
        {
            return null;
        }

        var selectedMove = await Task.Run(
            () => _aiMoveSelector.SelectBestMove(snapshot, aiColor, searchDepth, cancellationToken),
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (selectedMove is null)
        {
            return null;
        }

        if (!ReferenceEquals(snapshot, _gameSessionService.CurrentGameState))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!_gameSessionService.TryMakeMove(selectedMove.From, selectedMove.To, selectedMove.PromotionPieceType))
        {
            return null;
        }

        return selectedMove;
    }
}
