using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Enigma.MarkdownEditor.Desktop.Controls;
using Markdig.Syntax;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Controls;

/// <summary>
/// What the renderer builds for each kind of markdown.
/// </summary>
/// <remarks>
/// Structural, not visual: given text, these assert the control tree that comes out — the types, the
/// counts, the text and the properties that carry meaning. What it looks like is a manual pass; that
/// the right things are there, in the right order, with the right emphasis, is this.
/// </remarks>
public sealed class MarkdownPreviewTests
{
    private static readonly FontFamily Monospace = new(PreviewTree.NamedFamilies.CodeFontFamily);

    [AvaloniaFact]
    public void NothingToRender_ProducesNoBlocks()
    {
        Assert.Empty(PreviewTree.Blocks(null));
        Assert.Empty(PreviewTree.Blocks(string.Empty));
    }

    [AvaloniaFact]
    public void ASecondRender_ReplacesTheFirstRatherThanAppendingToIt()
    {
        MarkdownPreview preview = PreviewTree.Preview("# first");

        preview.Markdown = "# second\n\n# third";

        Assert.Equal(2, preview.Blocks.Count);
        Assert.Equal("second", PreviewTree.Text((TextBlock)preview.Blocks[0]));
    }

    [AvaloniaFact]
    public void EveryHeadingLevel_IsScaledForItsLevel()
    {
        IReadOnlyList<Control> blocks = PreviewTree.Blocks(
            """
            # one

            ## two

            ### three

            #### four

            ##### five

            ###### six
            """);

        Assert.Equal(6, blocks.Count);

        string[] words = ["one", "two", "three", "four", "five", "six"];

        for (int index = 0; index < blocks.Count; index++)
        {
            SelectableTextBlock heading = Assert.IsType<SelectableTextBlock>(blocks[index]);

            Assert.Equal(words[index], PreviewTree.Text(heading));
            Assert.Equal(
                MarkdownPreviewTheme.HeadingFontSize(index + 1, PreviewTree.Typography.BodyFontSize),
                heading.FontSize);
            Assert.Equal(MarkdownPreviewTheme.HeadingFontWeight(index + 1), heading.FontWeight);
        }
    }

    [AvaloniaFact]
    public void AParagraph_KeepsItsEmphasisCodeAndPlainText()
    {
        SelectableTextBlock paragraph = PreviewTree.Block<SelectableTextBlock>(
            "plain **bold** *italic* ~~gone~~ `code`",
            typography: PreviewTree.NamedFamilies);

        Assert.Equal("plain bold italic gone code", PreviewTree.Text(paragraph));

        IReadOnlyList<Span> spans = PreviewTree.Inlines<Span>(paragraph);

        Assert.Contains(spans, span => span.FontWeight == FontWeight.Bold);
        Assert.Contains(spans, span => span.FontStyle == FontStyle.Italic);
        Assert.Contains(spans, span => TextDecorations.Strikethrough.Equals(span.TextDecorations));

        Run code = Assert.Single(PreviewTree.CodeSpans(paragraph));

        Assert.Equal(Monospace, code.FontFamily);
    }

    [AvaloniaFact]
    public void NestedEmphasis_IsBothRatherThanOnlyTheOuterOne()
    {
        SelectableTextBlock paragraph = PreviewTree.Block<SelectableTextBlock>("***both***");

        Assert.Equal("both", PreviewTree.Text(paragraph));

        IReadOnlyList<Span> spans = PreviewTree.Inlines<Span>(paragraph);

        Assert.Contains(spans, span => span.FontWeight == FontWeight.Bold);
        Assert.Contains(spans, span => span.FontStyle == FontStyle.Italic);
    }

