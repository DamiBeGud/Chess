using System.IO;
using Avalonia.Media;

namespace Chess.UI.Assets;

internal interface ISvgImageParser
{
    IImage? Parse(Stream svgStream);
}
