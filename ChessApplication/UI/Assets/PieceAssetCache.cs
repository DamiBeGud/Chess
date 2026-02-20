using System.Collections.Generic;
using Chess.Domain;

namespace Chess.UI.Assets;

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
