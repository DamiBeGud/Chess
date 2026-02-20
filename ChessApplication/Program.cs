using Avalonia;
using System;

namespace Chess;

/// <summary>
/// Program is a concrete type within the AppShell module.
/// It encapsulates module-specific behavior and exposes operations consumed by adjacent layers.
/// Its production usage is currently local to this module or composed indirectly through surrounding types.
/// No constructor-injected collaborators were detected in this declaration.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> No direct production references outside this declaration file were found.</para>
/// <para><b>Usage pattern:</b> Startup code constructs and wires this type during application initialization and desktop-lifetime setup.</para>
/// <para><b>Dependencies/Collaborators:</b> No constructor-injected collaborators were detected in this declaration.</para>
/// <para><b>Boundary:</b> This type sits in the application shell boundary and participates in startup or desktop lifetime wiring.</para>
/// </remarks>
class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
