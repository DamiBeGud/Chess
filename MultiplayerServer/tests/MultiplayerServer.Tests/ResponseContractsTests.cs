using MultiplayerServer.Contracts.V1;

namespace MultiplayerServer.Tests;

public sealed class ResponseContractsTests
{
    [Fact]
    public void HealthResponse_OkNow_SetsOkStatusAndCurrentUtcTimestamp()
    {
        var before = DateTimeOffset.UtcNow;
        var response = HealthResponse.OkNow();
        var after = DateTimeOffset.UtcNow;

        Assert.Equal("ok", response.Status);
        Assert.InRange(response.UtcTime, before, after);
    }

    [Fact]
    public void ApiInfoResponse_V1_ReturnsExpectedServiceAndVersion()
    {
        var response = ApiInfoResponse.V1();

        Assert.Equal("MultiplayerServer", response.Service);
        Assert.Equal("v1", response.Version);
    }

    [Fact]
    public void MatchProtocolConstants_UseExpectedSeatsAndErrorCodes()
    {
        Assert.Equal("White", MatchProtocolConstants.CreatorSeat);
        Assert.Equal("Black", MatchProtocolConstants.JoinerSeat);
        Assert.Equal("match.snapshot", MatchProtocolConstants.EventMatchSnapshot);
        Assert.Equal("match.updated", MatchProtocolConstants.EventMatchUpdated);
        Assert.Equal("match.error", MatchProtocolConstants.EventMatchError);
        Assert.Equal("join_code_required", MatchProtocolConstants.ErrorJoinCodeRequired);
        Assert.Equal("match_id_required", MatchProtocolConstants.ErrorMatchIdRequired);
        Assert.Equal("invalid_match_id_format", MatchProtocolConstants.ErrorInvalidMatchIdFormat);
        Assert.Equal("player_token_required", MatchProtocolConstants.ErrorPlayerTokenRequired);
        Assert.Equal("invalid_player_token_format", MatchProtocolConstants.ErrorInvalidPlayerTokenFormat);
        Assert.Equal("move_coordinates_required", MatchProtocolConstants.ErrorMoveCoordinatesRequired);
        Assert.Equal("match_not_found", MatchProtocolConstants.ErrorMatchNotFound);
        Assert.Equal("match_not_ready", MatchProtocolConstants.ErrorMatchNotReady);
        Assert.Equal("match_full", MatchProtocolConstants.ErrorMatchFull);
        Assert.Equal("invalid_player_token", MatchProtocolConstants.ErrorInvalidPlayerToken);
        Assert.Equal("invalid_promotion", MatchProtocolConstants.ErrorInvalidPromotion);
        Assert.Equal("out_of_turn", MatchProtocolConstants.ErrorOutOfTurn);
        Assert.Equal("illegal_move", MatchProtocolConstants.ErrorIllegalMove);
        Assert.Equal("transport_forbidden", MatchProtocolConstants.ErrorTransportForbidden);
        Assert.Equal("transport_not_subscribed", MatchProtocolConstants.ErrorTransportNotSubscribed);
    }
}
