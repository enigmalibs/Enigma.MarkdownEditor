using System;
using System.Linq;
using Avalonia.Headless.XUnit;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Services;

/// <summary>
/// The catalog's two halves, tested apart for the reason they were written apart.
/// </summary>
/// <remarks>
/// Everything about <em>which</em> family the editor ends up with is asserted over a made-up font list
/// (see <see cref="TestFonts"/>), because the answer has to be the same whatever machine the suite runs
/// on. What the platform's own font list looks like is asserted separately and only for the properties
/// that hold on any machine at all — the headless font manager reports a single synthetic family, so
/// there is nothing else there to assert.
/// </remarks>
public sealed class FontCatalogTests
{
    [Fact]
    public void TheFamilyLists_AreSortedAndDeduplicated()
    {
        FontCatalog catalog = new(
            ["Beta Mono", "alpha sans", "Beta Mono", "BETA MONO"],
            ["Beta Mono"],
            "alpha sans");

        Assert.Equal(["alpha sans", "Beta Mono"], catalog.AllFamilies);
        Assert.Equal(["Beta Mono"], catalog.MonospaceFamilies);
    }

    [Fact]
    public void OnlyTheFixedWidthFamilies_AreOfferedAsMonospace()
    {
        FontCatalog catalog = TestFonts.Catalog();

        Assert.Equal(TestFonts.Monospace.Order(StringComparer.OrdinalIgnoreCase), catalog.MonospaceFamilies);
        Assert.DoesNotContain(TestFonts.UiFamily, catalog.MonospaceFamilies);
    }

    [Fact]
    public void AMonospaceNameWithNoFamilyBehindIt_IsNotOffered()
    {
        // A row in the picker for a family that is not installed would render as something else
        // entirely, which is the one thing a font picker must not do.
        FontCatalog catalog = new(["Fake Sans"], ["Ghost Mono"], "Fake Sans");

        Assert.Empty(catalog.MonospaceFamilies);
    }

    [Fact]
    public void TheDefaultMonospaceFamily_IsTheFirstPreferredOneInstalled()
    {
        // Second and fourth of the preferred list, in the wrong order and among other fonts: the
        // preferred order decides, not the order they were found in and not the alphabet.
        FontCatalog catalog = new(
            ["DejaVu Sans Mono", "Aaaa Mono", "Consolas", "Fake Sans"],
            ["DejaVu Sans Mono", "Aaaa Mono", "Consolas"],
            "Fake Sans");

        Assert.Equal("Consolas", catalog.DefaultMonospaceFamily);
    }

    [Fact]
    public void WithNoPreferredFamilyInstalled_TheDefaultIsAnyFixedWidthFamily()
    {
        FontCatalog catalog = TestFonts.Catalog();

        Assert.DoesNotContain(catalog.DefaultMonospaceFamily, FontCatalog.PreferredMonospaceFamilies);
        Assert.Contains(catalog.DefaultMonospaceFamily, catalog.MonospaceFamilies);
    }

    [Fact]
    public void WithNoFixedWidthFamilyAtAll_TheDefaultIsTheInterfaceFont()
    {
        // A proportional editor is a bad editor, but it beats an editor with nothing to render with.
        FontCatalog catalog = new(TestFonts.Proportional, [], TestFonts.UiFamily);

        Assert.Equal(TestFonts.UiFamily, catalog.DefaultMonospaceFamily);
    }

    [Theory]
    [InlineData("Fake Mono", true)]
    [InlineData("fake mono", true)]
    [InlineData("Fake Sans", true)]
    [InlineData(TestFonts.NotInstalled, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void IsAvailable_AnswersForTheWholeFontList_IgnoringCase(string? family, bool expected)
        => Assert.Equal(expected, TestFonts.Catalog().IsAvailable(family));

    [Fact]
    public void ResolvingAnInstalledFamily_ReturnsItUnchanged()
        => Assert.Equal("Fake Mono", TestFonts.Catalog().ResolveEditorFamily("Fake Mono"));

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData(TestFonts.NotInstalled)]
    [InlineData("Cascadia Mono, Consolas, DejaVu Sans Mono, Liberation Mono, monospace")]
    public void ResolvingAnythingThisMachineDoesNotHave_FallsBackToTheDefaultMonospace(string? stored)
    {
        FontCatalog catalog = TestFonts.Catalog();

        // The comma stack among these on purpose: as a family name it simply is not installed, which is
        // the same case as a font from another machine and wants the same answer.
        Assert.Equal(catalog.DefaultMonospaceFamily, catalog.ResolveEditorFamily(stored));
    }

    [Fact]
    public void ResolvingDoesNotRewriteTheStoredValue()
    {
        // Nothing here mutates, and that is the point: the setting keeps naming the font it names, so a
        // settings file carried between two machines still asks for the right font on each of them.
        FontCatalog catalog = TestFonts.Catalog();

        Assert.Equal(catalog.DefaultMonospaceFamily, catalog.ResolveEditorFamily(TestFonts.NotInstalled));
        Assert.False(catalog.IsAvailable(TestFonts.NotInstalled));
    }

    [Fact]
    public void EveryArgument_IsRequired()
    {
        Assert.Throws<ArgumentNullException>(() => new FontCatalog(null!, [], "Fake Sans"));
        Assert.Throws<ArgumentNullException>(() => new FontCatalog([], null!, "Fake Sans"));
        Assert.Throws<ArgumentNullException>(() => new FontCatalog([], [], null!));
        Assert.Throws<ArgumentException>(() => new FontCatalog([], [], "  "));
    }

    [AvaloniaFact]
    public void TheCatalogBuiltFromThisMachine_AlwaysHasSomethingToRenderWith()
    {
        // The headless platform reports one synthetic family, so what can be asserted is what must hold
        // on any machine: the probe completes, and whatever it found, the editor is left with a font
        // name rather than an empty string.
        FontCatalog catalog = FontCatalog.ForCurrentSystem();

        Assert.NotEmpty(catalog.AllFamilies);
        Assert.NotEmpty(catalog.SystemDefaultFamily);
        Assert.NotEmpty(catalog.DefaultMonospaceFamily);
        Assert.Contains(catalog.DefaultMonospaceFamily, catalog.AllFamilies);
        Assert.Equal(catalog.DefaultMonospaceFamily, catalog.ResolveEditorFamily(string.Empty));
    }

    [AvaloniaFact]
    public void ClassifyingAFamilyThisMachineDoesNotHave_SaysProportionalRatherThanThrowing()
        => Assert.False(FontCatalog.IsMonospace(TestFonts.NotInstalled));

    [AvaloniaTheory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ClassifyingNoFamilyAtAll_SaysProportional(string? family)
        => Assert.False(FontCatalog.IsMonospace(family));
}
