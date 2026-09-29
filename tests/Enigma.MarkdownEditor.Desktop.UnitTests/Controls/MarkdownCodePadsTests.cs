using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Enigma.MarkdownEditor.Desktop.Controls;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Controls;

/// <summary>
/// The padding that makes a code span a chip, and the record of it that keeps the padding off the
/// clipboard.
/// </summary>
/// <remarks>
/// <para>
/// Two claims, and they pull against each other: the chip has to carry characters the document never
/// wrote, and nothing the reader copies may contain them. What is asserted here is the shape — where
/// the padding is, and where the block recorded it; what a copy actually comes out as is asserted
/// over the real control in <see cref="MarkdownPreviewCopyTests"/>.
/// </para>
/// <para>
/// Every record here is built by rendering, never by hand: the collector's <c>Add</c> is internal to
/// the renderer, and a test that assembled one itself would be asserting against indices it had just
/// made up rather than against the ones a selection will be measured with.
/// </para>
/// </remarks>
public sealed class MarkdownCodePadsTests
{
    // "a " then the chip, then " span": index 2 is the opening pad, 7 the closing one.
    private const string OneChip = "a `code` span";

    [AvaloniaFact]
    public void ACodeSpan_CarriesOnePadEitherSideOfWhatTheDocumentWrote()
    {
        Run chip = Assert.Single(PreviewTree.CodeSpans(Paragraph(OneChip)));

        Assert.Equal(MarkdownCodePads.Pad + "code" + MarkdownCodePads.Pad, chip.Text);
        Assert.Equal("code", PreviewTree.Unpadded(chip));
    }

    [AvaloniaFact]
    public void TheBlock_RecordsBothPadsWhereTheyActuallyLanded()
    {
        MarkdownCodePads pads = Pads(Paragraph(OneChip));

        Assert.Equal(2, pads.Count);
        Assert.True(pads.IsPad(2), "The chip's opening pad is at index 2.");
        Assert.True(pads.IsPad(7), "The chip's closing pad is at index 7.");
        Assert.False(pads.IsPad(3), "The first character of the code itself is not padding.");
    }

    [AvaloniaFact]
    public void AChipThatOpensTheParagraph_RecordsThePadAtIndexZero()
    {
        MarkdownCodePads pads = Pads(Paragraph("`x` y"));

        Assert.True(pads.IsPad(0), "A pad at the very start of a block is still a pad.");
        Assert.True(pads.IsPad(2), "And its closing one is at index 2.");
    }

    [AvaloniaFact]
    public void AChipNestedInEmphasis_RecordsWhereItLandedRatherThanWhereItsSpanBegins()
    {
        // The reason the collector threads through the walk the way link ranges do: inside emphasis
        // the chip is built into a span of its own, and an index counted from that span would be six
        // characters short of where a selection will look for it.
        MarkdownCodePads pads = Pads(Paragraph("ab *cd `e` fg*"));

        Assert.Equal(2, pads.Count);
        Assert.True(pads.IsPad(6), "The chip opens at index 6 of the block's own text.");
        Assert.True(pads.IsPad(8), "And closes at index 8.");
    }

    [AvaloniaFact]
    public void AParagraphWithNoChipInIt_CarriesNoRecordAtAll()
    {
        SelectableTextBlock paragraph = Paragraph("no code here");

        Assert.Null(MarkdownCodePads.GetPads(paragraph));

        // Which is what makes the overwhelmingly common case — a paragraph of prose — free.
        Assert.Equal("no code here", MarkdownCodePads.Strip(paragraph, 0, "no code here"));
    }

    [AvaloniaFact]
    public void Strip_TakesTheChipsPaddingOutOfAWholeBlockSelection()
    {
        SelectableTextBlock paragraph = Paragraph(OneChip);

        Assert.Equal("a code span", MarkdownCodePads.Strip(paragraph, 0, Text(paragraph)));
    }

    [AvaloniaFact]
    public void Strip_OffsetsAgainstWhereTheSelectionStarted()
    {
        SelectableTextBlock paragraph = Paragraph(OneChip);
        string text = Text(paragraph);

        // From the opening pad, and from just past it: both still have to find the closing one.
        Assert.Equal("code span", MarkdownCodePads.Strip(paragraph, 2, text[2..]));
        Assert.Equal("code span", MarkdownCodePads.Strip(paragraph, 3, text[3..]));
    }

    [AvaloniaFact]
    public void Strip_LeavesASelectionThatMissedEveryPadExactlyAsItWas()
    {
        SelectableTextBlock paragraph = Paragraph(OneChip);
        string text = Text(paragraph);

        Assert.Equal(" span", MarkdownCodePads.Strip(paragraph, 8, text[8..]));
        Assert.Equal("a ", MarkdownCodePads.Strip(paragraph, 0, text[..2]));
        Assert.Equal(string.Empty, MarkdownCodePads.Strip(paragraph, 0, string.Empty));
    }

    [AvaloniaFact]
    public void ALinkAroundAChip_StillAnswersOverTheWholeOfIt()
    {
        // The padding is part of the clickable chip, so a link range measured after it was added has
        // to cover it — including the character the reader sees as the chip's left edge.
        SelectableTextBlock paragraph = Paragraph("[run `x` now](https://example.com)");
        MarkdownLinkRegions links = Assert.IsType<MarkdownLinkRegions>(MarkdownLinkRegions.GetRegions(paragraph));
        string text = Text(paragraph);

        Assert.NotEmpty(text);
        Assert.All(
            Enumerable.Range(0, text.Length),
            index => Assert.Equal("https://example.com", links.UrlAt(index)));
    }

    [AvaloniaFact]
    public void TheAttachedProperty_RejectsItsArguments()
    {
        Assert.Throws<ArgumentNullException>(() => MarkdownCodePads.GetPads(null!));
        Assert.Throws<ArgumentNullException>(() => MarkdownCodePads.SetPads(null!, new MarkdownCodePads()));
        Assert.Throws<ArgumentNullException>(() => MarkdownCodePads.Strip(null!, 0, "text"));
        Assert.Throws<ArgumentNullException>(() => MarkdownCodePads.Strip(new TextBlock(), 0, null!));
        Assert.Throws<ArgumentNullException>(() => new MarkdownCodePads().Strip(0, null!));
    }

    private static SelectableTextBlock Paragraph(string markdown)
        => PreviewTree.Block<SelectableTextBlock>(markdown);

    private static MarkdownCodePads Pads(SelectableTextBlock paragraph)
        => Assert.IsType<MarkdownCodePads>(MarkdownCodePads.GetPads(paragraph));

    // The block's text as the layout and the selection both see it — padding included, which is the
    // whole point: these are the indices Strip is given.
    private static string Text(SelectableTextBlock paragraph) => paragraph.Inlines?.Text ?? string.Empty;
}
