namespace MultiplayerServer.Hubs.V1;

public sealed class GuidMatchSyncEventIdGenerator : IMatchSyncEventIdGenerator
{
    public string Generate()
    {
        return Guid.NewGuid().ToString("N");
    }
}
