using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Enigma.MarkdownEditor.Desktop.Controls;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Controls;

/// <summary>
/// How the type the reader chose reaches the rendered document.
/// </summary>
/// <remarks>
/// The values arrive as one record, and each of them has to land somewhere different: the prose
/// family on the pane, for every block to inherit; the code family on code spans and fences alone; the
/// body size on prose with the headings scaled off it; the code size on those same spans and fences;
/// the line height on every run, at whatever size that run is set in. These assert where each one
/// ends up, not what it looks like.
/// </remarks>
public sealed class MarkdownPreviewTypographyTests
{
    private const string Prose = "Fake Serif";
    private const string Code = "Another Mono";

    // The code size is deliberately neither the body size nor a point under it: a derived size would
    // be 19, so an assertion of 15 cannot pass against the scale this phase replaced.
    private static readonly MarkdownPreviewTypography Chosen =
        new(Prose, Code, BodyFontSize: 20, CodeFontSize: 15, LineHeight: 2);

    [AvaloniaFact]
    public void TheProseFamily_IsSetOnThePaneSoEveryBlockInheritsIt()
    {
        MarkdownPreview preview = PreviewTree.Preview("Body text.", typography: Chosen);
        SelectableTextBlock paragraph = Assert.IsType<SelectableTextBlock>(Assert.Single(preview.Blocks));

        Assert.Equal(new FontFamily(Prose), preview.FontFamily);

        // Inherited rather than assigned per block: one setting on the pane is what a paragraph, a
        // list marker and a table cell all read, and a family stamped on each of them would be the
        // same value written in a dozen places for the renderer to keep in step.
        Assert.False(paragraph.IsSet(TextBlock.FontFamilyProperty));
        Assert.Equal(new FontFamily(Prose), paragraph.FontFamily);
    }

    [AvaloniaFact]
    public void TheBodySizeAndLineHeight_ReachProse()
    {
        SelectableTextBlock paragraph =
            PreviewTree.Block<SelectableTextBlock>("Body text.", typography: Chosen);

        Assert.Equal(20, paragraph.FontSize);
        Assert.Equal(40, paragraph.LineHeight);
    }

    [AvaloniaFact]
    public void AHeadingsLineHeight_IsAMultipleOfItsOwnSizeRatherThanOfTheBodys()
    {
        SelectableTextBlock heading = PreviewTree.Block<SelectableTextBlock>("# Title", typography: Chosen);

        // A single line height for the whole document would crowd an h1 or strand a paragraph; the
        // multiplier is what is shared, not the measurement.
        Assert.Equal(MarkdownPreviewTheme.HeadingFontSize(1, 20), heading.FontSize);
        Assert.Equal(heading.FontSize * 2, heading.LineHeight);
    }

    [AvaloniaFact]
    public void ACodeSpan_IsSetInTheCodeFamilyAndNotTheProseOne()
    {
        SelectableTextBlock paragraph =
            PreviewTree.Block<SelectableTextBlock>("a `code` span", typography: Chosen);

        Run code = Assert.Single(PreviewTree.Inlines<Run>(paragraph), run => run.Text == "code");

        Assert.Equal(new FontFamily(Code), code.FontFamily);

        // The size as well, which is the whole point of the setting: a span left to inherit was the
        // one place in the preview where code came out at the full prose size.
        Assert.Equal(15, code.FontSize);
        Assert.NotEqual(paragraph.FontSize, code.FontSize);
    }

    [AvaloniaFact]
    public void AFencedBlock_IsSetInTheCodeFamilyAtTheCodeSize()
    {
        MarkdownCodeBlock block = PreviewTree.Block<MarkdownCodeBlock>(
            """
            ```csharp
            var x = 1;
            ```
            """,
            typography: Chosen);

        SelectableTextBlock text = Assert.Single(PreviewTree.Descendants<SelectableTextBlock>(block));

        Assert.Equal(new FontFamily(Code), text.FontFamily);
        Assert.Equal(15, text.FontSize);
        Assert.Equal(30, text.LineHeight);
    }

    [AvaloniaFact]
    public void ATableCell_ReadsAsProseWhileTheCodeInsideItReadsAsCode()
    {
        Control table = PreviewTree.Blocks(
            """
            | Setting | Key      |
            |---------|----------|
            | Font    | `family` |
            """,
            typography: Chosen)[0];

        IReadOnlyList<SelectableTextBlock> cells = PreviewTree.Descendants<SelectableTextBlock>(table);

        // The cells themselves are prose — a table of words set in the code font would be a table
        // nobody asked for — so they carry the body size and inherit the pane's family.
        Assert.All(cells, cell => Assert.Equal(20, cell.FontSize));
        Assert.All(cells, cell => Assert.False(cell.IsSet(TextBlock.FontFamilyProperty)));

        // The one span that was written as code is the one that follows the code family.
        Run code = Assert.Single(
            cells.SelectMany(PreviewTree.Inlines<Run>),
            run => run.Text == "family");

        Assert.Equal(new FontFamily(Code), code.FontFamily);
    }

