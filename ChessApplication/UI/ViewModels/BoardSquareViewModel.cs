using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Avalonia;
using Avalonia.Media;
using Chess.Domain;
using Chess.UI.Assets;

namespace Chess.UI.ViewModels;

/// <summary>
/// BoardSquareViewModel is a concrete type within the UI/ViewModels module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include MainWindowViewModel (UI/ViewModels), MainWindow (AppShell), MainWindowSelectionState (UI/Services).
/// Key collaborators are Square, ICommand, IPieceAssetResolver, INotifyPropertyChanged.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindowViewModel (UI/ViewModels), MainWindow (AppShell), MainWindowSelectionState (UI/Services), IMainWindowSelectionState (UI/Services)</para>
/// <para><b>Usage pattern:</b> Avalonia bindings read from this type and invoke its commands; it then coordinates downstream services and updates presentation state.</para>
/// <para><b>Dependencies/Collaborators:</b> Square, ICommand, IPieceAssetResolver, INotifyPropertyChanged.</para>
/// <para><b>Boundary:</b> This type sits in the UI/ViewModels UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
public sealed class BoardSquareViewModel : INotifyPropertyChanged
{
    private static readonly IBrush LightSquareBrush = Brush.Parse("#F2E4CB");
    private static readonly IBrush DarkSquareBrush = Brush.Parse("#9E6B43");
    private static readonly IBrush SelectedSquareBrush = Brush.Parse("#F6D76A");
    private static readonly IBrush LastMoveHighlightOnLightSquareBrush = Brush.Parse("#e4c45b");
    private static readonly IBrush LastMoveHighlightOnDarkSquareBrush = Brush.Parse("#b08853");
    private static readonly IBrush LegalMoveIndicatorBrush = Brush.Parse("#3e863e");
    private const double LegalMoveIndicatorOpacityValue = 0.6d;
    private const double LegalMoveIndicatorDiameterValue = 40d;
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
    private bool _isLastMoveHighlighted;

    public BoardSquareViewModel(Square square, ICommand clickCommand, IPieceAssetResolver pieceAssetResolver)
    {
        System.ArgumentNullException.ThrowIfNull(clickCommand);
        System.ArgumentNullException.ThrowIfNull(pieceAssetResolver);
        Square = square;
        IsLightSquare = (square.File + square.Rank) % 2 != 0;
        CoordinateLabel = BoardGeometry.ToCoordinate(square);
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
            : _isLastMoveHighlighted
                ? IsLightSquare
                    ? LastMoveHighlightOnLightSquareBrush
                    : LastMoveHighlightOnDarkSquareBrush
                : IsLightSquare
                    ? LightSquareBrush
                    : DarkSquareBrush;

    public bool IsLegalDestinationIndicatorVisible => _isLegalDestination && !_isSelected;

    public IBrush LegalDestinationIndicatorBrush => LegalMoveIndicatorBrush;

    public double LegalDestinationIndicatorOpacity => LegalMoveIndicatorOpacityValue;

    public double LegalDestinationIndicatorDiameter => LegalMoveIndicatorDiameterValue;

    public IBrush BorderBrush => _isKeyboardFocused ? FocusedBorderBrush : DefaultBorderBrush;

    public Thickness BorderThickness => _isKeyboardFocused ? FocusedBorderThickness : DefaultBorderThickness;

    public bool IsLastMoveHighlighted => _isLastMoveHighlighted;

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
        OnPropertyChanged(nameof(IsLegalDestinationIndicatorVisible));
    }

    public void SetLegalDestination(bool isLegalDestination)
    {
        if (_isLegalDestination == isLegalDestination)
        {
            return;
        }

        _isLegalDestination = isLegalDestination;
        OnPropertyChanged(nameof(IsLegalDestinationIndicatorVisible));
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

    public void SetLastMoveHighlighted(bool isLastMoveHighlighted)
    {
        if (_isLastMoveHighlighted == isLastMoveHighlighted)
        {
            return;
        }

        _isLastMoveHighlighted = isLastMoveHighlighted;
        OnPropertyChanged(nameof(IsLastMoveHighlighted));
        OnPropertyChanged(nameof(Background));
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
