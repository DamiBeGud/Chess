namespace Chess.Domain;

public sealed record Move(
    Square From,
    Square To,
    Piece MovedPiece,
    Piece? CapturedPiece = null,
    bool IsCastling = false,
    bool IsEnPassant = false,
    PieceType? PromotionPieceType = null);
