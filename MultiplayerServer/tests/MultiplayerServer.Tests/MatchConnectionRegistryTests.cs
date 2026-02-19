using MultiplayerServer.Hubs.V1;

namespace MultiplayerServer.Tests;

public sealed class MatchConnectionRegistryTests
{
    [Fact]
    public void AddSubscription_TracksConnectionMembership()
    {
        var registry = new InMemoryMatchConnectionRegistry();

        registry.AddSubscription("conn-1", "match-1");

        Assert.True(registry.IsSubscribed("conn-1", "match-1"));
    }

    [Fact]
    public void RemoveSubscription_UnknownConnection_ReturnsFalse()
    {
        var registry = new InMemoryMatchConnectionRegistry();

        var removed = registry.RemoveSubscription("missing", "match-1");

        Assert.False(removed);
    }

    [Fact]
    public void RemoveConnection_ReturnsAndClearsTrackedMatches()
    {
        var registry = new InMemoryMatchConnectionRegistry();
        registry.AddSubscription("conn-1", "match-1");
        registry.AddSubscription("conn-1", "match-2");

        var removedMatches = registry.RemoveConnection("conn-1");

        Assert.Equal(2, removedMatches.Count);
        Assert.Contains("match-1", removedMatches);
        Assert.Contains("match-2", removedMatches);
        Assert.False(registry.IsSubscribed("conn-1", "match-1"));
        Assert.False(registry.IsSubscribed("conn-1", "match-2"));
    }
}
