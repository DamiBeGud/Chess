using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MultiplayerServer.Application.Matches;
using MultiplayerServer.Contracts.V1;
using MultiplayerServer.Hubs.V1;

namespace MultiplayerServer.Tests;

public sealed class MatchHubRealtimeTransportIntegrationTests
{
    [Fact]
    public async Task SubscribeMatch_TwoClientsReceiveSnapshotsAndMoveUpdates()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var (created, joined) = await CreateStartedMatchAsync(client);

        await using var creatorConnection = CreateHubConnection(factory, created.CreatorToken);
        await using var joinerConnection = CreateHubConnection(factory, joined.PlayerToken);

        var creatorSnapshots = new ConcurrentQueue<MatchSnapshotSyncEvent>();
        var joinerSnapshots = new ConcurrentQueue<MatchSnapshotSyncEvent>();
        var creatorUpdates = new ConcurrentQueue<MatchUpdatedSyncEvent>();
        var joinerUpdates = new ConcurrentQueue<MatchUpdatedSyncEvent>();
        using var creatorSnapshotSignal = new SemaphoreSlim(0, 4);
        using var joinerSnapshotSignal = new SemaphoreSlim(0, 4);
        using var creatorUpdateSignal = new SemaphoreSlim(0, 4);
        using var joinerUpdateSignal = new SemaphoreSlim(0, 4);

        creatorConnection.On<MatchSnapshotSyncEvent>(
            MatchProtocolConstants.EventMatchSnapshot,
            payload =>
            {
                creatorSnapshots.Enqueue(payload);
                creatorSnapshotSignal.Release();
            });
        joinerConnection.On<MatchSnapshotSyncEvent>(
            MatchProtocolConstants.EventMatchSnapshot,
            payload =>
            {
                joinerSnapshots.Enqueue(payload);
                joinerSnapshotSignal.Release();
            });
        creatorConnection.On<MatchUpdatedSyncEvent>(
            MatchProtocolConstants.EventMatchUpdated,
            payload =>
            {
                creatorUpdates.Enqueue(payload);
                creatorUpdateSignal.Release();
            });
        joinerConnection.On<MatchUpdatedSyncEvent>(
            MatchProtocolConstants.EventMatchUpdated,
            payload =>
            {
                joinerUpdates.Enqueue(payload);
                joinerUpdateSignal.Release();
            });

        await creatorConnection.StartAsync();
        await joinerConnection.StartAsync();

        await creatorConnection.InvokeAsync("SubscribeMatch", created.MatchId, created.CreatorToken);
        await joinerConnection.InvokeAsync("SubscribeMatch", created.MatchId, joined.PlayerToken);
        await WaitForSignalAsync(creatorSnapshotSignal, "creator snapshot");
        await WaitForSignalAsync(joinerSnapshotSignal, "joiner snapshot");

        var moveResponse = await client.PostAsJsonAsync(
            "/api/v1/matches/moves",
            new SubmitMoveRequest(created.MatchId, created.CreatorToken, "e2", "e4"));
        moveResponse.EnsureSuccessStatusCode();

        await WaitForSignalAsync(creatorUpdateSignal, "creator update");
        await WaitForSignalAsync(joinerUpdateSignal, "joiner update");

        var creatorSnapshot = Assert.Single(creatorSnapshots);
        var joinerSnapshot = Assert.Single(joinerSnapshots);
        var creatorUpdate = Assert.Single(creatorUpdates);
        var joinerUpdate = Assert.Single(joinerUpdates);

