namespace Chess.UI.Assets;

/// <summary>
/// SvgShapeStyle is a record type within the UI/Assets module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include SvgImageParser (UI/Assets), SvgStyleParser (UI/Assets), ISvgStyleParser (UI/Assets).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> SvgImageParser (UI/Assets), SvgStyleParser (UI/Assets), ISvgStyleParser (UI/Assets)</para>
/// <para><b>Usage pattern:</b> Presentation components call this type while resolving, parsing, caching, and rendering piece-related visual assets.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the UI/Assets UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal readonly record struct SvgShapeStyle(string? Fill, double? Opacity)
{
    public SvgShapeStyle Merge(SvgShapeStyle other)
    {
        return new SvgShapeStyle(
            Fill: other.Fill ?? Fill,
            Opacity: other.Opacity ?? Opacity);
    }
}
