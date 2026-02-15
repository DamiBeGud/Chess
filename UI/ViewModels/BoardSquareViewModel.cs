using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;
using Chess.Domain;
using Chess.UI.Assets;

namespace Chess.UI.ViewModels;

public sealed class BoardSquareViewModel : INotifyPropertyChanged
{
    private static readonly IBrush LightSquareBrush = Brush.Parse("#F2E4CB");
    private static readonly IBrush DarkSquareBrush = Brush.Parse("#9E6B43");
    private static readonly IBrush SelectedSquareBrush = Brush.Parse("#F6D76A");
    private static readonly IBrush LegalDestinationBrush = Brush.Parse("#88C34A");
    private static readonly IBrush DefaultBorderBrush = Brush.Parse("#4A3322");
    private static readonly IBrush FocusedBorderBrush = Brush.Parse("#1E4ED8");
    private static readonly Thickness DefaultBorderThickness = new(0);
    private static readonly Thickness FocusedBorderThickness = new(3);

    private readonly IPieceAssetResolver _pieceAssetResolver;
    private Piece? _piece;
    private IImage? _pieceImage;
    private string _pieceAssetUri = string.Empty;
    private bool _isUsingFallbackAsset;
    private bool _isSelected;
    private bool _isLegalDestination;
    private bool _isKeyboardFocused;

    public BoardSquareViewModel(Square square, ICommand clickCommand, IPieceAssetResolver pieceAssetResolver)
    {
        System.ArgumentNullException.ThrowIfNull(clickCommand);
        System.ArgumentNullException.ThrowIfNull(pieceAssetResolver);
        Square = square;
        IsLightSquare = (square.File + square.Rank) % 2 != 0;
        CoordinateLabel = $"{(char)('a' + square.File)}{square.Rank + 1}";
        ClickCommand = clickCommand;
        _pieceAssetResolver = pieceAssetResolver;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Square Square { get; }

    public bool IsLightSquare { get; }

    public string CoordinateLabel { get; }

    public ICommand ClickCommand { get; }

    public IImage? PieceImage => _pieceImage;

    public string PieceAssetUri => _pieceAssetUri;

    public bool IsUsingFallbackAsset => _isUsingFallbackAsset;

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

        var resolvedAsset = _piece is null ? null : _pieceAssetResolver.Resolve(_piece);
        SetPieceAsset(resolvedAsset);

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

    private void SetPieceAsset(ResolvedPieceAsset? resolvedAsset)
    {
        var nextImage = resolvedAsset?.Image;
        var nextAssetUri = resolvedAsset?.AssetUri ?? string.Empty;
        var nextIsUsingFallbackAsset = resolvedAsset?.UsedFallback ?? false;

        if (ReferenceEquals(_pieceImage, nextImage)
            && string.Equals(_pieceAssetUri, nextAssetUri, StringComparison.Ordinal)
            && _isUsingFallbackAsset == nextIsUsingFallbackAsset)
        {
            return;
        }

        _pieceImage = nextImage;
        _pieceAssetUri = nextAssetUri;
        _isUsingFallbackAsset = nextIsUsingFallbackAsset;
        OnPropertyChanged(nameof(PieceImage));
        OnPropertyChanged(nameof(PieceAssetUri));
        OnPropertyChanged(nameof(IsUsingFallbackAsset));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
