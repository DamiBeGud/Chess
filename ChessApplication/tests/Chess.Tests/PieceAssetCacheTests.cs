using Chess.Domain;
using Chess.UI.Assets;
using Xunit;

namespace Chess.Tests;

public sealed class PieceAssetCacheTests
{
    [Fact]
    public void Set_WhenAssetIsResolved_TryGetReturnsStoredAsset()
    {
        var cache = new PieceAssetCache();
        var key = (PieceColor.White, PieceType.King);
        var expected = new ResolvedPieceAsset(new TestImage(), "avares://test/white-king.svg", false);

        cache.Set(key, expected);

        var found = cache.TryGet(key, out var actual);

        Assert.True(found);
        Assert.Same(expected, actual);
    }

    [Fact]
    public void Set_WhenResolutionFailed_TryGetReturnsCachedNull()
    {
        var cache = new PieceAssetCache();
        var key = (PieceColor.Black, PieceType.Queen);

        cache.Set(key, null);

        var found = cache.TryGet(key, out var actual);

        Assert.True(found);
        Assert.Null(actual);
    }

    [Fact]
    public void TryGet_WhenKeyMissing_ReturnsFalse()
    {
        var cache = new PieceAssetCache();

        var found = cache.TryGet((PieceColor.White, PieceType.Bishop), out var actual);

        Assert.False(found);
        Assert.Null(actual);
    }
}
