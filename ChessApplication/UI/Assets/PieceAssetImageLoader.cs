using System;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Chess.UI.Assets;

internal sealed class PieceAssetImageLoader : IPieceAssetImageLoader
{
    private readonly ISvgImageParser _svgParser;

    internal PieceAssetImageLoader(ISvgImageParser svgParser)
    {
        _svgParser = svgParser ?? throw new ArgumentNullException(nameof(svgParser));
    }

    public IImage? Load(Uri assetUri)
    {
        ArgumentNullException.ThrowIfNull(assetUri);

        if (!AssetLoader.Exists(assetUri))
        {
            return null;
        }

        try
        {
            using var stream = AssetLoader.Open(assetUri);
            if (assetUri.AbsoluteUri.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                return _svgParser.Parse(stream);
            }

            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }
}
