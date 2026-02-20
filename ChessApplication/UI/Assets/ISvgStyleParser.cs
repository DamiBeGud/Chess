using System.Collections.Generic;
using System.Xml.Linq;

namespace Chess.UI.Assets;

/// <summary>
/// ISvgStyleParser defines a contract within the UI/Assets module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include SvgImageParser (UI/Assets), SvgStyleParser (UI/Assets).
/// Key collaborators are Implementations include SvgStyleParser (UI/Assets).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> SvgImageParser (UI/Assets), SvgStyleParser (UI/Assets)</para>
/// <para><b>Usage pattern:</b> Presentation components call this type while resolving, parsing, caching, and rendering piece-related visual assets.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include SvgStyleParser (UI/Assets).</para>
/// <para><b>Boundary:</b> This type sits in the UI/Assets UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal interface ISvgStyleParser
{
    IReadOnlyDictionary<string, SvgShapeStyle> ParseStyles(XElement root);
}
