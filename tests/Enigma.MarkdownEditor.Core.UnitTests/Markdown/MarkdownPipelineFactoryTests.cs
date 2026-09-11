using System;
using System.Linq;
using Enigma.MarkdownEditor.Core.Markdown;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Tables;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Markdown;

/// <summary>
/// What the preview's pipeline can and cannot parse.
/// </summary>
/// <remarks>
/// Asserted through what comes out of a parse rather than by reading the extension list back off the
/// pipeline. An extension is only configured if it changed the AST, and a bump of Markdig that
/// renamed or re-ordered one would still leave the list looking right.
/// </remarks>
public sealed class MarkdownPipelineFactoryTests
{
    [Fact]
    public void APipeTable_IsParsedAsATable()
    {
        MarkdownDocument document = MarkdownPipelineFactory.Parse(
            """
            | left | right |
            |------|-------|
            | 1    | 2     |
            """);

        Table table = Assert.Single(document.Descendants<Table>());

        // Header row plus one body row.
        Assert.Equal(2, table.Count);
    }

    [Theory]
    [InlineData("- [x] done", true)]
    [InlineData("- [ ] not done", false)]
    public void ATaskItem_IsParsedAsATaskList(string markdown, bool expected)
    {
        MarkdownDocument document = MarkdownPipelineFactory.Parse(markdown);

        TaskList task = Assert.Single(document.Descendants<TaskList>());

        Assert.Equal(expected, task.Checked);
    }

    [Fact]
    public void DoubleTildes_AreStrikethrough()
    {
        MarkdownDocument document = MarkdownPipelineFactory.Parse("this is ~~gone~~ now");

        EmphasisInline emphasis = Assert.Single(document.Descendants<EmphasisInline>());

        Assert.Equal('~', emphasis.DelimiterChar);
        Assert.Equal(2, emphasis.DelimiterCount);
    }

    [Fact]
    public void ASingleTilde_IsNotSubscript()
    {
        // The one EmphasisExtra that is switched on is strikethrough. Subscript would claim `~` in a
        // document that meant it literally — a chemical formula, a file name, a regex.
        MarkdownDocument document = MarkdownPipelineFactory.Parse("H~2~O");

        Assert.Empty(document.Descendants<EmphasisInline>());
    }

    [Fact]
    public void ABareAddress_IsParsedAsALink()
    {
        MarkdownDocument document = MarkdownPipelineFactory.Parse("visit https://example.com/docs now");

        LinkInline link = Assert.Single(document.Descendants<LinkInline>());

        Assert.Equal("https://example.com/docs", link.Url);
        Assert.False(link.IsImage);
    }

    [Fact]
    public void FrontMatter_IsParsedAsFrontMatterRatherThanAsContent()
    {
        MarkdownDocument document = MarkdownPipelineFactory.Parse(
            """
            ---
            title: A note
            ---

            # Heading
            """);

        Assert.IsType<YamlFrontMatterBlock>(document[0]);

        // And the heading beneath it is still a heading, not part of the metadata.
        Assert.Single(document.Descendants<HeadingBlock>());
    }

    [Fact]
    public void WithoutFrontMatter_AThematicBreakIsStillAThematicBreak()
    {
        MarkdownDocument document = MarkdownPipelineFactory.Parse(
            """
            Some text.

            ---

            More text.
            """);

        Assert.Single(document.Descendants<ThematicBreakBlock>());
        Assert.Empty(document.Descendants<YamlFrontMatterBlock>());
    }

    [Fact]
    public void EveryBlock_CarriesTheLineItCameFrom()
    {
        MarkdownDocument document = MarkdownPipelineFactory.Parse(
            """
            # Heading

            First paragraph.

            Second paragraph.
            """);

        // Zero-based, and this is what PHASE03's scroll sync is built on: without
        // UsePreciseSourceLocation the positions inside a block are relative to the block.
        Assert.Equal(0, document.Descendants<HeadingBlock>().Single().Line);

        int[] paragraphs = document.Descendants<ParagraphBlock>().Select(p => p.Line).ToArray();

        Assert.Equal([2, 4], paragraphs);
    }

    [Fact]
    public void AnInline_CarriesItsPositionInTheWholeDocument()
    {
        MarkdownDocument document = MarkdownPipelineFactory.Parse(
            """
            # Heading

            The body.
            """);

        LiteralInline body = document.Descendants<LiteralInline>().Last();

        Assert.Equal(2, body.Line);
        Assert.True(body.Span.Start > 0, "The span must be an offset into the document, not into the block.");
    }

    [Fact]
    public void Create_BuildsAnIndependentPipelineEachTime()
        => Assert.NotSame(MarkdownPipelineFactory.Create(), MarkdownPipelineFactory.Create());

    [Fact]
    public void EmptyText_ParsesToAnEmptyDocument()
        => Assert.Empty(MarkdownPipelineFactory.Parse(string.Empty));

    [Fact]
    public void NullText_IsRejected()
        => Assert.Throws<ArgumentNullException>(() => MarkdownPipelineFactory.Parse(null!));
}