    [AvaloniaFact]
    public void ASoftLineBreak_IsASpaceAndAHardOneIsALineBreak()
    {
        // Where the author ended the source line is not where the reader's line ends: the paragraph
        // is wrapped by the pane. Two trailing spaces are how markdown says "actually break here".
        Assert.Equal(
            "line one line two",
            PreviewTree.Text(PreviewTree.Block<SelectableTextBlock>("line one\nline two")));

        SelectableTextBlock hard = PreviewTree.Block<SelectableTextBlock>("line one  \nline two");

        Assert.Equal("line one\nline two", PreviewTree.Text(hard));
        Assert.Single(PreviewTree.Inlines<LineBreak>(hard));
    }

    [AvaloniaFact]
    public void ALink_ShowsItsLabelAndIsUnderlined()
    {
        SelectableTextBlock paragraph = PreviewTree.Block<SelectableTextBlock>("see the [docs](https://example.com)");

        Assert.Equal("see the docs", PreviewTree.Text(paragraph));

        Span link = Assert.Single(PreviewTree.Inlines<Span>(paragraph));

        Assert.Equal(TextDecorations.Underline, link.TextDecorations);
    }

    [AvaloniaTheory]
    [InlineData("<https://example.com/docs>")]
    [InlineData("https://example.com/docs")]
    public void ALinkWithNoLabel_ShowsTheAddressItself(string markdown)
    {
        SelectableTextBlock paragraph = PreviewTree.Block<SelectableTextBlock>(markdown);

        Assert.Equal("https://example.com/docs", PreviewTree.Text(paragraph));
        Assert.Single(PreviewTree.Inlines<Span>(paragraph));
    }

    [AvaloniaFact]
    public void ABulletedList_IsOneRowPerItem()
    {
        StackPanel list = PreviewTree.Block<StackPanel>("- one\n- two\n- three");

        Assert.Equal(3, list.Children.Count);
        Assert.Equal(["•", "•", "•"], Markers(list));
        Assert.Equal(["one", "two", "three"], ItemTexts(list));
    }

    [AvaloniaFact]
    public void ANestedList_SitsInsideTheItemItBelongsTo()
    {
        StackPanel list = PreviewTree.Block<StackPanel>("- one\n  - nested\n- two");

        Assert.Equal(2, list.Children.Count);

        // The nested list is a block inside the first item's content, which is what indents it: the
        // item's marker column is what the child list starts after.
        Grid first = Assert.IsType<Grid>(list.Children[0]);
        StackPanel content = Assert.IsType<StackPanel>(first.Children[1]);

        Assert.Equal(2, content.Children.Count);
        Assert.Equal("one", PreviewTree.Text((TextBlock)content.Children[0]));
        Assert.Equal(["nested"], ItemTexts(Assert.IsType<StackPanel>(content.Children[1])));
    }

    [AvaloniaFact]
    public void ANumberedList_CountsFromWhereTheDocumentStartsIt()
    {
        StackPanel list = PreviewTree.Block<StackPanel>("3. three\n4. four\n5. five");

        Assert.Equal(["3.", "4.", "5."], Markers(list));
    }

    [AvaloniaFact]
    public void ANumberedListWithABracket_KeepsTheDelimiterItWasWrittenWith()
    {
        StackPanel list = PreviewTree.Block<StackPanel>("1) one\n2) two");

        Assert.Equal(["1)", "2)"], Markers(list));
    }

    [AvaloniaFact]
    public void ATaskList_ShowsABoxInsteadOfABulletAndSaysNothingTwice()
    {
        StackPanel list = PreviewTree.Block<StackPanel>("- [ ] todo\n- [x] done");

        // The box is the marker. A bullet as well would say the same thing twice, and the box is
        // what the reader is scanning for. What the box itself carries and reports is
        // MarkdownTaskBoxTests'.
        //
        // Found by walking rather than by indexing the marker cell: the box is drawn inside the
        // wrapper that scales it to the reader's type (see MarkdownVisualBuilder.TaskBox), so what
        // sits in column 0 is that wrapper. What this asserts is that a real, tickable box is the
        // marker — not which control the renderer hangs it from.
        Assert.All(
            list.Children.OfType<Grid>(),
            row => Assert.Single(PreviewTree.Descendants<CheckBox>((Control)row.Children[0])));
        Assert.Equal(["todo", "done"], ItemTexts(list));
    }

