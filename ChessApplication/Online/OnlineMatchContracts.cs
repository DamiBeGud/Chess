using System;
using System.Collections.Generic;
using Chess.Domain;

namespace Chess.Online;

public sealed record OnlineCreateMatchResponse(
    string MatchId,
    string JoinCode,
    string CreatorToken);

public sealed record OnlineJoinMatchRequest(string? JoinCode);

public sealed record OnlineJoinMatchResponse(
    string MatchId,
    string Seat,
    string PlayerToken);

public sealed record OnlineSubmitMoveRequest(
    string? MatchId,
    string? PlayerToken,
    string? From,
    string? To,
    string? Promotion = null);

public sealed record OnlineSubmitMoveResponse(
    bool Accepted,
    OnlineMatchSnapshot Snapshot);

public sealed record OnlineSnapshotRequest(
    string? MatchId,
    string? PlayerToken);

public sealed record OnlineApiErrorResponse(
    string Code,
    string Message);

public sealed record OnlineMatchSnapshot(
    string MatchId,
    string SideToMove,
    int MoveNumber,
    IReadOnlyList<string> Board,
    string Status = OnlineMatchProtocolConstants.MatchStatusInProgress,
    string? Resolution = null,
    string? WinnerSeat = null,
    OnlineMatchPresence? Presence = null);

public sealed record OnlineMatchPresence(
    OnlineSeatPresence Creator,
    OnlineSeatPresence Joiner);

public sealed record OnlineSeatPresence(
    string Seat,
    bool IsConnected,
    bool IsReserved,
    DateTimeOffset? DisconnectedUtc,
    DateTimeOffset? GraceExpiresUtc);

public sealed record OnlineMatchEventMetadata(
    string MatchId,
    string EventId,
    long Sequence,
    DateTimeOffset OccurredUtc);

public sealed record OnlineMatchSnapshotSyncEvent(
    string EventType,
    OnlineMatchEventMetadata Metadata,
    OnlineMatchSnapshot Snapshot);

public sealed record OnlineMatchUpdatedSyncEvent(
    string EventType,
    OnlineMatchEventMetadata Metadata,
    OnlineMatchSnapshot Snapshot);

public sealed record OnlineMatchPresenceChangedSyncEvent(
    string EventType,
    OnlineMatchEventMetadata Metadata,
    OnlineMatchSnapshot Snapshot,
    string Seat);

public sealed record OnlineMatchEndedSyncEvent(
    string EventType,
    OnlineMatchEventMetadata Metadata,
    OnlineMatchSnapshot Snapshot);

public sealed record OnlineMatchErrorSyncEvent(
    string EventType,
    OnlineMatchEventMetadata Metadata,
    string Code,
    string Message);

public sealed record OnlineMatchCredentials(
    string MatchId,
    string PlayerToken,
    PieceColor Seat);

public sealed record OnlineCreatedMatch(
    string MatchId,
    string JoinCode,
    PieceColor Seat);

public sealed record OnlineJoinedMatch(
    string MatchId,
    PieceColor Seat);

public sealed record OnlineResumedMatch(
    string MatchId,
    PieceColor Seat);
