using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Chess.Domain;

namespace Chess.UI.Assets;

public sealed class PieceAssetResolver : IPieceAssetResolver
{
    private const string AssetBasePath = "avares://Chess/Assets/Pieces/ClassicOutline";
    private readonly Func<Uri, IImage?> _imageLoader;
    private readonly Dictionary<(PieceColor Color, PieceType Type), ResolvedPieceAsset?> _cache = [];

    public PieceAssetResolver()
        : this(LoadImage)
    {
    }

    public PieceAssetResolver(Func<Uri, IImage?> imageLoader)
    {
        ArgumentNullException.ThrowIfNull(imageLoader);
        _imageLoader = imageLoader;
    }

    public ResolvedPieceAsset? Resolve(Piece piece)
    {
        var key = (piece.Color, piece.Type);

        if (_cache.TryGetValue(key, out var cachedAsset))
        {
            return cachedAsset;
        }

        var primaryUri = BuildUri(key.Color, key.Type, ".svg");
        var fallbackUri = BuildUri(key.Color, key.Type, ".png");
        var resolvedAsset = ResolveWithFallback(primaryUri, fallbackUri);

        _cache[key] = resolvedAsset;
        return resolvedAsset;
    }

    private ResolvedPieceAsset? ResolveWithFallback(Uri primaryUri, Uri fallbackUri)
    {
        var primaryImage = _imageLoader(primaryUri);
        if (primaryImage is not null)
        {
            return new ResolvedPieceAsset(primaryImage, primaryUri.AbsoluteUri, false);
        }

        var fallbackImage = _imageLoader(fallbackUri);
        if (fallbackImage is not null)
        {
            return new ResolvedPieceAsset(fallbackImage, fallbackUri.AbsoluteUri, true);
        }

        return null;
    }

    private static Uri BuildUri(PieceColor color, PieceType type, string extension)
    {
        var colorToken = color.ToString().ToLowerInvariant();
        var pieceToken = type.ToString().ToLowerInvariant();
        return new Uri($"{AssetBasePath}/{colorToken}-{pieceToken}{extension}");
    }

    private static IImage? LoadImage(Uri assetUri)
    {
        if (!AssetLoader.Exists(assetUri))
        {
            return null;
        }

        try
        {
            return assetUri.AbsoluteUri.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                ? LoadSvgImage(assetUri)
                : LoadBitmapImage(assetUri);
        }
        catch
        {
            return null;
        }
    }

    private static IImage? LoadBitmapImage(Uri assetUri)
    {
        using var stream = AssetLoader.Open(assetUri);
        return new Bitmap(stream);
    }

    private static IImage? LoadSvgImage(Uri assetUri)
    {
        using var stream = AssetLoader.Open(assetUri);
        var document = XDocument.Load(stream);
        var root = document.Root;

        if (root is null)
        {
            return null;
        }

        var stylesByClass = ParseStyles(root);
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

    private static GeometryDrawing? BuildDrawing(XElement shapeElement, IReadOnlyDictionary<string, SvgShapeStyle> stylesByClass)
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

    private static SvgShapeStyle ResolveStyle(XElement shapeElement, IReadOnlyDictionary<string, SvgShapeStyle> stylesByClass)
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

    private static IReadOnlyDictionary<string, SvgShapeStyle> ParseStyles(XElement root)
    {
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

    private readonly record struct SvgShapeStyle(string? Fill, double? Opacity)
    {
        public SvgShapeStyle Merge(SvgShapeStyle other)
        {
            return new SvgShapeStyle(
                Fill: other.Fill ?? Fill,
                Opacity: other.Opacity ?? Opacity);
        }
    }
}