        Assert.Equal(created.MatchId, creatorUpdate.Metadata.MatchId);
        Assert.Equal(created.MatchId, joinerUpdate.Metadata.MatchId);
        Assert.Equal(creatorUpdate.Metadata.Sequence, joinerUpdate.Metadata.Sequence);
        Assert.True(creatorUpdate.Metadata.Sequence > creatorSnapshot.Metadata.Sequence);
        Assert.True(creatorUpdate.Metadata.Sequence > joinerSnapshot.Metadata.Sequence);
    }

    [Fact]
    public async Task ReconnectAndResync_DeliversSnapshotThenSubsequentUpdate()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var (created, joined) = await CreateStartedMatchAsync(client);

        await using (var initialConnection = CreateHubConnection(factory, created.CreatorToken))
        {
            using var initialSnapshotSignal = new SemaphoreSlim(0, 2);
            initialConnection.On<MatchSnapshotSyncEvent>(
                MatchProtocolConstants.EventMatchSnapshot,
                _ => initialSnapshotSignal.Release());

            await initialConnection.StartAsync();
            await initialConnection.InvokeAsync("SubscribeMatch", created.MatchId, created.CreatorToken);
            await WaitForSignalAsync(initialSnapshotSignal, "initial snapshot");
            await initialConnection.StopAsync();
        }

        await using var reconnected = CreateHubConnection(factory, created.CreatorToken);
        var snapshots = new ConcurrentQueue<MatchSnapshotSyncEvent>();
        var updates = new ConcurrentQueue<MatchUpdatedSyncEvent>();
        using var snapshotSignal = new SemaphoreSlim(0, 4);
        using var updateSignal = new SemaphoreSlim(0, 4);

        reconnected.On<MatchSnapshotSyncEvent>(
            MatchProtocolConstants.EventMatchSnapshot,
            payload =>
            {
                snapshots.Enqueue(payload);
                snapshotSignal.Release();
            });
        reconnected.On<MatchUpdatedSyncEvent>(
            MatchProtocolConstants.EventMatchUpdated,
            payload =>
            {
                updates.Enqueue(payload);
                updateSignal.Release();
            });

        await reconnected.StartAsync();
        await reconnected.InvokeAsync("SubscribeMatch", created.MatchId, created.CreatorToken);
        await WaitForSignalAsync(snapshotSignal, "reconnect snapshot");

        await reconnected.InvokeAsync("RequestResync", created.MatchId, created.CreatorToken);
        await WaitForSignalAsync(snapshotSignal, "explicit resync snapshot");

        var joinerMove = await client.PostAsJsonAsync(
            "/api/v1/matches/moves",
            new SubmitMoveRequest(created.MatchId, created.CreatorToken, "e2", "e4"));
        joinerMove.EnsureSuccessStatusCode();
        var creatorMove = await client.PostAsJsonAsync(
            "/api/v1/matches/moves",
            new SubmitMoveRequest(created.MatchId, joined.PlayerToken, "e7", "e5"));
        creatorMove.EnsureSuccessStatusCode();

        await WaitForSignalAsync(updateSignal, "post-resync update 1");
        await WaitForSignalAsync(updateSignal, "post-resync update 2");

        var snapshotEvents = snapshots.ToArray();
        Assert.True(snapshotEvents.Length >= 2);
        Assert.True(snapshotEvents[1].Metadata.Sequence > snapshotEvents[0].Metadata.Sequence);

        var updateEvents = updates.ToArray();
        Assert.Equal(2, updateEvents.Length);
        Assert.True(updateEvents[0].Metadata.Sequence > snapshotEvents[1].Metadata.Sequence);
        Assert.True(updateEvents[1].Metadata.Sequence > updateEvents[0].Metadata.Sequence);
    }

    [Fact]
    public async Task SubscribeMatch_UnauthorizedClientIsRejectedAndReceivesTransportError()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var (created, _) = await CreateStartedMatchAsync(client);

        var outsiderToken = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        await using var unauthorizedConnection = CreateHubConnection(factory, outsiderToken);
        var errors = new ConcurrentQueue<MatchErrorSyncEvent>();
        using var errorSignal = new SemaphoreSlim(0, 2);

        unauthorizedConnection.On<MatchErrorSyncEvent>(
            MatchProtocolConstants.EventMatchError,
            payload =>
            {
                errors.Enqueue(payload);
                errorSignal.Release();
            });

        await unauthorizedConnection.StartAsync();
        await Assert.ThrowsAsync<HubException>(() =>
            unauthorizedConnection.InvokeAsync(
                "SubscribeMatch",
                created.MatchId,
                outsiderToken));
        await WaitForSignalAsync(errorSignal, "unauthorized error");

        var error = Assert.Single(errors);
        Assert.Equal(MatchProtocolConstants.ErrorUnauthorizedResume, error.Code);
        Assert.Equal(created.MatchId, error.Metadata.MatchId);
    }

    [Fact]
    public async Task SubscribeMatch_MismatchedConnectionToken_ReturnsForbiddenTransportError()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var (created, joined) = await CreateStartedMatchAsync(client);

        await using var connection = CreateHubConnection(factory, created.CreatorToken);
        var errors = new ConcurrentQueue<MatchErrorSyncEvent>();
        using var errorSignal = new SemaphoreSlim(0, 2);
        connection.On<MatchErrorSyncEvent>(
            MatchProtocolConstants.EventMatchError,
            payload =>
            {
                errors.Enqueue(payload);
                errorSignal.Release();
            });

        await connection.StartAsync();

        await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync(
                "SubscribeMatch",
                created.MatchId,
                joined.PlayerToken));
        await WaitForSignalAsync(errorSignal, "forbidden mismatch transport error");

        var error = Assert.Single(errors);
        Assert.Equal(MatchProtocolConstants.ErrorUnauthorizedResume, error.Code);
        Assert.Equal(created.MatchId, error.Metadata.MatchId);
    }

    [Fact]
    public async Task SubscribeMatch_MissingMatchId_IsRejectedWithValidationError()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var (created, _) = await CreateStartedMatchAsync(client);

        await using var connection = CreateHubConnection(factory, created.CreatorToken);
        await connection.StartAsync();

        await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync(
                "SubscribeMatch",
                "   ",
                created.CreatorToken));
    }

    [Fact]
    public async Task SubscribeMatch_InvalidMatchIdFormat_IsRejectedWithValidationError()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var (created, _) = await CreateStartedMatchAsync(client);

        await using var connection = CreateHubConnection(factory, created.CreatorToken);
        await connection.StartAsync();

        await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync(
                "SubscribeMatch",
                "not-a-match-id",
                created.CreatorToken));
    }

    [Fact]
    public async Task RequestResync_WithoutSubscription_ReturnsNotSubscribedError()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var (created, _) = await CreateStartedMatchAsync(client);

        await using var connection = CreateHubConnection(factory, created.CreatorToken);
        var errors = new ConcurrentQueue<MatchErrorSyncEvent>();
        using var errorSignal = new SemaphoreSlim(0, 2);
        connection.On<MatchErrorSyncEvent>(
            MatchProtocolConstants.EventMatchError,
            payload =>
            {
                errors.Enqueue(payload);
                errorSignal.Release();
            });

        await connection.StartAsync();
        await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync(
                "RequestResync",
                created.MatchId,
                created.CreatorToken));
        await WaitForSignalAsync(errorSignal, "not subscribed transport error");

        var error = Assert.Single(errors);
        Assert.Equal(MatchProtocolConstants.ErrorTransportNotSubscribed, error.Code);
        Assert.Equal(created.MatchId, error.Metadata.MatchId);
    }

    [Fact]
    public async Task UnsubscribeMatch_DoesNotMarkSeatDisconnected()
    {
        await using var factory = CreateFactory(options => options.DisconnectGracePeriodSeconds = 1);
        using var client = factory.CreateClient();
        var (created, joined) = await CreateStartedMatchAsync(client);

        await using var creatorConnection = CreateHubConnection(factory, created.CreatorToken);
        await using var joinerConnection = CreateHubConnection(factory, joined.PlayerToken);
        var presenceEvents = new ConcurrentQueue<MatchPresenceChangedSyncEvent>();
        using var creatorSnapshotSignal = new SemaphoreSlim(0, 2);
        using var joinerSnapshotSignal = new SemaphoreSlim(0, 2);

        creatorConnection.On<MatchSnapshotSyncEvent>(
            MatchProtocolConstants.EventMatchSnapshot,
            _ => creatorSnapshotSignal.Release());
        joinerConnection.On<MatchSnapshotSyncEvent>(
            MatchProtocolConstants.EventMatchSnapshot,
            _ => joinerSnapshotSignal.Release());
        joinerConnection.On<MatchPresenceChangedSyncEvent>(
            MatchProtocolConstants.EventMatchPresenceChanged,
            payload => presenceEvents.Enqueue(payload));

        await creatorConnection.StartAsync();
        await joinerConnection.StartAsync();
        await creatorConnection.InvokeAsync("SubscribeMatch", created.MatchId, created.CreatorToken);
        await joinerConnection.InvokeAsync("SubscribeMatch", created.MatchId, joined.PlayerToken);
        await WaitForSignalAsync(creatorSnapshotSignal, "creator initial snapshot");
        await WaitForSignalAsync(joinerSnapshotSignal, "joiner initial snapshot");

        await creatorConnection.InvokeAsync("UnsubscribeMatch", created.MatchId);
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        Assert.Empty(presenceEvents);

        var creatorMove = await client.PostAsJsonAsync(
            "/api/v1/matches/moves",
            new SubmitMoveRequest(created.MatchId, created.CreatorToken, "e2", "e4"));
        creatorMove.EnsureSuccessStatusCode();

        await Task.Delay(TimeSpan.FromSeconds(2));
        var snapshotUseCase = factory.Services.GetRequiredService<IGetMatchSnapshotUseCase>();
        var current = Assert.IsType<GetMatchSnapshotSucceeded>(
            snapshotUseCase.GetMatchSnapshot(created.MatchId, created.CreatorToken)).Response.Snapshot;
        Assert.Equal(MatchProtocolConstants.MatchStatusInProgress, current.Status);
        Assert.True(current.Presence.Creator.IsConnected);
        Assert.Null(current.Presence.Creator.DisconnectedUtc);
        Assert.Null(current.Presence.Creator.GraceExpiresUtc);
    }

    [Fact]
    public async Task Publisher_DuplicateEventIdIsSuppressed_AndSequenceRemainsMonotonic()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var (created, _) = await CreateStartedMatchAsync(client);

        await using var connection = CreateHubConnection(factory, created.CreatorToken);
        var updates = new ConcurrentQueue<MatchUpdatedSyncEvent>();
        using var updateSignal = new SemaphoreSlim(0, 8);

        connection.On<MatchUpdatedSyncEvent>(
            MatchProtocolConstants.EventMatchUpdated,
            payload =>
            {
                updates.Enqueue(payload);
                updateSignal.Release();
            });

        await connection.StartAsync();
        await connection.InvokeAsync("SubscribeMatch", created.MatchId, created.CreatorToken);

        var snapshotUseCase = factory.Services.GetRequiredService<IGetMatchSnapshotUseCase>();
        var publisher = factory.Services.GetRequiredService<IMatchSyncPublisher>();
        var snapshotOutcome = snapshotUseCase.GetMatchSnapshot(created.MatchId, created.CreatorToken);
        var snapshot = Assert.IsType<GetMatchSnapshotSucceeded>(snapshotOutcome).Response.Snapshot;

        await publisher.PublishMatchUpdatedAsync(snapshot, "duplicate-event", CancellationToken.None);
        await publisher.PublishMatchUpdatedAsync(snapshot, "duplicate-event", CancellationToken.None);
        await publisher.PublishMatchUpdatedAsync(snapshot, "next-event", CancellationToken.None);

        await WaitForSignalAsync(updateSignal, "first unique update");
        await WaitForSignalAsync(updateSignal, "second unique update");

        var received = updates.ToArray();
        Assert.Equal(2, received.Length);
        Assert.Equal("duplicate-event", received[0].Metadata.EventId);
        Assert.Equal("next-event", received[1].Metadata.EventId);
        Assert.True(received[1].Metadata.Sequence > received[0].Metadata.Sequence);
    }

    [Fact]
    public async Task DisconnectMidGame_ReconnectWithinGrace_SeatRestoresAndPlayContinues()
    {
        await using var factory = CreateFactory(options => options.DisconnectGracePeriodSeconds = 2);
        using var client = factory.CreateClient();
        var (created, joined) = await CreateStartedMatchAsync(client);

        await using var creatorConnection = CreateHubConnection(factory, created.CreatorToken);
        await using var joinerConnection = CreateHubConnection(factory, joined.PlayerToken);
        var presenceEvents = new ConcurrentQueue<MatchPresenceChangedSyncEvent>();
        using var creatorSnapshotSignal = new SemaphoreSlim(0, 2);
        using var joinerSnapshotSignal = new SemaphoreSlim(0, 2);
        using var presenceSignal = new SemaphoreSlim(0, 4);

        creatorConnection.On<MatchSnapshotSyncEvent>(
            MatchProtocolConstants.EventMatchSnapshot,
            _ => creatorSnapshotSignal.Release());
        joinerConnection.On<MatchSnapshotSyncEvent>(
            MatchProtocolConstants.EventMatchSnapshot,
            _ => joinerSnapshotSignal.Release());
        joinerConnection.On<MatchPresenceChangedSyncEvent>(
            MatchProtocolConstants.EventMatchPresenceChanged,
            payload =>
            {
                presenceEvents.Enqueue(payload);
                presenceSignal.Release();
            });

        await creatorConnection.StartAsync();
        await joinerConnection.StartAsync();
        await creatorConnection.InvokeAsync("SubscribeMatch", created.MatchId, created.CreatorToken);
        await joinerConnection.InvokeAsync("SubscribeMatch", created.MatchId, joined.PlayerToken);
        await WaitForSignalAsync(creatorSnapshotSignal, "creator initial snapshot");
        await WaitForSignalAsync(joinerSnapshotSignal, "joiner initial snapshot");

        await creatorConnection.StopAsync();
        await WaitForSignalAsync(presenceSignal, "creator disconnect presence");

        await using var reconnected = CreateHubConnection(factory, created.CreatorToken);
        using var reconnectSnapshotSignal = new SemaphoreSlim(0, 2);
        reconnected.On<MatchSnapshotSyncEvent>(
            MatchProtocolConstants.EventMatchSnapshot,
            _ => reconnectSnapshotSignal.Release());
        await reconnected.StartAsync();
        await reconnected.InvokeAsync("SubscribeMatch", created.MatchId, created.CreatorToken);
        await WaitForSignalAsync(reconnectSnapshotSignal, "creator reconnect snapshot");
        await WaitForSignalAsync(presenceSignal, "creator reconnect presence");

        var observedPresence = presenceEvents.ToArray();
        Assert.True(observedPresence.Length >= 2);
        Assert.False(observedPresence[0].Snapshot.Presence!.Creator.IsConnected);
        Assert.True(observedPresence[^1].Snapshot.Presence!.Creator.IsConnected);

        var creatorMove = await client.PostAsJsonAsync(
            "/api/v1/matches/moves",
            new SubmitMoveRequest(created.MatchId, created.CreatorToken, "e2", "e4"));
        creatorMove.EnsureSuccessStatusCode();

        var joinerMove = await client.PostAsJsonAsync(
            "/api/v1/matches/moves",
            new SubmitMoveRequest(created.MatchId, joined.PlayerToken, "e7", "e5"));
        joinerMove.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task DisconnectAndNeverReturn_ForfeitResolution_EndsMatchAfterGrace()
    {
        await using var factory = CreateFactory(options =>
        {
            options.DisconnectGracePeriodSeconds = 1;
            options.AbandonmentResolution = MatchAbandonmentResolutionMode.Forfeit;
        });

        using var client = factory.CreateClient();
        var (created, joined) = await CreateStartedMatchAsync(client);

        await using var creatorConnection = CreateHubConnection(factory, created.CreatorToken);
        await using var joinerConnection = CreateHubConnection(factory, joined.PlayerToken);
        using var creatorSnapshotSignal = new SemaphoreSlim(0, 2);
        using var joinerSnapshotSignal = new SemaphoreSlim(0, 2);
        using var disconnectPresenceSignal = new SemaphoreSlim(0, 2);

        creatorConnection.On<MatchSnapshotSyncEvent>(
            MatchProtocolConstants.EventMatchSnapshot,
            _ => creatorSnapshotSignal.Release());
        joinerConnection.On<MatchSnapshotSyncEvent>(
            MatchProtocolConstants.EventMatchSnapshot,
            _ => joinerSnapshotSignal.Release());
        joinerConnection.On<MatchPresenceChangedSyncEvent>(
            MatchProtocolConstants.EventMatchPresenceChanged,
            payload =>
            {
                if (!payload.Snapshot.Presence!.Creator.IsConnected)
                {
                    disconnectPresenceSignal.Release();
                }
            });

        await creatorConnection.StartAsync();
        await joinerConnection.StartAsync();
        await creatorConnection.InvokeAsync("SubscribeMatch", created.MatchId, created.CreatorToken);
        await joinerConnection.InvokeAsync("SubscribeMatch", created.MatchId, joined.PlayerToken);
        await WaitForSignalAsync(creatorSnapshotSignal, "creator initial snapshot");
        await WaitForSignalAsync(joinerSnapshotSignal, "joiner initial snapshot");

        await creatorConnection.StopAsync();
        await WaitForSignalAsync(disconnectPresenceSignal, "forfeit disconnect presence", TimeSpan.FromSeconds(10));
        var endedSnapshot = await WaitForMatchEndedSnapshotAsync(factory, created.MatchId, joined.PlayerToken);
        Assert.Equal(MatchProtocolConstants.MatchStatusEnded, endedSnapshot.Status);
        Assert.Equal(MatchProtocolConstants.MatchResolutionForfeit, endedSnapshot.Resolution);
        Assert.Equal(MatchSeats.Joiner, endedSnapshot.WinnerSeat);

        var moveAfterEnd = await client.PostAsJsonAsync(
            "/api/v1/matches/moves",
            new SubmitMoveRequest(created.MatchId, joined.PlayerToken, "e7", "e5"));
        Assert.Equal(System.Net.HttpStatusCode.Conflict, moveAfterEnd.StatusCode);
        var error = await moveAfterEnd.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal(MatchProtocolConstants.ErrorMatchAlreadyEnded, error.Code);
    }

    [Fact]
    public async Task DisconnectAndNeverReturn_DrawResolution_EndsMatchAfterGrace()
    {
        await using var factory = CreateFactory(options =>
        {
            options.DisconnectGracePeriodSeconds = 1;
            options.AbandonmentResolution = MatchAbandonmentResolutionMode.Draw;
        });

        using var client = factory.CreateClient();
        var (created, joined) = await CreateStartedMatchAsync(client);

        await using var creatorConnection = CreateHubConnection(factory, created.CreatorToken);
        await using var joinerConnection = CreateHubConnection(factory, joined.PlayerToken);
        using var creatorSnapshotSignal = new SemaphoreSlim(0, 2);
        using var joinerSnapshotSignal = new SemaphoreSlim(0, 2);
        using var disconnectPresenceSignal = new SemaphoreSlim(0, 2);

        creatorConnection.On<MatchSnapshotSyncEvent>(
            MatchProtocolConstants.EventMatchSnapshot,
            _ => creatorSnapshotSignal.Release());
        joinerConnection.On<MatchSnapshotSyncEvent>(
            MatchProtocolConstants.EventMatchSnapshot,
            _ => joinerSnapshotSignal.Release());
        joinerConnection.On<MatchPresenceChangedSyncEvent>(
            MatchProtocolConstants.EventMatchPresenceChanged,
            payload =>
            {
                if (!payload.Snapshot.Presence!.Creator.IsConnected)
                {
                    disconnectPresenceSignal.Release();
                }
            });

        await creatorConnection.StartAsync();
        await joinerConnection.StartAsync();
        await creatorConnection.InvokeAsync("SubscribeMatch", created.MatchId, created.CreatorToken);
        await joinerConnection.InvokeAsync("SubscribeMatch", created.MatchId, joined.PlayerToken);
        await WaitForSignalAsync(creatorSnapshotSignal, "creator initial snapshot");
        await WaitForSignalAsync(joinerSnapshotSignal, "joiner initial snapshot");

        await creatorConnection.StopAsync();
        await WaitForSignalAsync(disconnectPresenceSignal, "draw disconnect presence", TimeSpan.FromSeconds(10));
        var endedSnapshot = await WaitForMatchEndedSnapshotAsync(factory, created.MatchId, joined.PlayerToken);
        Assert.Equal(MatchProtocolConstants.MatchStatusEnded, endedSnapshot.Status);
        Assert.Equal(MatchProtocolConstants.MatchResolutionDraw, endedSnapshot.Resolution);
        Assert.Null(endedSnapshot.WinnerSeat);
    }

    [Fact]
    public async Task UnauthorizedClient_CannotClaimDisconnectedSeat()
    {
        await using var factory = CreateFactory(options => options.DisconnectGracePeriodSeconds = 2);
        using var client = factory.CreateClient();
        var (created, joined) = await CreateStartedMatchAsync(client);

        await using var creatorConnection = CreateHubConnection(factory, created.CreatorToken);
        await using var joinerConnection = CreateHubConnection(factory, joined.PlayerToken);
        using var creatorSnapshotSignal = new SemaphoreSlim(0, 2);
        using var joinerSnapshotSignal = new SemaphoreSlim(0, 2);
        creatorConnection.On<MatchSnapshotSyncEvent>(
            MatchProtocolConstants.EventMatchSnapshot,
            _ => creatorSnapshotSignal.Release());
        joinerConnection.On<MatchSnapshotSyncEvent>(
            MatchProtocolConstants.EventMatchSnapshot,
            _ => joinerSnapshotSignal.Release());

        await creatorConnection.StartAsync();
        await joinerConnection.StartAsync();
        await creatorConnection.InvokeAsync("SubscribeMatch", created.MatchId, created.CreatorToken);
        await joinerConnection.InvokeAsync("SubscribeMatch", created.MatchId, joined.PlayerToken);
        await WaitForSignalAsync(creatorSnapshotSignal, "creator initial snapshot");
        await WaitForSignalAsync(joinerSnapshotSignal, "joiner initial snapshot");

        await creatorConnection.StopAsync();

        var outsiderToken = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        await using var outsiderConnection = CreateHubConnection(factory, outsiderToken);
        var errors = new ConcurrentQueue<MatchErrorSyncEvent>();
        using var errorSignal = new SemaphoreSlim(0, 2);
        outsiderConnection.On<MatchErrorSyncEvent>(
            MatchProtocolConstants.EventMatchError,
            payload =>
            {
                errors.Enqueue(payload);
                errorSignal.Release();
            });

        await outsiderConnection.StartAsync();
        await Assert.ThrowsAsync<HubException>(() =>
            outsiderConnection.InvokeAsync(
                "SubscribeMatch",
                created.MatchId,
                outsiderToken));
        await WaitForSignalAsync(errorSignal, "unauthorized resume error");

        var error = Assert.Single(errors);
        Assert.Equal(MatchProtocolConstants.ErrorUnauthorizedResume, error.Code);
    }

    [Fact]
    public async Task ReconnectNearGraceBoundary_TimeoutWinsDeterministically()
    {
        var clock = new AdjustableMatchClock(new DateTimeOffset(2026, 2, 1, 12, 0, 0, TimeSpan.Zero));
        var scheduler = new ManualDisconnectGraceScheduler();
        await using var factory = CreateFactory(
            options =>
            {
                options.DisconnectGracePeriodSeconds = 1;
                options.AbandonmentResolution = MatchAbandonmentResolutionMode.Forfeit;
            },
            services =>
            {
                services.RemoveAll<IMatchClock>();
                services.RemoveAll<IDisconnectGraceScheduler>();
                services.AddSingleton<IMatchClock>(clock);
                services.AddSingleton<IDisconnectGraceScheduler>(scheduler);
            });

        using var client = factory.CreateClient();
        var (created, joined) = await CreateStartedMatchAsync(client);

        await using var creatorConnection = CreateHubConnection(factory, created.CreatorToken);
        await using var joinerConnection = CreateHubConnection(factory, joined.PlayerToken);
        var endedEvents = new ConcurrentQueue<MatchEndedSyncEvent>();
        using var creatorSnapshotSignal = new SemaphoreSlim(0, 2);
        using var joinerSnapshotSignal = new SemaphoreSlim(0, 2);
        creatorConnection.On<MatchSnapshotSyncEvent>(
            MatchProtocolConstants.EventMatchSnapshot,
            _ => creatorSnapshotSignal.Release());
        joinerConnection.On<MatchSnapshotSyncEvent>(
            MatchProtocolConstants.EventMatchSnapshot,
            _ => joinerSnapshotSignal.Release());
        joinerConnection.On<MatchEndedSyncEvent>(
            MatchProtocolConstants.EventMatchEnded,
            payload =>
            {
                endedEvents.Enqueue(payload);
            });

        await creatorConnection.StartAsync();
        await joinerConnection.StartAsync();
        await creatorConnection.InvokeAsync("SubscribeMatch", created.MatchId, created.CreatorToken);
        await joinerConnection.InvokeAsync("SubscribeMatch", created.MatchId, joined.PlayerToken);
        await WaitForSignalAsync(creatorSnapshotSignal, "creator initial snapshot");
        await WaitForSignalAsync(joinerSnapshotSignal, "joiner initial snapshot");

        await creatorConnection.StopAsync();
        await WaitForConditionAsync(
            () => scheduler.HasScheduledSeat(created.MatchId, MatchSeats.Creator),
            "disconnect timeout scheduling");

        clock.Advance(TimeSpan.FromSeconds(1));

        await using var reconnecting = CreateHubConnection(factory, created.CreatorToken);
        await reconnecting.StartAsync();
        var reconnectAttempt = Assert.ThrowsAnyAsync<Exception>(() =>
            reconnecting.InvokeAsync("SubscribeMatch", created.MatchId, created.CreatorToken));
        await scheduler.TriggerSeatTimeoutAsync(created.MatchId, MatchSeats.Creator);
        await reconnectAttempt;

        await WaitForConditionAsync(
            () => endedEvents.Any(),
            "boundary ended event",
            TimeSpan.FromSeconds(10));
        var ended = Assert.Single(endedEvents);
        Assert.Equal(MatchProtocolConstants.MatchStatusEnded, ended.Snapshot.Status);
        Assert.Equal(MatchProtocolConstants.MatchResolutionForfeit, ended.Snapshot.Resolution);
        Assert.Equal(MatchSeats.Joiner, ended.Snapshot.WinnerSeat);
    }

    private static HubConnection CreateHubConnection(WebApplicationFactory<Program> factory, string accessToken)
    {
        var hubUri = new Uri(factory.Server.BaseAddress, ServerRouteConventions.MatchHubV1);
        return new HubConnectionBuilder()
            .WithUrl(
                hubUri,
                options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                })
            .Build();
    }

    private static WebApplicationFactory<Program> CreateFactory(
        Action<MatchDisconnectPolicyOptions>? configurePolicy = null,
        Action<IServiceCollection>? configureServices = null)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(
                builder =>
                {
                    builder.ConfigureServices(
                        services =>
                        {
                            if (configurePolicy is not null)
                            {
                                services.PostConfigure(configurePolicy);
                            }

                            configureServices?.Invoke(services);
                        });
                });
    }

    private static async Task<(CreateMatchResponse Created, JoinMatchResponse Joined)> CreateStartedMatchAsync(HttpClient client)
    {
        var createResponse = await client.PostAsync("/api/v1/matches", content: null);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<CreateMatchResponse>();
        Assert.NotNull(created);

        var joinResponse = await client.PostAsJsonAsync(
            "/api/v1/matches/join",
            new JoinMatchRequest(created.JoinCode));
        joinResponse.EnsureSuccessStatusCode();
        var joined = await joinResponse.Content.ReadFromJsonAsync<JoinMatchResponse>();
        Assert.NotNull(joined);

        return (created, joined);
    }

    private static async Task<MatchSnapshot> WaitForMatchEndedSnapshotAsync(
        WebApplicationFactory<Program> factory,
        string matchId,
        string playerToken,
        TimeSpan? timeout = null)
    {
        var useCase = factory.Services.GetRequiredService<IGetMatchSnapshotUseCase>();
        var until = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (DateTimeOffset.UtcNow < until)
        {
            var outcome = useCase.GetMatchSnapshot(matchId, playerToken);
            if (outcome is GetMatchSnapshotSucceeded { Response: { Snapshot: { } snapshot } } &&
                string.Equals(snapshot.Status, MatchStatuses.Ended, StringComparison.Ordinal))
            {
                return snapshot;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25));
        }

        throw new TimeoutException($"Timed out waiting for terminal snapshot for {matchId}.");
    }

    private sealed class AdjustableMatchClock(DateTimeOffset utcNow) : IMatchClock
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;

        public void Advance(TimeSpan by)
        {
            UtcNow = UtcNow.Add(by);
        }
    }

    private sealed class ManualDisconnectGraceScheduler : IDisconnectGraceScheduler
    {
        private readonly Dictionary<string, Func<CancellationToken, Task>> _callbacks =
            new(StringComparer.OrdinalIgnoreCase);

        public void ScheduleSeatGraceTimeout(
            string matchId,
            string seat,
            DateTimeOffset dueUtc,
            Func<CancellationToken, Task> onTimeoutAsync)
        {
            _callbacks[BuildKey(matchId, seat)] = onTimeoutAsync;
        }

        public void CancelSeatGraceTimeout(string matchId, string seat)
        {
            _callbacks.Remove(BuildKey(matchId, seat));
        }

        public void CancelMatchGraceTimeouts(string matchId)
        {
            foreach (var key in _callbacks.Keys.Where(key => key.StartsWith($"{matchId}|", StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                _callbacks.Remove(key);
            }
        }

        public bool HasScheduledSeat(string matchId, string seat)
        {
            return _callbacks.ContainsKey(BuildKey(matchId, seat));
        }

        public async Task TriggerSeatTimeoutAsync(string matchId, string seat)
        {
            var key = BuildKey(matchId, seat);
            if (!_callbacks.TryGetValue(key, out var callback))
            {
                return;
            }

            _callbacks.Remove(key);
            await callback(CancellationToken.None);
        }

        public void Dispose()
        {
            _callbacks.Clear();
        }

        private static string BuildKey(string matchId, string seat)
        {
            return $"{matchId}|{seat}";
        }
    }

    private static async Task WaitForSignalAsync(
        SemaphoreSlim signal,
        string description,
        TimeSpan? timeout = null)
    {
        var signaled = await signal.WaitAsync(timeout ?? TimeSpan.FromSeconds(5));
        Assert.True(signaled, $"Timed out waiting for {description}.");
    }

    private static async Task WaitForConditionAsync(
        Func<bool> condition,
        string description,
        TimeSpan? timeout = null)
    {
        var until = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        while (DateTimeOffset.UtcNow < until)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25));
        }

        Assert.True(condition(), $"Timed out waiting for {description}.");
    }
}
