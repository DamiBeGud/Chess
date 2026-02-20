using System;
using Avalonia.Media;
using Chess.Domain;

namespace Chess.UI.Assets;

public sealed class PieceAssetResolver : IPieceAssetResolver
{
    private readonly IPieceAssetUriStrategy _uriStrategy;
    private readonly IPieceAssetCache _cache;
    private readonly IPieceAssetImageLoader _imageLoader;

    public PieceAssetResolver()
        : this(
            new PieceAssetUriStrategy(),
            new PieceAssetCache(),
            new PieceAssetImageLoader(new SvgImageParser(new SvgStyleParser())))
    {
    }

    public PieceAssetResolver(Func<Uri, IImage?> imageLoader)
        : this(
            new PieceAssetUriStrategy(),
            new PieceAssetCache(),
            new DelegatePieceAssetImageLoader(imageLoader))
    {
    }

    internal PieceAssetResolver(
        IPieceAssetUriStrategy uriStrategy,
        IPieceAssetCache cache,
        IPieceAssetImageLoader imageLoader)
    {
        _uriStrategy = uriStrategy ?? throw new ArgumentNullException(nameof(uriStrategy));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _imageLoader = imageLoader ?? throw new ArgumentNullException(nameof(imageLoader));
    }

    public ResolvedPieceAsset? Resolve(Piece piece)
    {
        var key = (piece.Color, piece.Type);

        if (_cache.TryGet(key, out var cachedAsset))
        {
            return cachedAsset;
        }

        var primaryUri = _uriStrategy.BuildPrimaryUri(key.Color, key.Type);
        var fallbackUri = _uriStrategy.BuildFallbackUri(key.Color, key.Type);
        var resolvedAsset = ResolveWithFallback(primaryUri, fallbackUri);

        _cache.Set(key, resolvedAsset);
        return resolvedAsset;
    }

    private ResolvedPieceAsset? ResolveWithFallback(Uri primaryUri, Uri fallbackUri)
    {
        var primaryImage = _imageLoader.Load(primaryUri);
        if (primaryImage is not null)
        {
            return new ResolvedPieceAsset(primaryImage, primaryUri.AbsoluteUri, false);
        }

        var fallbackImage = _imageLoader.Load(fallbackUri);
        if (fallbackImage is not null)
        {
            return new ResolvedPieceAsset(fallbackImage, fallbackUri.AbsoluteUri, true);
        }

        return null;
    }
}
