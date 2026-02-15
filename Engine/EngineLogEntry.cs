using Chess.Domain;

namespace Chess.Engine;

public readonly record struct EngineLogEntry(
    EngineLogCategory Category,
    string EventName,
    string Message,
    Square? From = null,
    Square? To = null,
    PieceColor? SideToMove = null,
    PieceType? PieceType = null,
    PieceType? PromotionPieceType = null,
    GameStatus? GameStatus = null);
