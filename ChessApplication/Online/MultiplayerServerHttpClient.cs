using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Chess.Online;

public sealed class MultiplayerServerHttpClient : IOnlineMatchHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly IOnlineErrorMapper _errorMapper;

    public MultiplayerServerHttpClient(HttpClient httpClient, IOnlineErrorMapper errorMapper)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _errorMapper = errorMapper ?? throw new ArgumentNullException(nameof(errorMapper));
    }

    public async Task<OnlineOperationResult<OnlineCreateMatchResponse>> CreateMatchAsync(CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(
            OnlineMatchProtocolConstants.MatchesRoute,
            content: null,
            cancellationToken);

        return await ReadResponseAsync<OnlineCreateMatchResponse>(response, cancellationToken);
    }

    public async Task<OnlineOperationResult<OnlineJoinMatchResponse>> JoinMatchAsync(
        string joinCode,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            OnlineMatchProtocolConstants.JoinRoute,
            new OnlineJoinMatchRequest(joinCode),
            cancellationToken);

        return await ReadResponseAsync<OnlineJoinMatchResponse>(response, cancellationToken);
    }

    public async Task<OnlineOperationResult<OnlineSubmitMoveResponse>> SubmitMoveAsync(
        OnlineSubmitMoveRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await _httpClient.PostAsJsonAsync(
            OnlineMatchProtocolConstants.MovesRoute,
            request,
            cancellationToken);

        return await ReadResponseAsync<OnlineSubmitMoveResponse>(response, cancellationToken);
    }

    public async Task<OnlineOperationResult<OnlineMatchSnapshot>> GetSnapshotAsync(
        OnlineSnapshotRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var response = await _httpClient.PostAsJsonAsync(
            OnlineMatchProtocolConstants.SnapshotRoute,
            request,
            cancellationToken);

        return await ReadResponseAsync<OnlineMatchSnapshot>(response, cancellationToken);
    }

    private async Task<OnlineOperationResult<T>> ReadResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
            if (payload is null)
            {
                return OnlineOperationResult<T>.Failure(
                    new OnlineUserError(
                        "invalid_response",
                        "Server response payload was empty.",
                        OnlineUserAction.Retry));
            }

            return OnlineOperationResult<T>.Success(payload);
        }

        var transportError = await ReadErrorAsync(response, cancellationToken);
        return OnlineOperationResult<T>.Failure(_errorMapper.Map(transportError));
    }

    private static async Task<OnlineTransportError> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<OnlineApiErrorResponse>(cancellationToken);
            if (payload is not null && !string.IsNullOrWhiteSpace(payload.Code))
            {
                return new OnlineTransportError(payload.Code, payload.Message, (int)response.StatusCode);
            }
        }
        catch (NotSupportedException)
        {
        }
        catch (HttpRequestException)
        {
        }

        var fallbackCode = $"http_{(int)response.StatusCode}";
        var fallbackMessage = $"Request failed with status {(int)response.StatusCode}.";
        return new OnlineTransportError(fallbackCode, fallbackMessage, (int)response.StatusCode);
    }
}
