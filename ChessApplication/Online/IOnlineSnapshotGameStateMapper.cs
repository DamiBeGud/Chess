using Chess.Domain;

namespace Chess.Online;

public interface IOnlineSnapshotGameStateMapper
{
    GameState Map(OnlineMatchSnapshot snapshot);
}
