using System;
using System.Collections.Generic;
using Chess.Domain;
using Chess.UI.ViewModels;

namespace Chess.UI.Services;

/// <summary>
/// MainWindowSelectionState is a concrete type within the UI/Services module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include MainWindowViewModel (UI/ViewModels).
/// Key collaborators are Square, IMainWindowSelectionState.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindowViewModel (UI/ViewModels)</para>
/// <para><b>Usage pattern:</b> View models delegate focused interaction steps to this type, which applies deterministic UI workflow logic for the given context.</para>
/// <para><b>Dependencies/Collaborators:</b> Square, IMainWindowSelectionState.</para>
/// <para><b>Boundary:</b> This type sits in the UI/Services UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal sealed class MainWindowSelectionState : IMainWindowSelectionState
{
    private readonly HashSet<Square> _legalDestinationSquares = [];
    private Square? _selectedSquare;
    private bool _hasMovedFocusSinceSelection;
    private Square _focusedSquare;

    internal MainWindowSelectionState(Square initialFocusedSquare)
    {
        _focusedSquare = initialFocusedSquare;
    }

    public Square FocusedSquare => _focusedSquare;

    public Square? SelectedSquare => _selectedSquare;

    public IReadOnlyCollection<Square> LegalDestinationSquares => _legalDestinationSquares;

    public bool IsLegalDestination(Square square)
    {
        return _legalDestinationSquares.Contains(square);
    }

    public void SetFocusedSquare(Square square)
    {
        _focusedSquare = square;
    }

    public void MoveFocusedSquare(int fileDelta, int rankDelta)
    {
        var movementOrigin = _selectedSquare is not null && !_hasMovedFocusSinceSelection
            ? _selectedSquare.Value
            : _focusedSquare;
        var nextFile = Math.Clamp(movementOrigin.File + fileDelta, 0, 7);
        var nextRank = Math.Clamp(movementOrigin.Rank + rankDelta, 0, 7);
        _focusedSquare = new Square(nextFile, nextRank);

        if (_selectedSquare is not null)
        {
            _hasMovedFocusSinceSelection = true;
        }
    }

    public bool SelectSquare(Square square, IReadOnlyList<Move> legalMoves)
    {
        _selectedSquare = square;
        _hasMovedFocusSinceSelection = false;
        _legalDestinationSquares.Clear();

        foreach (var move in legalMoves)
        {
            _legalDestinationSquares.Add(move.To);
        }

        return _legalDestinationSquares.Count > 0;
    }

    public void SelectSquareWithoutLegalDestinations(Square square)
    {
        _selectedSquare = square;
        _hasMovedFocusSinceSelection = false;
        _legalDestinationSquares.Clear();
    }

    public void ClearSelection()
    {
        _selectedSquare = null;
        _hasMovedFocusSinceSelection = false;
        _legalDestinationSquares.Clear();
    }

    public void ApplyHighlights(IReadOnlyList<BoardSquareViewModel> boardSquares, bool showLegalMoveSuggestions)
    {
        foreach (var squareViewModel in boardSquares)
        {
            var isSelected = _selectedSquare is not null && squareViewModel.Square == _selectedSquare.Value;
            var isLegalDestination = showLegalMoveSuggestions
                && _legalDestinationSquares.Contains(squareViewModel.Square);
            var isKeyboardFocused = squareViewModel.Square == _focusedSquare;

            squareViewModel.SetSelected(isSelected);
            squareViewModel.SetLegalDestination(isLegalDestination);
            squareViewModel.SetKeyboardFocused(isKeyboardFocused);
        }
    }
}
