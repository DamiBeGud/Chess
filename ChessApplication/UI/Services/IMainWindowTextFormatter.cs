using System.Collections.Generic;
using Chess.Domain;
using Chess.UI.ViewModels;

namespace Chess.UI.Services;

/// <summary>
/// IMainWindowTextFormatter defines a contract within the UI/Services module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include MainWindowViewModel (UI/ViewModels), MainWindowLocalPlayCoordinator (UI/Services), MainWindowOnlinePlayCoordinator (UI/Services).
/// Key collaborators are Implementations include MainWindowTextFormatter (UI/Services).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindowViewModel (UI/ViewModels), MainWindowLocalPlayCoordinator (UI/Services), MainWindowOnlinePlayCoordinator (UI/Services), MainWindowTextFormatter (UI/Services)</para>
/// <para><b>Usage pattern:</b> View models delegate focused interaction steps to this type, which applies deterministic UI workflow logic for the given context.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include MainWindowTextFormatter (UI/Services).</para>
/// <para><b>Boundary:</b> This type sits in the UI/Services UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal interface IMainWindowTextFormatter
{
    string ToCoordinate(Square square);
    string BuildFocusedSquareText(Square square);
    string BuildGameStatusText(GameState gameState);
    string BuildHumanMoveLastAction(GameState previousState, Square fromSquare, Square toSquare, PieceType? movedPieceType);
    string BuildAiMoveLastAction(PieceColor aiColor, Move aiMove);
    string BuildNoPieceSelectionFeedback(Square square, PieceColor sideToMove);
    string BuildOpponentPieceSelectionFeedback(Piece piece, Square square, PieceColor sideToMove);
    string BuildNoLegalMovesFeedback(Square square);
    string BuildMoveRejectedFeedback(Square fromSquare, Square toSquare);
    string BuildInvalidMoveTargetFeedback(
        Square targetSquare,
        Square selectedSquare,
        IReadOnlyCollection<Square> legalDestinationSquares);
    IReadOnlyList<MoveHistoryEntryViewModel> BuildMoveHistoryEntries(IReadOnlyList<Move> moveHistory);
}
