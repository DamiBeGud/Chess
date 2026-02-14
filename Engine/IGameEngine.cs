using Chess.Domain;

namespace Chess.Engine;

public interface IGameEngine
{
    GameState CreateInitialGameState();
}
