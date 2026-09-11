// Using directives stay at file scope, above the namespace declaration, throughout this solution:
// inside a namespace beginning with "Enigma.", the simple name "Avalonia" resolves against the
// referenced Enigma.Avalonia.* assemblies instead of Avalonia itself.
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Enigma.MarkdownEditor.Desktop.UnitTests;

// Every [AvaloniaFact] and [AvaloniaTheory] in this assembly runs on the headless session this
// declares. One session is started for the whole run and shared, so the application object below is
// built once.
[assembly: AvaloniaTestApplication(typeof(HeadlessTestApp))]

namespace Enigma.MarkdownEditor.Desktop.UnitTests;

/// <summary>
/// The <see cref="Application"/> the headless tests run inside: the real application's resource
/// surface, and nothing else of it.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not the real <see cref="App"/>. Its
/// <c>OnFrameworkInitializationCompleted</c> is the application's composition root — it reads the
/// developer's own settings and workspaces files from <c>%AppData%</c>, builds the whole host, starts
/// NLog against the real log file and resolves the main window. A test suite must touch none of
/// that, and a headless lifetime would run all of it before the first assertion.
/// </para>
/// <para>
/// What the tests do need is the resources, because every brush the preview renderer applies is a
/// <c>DynamicResource</c> key: the FluentTheme for the framework's own keys, and the Enigma
/// dictionary — merged the same way and in the same order as in <c>App.axaml</c> — for the palette.
/// With those present a test can assert what the renderer resolves in each theme variant rather than
/// only what shape it built.
/// </para>
/// </remarks>
public sealed class HeadlessTestApp : Application
{
    /// <summary>
    /// Builds the headless application. Called by <c>AvaloniaTestApplication</c> by convention —
    /// keep it public, static and parameterless.
    /// </summary>
    /// <returns>The configured <see cref="AppBuilder"/>.</returns>
    /// <remarks>
    /// Headless drawing is left on: these tests assert the control tree the renderer builds, and
    /// nothing here needs pixels. It is also what keeps the suite off Skia, and therefore off a
    /// second copy of the graphics stack in the test project.
    /// </remarks>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<HeadlessTestApp>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .WithInterFont();

    /// <inheritdoc />
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());

        // AvaloniaEdit's control theme, after FluentTheme and for the reason App.axaml gives: without
        // it the TextEditor has no template at all, so its TextArea never reaches the visual tree and
        // no test can click in it or type into it.
        Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml"),
        });

        // The Enigma dictionary must come after FluentTheme for the same reason App.axaml gives: it
        // overrides keys the Fluent theme defines, and a dictionary merged first would be overridden
        // by it instead.
        Resources.MergedDictionaries.Add(new ResourceInclude((Uri?)null)
        {
            Source = new Uri("avares://Enigma.Avalonia.Desktop/Themes/Fluent.axaml"),
        });
    }
}
