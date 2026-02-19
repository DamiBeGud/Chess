using MultiplayerServer.Application.Matches;

namespace MultiplayerServer.Tests;

public sealed class InMemoryMatchLifecycleServiceGeneratorTests
{
    [Fact]
    public void CreateMatch_UsesInjectedGeneratorsAndSkipsDuplicateJoinCode()
    {
        var repository = new InMemoryMatchRepository();
        repository.Add(
            new MatchState
            {
                MatchId = "existing",
                JoinCode = "DUPLIC",
                CreatorToken = "token-existing",
                JoinerToken = null,
                Board = EmptyBoard(),
                SideToMove = MatchSeats.Creator,
                MoveNumber = 1,
                WhiteCanCastleKingSide = true,
                WhiteCanCastleQueenSide = true,
                BlackCanCastleKingSide = true,
                BlackCanCastleQueenSide = true,
                EnPassantTarget = null
            });

        var service = new InMemoryMatchLifecycleService(
            repository,
            new StubMatchIdGenerator("match-fixed"),
            new SequenceJoinCodeGenerator("DUPLIC", "UNIQUE"),
            new SequenceTokenGenerator("creator-token", "joiner-token"),
            new ClassicChessRulesEngine(),
            new MatchSnapshotFactory());

        var created = service.CreateMatch();
        var joined = service.JoinMatch(created.JoinCode);

        Assert.Equal("match-fixed", created.MatchId);
        Assert.Equal("UNIQUE", created.JoinCode);
        Assert.Equal("creator-token", created.CreatorToken);

        var joinSuccess = Assert.IsType<JoinMatchSucceeded>(joined);
        Assert.Equal("joiner-token", joinSuccess.Response.PlayerToken);
    }

    private static char[] EmptyBoard()
    {
        return Enumerable.Repeat('.', 64).ToArray();
    }

    private sealed class StubMatchIdGenerator(string value) : IMatchIdGenerator
    {
        public string Generate() => value;
    }

    private sealed class SequenceJoinCodeGenerator(params string[] values) : IJoinCodeGenerator
    {
        private readonly Queue<string> _values = new(values);

        public string Generate()
        {
            return _values.Dequeue();
        }
    }

    private sealed class SequenceTokenGenerator(params string[] values) : IPlayerTokenGenerator
    {
        private readonly Queue<string> _values = new(values);

        public string Generate()
        {
            return _values.Dequeue();
        }
    }
}
