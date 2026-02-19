using Avalonia;
using Avalonia.Media;

namespace Chess.Tests;

internal sealed class TestImage : IImage
{
    public Size Size => new(1, 1);

    public void Draw(DrawingContext context, Rect sourceRect, Rect destRect)
    {
    }
}
