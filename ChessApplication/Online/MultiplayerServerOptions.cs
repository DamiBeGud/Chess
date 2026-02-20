using System;

namespace Chess.Online;

/// <summary>
/// MultiplayerServerOptions is a concrete type within the Online module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include App (AppShell).
/// Key collaborators are Uri.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> App (AppShell)</para>
/// <para><b>Usage pattern:</b> Online coordinators and services call it along create/join/resume/submit/resync flows and realtime callback handling.</para>
/// <para><b>Dependencies/Collaborators:</b> Uri.</para>
/// <para><b>Boundary:</b> This type sits in the online multiplayer boundary and supports transport, session, or realtime synchronization flows.</para>
/// </remarks>
public sealed class MultiplayerServerOptions
{
    public const string EndpointEnvironmentVariableName = "CHESS_MULTIPLAYER_SERVER_URL";
    private const string DefaultEndpoint = "http://134.149.184.47:8080";

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
