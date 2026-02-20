using System;
using Avalonia.Media;

namespace Chess.UI.Assets;

/// <summary>
/// DelegatePieceAssetImageLoader is a concrete type within the UI/Assets module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include PieceAssetResolver (UI/Assets).
/// Key collaborators are IImage, IPieceAssetImageLoader.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> PieceAssetResolver (UI/Assets)</para>
/// <para><b>Usage pattern:</b> Presentation components call this type while resolving, parsing, caching, and rendering piece-related visual assets.</para>
/// <para><b>Dependencies/Collaborators:</b> IImage, IPieceAssetImageLoader.</para>
/// <para><b>Boundary:</b> This type sits in the UI/Assets UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal sealed class DelegatePieceAssetImageLoader : IPieceAssetImageLoader
{
    private readonly Func<Uri, IImage?> _load;

    internal DelegatePieceAssetImageLoader(Func<Uri, IImage?> load)
    {
        _load = load ?? throw new ArgumentNullException(nameof(load));
    }

    public IImage? Load(Uri assetUri)
    {
        return _load(assetUri);
    }
}
