namespace Chess.Domain;

/// <summary>
/// GameStatus is an enumeration within the Domain module.
/// Its named values model a bounded set of states, options, or outcomes used by collaborators.
/// Primary production consumers include ChessGameStatusEvaluator (Engine), OnlineSnapshotGameStateMapper (Online), MoveLookupFailureReason (Engine).
/// Its values are interpreted by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> ChessGameStatusEvaluator (Engine), OnlineSnapshotGameStateMapper (Online), MoveLookupFailureReason (Engine), MaterialMobilityPositionEvaluator (AI)</para>
/// <para><b>Usage pattern:</b> Values are created or transformed by engine/application services, then propagated through persistence, online, and UI workflows as immutable state.</para>
/// <para><b>Dependencies/Collaborators:</b> Its values are interpreted by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the chess domain model boundary and represents rule-level concepts.</para>
/// </remarks>
public enum GameStatus
{
    InProgress = 0,
    WhiteWin = 1,
    BlackWin = 2,
    Draw = 3
}
