using System;

namespace Chess.Domain;

public readonly record struct Square
{
    public Square(int file, int rank)
    {
        if (file < 0 || file > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(file), "File must be between 0 and 7.");
        }

        if (rank < 0 || rank > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(rank), "Rank must be between 0 and 7.");
        }

        File = file;
        Rank = rank;
    }

    public int File { get; init; }
    public int Rank { get; init; }
}
