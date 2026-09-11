using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Enigma.MarkdownEditor.Desktop.Controls;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Controls;

/// <summary>
/// The palette the preview is painted from, in both variants.
/// </summary>
/// <remarks>
/// This is the light-and-dark half of the phase's acceptance criteria, asserted where it can be:
/// every colour in the renderer is a resource key, so "renders correctly in dark and light" reduces
/// to "every key resolves to a brush in either variant, and a bound visual follows the switch". What
/// the resulting colours look like is a manual pass, not a test.
/// </remarks>
public sealed class MarkdownPreviewThemeTests
{
    // Any size at all: the tests below are about the shape of the scale, not about a particular body.
    private const double AnyBodySize = 16;

    [AvaloniaFact]
    public void EveryBrushKey_ResolvesInBothVariants()
    {
        List<string> missing = [];

        foreach (string key in MarkdownPreviewTheme.BrushKeys)
        {
            foreach (ThemeVariant variant in new[] { ThemeVariant.Dark, ThemeVariant.Light })
            {
                if (!Application.Current!.TryFindResource(key, variant, out object? value) || (value is not IBrush))
                {
                    missing.Add($"{key} [{variant}]");
                }
            }
        }

        Assert.Empty(missing);
    }

    [AvaloniaFact]
    public void TheBrushKeys_AreAllDistinct()
        => Assert.Equal(
            MarkdownPreviewTheme.BrushKeys.Count,
            MarkdownPreviewTheme.BrushKeys.Distinct(StringComparer.Ordinal).Count());

    [AvaloniaFact]
    public void ALink_IsPaintedWithTheThemesAccentInEitherVariant()
        => InEachVariant(variant =>
        {
            SelectableTextBlock paragraph = PreviewTree.Block<SelectableTextBlock>("[docs](https://example.com)");
            Span link = Assert.Single(PreviewTree.Inlines<Span>(paragraph));

            // A dynamic resource on a text inline is the case worth pinning down: inlines are not
            // controls, and a brush that failed to resolve there would leave every link painted as
            // body text — visibly wrong, and silent.
            Assert.Equal(Brush(MarkdownPreviewTheme.AccentKey, variant), link.Foreground?.ToString());
        });

    [AvaloniaFact]
    public void ACodeSpan_IsPaintedWithTheChipColourInEitherVariant()
        => InEachVariant(variant =>
        {
            SelectableTextBlock paragraph = PreviewTree.Block<SelectableTextBlock>("a `code` span");
            Run code = Assert.Single(
                PreviewTree.Inlines<Run>(paragraph),
                run => run.Background is not null);

            Assert.Equal(Brush(MarkdownPreviewTheme.CodeBackgroundKey, variant), code.Background?.ToString());
        });

    [AvaloniaFact]
    public void ACodeSpan_IsNoLongerPaintedLikeTheBlockAroundIt()
        => InEachVariant(variant =>
        {
            // The regression this whole item is about. A chip inside a sentence has nothing to be
            // read against but the sentence, so it cannot share the surface a fenced block uses —
            // that surface is one step off the pane, and one step is what made the chip invisible.
            // Asserted as "different from", not as a colour: what the two are is the palette's
            // business, that they are not the same thing is this renderer's.
            Assert.NotEqual(
                Brush(MarkdownPreviewTheme.CodeBlockBackgroundKey, variant),
                Brush(MarkdownPreviewTheme.CodeBackgroundKey, variant));

            SelectableTextBlock paragraph = PreviewTree.Block<SelectableTextBlock>("a `code` span");
            Run chip = Assert.Single(
                PreviewTree.Inlines<Run>(paragraph),
                run => run.Background is not null);
            MarkdownCodeBlock fence = PreviewTree.Block<MarkdownCodeBlock>("```\ncode\n```");

            Assert.NotEqual(fence.Background?.ToString(), chip.Background?.ToString());
        });

    [AvaloniaFact]
    public void AFencedBlockAndATableHeader_KeepTheSurfaceTheyAlwaysHad()
    {
        // The un-aliasing moved the code span alone. Both of these read correctly already — they are
        // blocks the width of the pane, with a border and real padding around them — and re-colouring
        // them would be a change nobody asked for.
        Assert.Equal("EnigmaSurfaceHighBrush", MarkdownPreviewTheme.CodeBlockBackgroundKey);
        Assert.Equal(MarkdownPreviewTheme.CodeBlockBackgroundKey, MarkdownPreviewTheme.TableHeaderBackgroundKey);
    }

