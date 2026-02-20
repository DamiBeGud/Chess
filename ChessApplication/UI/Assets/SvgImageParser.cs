using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Avalonia.Media;

namespace Chess.UI.Assets;

/// <summary>
/// SvgImageParser is a concrete type within the UI/Assets module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Primary production consumers include PieceAssetResolver (UI/Assets).
/// Key collaborators are ISvgStyleParser, ISvgImageParser.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> PieceAssetResolver (UI/Assets)</para>
/// <para><b>Usage pattern:</b> Presentation components call this type while resolving, parsing, caching, and rendering piece-related visual assets.</para>
/// <para><b>Dependencies/Collaborators:</b> ISvgStyleParser, ISvgImageParser.</para>
/// <para><b>Boundary:</b> This type sits in the UI/Assets UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal sealed class SvgImageParser : ISvgImageParser
{
    private readonly ISvgStyleParser _styleParser;

    internal SvgImageParser(ISvgStyleParser styleParser)
    {
        _styleParser = styleParser ?? throw new ArgumentNullException(nameof(styleParser));
    }

    public IImage? Parse(Stream svgStream)
    {
        ArgumentNullException.ThrowIfNull(svgStream);

        var document = XDocument.Load(svgStream);
        var root = document.Root;

        if (root is null)
        {
            return null;
        }

        var stylesByClass = _styleParser.ParseStyles(root);
        var drawingGroup = new DrawingGroup();

        foreach (var shapeElement in root.Descendants().Where(IsSupportedShape))
        {
            var drawing = BuildDrawing(shapeElement, stylesByClass);
            if (drawing is not null)
            {
                drawingGroup.Children.Add(drawing);
            }
        }

        if (drawingGroup.Children.Count == 0)
        {
            return null;
        }

        return new DrawingImage
        {
            Drawing = drawingGroup
        };
    }

    private static bool IsSupportedShape(XElement element)
    {
        return element.Name.LocalName switch
        {
            "path" => true,
            "rect" => true,
            "circle" => true,
            _ => false
        };
    }

    private static GeometryDrawing? BuildDrawing(XElement shapeElement, System.Collections.Generic.IReadOnlyDictionary<string, SvgShapeStyle> stylesByClass)
    {
        var style = ResolveStyle(shapeElement, stylesByClass);
        var brush = BuildBrush(style);
        if (brush is null)
        {
            return null;
        }

        var geometry = shapeElement.Name.LocalName switch
        {
            "path" => BuildPathGeometry(shapeElement),
            "rect" => BuildRectangleGeometry(shapeElement),
            "circle" => BuildCircleGeometry(shapeElement),
            _ => null
        };

        if (geometry is null)
        {
            return null;
        }

        return new GeometryDrawing
        {
            Geometry = geometry,
            Brush = brush
        };
    }

    private static Geometry? BuildPathGeometry(XElement shapeElement)
    {
        var pathData = (string?)shapeElement.Attribute("d");
        if (string.IsNullOrWhiteSpace(pathData))
        {
            return null;
        }

        return Geometry.Parse(pathData);
    }

    private static Geometry? BuildRectangleGeometry(XElement shapeElement)
    {
        var width = GetDoubleAttribute(shapeElement, "width", required: true);
        var height = GetDoubleAttribute(shapeElement, "height", required: true);

        if (width is null || height is null || width <= 0 || height <= 0)
        {
            return null;
        }

        var x = GetDoubleAttribute(shapeElement, "x", required: false) ?? 0d;
        var y = GetDoubleAttribute(shapeElement, "y", required: false) ?? 0d;
        var rx = GetDoubleAttribute(shapeElement, "rx", required: false) ?? 0d;
        var ry = GetDoubleAttribute(shapeElement, "ry", required: false) ?? 0d;

        var rect = new Avalonia.Rect(x, y, width.Value, height.Value);
        return rx > 0 || ry > 0
            ? new RectangleGeometry(rect, rx, ry)
            : new RectangleGeometry(rect);
    }

    private static Geometry? BuildCircleGeometry(XElement shapeElement)
    {
        var cx = GetDoubleAttribute(shapeElement, "cx", required: true);
        var cy = GetDoubleAttribute(shapeElement, "cy", required: true);
        var radius = GetDoubleAttribute(shapeElement, "r", required: true);

        if (cx is null || cy is null || radius is null || radius <= 0)
        {
            return null;
        }

        var diameter = radius.Value * 2d;
        return new EllipseGeometry(new Avalonia.Rect(cx.Value - radius.Value, cy.Value - radius.Value, diameter, diameter));
    }

    private static IBrush? BuildBrush(SvgShapeStyle style)
    {
        if (string.IsNullOrWhiteSpace(style.Fill))
        {
            return null;
        }

        if (string.Equals(style.Fill, "none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var opacity = Math.Clamp(style.Opacity ?? 1d, 0d, 1d);
        if (opacity <= 0d)
        {
            return null;
        }

        try
        {
            var color = Color.Parse(style.Fill);
            return new SolidColorBrush(color, opacity);
        }
        catch
        {
            return null;
        }
    }

    private static SvgShapeStyle ResolveStyle(
        XElement shapeElement,
        System.Collections.Generic.IReadOnlyDictionary<string, SvgShapeStyle> stylesByClass)
    {
        var style = default(SvgShapeStyle);
        var classAttribute = (string?)shapeElement.Attribute("class");

        if (!string.IsNullOrWhiteSpace(classAttribute))
        {
            foreach (var className in classAttribute.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (stylesByClass.TryGetValue(className, out var classStyle))
                {
                    style = style.Merge(classStyle);
                }
            }
        }

        var fillAttribute = (string?)shapeElement.Attribute("fill");
        var opacityAttribute = (string?)shapeElement.Attribute("opacity");

        if (!string.IsNullOrWhiteSpace(fillAttribute))
        {
            style = style with { Fill = fillAttribute.Trim() };
        }

        if (!string.IsNullOrWhiteSpace(opacityAttribute)
            && double.TryParse(opacityAttribute, NumberStyles.Float, CultureInfo.InvariantCulture, out var opacity))
        {
            style = style with { Opacity = opacity };
        }

        return style;
    }

    private static double? GetDoubleAttribute(XElement element, string name, bool required)
    {
        var rawValue = (string?)element.Attribute(name);
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return required ? null : 0d;
        }

        if (double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedValue))
        {
            return parsedValue;
        }

        return null;
    }
}
