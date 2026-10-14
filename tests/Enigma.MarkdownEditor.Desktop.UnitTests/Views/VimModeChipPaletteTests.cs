using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Enigma.MarkdownEditor.Desktop.Views;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Views;

/// <summary>
/// The status bar's vim mode chip, read off the real window's styles, and the palette it relies on.
/// </summary>
/// <remarks>
/// <para>
/// The chip paints its text on the severity fills <c>Enigma.Avalonia.Desktop</c> ships for its
/// InfoBar. Those fills are the library's to change — 1.2.0 lifted them in the Dark variant — and the
/// one pairing it promises stays readable on every one of them is the primary foreground. So the chip
/// is asserted to paint exactly that, and the palette is asserted to keep the promise: a future bump
/// that breaks it fails here rather than in the status bar.
/// </para>
/// <para>
/// The window is built with no ViewModel. Nothing here needs one: the chip's look is decided by its
/// classes, which the test sets itself, and by the window's styles, which load with the markup.
/// </para>
/// </remarks>
public sealed class VimModeChipPaletteTests
{
    /// <summary>WCAG 2.x AA, for text below the large-text threshold — the chip is 10 px.</summary>
    private const double MinimumTextContrast = 4.5;

    [AvaloniaFact]
    public void InInsertAndVisual_TheTextIsThePrimaryForeground()
        => WithChip(chip => InEachVariant(variant =>
        {
            foreach (string mode in new[] { "insert", "visual" })
            {
                chip.Classes.Set(mode, true);

                Assert.Equal(Colour("EnigmaForegroundBrush", variant), ColourOf(Text(chip).Foreground));

                chip.Classes.Set(mode, false);
            }
        }));

    [AvaloniaFact]
    public void InNormalMode_TheTextStaysQuiet()
        => WithChip(chip => InEachVariant(variant =>
            Assert.Equal(Colour("EnigmaForegroundSecondaryBrush", variant), ColourOf(Text(chip).Foreground))));

    [AvaloniaFact]
    public void TheModes_AreStillToldApartByTheirFills()
        => WithChip(chip => InEachVariant(variant =>
        {
            chip.Classes.Set("insert", true);
            Assert.Equal(Colour("EnigmaSuccessBackgroundBrush", variant), ColourOf(chip.Background));
            chip.Classes.Set("insert", false);

            chip.Classes.Set("visual", true);
            Assert.Equal(Colour("EnigmaWarningBackgroundBrush", variant), ColourOf(chip.Background));
            chip.Classes.Set("visual", false);
        }));

    [AvaloniaFact]
    public void ThePrimaryForeground_IsReadableOnBothFills_InEitherVariant()
    {
        foreach (ThemeVariant variant in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            Color text = Colour("EnigmaForegroundBrush", variant);

            foreach (string fill in new[] { "EnigmaSuccessBackgroundBrush", "EnigmaWarningBackgroundBrush" })
            {
                double contrast = Contrast(text, Colour(fill, variant));

                Assert.True(
                    contrast >= MinimumTextContrast,
                    $"{fill} [{variant}]: {contrast:0.00}:1 is below {MinimumTextContrast}:1");
            }
        }
    }

    private static void WithChip(Action<Border> body)
    {
        MainWindow window = new();
        window.Show();

        try
        {
            body(window.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("vimMode")));
        }
        finally
        {
            window.Close();
        }
    }

    private static TextBlock Text(Border chip) => Assert.IsType<TextBlock>(chip.Child);

    private static Color Colour(string key, ThemeVariant variant)
        => Application.Current!.TryFindResource(key, variant, out object? value)
            ? ColourOf(value as IBrush)
            : throw new InvalidOperationException($"{key} does not resolve in {variant}.");

    private static Color ColourOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    // WCAG 2.x contrast ratio over relative luminance, alpha ignored: every colour involved is opaque.
    private static double Contrast(Color first, Color second)
    {
        double lighter = Math.Max(Luminance(first), Luminance(second));
        double darker = Math.Min(Luminance(first), Luminance(second));

        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(Color colour)
        => (0.2126 * Linear(colour.R)) + (0.7152 * Linear(colour.G)) + (0.0722 * Linear(colour.B));

    private static double Linear(byte channel)
    {
        double value = channel / 255.0;

        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    // Same contract as MarkdownPreviewThemeTests.InEachVariant: the application is really in each
    // variant while the body runs, and is put back the way it was found.
    private static void InEachVariant(Action<ThemeVariant> body)
    {
        ThemeVariant? requested = Application.Current!.RequestedThemeVariant;

        try
        {
            foreach (ThemeVariant variant in new[] { ThemeVariant.Dark, ThemeVariant.Light })
            {
                Application.Current.RequestedThemeVariant = variant;
                body(variant);
            }
        }
        finally
        {
            Application.Current.RequestedThemeVariant = requested;
        }
    }
}
