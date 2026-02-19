using System;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Chess.Domain;
using Chess.UI.Assets;
using Xunit;

namespace Chess.Tests;

public sealed class PieceAssetResolverTests
{
    [Fact]
    public void Resolve_WhenSvgPrimaryAvailable_UsesPrimaryAsset()
    {
        var svgImage = CreateTestImage();
        var fallbackImage = CreateTestImage();
        var resolver = new PieceAssetResolver(uri =>
        {
            return uri.AbsoluteUri.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                ? svgImage
                : fallbackImage;
        });

        var resolvedAsset = resolver.Resolve(new Piece(PieceType.King, PieceColor.White));

        Assert.NotNull(resolvedAsset);
        Assert.Same(svgImage, resolvedAsset!.Image);
        Assert.False(resolvedAsset.UsedFallback);
        Assert.EndsWith("/white-king.svg", resolvedAsset.AssetUri, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_WhenSvgPrimaryUnavailable_UsesPngFallback()
    {
        var fallbackImage = CreateTestImage();
        var resolver = new PieceAssetResolver(uri =>
        {
            return uri.AbsoluteUri.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                ? fallbackImage
                : null;
        });

        var resolvedAsset = resolver.Resolve(new Piece(PieceType.Queen, PieceColor.Black));

        Assert.NotNull(resolvedAsset);
        Assert.Same(fallbackImage, resolvedAsset!.Image);
        Assert.True(resolvedAsset.UsedFallback);
        Assert.EndsWith("/black-queen.png", resolvedAsset.AssetUri, StringComparison.OrdinalIgnoreCase);
    }

    [AvaloniaFact]
    public void Resolve_WithConfiguredAssets_LoadsProjectPieceAsset()
    {
        var resolver = new PieceAssetResolver();

        var resolvedAsset = resolver.Resolve(new Piece(PieceType.Bishop, PieceColor.White));

        Assert.NotNull(resolvedAsset);
        Assert.NotNull(resolvedAsset!.Image);
        Assert.EndsWith("/white-bishop.svg", resolvedAsset.AssetUri, StringComparison.OrdinalIgnoreCase);
        Assert.False(resolvedAsset.UsedFallback);
    }

    private static IImage CreateTestImage()
    {
        return new TestImage();
    }
}
