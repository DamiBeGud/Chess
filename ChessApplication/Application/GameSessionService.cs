using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Chess.Domain;
using Chess.Engine;
using Chess.Persistence;

namespace Chess.AppCore;

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
