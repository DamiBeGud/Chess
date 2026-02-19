namespace MultiplayerServer.Application.Matches;

public sealed class MatchState
{
    public required string MatchId { get; init; }
    public required string JoinCode { get; init; }
    public required string CreatorToken { get; init; }
    public string? JoinerToken { get; set; }
    public required char[] Board { get; set; }
    public required string SideToMove { get; set; }
    public required int MoveNumber { get; set; }
    public required bool WhiteCanCastleKingSide { get; set; }
    public required bool WhiteCanCastleQueenSide { get; set; }
    public required bool BlackCanCastleKingSide { get; set; }
    public required bool BlackCanCastleQueenSide { get; set; }
    public required BoardSquare? EnPassantTarget { get; set; }
}

public readonly record struct BoardSquare(int Row, int Col);
