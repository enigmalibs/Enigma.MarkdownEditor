using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Enigma.Avalonia.Desktop.Controls;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Enigma.MarkdownEditor.Desktop.Views;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Views;

/// <summary>
/// The settings dialog's chrome, through the real view: the well the groups sit in, and the padding
/// around them.
/// </summary>
/// <remarks>
/// The dialog card the <c>ContentDialog</c> host draws is <c>EnigmaSurfaceHigh</c>, which is lighter
/// than the <c>EnigmaSurface</c> the groups are painted with, so the view paints the app's ground
/// under them itself; and the host's template already insets its content by 24, so the view adds no
/// outer margin of its own. Both are markup that a later XAML edit can undo without breaking
/// anything that compiles — which is exactly what these pin down. Whether the contrast reads right
/// in each variant is a manual pass, as in <see cref="SettingsPreviewSectionTests"/>.
/// </remarks>
public sealed class SettingsChromeTests : IDisposable
{
    private readonly TempSettings _settings = new();

    [AvaloniaFact]
    public void TheGroups_SitInAWellOfTheirOwn()
    {
        Border well = Well(Show());

        Assert.Equal(new Thickness(16), well.Padding);
        Assert.Equal(new CornerRadius(8), well.CornerRadius);
        Assert.Equal(new Thickness(1), well.BorderThickness);
    }

    [AvaloniaFact]
    public void TheWell_IsPaintedFromTheThemeRatherThanAColour()
    {
        Border well = Well(Show());

        // Same instance, not just the same colour: a hard-coded brush would compare equal in the
        // variant it was picked in and then stop following the switch.
        Assert.Same(Resource("EnigmaBackgroundBrush", well.ActualThemeVariant), well.Background);
        Assert.Same(Resource("EnigmaBorderSubtleBrush", well.ActualThemeVariant), well.BorderBrush);
    }

    [AvaloniaFact]
    public void TheWell_FollowsAVariantSwitch()
    {
        ThemeVariant? requested = Application.Current!.RequestedThemeVariant;

        try
        {
            Application.Current.RequestedThemeVariant = ThemeVariant.Dark;

            Border well = Well(Show());
            string? dark = well.Background?.ToString();

            Application.Current.RequestedThemeVariant = ThemeVariant.Light;

            Assert.NotNull(dark);
            Assert.NotEqual(dark, well.Background?.ToString());
        }
        finally
        {
            Application.Current.RequestedThemeVariant = requested;
        }
    }

    [AvaloniaFact]
    public void TheGroupPanel_AddsNoMarginOfItsOwn()
    {
        // The regression a future edit is most likely to reintroduce: the template insets the
        // content by 24 and the well by 16, and a margin here would be a third layer of padding.
        StackPanel groups = Assert.IsType<StackPanel>(Well(Show()).Child);

        Assert.Equal(default, groups.Margin);
        Assert.Equal(12, groups.Spacing);
    }

    [AvaloniaFact]
    public void TheFiveGroups_AreStillInsideTheWell()
    {
        StackPanel groups = Assert.IsType<StackPanel>(Well(Show()).Child);

        Assert.Equal(
            ["Appearance", "Editor", "Preview", "Behaviour", "Advanced"],
            groups.GetLogicalDescendants()
                .OfType<SettingsCardExpander>()
                .Select(group => group.Header?.ToString() ?? string.Empty));
    }

    public void Dispose() => _settings.Dispose();

    // The view's own root, rather than a walk: what is asserted is that the groups are wrapped in
    // one, so finding a Border anywhere below would prove nothing.
    private static Border Well(SettingsView view) => Assert.IsType<Border>(view.Content);

    private static IBrush Resource(string key, ThemeVariant variant)
    {
        Assert.True(Application.Current!.TryFindResource(key, variant, out object? value), $"{key} does not resolve.");

        return Assert.IsAssignableFrom<IBrush>(value);
    }

    // A UserControl since FEATURE-414B PHASE02, so it is hosted in a plain window here the way the
    // other dialog views are — the real one is the ContentDialog in the main window.
    private SettingsView Show()
    {
        SettingsViewModel viewModel = new(
            _settings.Config,
            new FakeThemeService(),
            TestFonts.Catalog(),
            NullLogger<SettingsViewModel>.Instance);

        SettingsView view = new() { DataContext = viewModel };
        Window window = new() { Content = view };

        window.Show();

        return view;
    }
}
