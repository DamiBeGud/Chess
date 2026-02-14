namespace Chess.Domain;

public sealed record Piece(PieceType Type, PieceColor Color, bool HasMoved = false);
