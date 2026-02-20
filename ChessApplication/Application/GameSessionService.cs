using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;
using Chess.Engine;
using Chess.Persistence;

namespace Chess.AppCore;

/// <summary>
/// GameSessionService is a concrete type within the Application module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include App (AppShell).
/// Key collaborators are IGameEngine, IGameStateStore, IGameSessionService.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> App (AppShell)</para>
/// <para><b>Usage pattern:</b> The UI layer triggers this service per user action, and the service delegates to engine/persistence collaborators to complete the operation.</para>
/// <para><b>Dependencies/Collaborators:</b> IGameEngine, IGameStateStore, IGameSessionService.</para>
/// <para><b>Boundary:</b> This type sits in the application-service boundary and orchestrates domain operations for callers.</para>
/// </remarks>
public sealed class GameSessionService : IGameSessionService
{
    private readonly IGameEngine _gameEngine;
    private readonly IGameStateStore _gameStateStore;
    private GameState _currentGameState;

    public GameSessionService(IGameEngine gameEngine, IGameStateStore gameStateStore)
    {
        _gameEngine = gameEngine;
        _gameStateStore = gameStateStore;
        _currentGameState = _gameEngine.CreateInitialGameState();
    }

    public GameState CurrentGameState => _currentGameState;

    public GameState StartNewGame()
    {
        _currentGameState = _gameEngine.CreateInitialGameState();
        return _currentGameState;
    }

    public IReadOnlyList<Move> GetLegalMovesFrom(Square fromSquare)
    {
        return _gameEngine.GenerateLegalMoves(_currentGameState, fromSquare);
    }

    public bool TryMakeMove(Square fromSquare, Square toSquare, PieceType? promotionPieceType = null)
    {
        if (!_gameEngine.TryApplyMove(_currentGameState, fromSquare, toSquare, out var updatedGameState, promotionPieceType))
        {
            return false;
        }

        _currentGameState = updatedGameState;
        return true;
    }

    public async Task SaveAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        await _gameStateStore.SaveAsync(filePath, _currentGameState, cancellationToken);
    }

    public async Task<GameState> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path is required.", nameof(filePath));
        }

        _currentGameState = await _gameStateStore.LoadAsync(filePath, cancellationToken);
        return _currentGameState;
    }
}
