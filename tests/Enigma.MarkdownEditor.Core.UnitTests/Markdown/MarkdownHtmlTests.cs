using System;
using System.IO;
using Enigma.MarkdownEditor.Core.Markdown;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Markdown;

/// <summary>
/// The HTML a copy out of the preview puts on the clipboard beside the plain text: the structure a
/// paste keeps, the styling it deliberately does not carry, and what happens to an image link once
/// the markup leaves this process.
/// </summary>
/// <remarks>
/// Rule by rule, over the smallest document that shows each one, the same way
/// <see cref="MarkdownPlainTextTests"/> is written. The sweep over the whole fixture corpus is in the
/// Desktop suite, where the fixtures are.
/// </remarks>
public sealed class MarkdownHtmlTests
{
    [Fact]
    public void TheDocument_IsRequired()
        => Assert.Throws<ArgumentNullException>(() => MarkdownHtml.Render(null!, basePath: null));

    [Fact]
    public void AnEmptyDocument_HasNothingToCopy()
        => Assert.Empty(MarkdownHtml.Render(string.Empty, basePath: null));

    [Fact]
    public void AHeading_KeepsItsLevel()
    {
        Assert.Contains("<h1>Heading one</h1>", Render("# Heading one"), StringComparison.Ordinal);
        Assert.Contains("<h3>Heading three</h3>", Render("### Heading three"), StringComparison.Ordinal);
    }

