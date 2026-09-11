using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Enigma.MarkdownEditor.Core.Markdown;
using Enigma.MarkdownEditor.Desktop.Controls;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Controls;

/// <summary>
/// Selecting and copying out of the preview: what a select-all reaches, which of the two copies the
/// renderer asks for, and what it deliberately leaves to the text blocks themselves.
/// </summary>
/// <remarks>
/// <para>
/// The two gestures are driven through the headless window rather than by calling the methods they
/// reach, because "the pane handles <c>Ctrl+A</c> and leaves <c>Ctrl+C</c> alone unless the whole
/// document is selected" is the claim — and a test that calls <see cref="MarkdownPreview.SelectAll"/>
/// itself proves neither half of it. Everything else calls the method, which is where those paths
/// end up anyway.
/// </para>
/// <para>
/// What reaches the clipboard is not asserted here and cannot be: this control knows nothing about
/// one. It reports which copy was asked for, and the pane's ViewModel is what writes it — see
/// <c>MarkdownPreviewViewModelTests</c>.
/// </para>
/// </remarks>
public sealed class MarkdownPreviewCopyTests
{
    private const string FixtureFolder = "Fixtures";

    private const string Document = "# Heading\n\nA paragraph with words in it.\n\n- an item\n- another item";

    public static TheoryData<string> Fixtures()
    {
        TheoryData<string> data = [];

        foreach (string name in FixtureNames())
        {
            data.Add(name);
        }

        return data;
    }

    [AvaloniaFact]
    public void SelectAll_ReachesEveryTextBlockInThePane()
    {
        MarkdownPreview preview = Hosted(Document).Preview;

        preview.SelectAll();

        IReadOnlyList<SelectableTextBlock> texts = Texts(preview);

        Assert.NotEmpty(texts);
        Assert.All(texts, block => Assert.NotEmpty(block.SelectedText ?? string.Empty));
    }

    [AvaloniaFact]
    public void ClearSelection_PutsEveryOneOfThemBack()
    {
        MarkdownPreview preview = Hosted(Document).Preview;

        preview.SelectAll();
        preview.ClearSelection();

        Assert.All(Texts(preview), block => Assert.Empty(block.SelectedText ?? string.Empty));
    }

    [AvaloniaFact]
    public void CtrlA_InThePane_SelectsTheWholeDocument()
    {
        (MarkdownPreview preview, Window host) = Hosted(Document);

        Assert.True(preview.Focus());
        host.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);

