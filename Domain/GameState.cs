using System.Collections.Generic;

namespace Chess.Domain;

public sealed record GameState(
    IReadOnlyList<PiecePlacement> Pieces,
    PieceColor SideToMove,
    CastlingRights CastlingRights,
    Square? EnPassantTarget,
    int HalfmoveClock,
    int FullmoveNumber,
    GameStatus Status,
    IReadOnlyList<Move> MoveHistory,
    int SchemaVersion = 1);