    [AvaloniaFact]
    public void ATaskItem_StartsWhereAnOrdinaryItemsTextStarts()
    {
        StackPanel list = PreviewTree.Block<StackPanel>("- [x] task\n- bullet");

        // The source wrote `- [x] task`, so the space after the box is in the literal that follows
        // it. Left in, a task's text sits one space further right than its neighbour's in the very
        // same list.
        Assert.Equal(
            ["task", "bullet"],
            list.Children.OfType<Grid>().Select(row => PreviewTree.Text((Control)row.Children[1])));
    }

    [AvaloniaFact]
    public void ABlockQuote_IsRuledDownItsLeftEdge()
    {
        Border quote = PreviewTree.Block<Border>("> quoted\n> over two lines");

        Assert.Equal(new Thickness(3, 0, 0, 0), quote.BorderThickness);
        Assert.Equal("quoted over two lines", PreviewTree.Text(quote));
    }

    [AvaloniaFact]
    public void ABlockQuote_KeepsTheBlocksInsideIt()
    {
        Border quote = PreviewTree.Block<Border>("> # a heading\n>\n> and a paragraph");

        Assert.Equal(2, PreviewTree.Descendants<SelectableTextBlock>(quote).Count);
    }

    [AvaloniaFact]
    public void AThematicBreak_IsAHairlineRatherThanText()
    {
        IReadOnlyList<Control> blocks = PreviewTree.Blocks("above\n\n---\n\nbelow");

        Assert.Equal(3, blocks.Count);

        Border rule = Assert.IsType<Border>(blocks[1]);

        Assert.Equal(1, rule.Height);
        Assert.Empty(PreviewTree.Descendants<TextBlock>(rule));
    }

    [AvaloniaFact]
    public void RawHtml_IsShownAsTheTextItWasWrittenAs()
    {
        // The documented v1 behaviour: a `<div>` in a note is visible rather than silently swallowed.
        SelectableTextBlock html = PreviewTree.Block<SelectableTextBlock>(
            "<div class=\"x\">raw</div>",
            typography: PreviewTree.NamedFamilies);

        Assert.Contains("<div class=\"x\">raw</div>", PreviewTree.Text(html), StringComparison.Ordinal);
        Assert.Equal(Monospace, html.FontFamily);
    }

    [AvaloniaFact]
    public void EveryBlockKind_ComesOutInDocumentOrder()
    {
        // The walk is the one place an arm can quietly swallow a block or claim one that belongs to
        // the arm below it — a fenced block IS a code block, and front matter IS an HTML block.
        IReadOnlyList<Control> blocks = PreviewTree.Blocks(
            """
            ---
            title: A note
            ---

            # Heading

            before

            ```csharp
            var x = 1;
            ```

            | a | b |
            |---|---|
            | 1 | 2 |

            > quoted

            ---

            after
            """);

        Assert.Collection(
            blocks,
            chip => Assert.Contains(MarkdownPreviewTheme.MetadataChipClass, chip.Classes),
            heading => Assert.Equal("Heading", PreviewTree.Text(heading)),
            paragraph => Assert.Equal("before", PreviewTree.Text(paragraph)),
            fence => Assert.IsType<MarkdownCodeBlock>(fence),
            table => Assert.Contains(
                PreviewTree.Descendants<Border>(table),
                frame => frame.Classes.Contains(MarkdownPreviewTheme.TableClass)),
            quote => Assert.Equal("quoted", PreviewTree.Text(quote)),
            rule => Assert.IsType<Border>(rule),
            paragraph => Assert.Equal("after", PreviewTree.Text(paragraph)));
    }

