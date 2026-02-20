namespace Chess.Domain;

/// <summary>
/// PieceColor is an enumeration within the Domain module.
/// Its named values model a bounded set of states, options, or outcomes used by collaborators.
/// Primary production consumers include ChessMoveGenerator (Engine), MoveHistoryEntryViewModel (UI/ViewModels), InitialPositionBuilder (Engine).
/// Its values are interpreted by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> ChessMoveGenerator (Engine), MoveHistoryEntryViewModel (UI/ViewModels), InitialPositionBuilder (Engine), ChessMoveApplication (Engine)</para>
/// <para><b>Usage pattern:</b> Values are created or transformed by engine/application services, then propagated through persistence, online, and UI workflows as immutable state.</para>
/// <para><b>Dependencies/Collaborators:</b> Its values are interpreted by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the chess domain model boundary and represents rule-level concepts.</para>
/// </remarks>
public enum PieceColor
{
    White = 0,
    Black = 1
}
