using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MultiplayerServer.Contracts.V1;

namespace MultiplayerServer.Application.Matches;

public sealed class InMemoryMatchLifecycleService : IMatchLifecycleService
{
    private const int JoinCodeLength = 6;
    private static readonly char[] JoinCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    private readonly object _sync = new();
    private readonly Dictionary<string, MatchRecord> _matchesById = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _matchIdByJoinCode = new(StringComparer.Ordinal);
    private readonly ILogger<InMemoryMatchLifecycleService> _logger;

    public InMemoryMatchLifecycleService(ILogger<InMemoryMatchLifecycleService>? logger = null)
    {
        _logger = logger ?? NullLogger<InMemoryMatchLifecycleService>.Instance;
    }

    public CreateMatchResponse CreateMatch()
    {
        lock (_sync)
        {
            var matchId = Guid.NewGuid().ToString("N");
            var joinCode = GenerateUniqueJoinCode();
            var creatorToken = GenerateToken();

            _matchesById[matchId] = new MatchRecord
            {
                MatchId = matchId,
                JoinCode = joinCode,
                CreatorToken = creatorToken
            };

            _matchIdByJoinCode[joinCode] = matchId;
            _logger.LogInformation("Match created {MatchId}", matchId);

            return new CreateMatchResponse(matchId, joinCode, creatorToken);
        }
    }

    public JoinMatchResult JoinMatch(string? joinCode)
    {
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            _logger.LogWarning(
                "Join rejected with {ErrorCode}",
                MatchProtocolConstants.ErrorJoinCodeRequired);
            return JoinMatchResult.Failed(
                new JoinMatchFailure(
                    MatchProtocolConstants.ErrorJoinCodeRequired,
                    "joinCode is required."));
        }

        var normalizedJoinCode = joinCode.Trim().ToUpperInvariant();

        lock (_sync)
        {
            if (!_matchIdByJoinCode.TryGetValue(normalizedJoinCode, out var matchId) ||
                !_matchesById.TryGetValue(matchId, out var match))
            {
                _logger.LogWarning(
                    "Join rejected with {ErrorCode}",
                    MatchProtocolConstants.ErrorMatchNotFound);
                return JoinMatchResult.Failed(
                    new JoinMatchFailure(
                        MatchProtocolConstants.ErrorMatchNotFound,
                        "Match was not found."));
            }

            if (match.JoinerToken is not null)
            {
                _logger.LogWarning(
                    "Join rejected for {MatchId} with {ErrorCode}",
                    match.MatchId,
                    MatchProtocolConstants.ErrorMatchFull);
                return JoinMatchResult.Failed(
                    new JoinMatchFailure(
                        MatchProtocolConstants.ErrorMatchFull,
                        "Match already has two players."));
            }

            var joinerToken = GenerateToken();
            match.JoinerToken = joinerToken;
            _logger.LogInformation(
                "Join accepted for {MatchId}; assigned {Seat}",
                match.MatchId,
                MatchProtocolConstants.JoinerSeat);

            // Deterministic seat assignment (MS-002): creator is White, first joiner is Black.
            return JoinMatchResult.Success(
                new JoinMatchResponse(match.MatchId, MatchProtocolConstants.JoinerSeat, joinerToken));
        }
    }

    private string GenerateUniqueJoinCode()
    {
        while (true)
        {
            var candidate = GenerateJoinCode();
            if (_matchIdByJoinCode.ContainsKey(candidate))
            {
                continue;
            }

            return candidate;
        }
    }

    private static string GenerateJoinCode()
    {
        Span<char> buffer = stackalloc char[JoinCodeLength];
        Span<byte> randomBytes = stackalloc byte[JoinCodeLength];
        RandomNumberGenerator.Fill(randomBytes);

        for (var i = 0; i < JoinCodeLength; i++)
        {
            buffer[i] = JoinCodeAlphabet[randomBytes[i] % JoinCodeAlphabet.Length];
        }

        return new string(buffer);
    }

    private static string GenerateToken()
    {
        Span<byte> tokenBytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(tokenBytes);
        return Convert.ToHexString(tokenBytes).ToLowerInvariant();
    }

    private sealed class MatchRecord
    {
        public required string MatchId { get; init; }
        public required string JoinCode { get; init; }
        public required string CreatorToken { get; init; }
        public string? JoinerToken { get; set; }
    }
}
