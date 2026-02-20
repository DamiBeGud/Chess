using System;
using System.Collections.Generic;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Chess.Domain;
using Chess.UI.Assets;

namespace Chess.UI.ViewModels;

/// <summary>
/// MoveHistoryEntryViewModel is a concrete type within the UI/ViewModels module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include MainWindowTextFormatter (UI/Services), MainWindowViewModel (UI/ViewModels), IMainWindowTextFormatter (UI/Services).
/// Key collaborators are string, PieceType, PieceColor, ResolvedPieceAsset.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindowTextFormatter (UI/Services), MainWindowViewModel (UI/ViewModels), IMainWindowTextFormatter (UI/Services)</para>
/// <para><b>Usage pattern:</b> Avalonia bindings read from this type and invoke its commands; it then coordinates downstream services and updates presentation state.</para>
/// <para><b>Dependencies/Collaborators:</b> string, PieceType, PieceColor, ResolvedPieceAsset.</para>
/// <para><b>Boundary:</b> This type sits in the UI/ViewModels UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
public sealed class MoveHistoryEntryViewModel
{
    private const string DeterministicIconAssetBasePath = "avares://Chess/Assets/Pieces/ClassicOutline";
    private const string WhiteSideColorHex = "#F7F3EA";
    private const string WhiteSideBorderHex = "#B9A98D";
    private const string WhiteSideForegroundHex = "#1F2937";
    private const string BlackSideColorHex = "#2B2B2B";
    private const string BlackSideBorderHex = "#0F0F0F";
    private const string BlackSideForegroundHex = "#F7F3EA";
    private static readonly object DeterministicIconCacheSync = new();
    private static readonly Dictionary<(PieceColor Side, PieceType PieceType), ResolvedPieceAsset?> DeterministicIconCache = [];
    private static readonly IBrush WhiteSideColorBrush = Brush.Parse(WhiteSideColorHex);
    private static readonly IBrush WhiteSideBorderBrush = Brush.Parse(WhiteSideBorderHex);
    private static readonly IBrush WhiteSideForegroundBrush = Brush.Parse(WhiteSideForegroundHex);
    private static readonly IBrush BlackSideColorBrush = Brush.Parse(BlackSideColorHex);
    private static readonly IBrush BlackSideBorderBrush = Brush.Parse(BlackSideBorderHex);
    private static readonly IBrush BlackSideForegroundBrush = Brush.Parse(BlackSideForegroundHex);

    public MoveHistoryEntryViewModel(
        string movePrefix,
        PieceType movedPieceType,
        PieceColor side,
        string notation,
        ResolvedPieceAsset? resolvedPieceAsset)
    {
        if (string.IsNullOrWhiteSpace(movePrefix))
        {
            throw new ArgumentException("Move history prefix is required.", nameof(movePrefix));
        }

        if (string.IsNullOrWhiteSpace(notation))
        {
            throw new ArgumentException("Move notation is required.", nameof(notation));
        }

        MovePrefix = movePrefix;
        MovedPieceType = movedPieceType;
        Side = side;
        Notation = notation;

        var iconAsset = resolvedPieceAsset ?? ResolveDeterministicIconAsset(side, movedPieceType);
        PieceIconImage = iconAsset?.Image;
        PieceIconAssetUri = iconAsset?.AssetUri ?? string.Empty;
        IsUsingRasterFallbackAsset = iconAsset?.UsedFallback ?? false;
        IsUsingDeterministicFallbackIcon = resolvedPieceAsset is null && iconAsset is not null;
    }

    public string MovePrefix { get; }

    public PieceType MovedPieceType { get; }

    public PieceColor Side { get; }

    public string Notation { get; }

    public IImage? PieceIconImage { get; }

    public string PieceIconAssetUri { get; }

    public bool HasPieceIconImage => PieceIconImage is not null;

    public bool IsUsingRasterFallbackAsset { get; }

    public bool IsUsingDeterministicFallbackIcon { get; }

    public IBrush SideColorBrush => Side == PieceColor.White ? WhiteSideColorBrush : BlackSideColorBrush;

    public IBrush SideBorderBrush => Side == PieceColor.White ? WhiteSideBorderBrush : BlackSideBorderBrush;

    public IBrush SideForegroundBrush => Side == PieceColor.White ? WhiteSideForegroundBrush : BlackSideForegroundBrush;

    public string SideColorHex => Side == PieceColor.White ? WhiteSideColorHex : BlackSideColorHex;

    public string SideForegroundHex => Side == PieceColor.White ? WhiteSideForegroundHex : BlackSideForegroundHex;

    public override string ToString()
    {
        return $"{MovePrefix} {Notation}";
    }

    private static ResolvedPieceAsset? ResolveDeterministicIconAsset(PieceColor side, PieceType pieceType)
    {
        var key = (side, pieceType);

        lock (DeterministicIconCacheSync)
        {
            if (DeterministicIconCache.TryGetValue(key, out var cachedAsset))
            {
                return cachedAsset;
            }

            var deterministicAsset = LoadDeterministicIconAsset(side, pieceType);
            DeterministicIconCache[key] = deterministicAsset;
            return deterministicAsset;
        }
    }

    private static ResolvedPieceAsset? LoadDeterministicIconAsset(PieceColor side, PieceType pieceType)
    {
        var colorToken = side.ToString().ToLowerInvariant();
        var pieceToken = pieceType.ToString().ToLowerInvariant();
        var deterministicIconUri = new Uri($"{DeterministicIconAssetBasePath}/{colorToken}-{pieceToken}.png");

        bool exists;
        try
        {
            exists = AssetLoader.Exists(deterministicIconUri);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        if (!exists)
        {
            return null;
        }

        try
        {
            using var stream = AssetLoader.Open(deterministicIconUri);
            var bitmap = new Bitmap(stream);
            return new ResolvedPieceAsset(bitmap, deterministicIconUri.AbsoluteUri, true);
        }
        catch
        {
            return null;
        }
    }
}
