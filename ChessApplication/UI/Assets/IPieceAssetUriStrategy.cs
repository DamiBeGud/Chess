using System;
using Chess.Domain;

namespace Chess.UI.Assets;

internal interface IPieceAssetUriStrategy
{
    Uri BuildPrimaryUri(PieceColor color, PieceType type);

    Uri BuildFallbackUri(PieceColor color, PieceType type);
}
