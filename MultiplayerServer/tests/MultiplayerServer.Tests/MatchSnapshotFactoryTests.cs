using MultiplayerServer.Application.Matches;

namespace MultiplayerServer.Tests;

public sealed class MatchSnapshotFactoryTests
{
    [Fact]
    public void Create_MapsStateIntoSnapshotContractShape()
    {
        var factory = new MatchSnapshotFactory();
        var match = new MatchState
        {
            MatchId = "match-1",
            JoinCode = "ABC123",
            CreatorToken = "creator",
            JoinerToken = "joiner",
            Board = BoardFromRows(
                "rnbqkbnr",
                "pppppppp",
                "........",
                "...P....",
                "........",
                "........",
                "PPPP.PPP",
                "RNBQKBNR"),
            SideToMove = MatchSeats.Joiner,
            MoveNumber = 2,
            WhiteCanCastleKingSide = true,
            WhiteCanCastleQueenSide = true,
            BlackCanCastleKingSide = true,
            BlackCanCastleQueenSide = true,
            EnPassantTarget = null
        };

        var snapshot = factory.Create(match);

        Assert.Equal("match-1", snapshot.MatchId);
        Assert.Equal(MatchSeats.Joiner, snapshot.SideToMove);
        Assert.Equal(2, snapshot.MoveNumber);
        Assert.Equal(8, snapshot.Board.Count);
        Assert.Equal("...P....", snapshot.Board[3]);
    }

    private static char[] BoardFromRows(params string[] rows)
    {
        var board = new char[64];
        for (var row = 0; row < 8; row++)
        {
            for (var col = 0; col < 8; col++)
            {
                board[(row * 8) + col] = rows[row][col];
            }
        }

        return board;
    }
}
