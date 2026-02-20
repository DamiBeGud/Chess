using Chess.Domain;

namespace Chess.UI.Assets;

internal interface IPieceAssetCache
{
    bool TryGet((PieceColor Color, PieceType Type) key, out ResolvedPieceAsset? asset);

    void Set((PieceColor Color, PieceType Type) key, ResolvedPieceAsset? asset);
}
