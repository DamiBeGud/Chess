using System.Collections.Generic;
using Chess.Domain;
using Chess.UI.ViewModels;

namespace Chess.UI.Services;

/// <summary>
/// IMainWindowSelectionState defines a contract within the UI/Services module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include MainWindowViewModel (UI/ViewModels), MainWindowLocalPlayCoordinator (UI/Services), MainWindowOnlinePlayCoordinator (UI/Services).
/// Key collaborators are Implementations include MainWindowSelectionState (UI/Services).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindowViewModel (UI/ViewModels), MainWindowLocalPlayCoordinator (UI/Services), MainWindowOnlinePlayCoordinator (UI/Services), MainWindowSelectionState (UI/Services)</para>
/// <para><b>Usage pattern:</b> View models delegate focused interaction steps to this type, which applies deterministic UI workflow logic for the given context.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include MainWindowSelectionState (UI/Services).</para>
/// <para><b>Boundary:</b> This type sits in the UI/Services UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal interface IMainWindowSelectionState
{
    Square FocusedSquare { get; }
    Square? SelectedSquare { get; }
    IReadOnlyCollection<Square> LegalDestinationSquares { get; }
    bool IsLegalDestination(Square square);
    void SetFocusedSquare(Square square);
    void MoveFocusedSquare(int fileDelta, int rankDelta);
    bool SelectSquare(Square square, IReadOnlyList<Move> legalMoves);
    void SelectSquareWithoutLegalDestinations(Square square);
    void ClearSelection();
    void ApplyHighlights(IReadOnlyList<BoardSquareViewModel> boardSquares, bool showLegalMoveSuggestions);
}
