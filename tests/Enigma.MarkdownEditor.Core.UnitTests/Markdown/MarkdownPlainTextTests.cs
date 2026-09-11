using System;
using Enigma.MarkdownEditor.Core.Markdown;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Markdown;

/// <summary>
/// The plain text a copy out of the preview produces: what survives the markup being taken off, and
/// what the renderer draws that says nothing at all.
/// </summary>
/// <remarks>
/// Rule by rule, over the smallest document that shows each one. The same rules over the whole
/// fixture corpus — and the correspondence with what the renderer actually drew — are asserted in
/// the Desktop suite, where the fixtures and the control both are.
/// </remarks>
public sealed class MarkdownPlainTextTests
{
    [Fact]
    public void TheDocument_IsRequired()
    {
        Assert.Throws<ArgumentNullException>(() => MarkdownPlainText.Render(null!));
        Assert.Throws<ArgumentNullException>(() => MarkdownPlainText.Blocks(null!));
    }

    [Fact]
    public void AnEmptyDocument_HasNothingToCopy()
    {
        Assert.Empty(MarkdownPlainText.Render(string.Empty));
        Assert.Empty(MarkdownPlainText.Blocks(string.Empty));
    }

    [Fact]
    public void AHeading_ComesOutBare()
        => Assert.Equal("Heading one", MarkdownPlainText.Render("# Heading one"));

    [Fact]
    public void Emphasis_KeepsItsTextAndLosesItsDelimiters()
        => Assert.Equal(
            "Plain, bold, italic, both, struck and code.",
            MarkdownPlainText.Render("Plain, **bold**, *italic*, ***both***, ~~struck~~ and `code`."));

    [Fact]
    public void ASoftBreak_IsASpaceAndAHardBreak_IsALine()
        => Assert.Equal(
            Lines("one two", "three"),

            // Two trailing spaces is a hard break; the plain line ending before it is a soft one.
            MarkdownPlainText.Render("one\ntwo  \nthree"));

    [Fact]
    public void ABulletList_KeepsTheMarkerTheReaderIsLookingAt()
        => Assert.Equal(Lines("• first", "• second"), MarkdownPlainText.Render("- first\n- second"));

    [Fact]
    public void AnOrderedList_KeepsItsNumbersAndItsDelimiter()
    {
        Assert.Equal(Lines("1. one", "2. two"), MarkdownPlainText.Render("1. one\n2. two"));
        Assert.Equal(Lines("7) seven", "8) eight"), MarkdownPlainText.Render("7) seven\n8) eight"));
    }

    [Fact]
    public void ANestedList_ReadsAsNested()
        => Assert.Equal(
            Lines("• first", "  • nested", "    • deeper", "• second"),
            MarkdownPlainText.Render("- first\n  - nested\n    - deeper\n- second"));

    [Fact]
    public void ATask_KeepsItsBoxAndOneSpaceAfterIt()
        => Assert.Equal(
            Lines("[ ] todo", "[x] done"),

            // The space after the box belongs to the source, not to the text: written out as well it
            // would indent every task one further than the bullet beside it.
            MarkdownPlainText.Render("- [ ] todo\n- [x] done"));

    [Fact]
    public void ASecondParagraphInAnItem_LinesUpUnderTheItemsOwnText()
        => Assert.Equal(
            Lines("• first", "• second", "  and a second paragraph"),

            // Indented to where the item's text starts rather than to where its marker does, which is
            // the column the pane wraps it into.
            MarkdownPlainText.Render("- first\n\n- second\n\n  and a second paragraph"));

    [Fact]
    public void AFence_ComesOutVerbatimWithoutItsFenceLines()
        => Assert.Equal(
            Lines("var x = 1;", "var y = 2;"),
            MarkdownPlainText.Render("```csharp\nvar x = 1;\nvar y = 2;\n```"));

    [Fact]
    public void AnIndentedCodeBlock_IsCodeToo()
        => Assert.Equal("var indented = true;", MarkdownPlainText.Render("    var indented = true;"));

    [Fact]
    public void ATable_IsTabSeparatedSoASpreadsheetGetsOneCellPerColumn()
        => Assert.Equal(
            Lines("left\tright", "one\ttwo"),
            MarkdownPlainText.Render("| left | right |\n|------|-------|\n| one  | two   |"));

