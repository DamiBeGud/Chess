using MultiplayerServer.Hubs.V1;

namespace MultiplayerServer.Tests;

public sealed class MatchConnectionRegistryTests
{
    [Fact]
    public void AddSubscription_TracksConnectionMembership()
    {
        var registry = new InMemoryMatchConnectionRegistry();

        registry.AddSubscription("conn-1", "match-1", "token-1");

        Assert.True(registry.IsSubscribed("conn-1", "match-1"));
    }

    [Fact]
    public void RemoveSubscription_UnknownConnection_ReturnsFalse()
    {
        var registry = new InMemoryMatchConnectionRegistry();

        var removed = registry.RemoveSubscription("missing", "match-1");

        Assert.Null(removed);
    }

    [Fact]
    public void RemoveConnection_ReturnsAndClearsTrackedMatches()
    {
        var registry = new InMemoryMatchConnectionRegistry();
        registry.AddSubscription("conn-1", "match-1", "token-1");
        registry.AddSubscription("conn-1", "match-2", "token-2");

        var removedMatches = registry.RemoveConnection("conn-1");

        Assert.Equal(2, removedMatches.Count);
        Assert.Contains(removedMatches, subscription => subscription.MatchId == "match-1" && subscription.PlayerToken == "token-1");
        Assert.Contains(removedMatches, subscription => subscription.MatchId == "match-2" && subscription.PlayerToken == "token-2");
        Assert.False(registry.IsSubscribed("conn-1", "match-1"));
        Assert.False(registry.IsSubscribed("conn-1", "match-2"));
    }
}
