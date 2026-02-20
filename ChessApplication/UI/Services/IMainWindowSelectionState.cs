using System.Collections.Generic;
using Chess.Domain;
using Chess.UI.ViewModels;

namespace Chess.UI.Services;

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
