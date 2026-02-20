namespace Chess.UI.Assets;

internal readonly record struct SvgShapeStyle(string? Fill, double? Opacity)
{
    public SvgShapeStyle Merge(SvgShapeStyle other)
    {
        return new SvgShapeStyle(
            Fill: other.Fill ?? Fill,
            Opacity: other.Opacity ?? Opacity);
    }
}
