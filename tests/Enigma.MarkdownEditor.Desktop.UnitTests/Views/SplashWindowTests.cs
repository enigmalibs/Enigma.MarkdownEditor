using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Enigma.MarkdownEditor.Core;
using Enigma.MarkdownEditor.Desktop.Views;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Views;

/// <summary>
/// The splash window, through the real markup.
/// </summary>
/// <remarks>
/// What it shows comes from <see cref="AppInfo"/> through <c>x:Static</c> rather than from a
/// ViewModel, so there is nothing to vary and nothing to inject: the assertions are that the markup
/// loads, that the asset behind the icon exists, that the two lines say what the product says, and
/// that the window flags a splash depends on are the ones it was given. Whether it looks right is a
/// manual reading, recorded in the completion doc.
/// </remarks>
public sealed class SplashWindowTests
{
    [AvaloniaFact]
    public void TheSplash_ShowsTheProductAndItsVersion()
    {
        SplashWindow splash = Show();

        string[] texts = [.. splash.GetLogicalDescendants().OfType<TextBlock>().Select(block => block.Text ?? string.Empty)];

        Assert.Contains(AppInfo.DisplayName, texts);
        Assert.Contains(AppInfo.Version, texts);
    }

    [AvaloniaFact]
    public void TheIcon_IsAResourceThatResolves()
    {
        SplashWindow splash = Show();

        // A missing or unreadable AvaloniaResource fails here rather than silently drawing nothing:
        // the Source converter opens the asset as the markup is loaded.
        Image icon = Assert.Single(splash.GetLogicalDescendants().OfType<Image>());

        Assert.NotNull(icon.Source);
    }

    [AvaloniaFact]
    public void TheWindow_IsShapedLikeASplash()
    {
        SplashWindow splash = Show();

        // Each of these is load-bearing: undecorated and centred because it is not a window anyone
        // interacts with, off the taskbar because it is gone before it could be clicked there, and on
        // top because the main window is shown behind it during the hand-over.
        Assert.Equal(WindowDecorations.None, splash.WindowDecorations);
        Assert.Equal(WindowStartupLocation.CenterScreen, splash.WindowStartupLocation);
        Assert.False(splash.ShowInTaskbar);
        Assert.False(splash.CanResize);
        Assert.True(splash.Topmost);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheSurfaceAndItsBorder_ResolveInEitherVariant(bool dark)
    {
        SplashWindow splash = new()
        {
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
        };

        splash.Show();

        // Both are DynamicResource Enigma* keys, so a renamed or missing key resolves to nothing and
        // the splash paints as a bare rectangle. Neither brush being null is what says the palette
        // answered — in both variants, since the splash is shown before anything else could repaint.
        Border frame = Assert.Single(splash.GetLogicalDescendants().OfType<Border>());

        Assert.NotNull(splash.Background);
        Assert.NotNull(frame.BorderBrush);
    }

    private static SplashWindow Show()
    {
        SplashWindow splash = new();

        splash.Show();

        return splash;
    }
}
