using System.Xml.Linq;
using Chess.UI.Assets;
using Xunit;

namespace Chess.Tests;

public sealed class SvgStyleParserTests
{
    [Fact]
    public void ParseStyles_WhenCssContainsClassRules_ParsesAndMergesClassStyles()
    {
        var parser = new SvgStyleParser();
        var root = XElement.Parse(
            """
            <svg xmlns="http://www.w3.org/2000/svg">
              <style>
                .piece, .accent { fill: #112233; opacity: 0.4; }
                .accent { opacity: 0.8; }
              </style>
            </svg>
            """);

        var styles = parser.ParseStyles(root);

        Assert.True(styles.TryGetValue("piece", out var pieceStyle));
        Assert.Equal("#112233", pieceStyle.Fill);
        Assert.Equal(0.4d, pieceStyle.Opacity);

        Assert.True(styles.TryGetValue("accent", out var accentStyle));
        Assert.Equal("#112233", accentStyle.Fill);
        Assert.Equal(0.8d, accentStyle.Opacity);
    }

    [Fact]
    public void ParseStyles_WhenNoStyleElementExists_ReturnsEmptyDictionary()
    {
        var parser = new SvgStyleParser();
        var root = XElement.Parse("<svg xmlns=\"http://www.w3.org/2000/svg\"><path d=\"M0 0 Z\" /></svg>");

        var styles = parser.ParseStyles(root);

        Assert.Empty(styles);
    }
}
