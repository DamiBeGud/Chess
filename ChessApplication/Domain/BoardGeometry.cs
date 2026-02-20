using System;
using System.Collections.Generic;

namespace Chess.Domain;

public static class BoardGeometry
{
    public const int BoardSize = 8;

    public static IReadOnlyList<(int File, int Rank)> KnightOffsets { get; } = new (int File, int Rank)[]
    {
        (-2, -1), (-2, 1), (-1, -2), (-1, 2),
        (1, -2), (1, 2), (2, -1), (2, 1)
    };

    public static IReadOnlyList<(int File, int Rank)> KingOffsets { get; } = new (int File, int Rank)[]
    {
        (-1, -1), (-1, 0), (-1, 1),
        (0, -1),           (0, 1),
        (1, -1),  (1, 0),  (1, 1)
    };

    public static IReadOnlyList<(int File, int Rank)> DiagonalDirections { get; } = new (int File, int Rank)[]
    {
        (-1, -1), (-1, 1), (1, -1), (1, 1)
    };

    public static IReadOnlyList<(int File, int Rank)> OrthogonalDirections { get; } = new (int File, int Rank)[]
    {
        (-1, 0), (1, 0), (0, -1), (0, 1)
    };

    public static IReadOnlyList<(int File, int Rank)> QueenDirections { get; } = new (int File, int Rank)[]
    {
        (-1, -1), (-1, 1), (1, -1), (1, 1),
        (-1, 0), (1, 0), (0, -1), (0, 1)
    };

    public static IReadOnlyList<int> PawnCaptureFileOffsets { get; } = new[] { -1, 1 };

    public static bool IsWithinBoard(int file, int rank)
    {
        return file is >= 0 and < BoardSize
            && rank is >= 0 and < BoardSize;
    }

    public static string ToCoordinate(Square square)
    {
        return $"{(char)('a' + square.File)}{square.Rank + 1}";
    }

    public static bool TryParseCoordinate(string? coordinate, out Square square)
    {
        square = default;

        if (string.IsNullOrWhiteSpace(coordinate))
        {
            return false;
        }

        var normalized = coordinate.Trim();
        if (normalized.Length != 2)
        {
            return false;
        }

        var fileChar = char.ToLowerInvariant(normalized[0]);
        var rankChar = normalized[1];

        var file = fileChar - 'a';
        var rank = rankChar - '1';

        if (!IsWithinBoard(file, rank))
        {
            return false;
        }

        square = new Square(file, rank);
        return true;
    }

    public static Square ParseCoordinate(string coordinate)
    {
        if (!TryParseCoordinate(coordinate, out var square))
        {
            throw new FormatException($"Invalid square coordinate '{coordinate}'. Expected format like 'e2'.");
        }

        return square;
    }
}
