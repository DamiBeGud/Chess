using Avalonia.Media;

namespace Chess.UI.Assets;

public sealed record ResolvedPieceAsset(IImage Image, string AssetUri, bool UsedFallback);
