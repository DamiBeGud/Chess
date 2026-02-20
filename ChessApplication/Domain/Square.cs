using System;

namespace Chess.Domain;

/// <summary>
/// Square is a record type within the Domain module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include ChessMoveGenerator (Engine), ChessMoveApplication (Engine), MainWindowSelectionState (UI/Services).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> ChessMoveGenerator (Engine), ChessMoveApplication (Engine), MainWindowSelectionState (UI/Services), ChessAttackDetector (Engine)</para>
/// <para><b>Usage pattern:</b> Values are created or transformed by engine/application services, then propagated through persistence, online, and UI workflows as immutable state.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the chess domain model boundary and represents rule-level concepts.</para>
/// </remarks>
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
