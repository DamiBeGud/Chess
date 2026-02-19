namespace MultiplayerServer.Contracts.V1;

public sealed record HealthResponse(string Status, DateTimeOffset UtcTime)
{
    public static HealthResponse OkNow()
    {
        return new HealthResponse("ok", DateTimeOffset.UtcNow);
    }
}
