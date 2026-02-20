using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace Chess.UI.Assets;

/// <summary>
/// SvgStyleParser is a concrete type within the UI/Assets module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include PieceAssetResolver (UI/Assets).
/// Key collaborators are ISvgStyleParser.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> PieceAssetResolver (UI/Assets)</para>
/// <para><b>Usage pattern:</b> Presentation components call this type while resolving, parsing, caching, and rendering piece-related visual assets.</para>
/// <para><b>Dependencies/Collaborators:</b> ISvgStyleParser.</para>
/// <para><b>Boundary:</b> This type sits in the UI/Assets UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal sealed class SvgStyleParser : ISvgStyleParser
{
    public IReadOnlyDictionary<string, SvgShapeStyle> ParseStyles(XElement root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var styleElements = root.Descendants().Where(element => element.Name.LocalName == "style");
        var stylesByClass = new Dictionary<string, SvgShapeStyle>(StringComparer.Ordinal);

        foreach (var styleElement in styleElements)
        {
            var css = styleElement.Value;
            var blocks = css.Split('}', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var block in blocks)
            {
                var separatorIndex = block.IndexOf('{');
                if (separatorIndex < 0)
                {
                    continue;
                }

                var selectors = block[..separatorIndex];
                var declarations = block[(separatorIndex + 1)..];
                var parsedStyle = ParseDeclarationBlock(declarations);

                foreach (var rawSelector in selectors.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!rawSelector.StartsWith(".", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var className = rawSelector[1..].Trim();
                    if (string.IsNullOrWhiteSpace(className))
                    {
                        continue;
                    }

                    stylesByClass.TryGetValue(className, out var existingStyle);
                    stylesByClass[className] = existingStyle.Merge(parsedStyle);
                }
            }
        }

        return stylesByClass;
    }

    private static SvgShapeStyle ParseDeclarationBlock(string declarations)
    {
        var style = default(SvgShapeStyle);
        var entries = declarations.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var entry in entries)
        {
            var separatorIndex = entry.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var property = entry[..separatorIndex].Trim();
            var rawValue = entry[(separatorIndex + 1)..].Trim();

            if (property.Equals("fill", StringComparison.OrdinalIgnoreCase))
            {
                style = style with { Fill = rawValue };
                continue;
            }

            if (property.Equals("opacity", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var opacity))
            {
                style = style with { Opacity = opacity };
            }
        }

        return style;
    }
}
