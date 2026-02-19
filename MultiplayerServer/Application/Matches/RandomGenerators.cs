using System.Security.Cryptography;

namespace MultiplayerServer.Application.Matches;

public sealed class GuidMatchIdGenerator : IMatchIdGenerator
{
    public string Generate()
    {
        return Guid.NewGuid().ToString("N");
    }
}

public sealed class RandomJoinCodeGenerator : IJoinCodeGenerator
{
    private const int JoinCodeLength = 6;
    private static readonly char[] JoinCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

    public string Generate()
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
}

public sealed class RandomPlayerTokenGenerator : IPlayerTokenGenerator
{
    public string Generate()
    {
        Span<byte> tokenBytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(tokenBytes);
        return Convert.ToHexString(tokenBytes).ToLowerInvariant();
    }
}
