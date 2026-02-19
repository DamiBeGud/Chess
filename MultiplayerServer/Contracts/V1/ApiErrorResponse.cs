namespace MultiplayerServer.Contracts.V1;

public sealed record ApiErrorResponse(
    string Code,
    string Message);
