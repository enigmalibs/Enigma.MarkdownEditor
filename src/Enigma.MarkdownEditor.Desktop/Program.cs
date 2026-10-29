// Using directives stay at file scope, above the namespace declaration, throughout this project:
// inside a namespace beginning with "Enigma.", the simple name "Avalonia" resolves against the
// referenced Enigma.Avalonia.* assemblies instead of Avalonia itself. See docs/plan/FEATURE-0F9A.md.
using System;
using Avalonia;
using Enigma.MarkdownEditor.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop;

/// <summary>
/// Application entry point.
/// </summary>
public static class Program
{
    /// <summary>
    /// Starts the Avalonia classic desktop lifetime. Deliberately synchronous: the lifetime owns the
    /// message loop, so awaiting a host here would return before a window was ever shown. The DI
    /// container is composed in <see cref="App.OnFrameworkInitializationCompleted"/> instead.
    /// </summary>
    /// <param name="args">Command-line arguments, forwarded to the Avalonia lifetime.</param>
    /// <remarks>
    /// The process-wide unhandled-exception hooks come first, so a failure anywhere in the start is
    /// caught too; what is caught before the crash reporter exists is kept for it.
    /// </remarks>
    [STAThread]
    public static void Main(string[] args)
    {
        UnhandledExceptionHooks.Install();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Builds the Avalonia application. Also used by the XAML previewer, which bypasses
    /// <see cref="Main"/> and calls this method by convention — keep it public and parameterless.
    /// </summary>
    /// <returns>The configured <see cref="AppBuilder"/>.</returns>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            // Avalonia 12 replacement for the removed Avalonia.Diagnostics / this.AttachDevTools().
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
