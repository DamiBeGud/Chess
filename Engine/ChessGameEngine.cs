using Chess.Domain;

namespace Chess.Engine;

public sealed class ChessGameEngine : IGameEngine
{
    public GameState CreateInitialGameState()
    {
        return InitialPositionBuilder.CreateInitialState();
    }
}
