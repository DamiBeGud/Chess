using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;
using Chess.Domain;

namespace Chess.UI.ViewModels;

public sealed class BoardSquareViewModel : INotifyPropertyChanged
{
    private static readonly IBrush LightSquareBrush = Brush.Parse("#F2E4CB");
    private static readonly IBrush DarkSquareBrush = Brush.Parse("#9E6B43");
    private static readonly IBrush SelectedSquareBrush = Brush.Parse("#F6D76A");
    private static readonly IBrush LegalDestinationBrush = Brush.Parse("#88C34A");
    private static readonly IBrush DefaultBorderBrush = Brush.Parse("#4A3322");
    private static readonly IBrush FocusedBorderBrush = Brush.Parse("#1E4ED8");
    private static readonly IBrush LightCoordinateBrush = Brush.Parse("#4A3322");
    private static readonly IBrush DarkCoordinateBrush = Brush.Parse("#F6ECDD");
    private static readonly Thickness DefaultBorderThickness = new(1);
    private static readonly Thickness FocusedBorderThickness = new(3);

    private Piece? _piece;
    private bool _isSelected;
    private bool _isLegalDestination;
    private bool _isKeyboardFocused;

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

    public IBrush BorderBrush => _isKeyboardFocused ? FocusedBorderBrush : DefaultBorderBrush;

    public Thickness BorderThickness => _isKeyboardFocused ? FocusedBorderThickness : DefaultBorderThickness;

    public IBrush CoordinateForeground => IsLightSquare ? LightCoordinateBrush : DarkCoordinateBrush;

    public string SquareDescription => _piece is null
        ? $"{CoordinateLabel}: empty square"
        : $"{CoordinateLabel}: {_piece.Color} {_piece.Type}";

    public void SetPiece(Piece? piece)
    {
        if (_piece == piece)
        {
            return;
        }

        _piece = piece;
        OnPropertyChanged(nameof(PieceGlyph));
        OnPropertyChanged(nameof(SquareDescription));
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

    public void SetKeyboardFocused(bool isKeyboardFocused)
    {
        if (_isKeyboardFocused == isKeyboardFocused)
        {
            return;
        }

        _isKeyboardFocused = isKeyboardFocused;
        OnPropertyChanged(nameof(BorderBrush));
        OnPropertyChanged(nameof(BorderThickness));
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
