namespace MultiplayerServer.Contracts.V1;

public sealed record MatchSnapshotResponse(
    string MatchId,
    string SideToMove,
    int MoveNumber,
    IReadOnlyList<string> Board,
    string Status = MatchProtocolConstants.MatchStatusInProgress,
    string? Resolution = null,
    string? WinnerSeat = null,
    MatchPresenceResponse? Presence = null);

public sealed record MatchPresenceResponse(
    MatchSeatPresenceResponse Creator,
    MatchSeatPresenceResponse Joiner)
{
    public static readonly MatchPresenceResponse Empty = new(
        new MatchSeatPresenceResponse(
            MatchProtocolConstants.CreatorSeat,
            false,
            true,
            null,
            null),
        new MatchSeatPresenceResponse(
            MatchProtocolConstants.JoinerSeat,
            false,
            false,
            null,
            null));
}

public sealed record MatchSeatPresenceResponse(
    string Seat,
    bool IsConnected,
    bool IsReserved,
    DateTimeOffset? DisconnectedUtc,
    DateTimeOffset? GraceExpiresUtc);
