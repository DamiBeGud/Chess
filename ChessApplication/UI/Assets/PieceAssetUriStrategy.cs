using System;
using Chess.Domain;

namespace Chess.UI.Assets;

/// <summary>
/// PieceAssetUriStrategy is a concrete type within the UI/Assets module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include PieceAssetResolver (UI/Assets).
/// Key collaborators are IPieceAssetUriStrategy.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> PieceAssetResolver (UI/Assets)</para>
/// <para><b>Usage pattern:</b> Presentation components call this type while resolving, parsing, caching, and rendering piece-related visual assets.</para>
/// <para><b>Dependencies/Collaborators:</b> IPieceAssetUriStrategy.</para>
/// <para><b>Boundary:</b> This type sits in the UI/Assets UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal sealed class PieceAssetUriStrategy : IPieceAssetUriStrategy
{
    private const string AssetBasePath = "avares://Chess/Assets/Pieces/ClassicOutline";

    public Uri BuildPrimaryUri(PieceColor color, PieceType type)
    {
        return BuildUri(color, type, ".svg");
    }

    public Uri BuildFallbackUri(PieceColor color, PieceType type)
    {
        return BuildUri(color, type, ".png");
    }

    private static Uri BuildUri(PieceColor color, PieceType type, string extension)
    {
        var colorToken = color.ToString().ToLowerInvariant();
        var pieceToken = type.ToString().ToLowerInvariant();
        return new Uri($"{AssetBasePath}/{colorToken}-{pieceToken}{extension}");
    }
}
