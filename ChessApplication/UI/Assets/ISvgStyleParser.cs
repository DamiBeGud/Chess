using System.Collections.Generic;
using System.Xml.Linq;

namespace Chess.UI.Assets;

internal interface ISvgStyleParser
{
    IReadOnlyDictionary<string, SvgShapeStyle> ParseStyles(XElement root);
}
