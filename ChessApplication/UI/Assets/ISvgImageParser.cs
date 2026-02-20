using System.IO;
using Avalonia.Media;

namespace Chess.UI.Assets;

/// <summary>
/// ISvgImageParser defines a contract within the UI/Assets module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include PieceAssetImageLoader (UI/Assets), SvgImageParser (UI/Assets).
/// Key collaborators are Implementations include SvgImageParser (UI/Assets).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> PieceAssetImageLoader (UI/Assets), SvgImageParser (UI/Assets)</para>
/// <para><b>Usage pattern:</b> Presentation components call this type while resolving, parsing, caching, and rendering piece-related visual assets.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include SvgImageParser (UI/Assets).</para>
/// <para><b>Boundary:</b> This type sits in the UI/Assets UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal interface ISvgImageParser
{
    IImage? Parse(Stream svgStream);
}
