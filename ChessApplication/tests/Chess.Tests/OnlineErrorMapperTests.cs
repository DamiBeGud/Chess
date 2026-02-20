using Chess.Online;
using Xunit;

namespace Chess.Tests;

public sealed class OnlineErrorMapperTests
{
    private readonly OnlineErrorMapper _mapper = new();

    [Fact]
    public void Map_InvalidPlayerToken_ReturnsReconnectAction()
    {
        var mapped = _mapper.Map(
            new OnlineTransportError(
                OnlineMatchProtocolConstants.ErrorInvalidPlayerToken,
                "Invalid player token.",
                StatusCode: 403));

        Assert.Equal(OnlineMatchProtocolConstants.ErrorInvalidPlayerToken, mapped.Code);
        Assert.Equal(OnlineUserAction.Reconnect, mapped.RecommendedAction);
        Assert.False(mapped.IsTerminal);
    }

    [Fact]
    public void Map_OutOfTurn_ReturnsWaitAction()
    {
        var mapped = _mapper.Map(
            new OnlineTransportError(
                OnlineMatchProtocolConstants.ErrorOutOfTurn,
                "Out of turn.",
                StatusCode: 409));

        Assert.Equal(OnlineUserAction.WaitForOpponent, mapped.RecommendedAction);
        Assert.False(mapped.IsTerminal);
    }

    [Fact]
    public void Map_MatchAlreadyEnded_ReturnsTerminalStartNewMatchAction()
    {
        var mapped = _mapper.Map(
            new OnlineTransportError(
                OnlineMatchProtocolConstants.ErrorMatchAlreadyEnded,
                "Match already ended.",
                StatusCode: 409));

        Assert.Equal(OnlineUserAction.StartNewMatch, mapped.RecommendedAction);
        Assert.True(mapped.IsTerminal);
    }

    [Fact]
    public void Map_UnknownConflictStatus_MapsToResyncAction()
    {
        var mapped = _mapper.Map(
            new OnlineTransportError(
                "custom_conflict",
                "Unexpected conflict.",
                StatusCode: 409));

        Assert.Equal(OnlineUserAction.RequestResync, mapped.RecommendedAction);
    }
}
