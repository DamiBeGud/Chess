namespace MultiplayerServer.Application.Matches;

public enum MatchAbandonmentResolutionMode
{
    Forfeit,
    Draw
}

public sealed class MatchDisconnectPolicyOptions
{
    public const string SectionName = "MatchDisconnectPolicy";

    // Recommended production range: 30-120 seconds.
    public int DisconnectGracePeriodSeconds { get; set; } = 60;
    public MatchAbandonmentResolutionMode AbandonmentResolution { get; set; } = MatchAbandonmentResolutionMode.Forfeit;
    public bool RequireBothPlayersConnectedToStart { get; set; }
}
