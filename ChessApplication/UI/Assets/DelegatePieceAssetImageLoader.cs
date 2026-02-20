using System;
using Avalonia.Media;

namespace Chess.UI.Assets;

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
