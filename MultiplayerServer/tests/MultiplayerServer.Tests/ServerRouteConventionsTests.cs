using MultiplayerServer.Contracts.V1;

namespace MultiplayerServer.Tests;

public sealed class ServerRouteConventionsTests
{
    [Fact]
    public void Routes_AreVersionedAndAbsolute()
    {
        Assert.StartsWith("/", ServerRouteConventions.Health);
        Assert.StartsWith("/api/v1", ServerRouteConventions.ApiV1Prefix);
        Assert.StartsWith("/hubs/v1", ServerRouteConventions.MatchHubV1);
    }

    [Fact]
    public void Routes_UseExpectedValues()
    {
        Assert.Equal("/health", ServerRouteConventions.Health);
        Assert.Equal("/api/v1", ServerRouteConventions.ApiV1Prefix);
        Assert.Equal("/", ServerRouteConventions.ApiV1Root);
        Assert.Equal("/hubs/v1/matches", ServerRouteConventions.MatchHubV1);
    }
}
