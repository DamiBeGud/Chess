using System;
using Chess.Domain;

namespace Chess.UI.Assets;

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
