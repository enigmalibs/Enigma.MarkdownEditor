using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Enigma.MarkdownEditor.Core.Markdown;
using Enigma.MarkdownEditor.Desktop.Controls;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Controls;

/// <summary>
/// The renderer's half of the scroll sync: the index it builds over what it drew, and where asking it
/// for a source line leaves the view.
/// </summary>
/// <remarks>
/// <para>
/// The arithmetic itself is <c>MarkdownLineMapTests</c>, in Core, where it needs no window. What is
/// asserted here is the part only the control can answer: that the index describes the blocks that
/// were actually produced — the walk skips block kinds it does not draw, so the <em>n</em>th visual is
/// not the <em>n</em>th block of the document — and that a scroll lands inside the right one.
/// </para>
/// <para>
/// Every test renders inside a shown window, and the offsets are read after a layout pass: a block's
/// bounds are all zero until one has run, which is exactly how a green test could hide a sync that
/// never moves anything.
/// </para>
/// </remarks>
public sealed class MarkdownPreviewSyncTests
{
    private const string ThreeBlocks =
        """
        # A heading

        A paragraph under it.

        Another paragraph, further down.
        """;

    [AvaloniaFact]
    public void AnEmptyPreview_HasAnEmptyIndex()
    {
        MarkdownPreview preview = PreviewTree.Preview(null);

        Assert.True(preview.LineMap.IsEmpty);
    }

    [AvaloniaFact]
    public void TheIndex_HasOneEntryPerRenderedBlock()
    {
        MarkdownPreview preview = PreviewTree.Preview(ThreeBlocks);

        Assert.Equal(3, preview.Blocks.Count);
        Assert.Equal(preview.Blocks.Count, preview.LineMap.Count);
    }

    [AvaloniaFact]
    public void TheIndex_CarriesTheSourceLineOfEachBlockOneBased()
    {
        MarkdownPreview preview = PreviewTree.Preview(ThreeBlocks);

        Assert.Equal(1, preview.LineMap.StartLineAt(0));
        Assert.Equal(3, preview.LineMap.StartLineAt(1));
        Assert.Equal(5, preview.LineMap.StartLineAt(2));
    }

    [AvaloniaFact]
    public void TheIndex_SkipsWhatTheRendererDidNotDraw()
    {
        // A link reference definition is a block of the document that the walk draws nothing for, so
        // the first visual is the heading on line 3. An index built by walking the AST a second time
        // rather than by carrying the line out with the visual would have it on line 1.
        MarkdownPreview preview = PreviewTree.Preview(
            """
            [ref]: https://example.com

            # A heading

            A paragraph.
            """);

        Assert.Equal(2, preview.Blocks.Count);
        Assert.Equal(preview.Blocks.Count, preview.LineMap.Count);
        Assert.Equal(3, preview.LineMap.StartLineAt(0));
        Assert.Equal(5, preview.LineMap.StartLineAt(1));
    }

    [AvaloniaFact]
    public void TheIndex_CountsTheFrontMatterChipAsTheBlockItIs()
    {
        // Front matter is not drawn as content but it is drawn — as the chip saying the note has some
        // — so it holds a place in both lists, on the line it was written on.
        MarkdownPreview preview = PreviewTree.Preview(
            """
            ---
            title: A note
            ---
            # A heading

            A paragraph.
            """);

        Assert.Equal(3, preview.Blocks.Count);
        Assert.Equal(preview.Blocks.Count, preview.LineMap.Count);
        Assert.Equal(1, preview.LineMap.StartLineAt(0));
        Assert.Equal(4, preview.LineMap.StartLineAt(1));
        Assert.Equal(6, preview.LineMap.StartLineAt(2));
    }

    [AvaloniaFact]
    public void TheIndex_IsRebuiltWithEveryRender()
    {
        MarkdownPreview preview = PreviewTree.Preview(ThreeBlocks);

        Assert.Equal(3, preview.LineMap.Count);

        preview.Markdown = "Just the one.";

        Assert.Equal(1, preview.LineMap.Count);
        Assert.Equal(1, preview.LineMap.StartLineAt(0));
    }

    [AvaloniaFact]
    public void ScrollingToALine_LandsInsideTheBlockThatOwnsIt()
    {
        MarkdownPreview preview = Laid(TallDocument());

        // A block in the middle, not the last one: the view cannot scroll past the end of the
        // document, so the final block's top is not somewhere the pane can actually be put.
        int index = preview.LineMap.Count / 2;
        int line = preview.LineMap.StartLineAt(index);

        preview.ScrollToSourceLine(line);

        Control block = preview.Blocks[index];
        double offset = Offset(preview);

        Assert.True(offset > 0, "the sync did not move the view at all");
        Assert.InRange(offset, block.Bounds.Y - 1, block.Bounds.Y + block.Bounds.Height + 1);
    }

