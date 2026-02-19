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
            snapshot.Board,
            snapshot.Status,
            snapshot.Resolution,
            snapshot.WinnerSeat,
            new MatchPresenceResponse(
                new MatchSeatPresenceResponse(
                    snapshot.Presence.Creator.Seat,
                    snapshot.Presence.Creator.IsConnected,
                    snapshot.Presence.Creator.IsReserved,
                    snapshot.Presence.Creator.DisconnectedUtc,
                    snapshot.Presence.Creator.GraceExpiresUtc),
                new MatchSeatPresenceResponse(
                    snapshot.Presence.Joiner.Seat,
                    snapshot.Presence.Joiner.IsConnected,
                    snapshot.Presence.Joiner.IsReserved,
                    snapshot.Presence.Joiner.DisconnectedUtc,
                    snapshot.Presence.Joiner.GraceExpiresUtc)));
    }
}
