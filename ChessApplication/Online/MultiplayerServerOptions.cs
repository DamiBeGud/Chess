using System;

namespace Chess.Online;

public sealed class MultiplayerServerOptions
{
    public const string EndpointEnvironmentVariableName = "CHESS_MULTIPLAYER_SERVER_URL";
    private const string DefaultEndpoint = "http://localhost:8080";

    public MultiplayerServerOptions(Uri baseUri)
    {
        BaseUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
    }

    public Uri BaseUri { get; }

    public static MultiplayerServerOptions FromEnvironment()
    {
        var rawValue = Environment.GetEnvironmentVariable(EndpointEnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(rawValue) &&
            Uri.TryCreate(rawValue.Trim(), UriKind.Absolute, out var parsed))
        {
            return new MultiplayerServerOptions(parsed);
        }

        return new MultiplayerServerOptions(new Uri(DefaultEndpoint, UriKind.Absolute));
    }
}
