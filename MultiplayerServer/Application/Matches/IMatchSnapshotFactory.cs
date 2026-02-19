namespace MultiplayerServer.Application.Matches;

public interface IMatchSnapshotFactory
{
    MatchSnapshot Create(MatchState matchState);
}
