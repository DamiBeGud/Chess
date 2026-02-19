using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;

[assembly: AvaloniaTestApplication(typeof(Chess.Tests.AvaloniaTestApplication))]

namespace Chess.Tests;

public static class AvaloniaTestApplication
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<Chess.App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = true
            })
            .WithInterFont();
    }
}