    [Fact]
    public void Emphasis_BecomesTheTagsThatMeanIt()
    {
        string html = Render("Plain, **bold**, *italic* and ~~struck~~.");

        Assert.Contains("<strong>bold</strong>", html, StringComparison.Ordinal);
        Assert.Contains("<em>italic</em>", html, StringComparison.Ordinal);
        Assert.Contains("<del>struck</del>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ALink_KeepsItsAddressAndItsLabel()
        => Assert.Contains(
            "<a href=\"https://example.com/docs\">the docs</a>",
            Render("An inline link to [the docs](https://example.com/docs)."),
            StringComparison.Ordinal);

    [Fact]
    public void ATable_ComesOutAsATable()
    {
        // The one thing a plain-text paste cannot keep, and the main reason this flavour exists at
        // all: tab-separated rows land in a spreadsheet, but only a table lands in Word as a table.
        string html = Render("| a | b |\n|---|---|\n| 1 | 2 |");

        Assert.Contains("<table>", html, StringComparison.Ordinal);
        Assert.Contains("<th>a</th>", html, StringComparison.Ordinal);
        Assert.Contains("<td>1</td>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ATablesAlignment_IsTheOneStyleThatSurvives()
    {
        // Alignment is not appearance imposed by the pane — the author wrote `:---:` and meant it, so
        // it is content. It is also the only `style` this projection ever emits, which is what the
        // no-styling test below is careful to allow for.
        string html = Render("| a |\n|:-:|\n| 1 |");

        Assert.Contains("text-align: center;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ATaskList_KeepsItsBoxesAndWhetherTheyAreTicked()
    {
        string html = Render("- [ ] not done\n- [x] done");

        Assert.Contains("type=\"checkbox\"", html, StringComparison.Ordinal);
        Assert.Contains("checked=\"checked\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AFence_KeepsItsLinesAndLosesItsFence()
    {
        string html = Render("```csharp\nvar x = 1;\n```");

        Assert.Contains("<code", html, StringComparison.Ordinal);
        Assert.Contains("var x = 1;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("```", html, StringComparison.Ordinal);
    }

    [Fact]
    public void FrontMatter_NeverAppears()
    {
        // The same rule the pane follows and the plain text follows: the block is metadata, not
        // content. The pane draws a quiet chip in its place; a paste carries nothing at all.
        string html = Render("---\ntitle: A note\nsecret: do not paste me\n---\n\nBody text.");

        Assert.Contains("Body text.", html, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", html, StringComparison.Ordinal);
        Assert.DoesNotContain("do not paste me", html, StringComparison.Ordinal);
    }

    [Fact]
    public void NoFontSizeOrColourStyling_IsEverCarried()
    {
        // Deliberately unstyled: the pane may be in dark theme and the document being pasted into is
        // white, so carrying the pane's own appearance across would land pale text on white paper.
        string html = Render(
            "# Heading\n\nSome **bold** text with a `span`.\n\n> A quote\n\n- an item\n\n```cs\nvar x = 1;\n```");

        Assert.DoesNotContain("font-", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("color", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<style", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("style=", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ItIsAFragment_NotAWholePage()
        => Assert.DoesNotContain("<html", Render("# Heading"), StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void ARelativeImage_BecomesAnAbsoluteFileUri()
    {
        // The whole point of resolving them: a relative src means nothing once the HTML is inside
        // somebody else's document.
        string basePath = Path.Combine(Path.GetTempPath(), "notes");

        Assert.Contains(
            $"src=\"{Expected(basePath, "assets/shot.png")}\"",
            Render("![a shot](assets/shot.png)", basePath),
            StringComparison.Ordinal);
    }

    [Fact]
    public void AnImageThatIsNotThere_IsStillResolved()
    {
        // Not gated on the file existing: the pane draws a placeholder and still copies the link, and
        // a file that turns up later makes the pasted document right without a second paste.
        string basePath = Path.Combine(Path.GetTempPath(), "notes");

        Assert.Contains(
            Expected(basePath, "missing.png"),
            Render("![gone](missing.png)", basePath),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ARelativeImage_WithNoFolderToResolveAgainst_IsLeftAsItWasWritten()
    {
        // An unsaved buffer has no folder, and an address invented from the working directory would
        // point somewhere the reader never meant.
        string html = Render("![a shot](assets/shot.png)", basePath: null);

        Assert.Contains("src=\"assets/shot.png\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("file:", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ARemoteImage_IsLeftAlone()
        => Assert.Contains(
            "src=\"https://example.com/shot.png\"",
            Render("![remote](https://example.com/shot.png)", Path.GetTempPath()),
            StringComparison.Ordinal);

    [Fact]
    public void AnImageWithNoAddressAtAll_IsLeftAlone()
        => Assert.Contains(
            "src=\"\"",
            Render("![]()", Path.GetTempPath()),
            StringComparison.Ordinal);

    [Fact]
    public void ARelativeLinkThatIsNotAnImage_IsLeftAlone()
    {
        // Images only. A note link is followed inside the editor, and rewriting it would turn a link
        // that opens a tab into one the operating system is asked to open instead.
        string html = Render("[another note](./other-note.md)", Path.GetTempPath());

        Assert.Contains("href=\"./other-note.md\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AnImageNestedInsideOtherMarkup_IsResolvedToo()
    {
        string basePath = Path.Combine(Path.GetTempPath(), "notes");

        Assert.Contains(
            Expected(basePath, "shot.png"),
            Render("A **paragraph with [![a shot](shot.png)](https://example.com) in it**.", basePath),
            StringComparison.Ordinal);
    }

    [Fact]
    public void RawHtml_IsCarriedThroughAsHtml()
    {
        // A deliberate difference from the pane, which shows a raw block as the text it was written
        // as: the author wrote HTML, and this flavour is HTML. Asserted so the difference is a
        // decision on the record rather than something nobody noticed.
        Assert.Contains(
            "<div class=\"callout\">Not a div in the pane.</div>",
            Render("<div class=\"callout\">Not a div in the pane.</div>"),
            StringComparison.Ordinal);
    }

    private static string Expected(string basePath, string url)
        => new Uri(Path.GetFullPath(Path.Combine(basePath, url))).AbsoluteUri;

    private static string Render(string markdown, string? basePath = null)
        => MarkdownHtml.Render(markdown, basePath);
}
