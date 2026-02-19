using MultiplayerServer.Application.Matches;

namespace MultiplayerServer.Tests;

public sealed class InMemoryMatchRepositoryTests
{
    [Fact]
    public void Add_StoresMatchRetrievableByIdAndJoinCode()
    {
        var repository = new InMemoryMatchRepository();
        var match = CreateMatchState(matchId: "m1", joinCode: "ABC123");

        repository.Add(match);

        var byId = repository.WithMatchById("m1", value => value);
        var byJoinCode = repository.WithMatchByJoinCode("ABC123", value => value);

        Assert.Same(match, byId);
        Assert.Same(match, byJoinCode);
    }

    [Fact]
    public void UnknownMatch_ReturnsNullFromLookupCallbacks()
    {
        var repository = new InMemoryMatchRepository();

        var byId = repository.WithMatchById("missing", value => value);
        var byJoinCode = repository.WithMatchByJoinCode("missing", value => value);

        Assert.Null(byId);
        Assert.Null(byJoinCode);
        Assert.False(repository.JoinCodeExists("missing"));
    }

    private static MatchState CreateMatchState(string matchId, string joinCode)
    {
        return new MatchState
        {
            MatchId = matchId,
            JoinCode = joinCode,
            CreatorToken = "creator",
            JoinerToken = null,
            Board = EmptyBoard(),
            SideToMove = MatchSeats.Creator,
            MoveNumber = 1,
            WhiteCanCastleKingSide = true,
            WhiteCanCastleQueenSide = true,
            BlackCanCastleKingSide = true,
            BlackCanCastleQueenSide = true,
            EnPassantTarget = null
        };
    }

    private static char[] EmptyBoard()
    {
        return Enumerable.Repeat('.', 64).ToArray();
    }
}
