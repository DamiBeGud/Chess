using System.Diagnostics;
using MultiplayerServer.Application.Matches;
using MultiplayerServer.Contracts.V1;
using MultiplayerServer.Hubs.V1;
using MultiplayerServer.Transport.V1;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.SetMinimumLevel(LogLevel.Information);

builder.Services.AddSignalR();
builder.Services.AddSingleton<InMemoryMatchLifecycleService>();
builder.Services.AddSingleton<ICreateMatchUseCase>(sp => sp.GetRequiredService<InMemoryMatchLifecycleService>());
builder.Services.AddSingleton<IJoinMatchUseCase>(sp => sp.GetRequiredService<InMemoryMatchLifecycleService>());
builder.Services.AddSingleton<ISubmitMoveUseCase>(sp => sp.GetRequiredService<InMemoryMatchLifecycleService>());
builder.Services.AddSingleton<IMatchLifecycleService>(sp => sp.GetRequiredService<InMemoryMatchLifecycleService>());
builder.Services.AddSingleton<IMatchErrorHttpMapper, V1MatchErrorHttpMapper>();

var app = builder.Build();
var logger = app.Logger;

logger.LogInformation(
    "MultiplayerServer starting in {Environment} mode",
    app.Environment.EnvironmentName);

app.Use(async (context, next) =>
{
    var stopwatch = Stopwatch.StartNew();
    var method = context.Request.Method;
    var path = context.Request.Path.Value ?? "/";
    var traceId = context.TraceIdentifier;
    var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var requestFailed = false;

    logger.LogInformation(
        "HTTP request started {Method} {Path} from {RemoteIp} trace {TraceId}",
        method,
        path,
        remoteIp,
        traceId);

    try
    {
        await next();
    }
    catch (Exception ex)
    {
        requestFailed = true;
        var effectiveStatusCode = ResolveStatusCode(context, requestFailed);

        logger.LogError(
            ex,
            "HTTP request failed {Method} {Path} with {StatusCode} trace {TraceId}",
            method,
            path,
            effectiveStatusCode,
            traceId);
        throw;
    }
    finally
    {
        var effectiveStatusCode = ResolveStatusCode(context, requestFailed);

        stopwatch.Stop();
        logger.LogInformation(
            "HTTP request completed {Method} {Path} with {StatusCode} in {ElapsedMs}ms trace {TraceId}",
            method,
            path,
            effectiveStatusCode,
            stopwatch.Elapsed.TotalMilliseconds,
            traceId);
    }
});

app.MapGet(ServerRouteConventions.Health, () => Results.Ok(HealthResponse.OkNow()));

var apiV1 = app.MapGroup(ServerRouteConventions.ApiV1Prefix);
apiV1.MapGet(ServerRouteConventions.ApiV1Root, () => Results.Ok(ApiInfoResponse.V1()));
apiV1.MapMatchLifecycleEndpoints();

app.MapHub<MatchHub>(ServerRouteConventions.MatchHubV1);

app.Run();

static int ResolveStatusCode(HttpContext context, bool requestFailed)
{
    var statusCode = context.Response.StatusCode;
    return requestFailed && !context.Response.HasStarted && statusCode < StatusCodes.Status400BadRequest
        ? StatusCodes.Status500InternalServerError
        : statusCode;
}

public partial class Program
{
}