    [AvaloniaFact]
    public void AssigningTheTypography_RebuildsTheDocumentAtTheNewSize()
    {
        MarkdownPreview preview = PreviewTree.Preview("# Title\n\nBody text.");
        Control heading = preview.Blocks[0];

        preview.Typography = Chosen;

        // A size decides how much room a block takes, which decides where every block after it sits —
        // so unlike a colour there is nothing to change in place, and the tree is built again.
        Assert.NotSame(heading, preview.Blocks[0]);
        Assert.Equal(
            MarkdownPreviewTheme.HeadingFontSize(1, 20),
            Assert.IsType<SelectableTextBlock>(preview.Blocks[0]).FontSize);
        Assert.Equal(20, Assert.IsType<SelectableTextBlock>(preview.Blocks[1]).FontSize);
    }

    [AvaloniaFact]
    public void AtTheDefaultBodySize_NoHeadingIsSmallerThanTheProseUnderIt()
    {
        // 18 is what a fresh settings file gives the preview, and the size the old absolute scale
        // would have broken at: an h3 fixed at 17 under an 18pt paragraph.
        MarkdownPreviewTypography typography =
            new(Prose, Code, BodyFontSize: 18, CodeFontSize: 14, LineHeight: 1.5);

        IReadOnlyList<Control> blocks = PreviewTree.Blocks(
            """
            # one

            ## two

            ### three

            #### four

            ##### five

            ###### six

            Body text.
            """,
            typography: typography);

        double body = Assert.IsType<SelectableTextBlock>(blocks[^1]).FontSize;

        Assert.Equal(18, body);
        Assert.All(
            blocks.Take(6).Cast<SelectableTextBlock>(),
            heading => Assert.True(
                heading.FontSize >= body,
                $"A heading set at {heading.FontSize} would be smaller than the {body}pt body."));
    }

    [AvaloniaFact]
    public void AnEmptyFamily_LeavesTheVisualsInheritingRatherThanNamingNothing()
    {
        // What MarkdownPreviewTypography.Default holds, and what a preview built by hand gets: no
        // family at all, because "the best monospace font here" is not a name a control can know.
        MarkdownPreview preview = PreviewTree.Preview(
            "a `code` span",
            typography: MarkdownPreviewTypography.Default);

        SelectableTextBlock paragraph = Assert.IsType<SelectableTextBlock>(Assert.Single(preview.Blocks));
        Run code = Assert.Single(PreviewTree.Inlines<Run>(paragraph), run => run.Text == "code");

        Assert.False(preview.IsSet(TemplatedControl.FontFamilyProperty));
        Assert.False(code.IsSet(TextElement.FontFamilyProperty));
    }

    [Fact]
    public void TheCodeSize_IsCarriedRatherThanDerivedFromTheBodyOne()
    {
        // The two are set independently because a fixed-width face at the same nominal size reads
        // larger than the prose beside it, by an amount that depends on the two families the reader
        // picked. Nothing here may reintroduce an arithmetic relationship between them.
        MarkdownPreviewTypography typography = Chosen with { BodyFontSize = 30 };

        Assert.Equal(15, typography.CodeFontSize);
        Assert.Equal(28, typography.AsideFontSize);
    }

    [Fact]
    public void TheTaskBoxSize_FollowsTheProseAndIsHeldBetweenTwoLimits()
    {
        // 18 is what a fresh settings file gives the preview, and 20 is the size this renderer drew a
        // task box at before the box joined the scale. The default has to be continuous, or sizing
        // the box would be a visible change to a preview nobody complained about.
        Assert.Equal(20, (Chosen with { BodyFontSize = 18 }).TaskBoxSize);

        Assert.True(
            (Chosen with { BodyFontSize = 12 }).TaskBoxSize < (Chosen with { BodyFontSize = 24 }).TaskBoxSize);

        // Both ends of the 6-72pt the setting allows: below 12 the box stops being something a
        // pointer can hit, and above 36 it reads as a button rather than as a marker.
        Assert.Equal(12, (Chosen with { BodyFontSize = 6 }).TaskBoxSize);
        Assert.Equal(36, (Chosen with { BodyFontSize = 72 }).TaskBoxSize);
    }

    [AvaloniaFact]
    public void TheDefault_IsTheScaleTheRendererWasFixedAtBeforeItBecameASetting()
    {
        Assert.Equal(14, MarkdownPreviewTypography.Default.BodyFontSize);
        Assert.Equal(13, MarkdownPreviewTypography.Default.CodeFontSize);
        Assert.Equal(12, MarkdownPreviewTypography.Default.AsideFontSize);
        Assert.Empty(MarkdownPreviewTypography.Default.FontFamily);
        Assert.Empty(MarkdownPreviewTypography.Default.CodeFontFamily);
    }

    [AvaloniaFact]
    public void ANullTypography_FallsBackToTheDefaultRatherThanThrowing()
    {
        MarkdownPreview preview = PreviewTree.Preview(null);

        preview.Typography = null!;
        preview.Markdown = "Body text.";

        Assert.Equal(
            MarkdownPreviewTypography.Default.BodyFontSize,
            Assert.IsType<SelectableTextBlock>(Assert.Single(preview.Blocks)).FontSize);
    }
}
