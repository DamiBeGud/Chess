using System.Collections.Generic;
using Chess.Domain;

namespace Chess.UI.Assets;

/// <summary>
/// PieceAssetCache is a concrete type within the UI/Assets module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include PieceAssetResolver (UI/Assets).
/// Key collaborators are IPieceAssetCache.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> PieceAssetResolver (UI/Assets)</para>
/// <para><b>Usage pattern:</b> Presentation components call this type while resolving, parsing, caching, and rendering piece-related visual assets.</para>
/// <para><b>Dependencies/Collaborators:</b> IPieceAssetCache.</para>
/// <para><b>Boundary:</b> This type sits in the UI/Assets UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal sealed class PieceAssetCache : IPieceAssetCache
{
    private readonly Dictionary<(PieceColor Color, PieceType Type), ResolvedPieceAsset?> _cache = [];

    public bool TryGet((PieceColor Color, PieceType Type) key, out ResolvedPieceAsset? asset)
    {
        return _cache.TryGetValue(key, out asset);
    }

    public void Set((PieceColor Color, PieceType Type) key, ResolvedPieceAsset? asset)
    {
        _cache[key] = asset;
    }
}
