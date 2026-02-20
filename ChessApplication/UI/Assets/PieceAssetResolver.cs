using System;
using Avalonia.Media;
using Chess.Domain;

namespace Chess.UI.Assets;

/// <summary>
/// PieceAssetResolver is a concrete type within the UI/Assets module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include MainWindowViewModel (UI/ViewModels), App (AppShell).
/// Key collaborators are IImage, IPieceAssetUriStrategy, IPieceAssetCache, IPieceAssetImageLoader, IPieceAssetResolver.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindowViewModel (UI/ViewModels), App (AppShell)</para>
/// <para><b>Usage pattern:</b> Presentation components call this type while resolving, parsing, caching, and rendering piece-related visual assets.</para>
/// <para><b>Dependencies/Collaborators:</b> IImage, IPieceAssetUriStrategy, IPieceAssetCache, IPieceAssetImageLoader, IPieceAssetResolver.</para>
/// <para><b>Boundary:</b> This type sits in the UI/Assets UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
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
