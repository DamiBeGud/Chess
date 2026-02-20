using MultiplayerServer.Contracts.V1;

namespace MultiplayerServer.Tests;

public sealed class ServerRouteConventionsTests
{
    [Fact]
    public void Routes_AreVersionedAndAbsolute()
    {
        Assert.StartsWith("/", ServerRouteConventions.Health);
        Assert.StartsWith("/api/v1", ServerRouteConventions.ApiV1Prefix);
        Assert.StartsWith("/matches", ServerRouteConventions.ApiV1Matches);
        Assert.StartsWith("/matches", ServerRouteConventions.ApiV1MatchesJoin);
        Assert.StartsWith("/matches", ServerRouteConventions.ApiV1MatchesMoves);
        Assert.StartsWith("/matches", ServerRouteConventions.ApiV1MatchesSnapshot);
        Assert.StartsWith("/hubs/v1", ServerRouteConventions.MatchHubV1);
    }

    [Fact]
    public void Routes_UseExpectedValues()
    {
        Assert.Equal("/health", ServerRouteConventions.Health);
        Assert.Equal("/api/v1", ServerRouteConventions.ApiV1Prefix);
        Assert.Equal("/", ServerRouteConventions.ApiV1Root);
        Assert.Equal("/matches", ServerRouteConventions.ApiV1Matches);
        Assert.Equal("/matches/join", ServerRouteConventions.ApiV1MatchesJoin);
        Assert.Equal("/matches/moves", ServerRouteConventions.ApiV1MatchesMoves);
        Assert.Equal("/matches/snapshot", ServerRouteConventions.ApiV1MatchesSnapshot);
        Assert.Equal("/hubs/v1/matches", ServerRouteConventions.MatchHubV1);
    }
}
