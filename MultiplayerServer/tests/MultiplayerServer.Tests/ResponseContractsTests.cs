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
        Assert.Equal("join_code_required", MatchProtocolConstants.ErrorJoinCodeRequired);
        Assert.Equal("match_not_found", MatchProtocolConstants.ErrorMatchNotFound);
        Assert.Equal("match_full", MatchProtocolConstants.ErrorMatchFull);
    }
}