        Assert.All(Texts(preview), block => Assert.NotEmpty(block.SelectedText ?? string.Empty));
    }

    [AvaloniaFact]
    public void CtrlC_AfterASelectAll_AsksForTheWholeNote()
    {
        (MarkdownPreview preview, Window host) = Hosted(Document);
        List<MarkdownCopyRequest> requests = Requests(preview);

        Assert.True(preview.Focus());
        host.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);
        host.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.Control);

        MarkdownCopyRequest request = Assert.Single(requests);

        Assert.False(request.IsSelection);
        Assert.Null(request.SelectedText);
    }

    [AvaloniaFact]
    public void CtrlC_WithoutASelectAll_IsLeftToTheBlocksOwnHandler()
    {
        // The path that already worked before any of this existed: a selection inside one paragraph
        // is copied by the block itself, and taking the gesture over would break it.
        (MarkdownPreview preview, Window host) = Hosted(Document);
        List<MarkdownCopyRequest> requests = Requests(preview);

        Assert.True(preview.Focus());
        host.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.Control);

        Assert.Empty(requests);
    }

    [AvaloniaFact]
    public void ACopyWithOneParagraphSelected_CarriesThatTextAndNoMore()
    {
        MarkdownPreview preview = Hosted(Document).Preview;
        List<MarkdownCopyRequest> requests = Requests(preview);

        SelectableTextBlock paragraph = Texts(preview)[1];
        paragraph.SelectionStart = 2;
        paragraph.SelectionEnd = 11;

        preview.RequestCopy();

        MarkdownCopyRequest request = Assert.Single(requests);

        Assert.True(request.IsSelection);
        Assert.Equal("paragraph", request.SelectedText);
    }

    [AvaloniaFact]
    public void ACopyWithNothingSelected_AsksForTheWholeNote()
    {
        MarkdownPreview preview = Hosted(Document).Preview;
        List<MarkdownCopyRequest> requests = Requests(preview);

        preview.RequestCopy();

        Assert.False(Assert.Single(requests).IsSelection);
    }

    [AvaloniaFact]
    public void ARender_EndsTheDocumentWideSelection()
    {
        // Typing in the editor rebuilds the pane a quarter of a second later, and the blocks holding
        // the selection go with it. A Ctrl+C after that must not still claim the whole note.
        (MarkdownPreview preview, Window host) = Hosted(Document);
        List<MarkdownCopyRequest> requests = Requests(preview);

        Assert.True(preview.Focus());
        host.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);

        preview.Markdown = Document + "\n\nAnd a sentence that was just typed.";

        host.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.Control);

        Assert.Empty(requests);
    }

    [AvaloniaFact]
    public void APressInThePane_EndsTheDocumentWideSelection()
    {
        (MarkdownPreview preview, Window host) = Hosted(Document);
        List<MarkdownCopyRequest> requests = Requests(preview);

        Assert.True(preview.Focus());
        host.KeyPressQwerty(PhysicalKey.A, RawInputModifiers.Control);

        SelectableTextBlock paragraph = Texts(preview)[1];
        Point centre = paragraph
            .TranslatePoint(new Point(paragraph.Bounds.Width / 2, paragraph.Bounds.Height / 2), host)!
            .Value;

        host.MouseDown(centre, MouseButton.Left);
        host.MouseUp(centre, MouseButton.Left);

        host.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.Control);

        Assert.Empty(requests);
    }

    [AvaloniaFact]
    public void WithNothingRendered_ThereIsNothingToCopyAndNothingIsAsked()
    {
        MarkdownPreview preview = Hosted(markdown: null).Preview;
        List<MarkdownCopyRequest> requests = Requests(preview);

        Assert.False(preview.HasContent);

        preview.RequestCopy();

        Assert.Empty(requests);
    }

    [AvaloniaFact]
    public void ADocumentWithSomethingInIt_HasSomethingToCopy()
        => Assert.True(Hosted(Document).Preview.HasContent);

    [AvaloniaFact]
    public void AHandlerThatThrows_IsReportedRatherThanLeftUnhandled()
    {
        // Raised from a key handler, so an exception out of it would reach the dispatcher rather than
        // any caller. It goes to the same place a render failure does.
        MarkdownPreview preview = Hosted(Document).Preview;
        List<Exception> failures = [];

        preview.RenderFailed += (_, failure) => failures.Add(failure);
        preview.CopyRequested += (_, _) => throw new InvalidOperationException("nope");

        preview.RequestCopy();

        Assert.Single(failures);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Fixtures))]
    public void EveryFixture_ProjectsOneBlockOfTextPerBlockTheRendererDraws(string fileName)
    {
        // The drift guard, and the reason MarkdownPlainText.Blocks is public: a block kind added to
        // the renderer and not to the projection — or the other way round — changes this count or
        // this order, and fails here rather than quietly copying something nobody can see.
        string markdown = File.ReadAllText(Path.Combine(Folder(), fileName));

        Assert.Equal(
            PreviewTree.Blocks(markdown, Folder()).Count,
            MarkdownPlainText.Blocks(markdown).Count);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Fixtures))]
    public void EveryFixture_CopiesWithSomethingInItAndNoFenceLines(string fileName)
    {
        string text = Render(fileName);

        Assert.NotEmpty(text);
        Assert.DoesNotContain("```", text, StringComparison.Ordinal);
    }

    [AvaloniaTheory]
    [MemberData(nameof(Fixtures))]
    public void EveryFixture_RendersHtmlThatCarriesNoAppearanceOfItsOwn(string fileName)
    {
        // The rich-text flavour, over the same corpus. Unstyled is the claim, and it is not one a
        // rule-by-rule test can make on its own: an extension or a Markdig upgrade that started
        // emitting a colour would show up here first, on real documents.
        string html = MarkdownHtml.Render(File.ReadAllText(Path.Combine(Folder(), fileName)), Folder());

        Assert.NotEmpty(html);
        Assert.DoesNotContain("font-", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("color", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<style", html, StringComparison.OrdinalIgnoreCase);

        // The one exception, and it is content rather than appearance: a table author who wrote
        // `:---:` asked for centred cells, and the pane centres them too.
        foreach (string style in Styles(html))
        {
            Assert.StartsWith("text-align:", style, StringComparison.Ordinal);
        }
    }

    [AvaloniaFact]
    public void TheImagesFixture_RendersEveryRelativeImageAsAnAbsoluteFileUri()
    {
        string html = MarkdownHtml.Render(File.ReadAllText(Path.Combine(Folder(), "08-images.md")), Folder());

        Assert.Contains($"src=\"{new Uri(Path.Combine(Folder(), "assets", "one.png")).AbsoluteUri}\"", html, StringComparison.Ordinal);

        // The remote one keeps its address — it already travels — and the empty link has nothing to
        // resolve, so neither is rewritten.
        Assert.Contains("src=\"https://example.com/shot.png\"", html, StringComparison.Ordinal);
        Assert.Contains("src=\"\"", html, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TheHeadingsFixture_CopiesItsHeadingsBare()
    {
        string text = Render("01-headings.md");

        Assert.Contains("Heading one", text, StringComparison.Ordinal);
        Assert.DoesNotContain("#", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TheListsFixture_KeepsItsMarkersAndItsNesting()
    {
        string text = Render("03-lists.md");

        Assert.Contains("• first item", text, StringComparison.Ordinal);
        Assert.Contains("  • nested under the third", text, StringComparison.Ordinal);
        Assert.Contains("1. numbered one", text, StringComparison.Ordinal);
        Assert.Contains("1) one", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TheTasksFixture_KeepsItsBoxesAndLosesItsBullets()
    {
        string text = Render("04-tasks.md");

        Assert.Contains("[ ] not done yet", text, StringComparison.Ordinal);
        Assert.Contains("[x] done", text, StringComparison.Ordinal);
        Assert.Contains("  [ ] nested and not done", text, StringComparison.Ordinal);
        Assert.DoesNotContain("- [", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TheQuotesFixture_DropsItsRules()
    {
        string text = Render("05-quotes-and-rules.md");

        Assert.Contains("A single-line quote.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("---", text, StringComparison.Ordinal);
        Assert.DoesNotContain("***", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TheTablesFixture_SeparatesItsColumnsWithTabs()
    {
        string text = Render("06-tables.md");

        Assert.Contains("left\tcentre\tright", text, StringComparison.Ordinal);
        Assert.Contains("one\ttwo\tthree", text, StringComparison.Ordinal);

        // No pipes at all: the whole point of the tabs is that a paste into a spreadsheet lands one
        // cell per column rather than one cell holding the row's markup.
        Assert.DoesNotContain("|", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TheFencesFixture_CopiesItsCodeVerbatim()
    {
        string text = Render("07-fences.md");

        Assert.Contains("public sealed class Note", text, StringComparison.Ordinal);
        Assert.Contains("    public string Title { get; init; } = string.Empty;", text, StringComparison.Ordinal);
        Assert.Contains("no language at all", text, StringComparison.Ordinal);
        Assert.Contains("var indented = true;", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TheImagesFixture_LeavesTheAltTextWhereEachPictureSat()
    {
        string text = Render("08-images.md");

        Assert.Contains("[a screenshot]", text, StringComparison.Ordinal);
        Assert.Contains("[one] [two]", text, StringComparison.Ordinal);
        Assert.Contains("before [inline] after", text, StringComparison.Ordinal);
        Assert.DoesNotContain("![", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TheFrontMatterFixture_CopiesNeitherTheBlockNorTheChip()
    {
        string text = Render("09-front-matter.md");

        // Not the metadata, and not the one line the pane puts in its place either: a chip saying
        // the note has front matter is a thing to look at, not a thing to paste.
        Assert.DoesNotContain("A note with front matter", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Front matter ·", text, StringComparison.Ordinal);
        Assert.Contains("A paragraph, so the fixture has something to render.", text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TheLinksFixture_KeepsTheLabelsAndDropsTheAddresses()
    {
        string text = Render("10-links-and-html.md");

        Assert.Contains("An inline link to the docs", text, StringComparison.Ordinal);
        Assert.Contains("https://example.com/angle", text, StringComparison.Ordinal);
        Assert.Contains("<div class=\"callout\">Not rendered as a div.</div>", text, StringComparison.Ordinal);
        Assert.DoesNotContain("](", text, StringComparison.Ordinal);
    }

    private static string Render(string fileName)
        => MarkdownPlainText.Render(File.ReadAllText(Path.Combine(Folder(), fileName)));

    // Every style="…" the HTML carries, so the no-appearance test can say what is in them rather than
    // only that the attribute is absent.
    private static IReadOnlyList<string> Styles(string html)
        => [.. Regex
            .Matches(html, "style=\"(?<value>[^\"]*)\"", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Select(match => match.Groups["value"].Value)];

    private static IReadOnlyList<SelectableTextBlock> Texts(MarkdownPreview preview)
        => PreviewTree.Descendants<SelectableTextBlock>(preview);

    private static List<MarkdownCopyRequest> Requests(MarkdownPreview preview)
    {
        List<MarkdownCopyRequest> requests = [];
        preview.CopyRequested += (_, request) => requests.Add(request);

        return requests;
    }

    /// <summary>
    /// The control in a shown window, laid out, so a selection has text to be taken out of and a
    /// pointer has somewhere to land.
    /// </summary>
    private static (MarkdownPreview Preview, Window Host) Hosted(string? markdown)
    {
        MarkdownPreview preview = new();
        Window host = PreviewTree.Host(preview);

        preview.Typography = PreviewTree.Typography;
        preview.Markdown = markdown;

        host.UpdateLayout();

        return (preview, host);
    }

    private static IReadOnlyList<string> FixtureNames()
        => [.. Directory
            .EnumerateFiles(Folder(), "*.md")
            .Select(Path.GetFileName)
            .OfType<string>()
            .OrderBy(name => name, StringComparer.Ordinal)];

    private static string Folder() => Path.Combine(AppContext.BaseDirectory, FixtureFolder);
}
