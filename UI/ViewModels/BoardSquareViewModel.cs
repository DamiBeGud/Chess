using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia.Media;
using Chess.Domain;

namespace Chess.UI.ViewModels;

public sealed class BoardSquareViewModel : INotifyPropertyChanged
{
    private static readonly IBrush LightSquareBrush = new SolidColorBrush(Color.Parse("#F0D9B5"));
    private static readonly IBrush DarkSquareBrush = new SolidColorBrush(Color.Parse("#B58863"));
    private static readonly IBrush SelectedSquareBrush = new SolidColorBrush(Color.Parse("#F4E36B"));
    private static readonly IBrush LegalDestinationBrush = new SolidColorBrush(Color.Parse("#A9CF54"));

    private Piece? _piece;
    private bool _isSelected;
    private bool _isLegalDestination;

    public BoardSquareViewModel(Square square, ICommand clickCommand)
    {
        System.ArgumentNullException.ThrowIfNull(clickCommand);
        Square = square;
        IsLightSquare = (square.File + square.Rank) % 2 != 0;
        CoordinateLabel = $"{(char)('a' + square.File)}{square.Rank + 1}";
        ClickCommand = clickCommand;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Square Square { get; }

    public bool IsLightSquare { get; }

    public string CoordinateLabel { get; }

    public ICommand ClickCommand { get; }

    public string PieceGlyph => _piece is null ? string.Empty : GetPieceGlyph(_piece);

    public IBrush Background =>
        _isSelected
            ? SelectedSquareBrush
            : _isLegalDestination
                ? LegalDestinationBrush
                : IsLightSquare
                    ? LightSquareBrush
                    : DarkSquareBrush;

    public void SetPiece(Piece? piece)
    {
        if (_piece == piece)
        {
            return;
        }

        _piece = piece;
        OnPropertyChanged(nameof(PieceGlyph));
    }

    public void SetSelected(bool isSelected)
    {
        if (_isSelected == isSelected)
        {
            return;
        }

        _isSelected = isSelected;
        OnPropertyChanged(nameof(Background));
    }

    public void SetLegalDestination(bool isLegalDestination)
    {
        if (_isLegalDestination == isLegalDestination)
        {
            return;
        }

        _isLegalDestination = isLegalDestination;
        OnPropertyChanged(nameof(Background));
    }

    private static string GetPieceGlyph(Piece piece)
    {
        return (piece.Color, piece.Type) switch
        {
            (PieceColor.White, PieceType.King) => "\u2654",
            (PieceColor.White, PieceType.Queen) => "\u2655",
            (PieceColor.White, PieceType.Rook) => "\u2656",
            (PieceColor.White, PieceType.Bishop) => "\u2657",
            (PieceColor.White, PieceType.Knight) => "\u2658",
            (PieceColor.White, PieceType.Pawn) => "\u2659",
            (PieceColor.Black, PieceType.King) => "\u265A",
            (PieceColor.Black, PieceType.Queen) => "\u265B",
            (PieceColor.Black, PieceType.Rook) => "\u265C",
            (PieceColor.Black, PieceType.Bishop) => "\u265D",
            (PieceColor.Black, PieceType.Knight) => "\u265E",
            (PieceColor.Black, PieceType.Pawn) => "\u265F",
            _ => string.Empty
        };
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
