using System.IO;
using System.Text;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Chess.UI.Assets;
using Xunit;

namespace Chess.Tests;

public sealed class SvgImageParserTests
{
    [AvaloniaFact]
    public void Parse_WhenSvgContainsRenderableShapes_ReturnsDrawingImage()
    {
        var parser = new SvgImageParser(new SvgStyleParser());
        using var svgStream = CreateStream(
            """
            <svg xmlns="http://www.w3.org/2000/svg">
              <path d="M0 0 L10 0 L10 10 Z" fill="#000000" />
            </svg>
            """);

        var image = parser.Parse(svgStream);

        var drawingImage = Assert.IsType<DrawingImage>(image);
        var drawingGroup = Assert.IsType<DrawingGroup>(drawingImage.Drawing);
        Assert.Single(drawingGroup.Children);
    }

    [AvaloniaFact]
    public void Parse_WhenSvgContainsNoRenderableShapes_ReturnsNull()
    {
        var parser = new SvgImageParser(new SvgStyleParser());
        using var svgStream = CreateStream(
            """
            <svg xmlns="http://www.w3.org/2000/svg">
              <path d="M0 0 L10 0 L10 10 Z" fill="none" />
            </svg>
            """);

        var image = parser.Parse(svgStream);

        Assert.Null(image);
    }

    private static MemoryStream CreateStream(string xml)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(xml));
    }
}