    [AvaloniaFact]
    public void ScrollingDownTheSource_NeverTakesTheViewBackUp()
    {
        MarkdownPreview preview = Laid(TallDocument());
        double previous = -1;

        for (int line = 1; line <= 60; line++)
        {
            preview.ScrollToSourceLine(line);

            double offset = Offset(preview);

            // Monotonic, which is what "no oscillation" means from the reader's side: a source line
            // further down never scrolls the preview back towards the top.
            Assert.True(offset >= previous, $"line {line} scrolled back up");
            previous = offset;
        }
    }

    [AvaloniaFact]
    public void ScrollingToTheFirstLine_GoesBackToTheTop()
    {
        MarkdownPreview preview = Laid(TallDocument());

        preview.ScrollToSourceLine(60);
        Assert.True(Offset(preview) > 0);

        preview.ScrollToSourceLine(1);
        Assert.Equal(0, Offset(preview));
    }

    [AvaloniaFact]
    public void ScrollingAnEmptyPreview_DoesNothingRatherThanThrow()
    {
        MarkdownPreview preview = PreviewTree.Preview(null);

        preview.ScrollToSourceLine(12);

        Assert.Equal(0, Offset(preview));
    }

    [AvaloniaFact]
    public void ALineNobodyWrote_IsClampedRatherThanRefused()
    {
        MarkdownPreview preview = Laid(TallDocument());

        preview.ScrollToSourceLine(100_000);

        Assert.True(Offset(preview) > 0);
    }

    [AvaloniaFact]
    public void RetypingTheSameDocument_KeepsTheReaderWhereTheyWere()
    {
        MarkdownPreview preview = Laid(TallDocument(), sourcePath: "/notes/tall.md");

        preview.ScrollToSourceLine(40);
        double before = Offset(preview);
        Assert.True(before > 0);

        // An edit at the very end: everything around the viewport is unchanged, so the reader should
        // not be thrown back to the top a quarter of a second after every keystroke.
        preview.Markdown = TallDocument() + "\n\nOne more paragraph.\n";
        Layout(preview);

        Assert.Equal(before, Offset(preview), 0);
    }

    [AvaloniaFact]
    public void OpeningADifferentDocument_StartsAtTheTop()
    {
        MarkdownPreview preview = Laid(TallDocument(), sourcePath: "/notes/tall.md");

        preview.ScrollToSourceLine(40);
        Assert.True(Offset(preview) > 0);

        // What SourcePath is for: two notes in one folder are the same BasePath, and landing half way
        // down a note that was only just opened would be wrong. The same text on purpose — two notes
        // can hold it, and an assignment of markdown that has not changed renders nothing, so the
        // reset has to hang off the path rather than off the render.
        preview.SourcePath = "/notes/other.md";
        preview.Markdown = TallDocument();
        Layout(preview);

        Assert.Equal(0, Offset(preview));
    }

    [AvaloniaFact]
    public void AskingBeforeTheTreeIsLaidOut_IsAnsweredOnceItIs()
    {
        MarkdownPreview preview = new();
        Window host = PreviewTree.Host(preview);

        preview.Markdown = TallDocument();

        // Deliberately no layout pass yet: every block's bounds are zero, so answering now would
        // scroll to nowhere. The request has to survive until there is something to measure.
        preview.ScrollToSourceLine(40);

        host.UpdateLayout();

        Assert.True(Offset(preview) > 0);
    }

    // Forty numbered paragraphs, so a block is always identifiable by the line it started on and the
    // document is certainly taller than the window.
    private static string TallDocument()
    {
        List<string> lines = [];

        for (int number = 1; number <= 40; number++)
        {
            lines.Add($"Paragraph number {number}, with enough words in it to take a line of its own.");
            lines.Add(string.Empty);
        }

        return string.Join('\n', lines);
    }

    private static MarkdownPreview Laid(string markdown, string? sourcePath = null)
    {
        MarkdownPreview preview = new();
        Window host = PreviewTree.Host(preview);

        preview.SourcePath = sourcePath;
        preview.Markdown = markdown;

        host.UpdateLayout();

        return preview;
    }

    private static void Layout(MarkdownPreview preview) => (preview.Parent as Window)?.UpdateLayout();

    private static double Offset(MarkdownPreview preview)
        => PreviewTree.Descendants<ScrollViewer>(preview)[0].Offset.Y;
}
