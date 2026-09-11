using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Enigma.MarkdownEditor.Desktop.Controls;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Controls;

/// <summary>
/// Which characters of a rendered paragraph are a link, and what clicking one does.
/// </summary>
/// <remarks>
/// A link is a text span, and a span cannot be clicked — so the renderer records the character range
/// each one occupies and the block around them turns a pointer back into a URL. These assert both
/// halves: the ranges, which is where an off-by-one would send the reader to the wrong page, and one
/// real click through the headless window, which is the only thing that proves the two are wired
/// together.
/// </remarks>
public sealed class MarkdownLinkRegionsTests
{
    [AvaloniaFact]
    public void ALinksRange_CoversItsLabelAndNothingElse()
    {
        SelectableTextBlock paragraph = PreviewTree.Block<SelectableTextBlock>("see the [docs](https://example.com) now");
        MarkdownLinkRegions links = Regions(paragraph);

        Assert.Equal(1, links.Count);

        string text = Text(paragraph);
        int start = text.IndexOf("docs", StringComparison.Ordinal);

        Assert.Equal("https://example.com", links.UrlAt(start));
        Assert.Equal("https://example.com", links.UrlAt(start + 3));
        Assert.Null(links.UrlAt(start - 1));
        Assert.Null(links.UrlAt(start + 4));
    }

    [AvaloniaFact]
    public void TwoLinks_AreToldApart()
    {
        SelectableTextBlock paragraph =
            PreviewTree.Block<SelectableTextBlock>("[first](https://one.example) and [second](https://two.example)");

        MarkdownLinkRegions links = Regions(paragraph);
        string text = Text(paragraph);

        Assert.Equal(2, links.Count);
        Assert.Equal("https://one.example", links.UrlAt(text.IndexOf("first", StringComparison.Ordinal)));
        Assert.Equal("https://two.example", links.UrlAt(text.IndexOf("second", StringComparison.Ordinal)));
    }

    [AvaloniaFact]
    public void AnAutolink_CoversTheAddressItShows()
    {
        SelectableTextBlock paragraph = PreviewTree.Block<SelectableTextBlock>("go to <https://example.com/angle>");
        MarkdownLinkRegions links = Regions(paragraph);

        string text = Text(paragraph);

        Assert.Equal("https://example.com/angle", links.UrlAt(text.IndexOf("https", StringComparison.Ordinal)));
    }

    [AvaloniaFact]
    public void ALinkInsideEmphasis_IsWhereItIsOnScreenRatherThanWhereItIsInItsSpan()
    {
        // The offset has to be absolute in the block's text. Counted from inside the emphasis it would
        // be a few characters short, and every click would land on the word before the link.
        SelectableTextBlock paragraph =
            PreviewTree.Block<SelectableTextBlock>("**bold and [linked](https://example.com)**");

        MarkdownLinkRegions links = Regions(paragraph);
        string text = Text(paragraph);

        Assert.Equal("https://example.com", links.UrlAt(text.IndexOf("linked", StringComparison.Ordinal)));
        Assert.Null(links.UrlAt(0));
    }

    [AvaloniaFact]
    public void ALinkAfterAHardLineBreak_IsStillWhereItIs()
    {
        // A line break contributes characters of its own to the laid-out text, and how many is the
        // platform's business rather than the renderer's. Measuring the collection is what makes this
        // pass without anyone having to know.
        SelectableTextBlock paragraph =
            PreviewTree.Block<SelectableTextBlock>("first line  \nthen [docs](https://example.com)");

        MarkdownLinkRegions links = Regions(paragraph);
        string text = Text(paragraph);

        Assert.Equal("https://example.com", links.UrlAt(text.IndexOf("docs", StringComparison.Ordinal)));
    }

    [AvaloniaFact]
    public void ALinkAfterAnImage_IsStillWhereItIs()
    {
        SelectableTextBlock paragraph =
            PreviewTree.Block<SelectableTextBlock>("![](missing.png) then [docs](https://example.com)");

        MarkdownLinkRegions links = Regions(paragraph);
        string text = Text(paragraph);

        Assert.Equal("https://example.com", links.UrlAt(text.IndexOf("docs", StringComparison.Ordinal)));
    }

    [AvaloniaFact]
    public void ALinkInAHeading_IsClickableToo()
    {
        SelectableTextBlock heading = PreviewTree.Block<SelectableTextBlock>("# [A titled link](./other.md)");

        Assert.Equal("./other.md", Regions(heading).UrlAt(0));
    }

    [AvaloniaFact]
    public void AParagraphWithNoLinks_CarriesNoneAtAll()
    {
        SelectableTextBlock paragraph = PreviewTree.Block<SelectableTextBlock>("plain **bold** `code` prose");

        // Nothing attached: most of a note looks like this, and hit-testing every pointer move over
        // all of it would be work for nothing.
        Assert.Null(MarkdownLinkRegions.GetRegions(paragraph));
    }

    [AvaloniaFact]
    public void AnImage_IsNotALink()
    {
        SelectableTextBlock paragraph = PreviewTree.Block<SelectableTextBlock>("text ![alt](missing.png) more text");

        Assert.Null(MarkdownLinkRegions.GetRegions(paragraph));
    }

    [AvaloniaFact]
    public void ClickingALink_ReportsTheUrlTheDocumentWrote()
    {
        MarkdownPreview preview = new();
        Window host = PreviewTree.Host(preview);

        List<string> activated = [];
        preview.LinkActivated += (_, url) => activated.Add(url);

        preview.Markdown = "see the [docs](./other-note.md) now";

        SelectableTextBlock paragraph = Assert.IsType<SelectableTextBlock>(Assert.Single(preview.Blocks));

        Click(host, paragraph, Text(paragraph).IndexOf("docs", StringComparison.Ordinal));

        Assert.Equal(["./other-note.md"], activated);
    }

    [AvaloniaFact]
    public void ClickingBesideALink_ReportsNothing()
    {
        MarkdownPreview preview = new();
        Window host = PreviewTree.Host(preview);

        List<string> activated = [];
        preview.LinkActivated += (_, url) => activated.Add(url);

        preview.Markdown = "see the [docs](./other-note.md) now";

        SelectableTextBlock paragraph = Assert.IsType<SelectableTextBlock>(Assert.Single(preview.Blocks));

        Click(host, paragraph, 0);

        Assert.Empty(activated);
    }

    private static MarkdownLinkRegions Regions(SelectableTextBlock block)
        => Assert.IsType<MarkdownLinkRegions>(MarkdownLinkRegions.GetRegions(block));

    private static string Text(SelectableTextBlock block) => block.Inlines?.Text ?? string.Empty;

    /// <summary>
    /// Clicks the character at <paramref name="characterIndex"/>, through the window, as a pointer
    /// would.
    /// </summary>
    private static void Click(Window host, SelectableTextBlock block, int characterIndex)
    {
        // The layout has to have run before a character has a position at all.
        Dispatcher.UIThread.RunJobs();

        Rect glyph = block.TextLayout.HitTestTextPosition(characterIndex);
        Point inBlock = new(glyph.Center.X + block.Padding.Left, glyph.Center.Y + block.Padding.Top);
        Point inWindow = block.TranslatePoint(inBlock, host) ?? inBlock;

        host.MouseMove(inWindow);
        host.MouseDown(inWindow, MouseButton.Left);
        host.MouseUp(inWindow, MouseButton.Left);

        Dispatcher.UIThread.RunJobs();
    }
}
