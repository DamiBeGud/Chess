using Chess.Domain;

namespace Chess.UI.Assets;

public interface IPieceAssetResolver
{
    ResolvedPieceAsset? Resolve(Piece piece);
}