    [Fact]
    public void ARowWithFewerCellsThanItsHeader_LeavesTheGapEmpty()
        => Assert.Equal(
            Lines("a\tb\tc", "one\ttwo\t"),
            MarkdownPlainText.Render("| a | b | c |\n|---|---|---|\n| one | two |"));

    [Fact]
    public void ATableCell_KeepsItsTextAndLosesItsMarkup()
        => Assert.Equal(
            Lines("what\tnote", "bold\tcode span"),
            MarkdownPlainText.Render("| what | note |\n|------|------|\n| **bold** | `code span` |"));

    [Fact]
    public void AnImage_LeavesItsAltTextWhereItSat()
    {
        Assert.Equal("[a screenshot]", MarkdownPlainText.Render("![a screenshot](assets/shot.png)"));
        Assert.Equal("before [inline] after", MarkdownPlainText.Render("before ![inline](a.png) after"));
        Assert.Equal("[]", MarkdownPlainText.Render("![]()"));
    }

    [Fact]
    public void ALink_KeepsItsLabelAndNotItsAddress()
        => Assert.Equal("Read the docs for more.", MarkdownPlainText.Render("Read [the docs](https://example.com) for more."));

    [Fact]
    public void AnAutolink_IsItsOwnLabel()
    {
        Assert.Equal("See https://example.com/angle", MarkdownPlainText.Render("See <https://example.com/angle>"));
        Assert.Equal("See https://example.com/bare", MarkdownPlainText.Render("See https://example.com/bare"));
    }

    [Fact]
    public void AQuote_ReadsPlain()
        => Assert.Equal(
            Lines("A quote.", "", "With a second paragraph."),
            MarkdownPlainText.Render("> A quote.\n>\n> With a second paragraph."));

    [Fact]
    public void AnHtmlBlock_IsTheLiteralTextItWasWrittenAs()
        => Assert.Equal(
            "<div class=\"callout\">Not a div.</div>",
            MarkdownPlainText.Render("<div class=\"callout\">Not a div.</div>"));

    [Fact]
    public void AThematicRule_IsDrawnAndSaysNothing()
    {
        Assert.Equal(Lines("above", "", "below"), MarkdownPlainText.Render("above\n\n---\n\nbelow"));

        // Three blocks are drawn — the rule is one of them — and the middle one carries no text, so
        // a copy comes out with one blank line between the two paragraphs rather than three.
        Assert.Equal(["above", string.Empty, "below"], MarkdownPlainText.Blocks("above\n\n---\n\nbelow"));
    }

    [Fact]
    public void FrontMatter_IsDrawnAsAChipAndNeverAsContent()
    {
        const string markdown = "---\ntitle: A note\ntags: [a, b]\n---\n\n# Heading\n";

        Assert.Equal("Heading", MarkdownPlainText.Render(markdown));
        Assert.Equal([string.Empty, "Heading"], MarkdownPlainText.Blocks(markdown));
    }

    [Fact]
    public void Blocks_AreSeparatedByOneBlankLine()
        => Assert.Equal(
            Lines("Heading", "", "A paragraph.", "", "• an item"),
            MarkdownPlainText.Render("# Heading\n\nA paragraph.\n\n- an item"));

    [Fact]
    public void EveryLineEnding_IsThePlatformsOwn()
        // The clipboard is read by other applications on this machine, so a note carrying the other
        // platform's line endings — this one holds both — must not arrive with them.
        => Assert.Equal(
            Lines("one", "", "two", "", "three"),
            MarkdownPlainText.Render("one\n\ntwo\r\n\r\nthree"));

    [Fact]
    public void ADocumentOfNothingButARule_HasABlockAndNoText()
    {
        // Which is why the pane's copy is gated on the render rather than on the text: there is
        // something on screen and nothing to carry off it.
        Assert.Single(MarkdownPlainText.Blocks("---"));
        Assert.Empty(MarkdownPlainText.Render("---"));
    }

    // Joined with the platform's own separator, which is what the projection emits.
    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines);
}