    [AvaloniaFact]
    public void SwitchingVariant_RepaintsWithoutRebuildingTheTree()
    {
        ThemeVariant? requested = Application.Current!.RequestedThemeVariant;

        try
        {
            Application.Current.RequestedThemeVariant = ThemeVariant.Dark;

            MarkdownPreview preview = PreviewTree.Preview("Body text.");
            Control paragraph = Assert.Single(preview.Blocks);

            string? dark = preview.Foreground?.ToString();

            Application.Current.RequestedThemeVariant = ThemeVariant.Light;

            Assert.NotEqual(dark, preview.Foreground?.ToString());

            // Nothing re-rendered: the same visual is still there, painted differently. That is the
            // point of binding keys rather than resolving brushes — a theme switch costs no walk of
            // the document.
            Assert.Same(paragraph, Assert.Single(preview.Blocks));
        }
        finally
        {
            Application.Current.RequestedThemeVariant = requested;
        }
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 3)]
    [InlineData(3, 4)]
    [InlineData(4, 5)]
    public void AHeading_IsLargerThanTheOneBelowIt(int level, int deeper)
        => Assert.True(
            MarkdownPreviewTheme.HeadingFontSize(level, AnyBodySize)
            > MarkdownPreviewTheme.HeadingFontSize(deeper, AnyBodySize),
            $"h{level} must be larger than h{deeper}.");

    [Theory]
    [InlineData(1, 24)]
    [InlineData(2, 20)]
    [InlineData(3, 17)]
    [InlineData(4, 15.5)]
    [InlineData(5, 14.5)]
    [InlineData(6, 14)]
    public void AtTheSizeTheScaleUsedToBeFixedAt_ItIsExactlyTheScaleItUsedToBe(int level, double expected)
    {
        // The guard on the refactor that made the scale relative to a body size that is now a setting.
        // Exact rather than approximate on purpose: at the size the scale is written at, the arithmetic
        // has to hand the numbers back unchanged, which is what scaling by 14/14 rather than by a
        // ratio rounded on the way in buys.
        Assert.Equal(expected, MarkdownPreviewTheme.HeadingFontSize(level, 14));
    }

    [Fact]
    public void AtALargerBodySize_TheHeadingsGrowWithItRatherThanStayingPut()
    {
        // What the absolute scale could not do: at an 18pt body, an h3 fixed at 17 would have been a
        // heading set smaller than the paragraph under it.
        double[] sizes = [.. Enumerable.Range(1, 6).Select(level => MarkdownPreviewTheme.HeadingFontSize(level, 18))];

        Assert.All(sizes, size => Assert.True(size >= 18, $"{size} must not be smaller than the 18pt body."));

        // 31/26/22/20/19/18 to the nearest point.
        Assert.Equal([31, 26, 22, 20, 19, 18], [.. sizes.Select(size => Math.Round(size))]);
    }

    [Fact]
    public void TheDeepestHeadings_StayBoldRatherThanShrinkingBelowBodyText()
    {
        // An h6 that is both smaller and lighter than the paragraph under it stops reading as a
        // heading at all.
        Assert.Equal(AnyBodySize, MarkdownPreviewTheme.HeadingFontSize(6, AnyBodySize));
        Assert.Equal(FontWeight.SemiBold, MarkdownPreviewTheme.HeadingFontWeight(6));
        Assert.Equal(FontWeight.Bold, MarkdownPreviewTheme.HeadingFontWeight(1));
    }

    [Fact]
    public void ALevelOutsideTheRange_IsClampedRatherThanThrowing()
    {
        Assert.Equal(
            MarkdownPreviewTheme.HeadingFontSize(1, AnyBodySize),
            MarkdownPreviewTheme.HeadingFontSize(0, AnyBodySize));
        Assert.Equal(
            MarkdownPreviewTheme.HeadingFontSize(6, AnyBodySize),
            MarkdownPreviewTheme.HeadingFontSize(99, AnyBodySize));
    }

    [Fact]
    public void BindBrush_RejectsItsArguments()
    {
        TextBlock target = new();

        Assert.Throws<ArgumentNullException>(
            () => MarkdownPreviewTheme.BindBrush(null!, TextBlock.ForegroundProperty, MarkdownPreviewTheme.AccentKey));
        Assert.Throws<ArgumentNullException>(
            () => MarkdownPreviewTheme.BindBrush(target, null!, MarkdownPreviewTheme.AccentKey));
        Assert.Throws<ArgumentException>(
            () => MarkdownPreviewTheme.BindBrush(target, TextBlock.ForegroundProperty, "  "));
    }

    private static string? Brush(string key, ThemeVariant variant)
        => Application.Current!.TryFindResource(key, variant, out object? value) ? value?.ToString() : null;

    /// <summary>
    /// Runs <paramref name="body"/> once per variant, with the application actually in it, and puts
    /// the requested variant back afterwards.
    /// </summary>
    /// <remarks>
    /// The restore is belt and braces — the headless harness builds a fresh application per test —
    /// but a test that leaves the application in a variant it did not find it in is a trap either
    /// way.
    /// </remarks>
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
