using MultiplayerServer.Application.Matches;
using MultiplayerServer.Contracts.V1;

namespace MultiplayerServer.Transport.V1;

internal static class MatchContractMapper
{
    public static MatchSnapshotResponse ToContractSnapshot(MatchSnapshot snapshot)
    {
        return new MatchSnapshotResponse(
            snapshot.MatchId,
            snapshot.SideToMove,
            snapshot.MoveNumber,
            snapshot.Board);
    }
}