    [AvaloniaFact]
    public void AnImageOnItsOwn_IsABlockConstrainedToThePane()
        => WithFolder(folder =>
        {
            folder.WriteImage("assets/shot.png");

            Image image = PreviewTree.Block<Image>("![a screenshot](assets/shot.png)", folder.Root);

            Assert.Equal(Stretch.Uniform, image.Stretch);

            // DownOnly is what stops a small screenshot being blown up to the pane's width.
            Assert.Equal(StretchDirection.DownOnly, image.StretchDirection);
        });

    [AvaloniaFact]
    public void AnImageThatIsNotThere_IsAPlaceholderShowingTheLinkAsWritten()
        => WithFolder(folder =>
        {
            Border placeholder = PreviewTree.Block<Border>("![](assets/gone.png)", folder.Root);

            Assert.Contains("assets/gone.png", PreviewTree.Text(placeholder), StringComparison.Ordinal);
        });

    [AvaloniaFact]
    public void ARemoteImage_SaysSoRatherThanBeingFetched()
    {
        Border placeholder = PreviewTree.Block<Border>("![](https://example.com/shot.png)");

        Assert.Contains("https://example.com/shot.png", PreviewTree.Text(placeholder), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TwoImagesOnOneLine_AreTwoBlocksInOnePanel()
        => WithFolder(folder =>
        {
            StackPanel panel = PreviewTree.Block<StackPanel>("![](a.png) ![](b.png)", folder.Root);

            Assert.Equal(2, panel.Children.Count);
        });

    [AvaloniaFact]
    public void AnImageInsideASentence_StaysInline()
        => WithFolder(folder =>
        {
            folder.WriteImage("shot.png");

            SelectableTextBlock paragraph =
                PreviewTree.Block<SelectableTextBlock>("before ![](shot.png) after", folder.Root);

            Assert.Single(PreviewTree.Inlines<InlineUIContainer>(paragraph));
            Assert.Contains("before", PreviewTree.Text(paragraph), StringComparison.Ordinal);
            Assert.Contains("after", PreviewTree.Text(paragraph), StringComparison.Ordinal);
        });

    [AvaloniaFact]
    public void AnImageLinkTheFileSystemRefuses_BecomesOneBlockRatherThanACrash()
    {
        // 33 000 characters. Windows refuses the path outright — Path.GetFullPath raises
        // PathTooLongException, which is not one of the ArgumentExceptions the resolver answers for —
        // while a Unix file system simply has no such file. Both are the requirement being met: one
        // block, no exception out of the render, and a reported failure exactly when the block is the
        // error block.
        MarkdownPreview preview = PreviewTree.Preview(null);
        List<Exception> failures = [];
        preview.RenderFailed += (_, failure) => failures.Add(failure);

        preview.BasePath = Path.GetTempPath();
        preview.Markdown = $"![]({new string('a', 33_000)})";

        Control only = Assert.Single(preview.Blocks);

        Assert.IsType<Border>(only);
        Assert.Equal(only.Classes.Contains(MarkdownPreviewTheme.ErrorBlockClass), failures.Count == 1);
    }

    [AvaloniaFact]
    public void ARenderThatThrows_BecomesTheInlineErrorBlockAndIsReported()
    {
        ThrowingPreview preview = new();
        List<Exception> failures = [];
        preview.RenderFailed += (_, failure) => failures.Add(failure);

        PreviewTree.Host(preview);
        preview.Markdown = "# a heading";

        Control only = Assert.Single(preview.Blocks);

        Assert.Contains(MarkdownPreviewTheme.ErrorBlockClass, only.Classes);
        Assert.Contains("the walk failed", PreviewTree.Text(only), StringComparison.Ordinal);

        Exception reported = Assert.Single(failures);
        Assert.IsType<InvalidOperationException>(reported);
    }

    [AvaloniaFact]
    public void AFailureHandlerThatThrows_DoesNotTakeTheRenderDownWithIt()
    {
        ThrowingPreview preview = new();
        preview.RenderFailed += (_, _) => throw new NotSupportedException("a handler that misbehaves");

        PreviewTree.Host(preview);

        // The whole point of the guard: reporting a failure must not replace the error block with an
        // unhandled exception.
        preview.Markdown = "# a heading";

        Assert.Contains(MarkdownPreviewTheme.ErrorBlockClass, Assert.Single(preview.Blocks).Classes);
    }

    [AvaloniaFact]
    public void AChangeOfFolder_ReResolvesTheImagesWithoutTheTextChanging()
        => WithFolder(folder =>
        {
            folder.WriteImage("here/shot.png");

            MarkdownPreview preview = PreviewTree.Preview("![](shot.png)", Path.Combine(folder.Root, "nowhere"));

            Assert.IsType<Border>(Assert.Single(preview.Blocks));

            preview.BasePath = Path.Combine(folder.Root, "here");

            Assert.IsType<Image>(Assert.Single(preview.Blocks));
        });

    [AvaloniaFact]
    public void FrontMatter_IsAChipRatherThanContent()
    {
        IReadOnlyList<Control> blocks = PreviewTree.Blocks(
            """
            ---
            title: A note with front matter
            tags: [preview, fixture]
            ---

            # Front matter

            A paragraph.
            """);

        Assert.Equal(3, blocks.Count);

        Border chip = Assert.IsType<Border>(blocks[0]);

        Assert.Contains(MarkdownPreviewTheme.MetadataChipClass, chip.Classes);

        // The keys, never the values: the chip says the note has metadata and roughly what about. The
        // block's contents rendering as text is the one outcome that would make the preview wrong.
        Assert.Equal("Front matter · title · tags", PreviewTree.Text(chip));
        Assert.DoesNotContain("A note with front matter", PreviewTree.Text(chip), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void FrontMatterWithNoNamedEntries_StillSaysThereIsSome()
    {
        Border chip = Assert.IsType<Border>(
            PreviewTree.Blocks(
                """
                ---
                - a bare sequence
                ---

                Body.
                """)[0]);

        Assert.Equal("Front matter", PreviewTree.Text(chip));
    }

    private static IReadOnlyList<string> Markers(StackPanel list)
        => [.. list.Children
            .OfType<Grid>()
            .Select(row => PreviewTree.Text((TextBlock)row.Children[0]))];

    private static IReadOnlyList<string> ItemTexts(StackPanel list)
        => [.. list.Children
            .OfType<Grid>()
            .Select(row => PreviewTree.Text((Control)row.Children[1]).Trim())];

    private static void WithFolder(Action<TempImageFolder> body)
    {
        using TempImageFolder folder = new();

        body(folder);
    }

    /// <summary>
    /// A preview whose block walk always fails, so the isolation around it can be asserted on every
    /// platform rather than only where a path happens to be rejected.
    /// </summary>
    private sealed class ThrowingPreview : MarkdownPreview
    {
        protected override IReadOnlyList<MarkdownBlockVisual> BuildBlocks(MarkdownDocument document)
            => throw new InvalidOperationException("the walk failed");
    }

    private sealed class TempImageFolder : IDisposable
    {
        public TempImageFolder()
        {
            Root = Path.Combine(
                Path.GetTempPath(),
                "Enigma.MarkdownEditor.Tests",
                Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        /// <summary>
        /// Writes a file where an image link points. The bytes are not a real PNG on purpose: what
        /// these tests assert is the tree the renderer builds, and decoding happens off the UI thread
        /// afterwards — a failed decode is a log entry, never a different tree.
        /// </summary>
        public string WriteImage(string relativePath)
        {
            string path = Path.GetFullPath(Path.Combine(Root, relativePath));

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, [0x89, 0x50, 0x4E, 0x47]);

            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A leftover temp folder is not worth failing a green test over.
            }
        }
    }
}
