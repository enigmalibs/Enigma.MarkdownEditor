using System;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.Views;
using TextMateSharp.Grammars;
using TextMateSharp.Themes;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Views;

/// <summary>
/// The editor's appearance: which theme a variant turns on, and which font family the chosen setting
/// actually leaves on the control.
/// </summary>
/// <remarks>
/// The theme half needs no control at all — installing TextMate does, but choosing which theme to
/// install does not, and that choice is what a variant switch turns on. The font half needs the real
/// pane in a shown window, because the setting reaches the editor through a binding and a binding
/// that does not carry is exactly the defect these tests stand against.
/// </remarks>
public sealed class EditorAppearanceTests
{
    [AvaloniaFact]
    public async Task TheChosenEditorFont_IsTheOneTheControlRendersWith()
    {
        // Through the real pane: the family is carried by a binding, and a compiled binding whose
        // source type does not match its target silently leaves the control on its inherited default
        // — which is the whole of this bug and cannot be seen from the ViewModel alone.
        using TabStrip strip = await TabStrip.ShowAsync("one.md");

        strip.Settings.EditorFontFamily = TestFonts.Monospace[1];
        strip.ApplySettings();

        Assert.Equal(TestFonts.Monospace[1], strip.Editor.FontFamily.Name);
    }

    [AvaloniaFact]
    public async Task ChangingTheEditorFont_MovesTheControlOntoTheNewOne()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md");

        strip.Settings.EditorFontFamily = TestFonts.Monospace[1];
        strip.ApplySettings();

        strip.Settings.EditorFontFamily = TestFonts.Monospace[0];
        strip.ApplySettings();

        Assert.Equal(TestFonts.Monospace[0], strip.Editor.FontFamily.Name);
    }

    [AvaloniaFact]
    public async Task WithNoFontChosen_TheControlRendersInTheFallbackRatherThanTheInterfaceFont()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md");

        // A fresh settings file names no family, and the resolved fallback has to reach the control
        // just as a chosen one does — otherwise the editor comes out in whatever the tree inherits.
        Assert.Empty(strip.Settings.EditorFontFamily);
        Assert.Equal(strip.Fonts.DefaultMonospaceFamily, strip.Editor.FontFamily.Name);
        Assert.NotEqual(TestFonts.UiFamily, strip.Editor.FontFamily.Name);
    }

    [AvaloniaFact]
    public async Task AStoredFontThisMachineDoesNotHave_LeavesTheControlOnTheFallback()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md");

        strip.Settings.EditorFontFamily = TestFonts.NotInstalled;
        strip.ApplySettings();

        Assert.Equal(strip.Fonts.DefaultMonospaceFamily, strip.Editor.FontFamily.Name);

        // And the file still says what it said: the machine that opens it next may well have it.
        Assert.Equal(TestFonts.NotInstalled, strip.Settings.EditorFontFamily);
    }

    [Fact]
    public void TheDarkVariant_UsesTheDarkTheme()
        => Assert.Equal(ThemeName.DarkPlus, EditorAppearance.ThemeNameFor(ThemeVariant.Dark));

    [Fact]
    public void TheLightVariant_UsesTheLightTheme()
        => Assert.Equal(ThemeName.LightPlus, EditorAppearance.ThemeNameFor(ThemeVariant.Light));

    [Fact]
    public void AVariantThatIsNeither_FallsBackToTheLightTheme()
    {
        // ActualThemeVariant is resolved to light or dark before it ever reaches the editor, so this
        // is the guard for a variant the application does not define rather than a case it hits.
        Assert.Equal(ThemeName.LightPlus, EditorAppearance.ThemeNameFor(ThemeVariant.Default));
        Assert.Equal(ThemeName.LightPlus, EditorAppearance.ThemeNameFor(new ThemeVariant("Sepia", null)));
    }

    [Fact]
    public void TheVariantIsRequired()
        => Assert.Throws<ArgumentNullException>(() => EditorAppearance.ThemeNameFor(null!));

    [Theory]
    [InlineData(ThemeName.DarkPlus)]
    [InlineData(ThemeName.LightPlus)]
    public void BothThemesOfThePair_AreLoadable(ThemeName theme)
    {
        // The pair is only as live as the package's resources are: a theme renamed or dropped by a
        // TextMateSharp.Grammars bump would otherwise only show up as an editor that stops
        // re-colouring halfway through a variant switch.
        IRawTheme loaded = new RegistryOptions(theme).LoadTheme(theme);

        Assert.NotNull(loaded);
    }
}
