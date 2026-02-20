using System;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Chess.UI.Assets;

/// <summary>
/// PieceAssetImageLoader is a concrete type within the UI/Assets module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include PieceAssetResolver (UI/Assets).
/// Key collaborators are ISvgImageParser, IPieceAssetImageLoader.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> PieceAssetResolver (UI/Assets)</para>
/// <para><b>Usage pattern:</b> Presentation components call this type while resolving, parsing, caching, and rendering piece-related visual assets.</para>
/// <para><b>Dependencies/Collaborators:</b> ISvgImageParser, IPieceAssetImageLoader.</para>
/// <para><b>Boundary:</b> This type sits in the UI/Assets UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
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
