namespace MultiplayerServer.Contracts.V1;

public sealed record ApiInfoResponse(string Service, string Version)
{
    public static ApiInfoResponse V1()
    {
        return new ApiInfoResponse("MultiplayerServer", "v1");
    }
}
