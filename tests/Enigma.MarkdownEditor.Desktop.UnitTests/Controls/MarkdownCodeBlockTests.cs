using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Enigma.MarkdownEditor.Desktop.Controls;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Controls;

/// <summary>
/// Fenced code: what the tokenizer makes of it, and what the block draws.
/// </summary>
/// <remarks>
/// The tokenizer is asserted directly because it is the half that decides the colours, and it needs
/// no window to do it; the control is asserted through a rendered preview, because what matters there
/// is that the runs reach the tree and follow the window's variant.
/// </remarks>
public sealed class MarkdownCodeBlockTests
{
    // A scope both Dark+ and Light+ mark bold, which is what makes the weight assertion below mean
    // something. It is markdown inside a markdown fence, which is legal and is what the grammar reads.
    private const string BoldSource = "**bold** and *italic*";

    private const string MarkdownFence =
        """
        ```markdown
        **bold** and *italic*
        ```
        """;

    private const string CSharpFence =
        """
        ```csharp
        public sealed class Note
        {
            public string Title { get; init; } = string.Empty;
        }
        ```
        """;

    [Theory]
    [InlineData("csharp")]
    [InlineData("cs")]
    [InlineData("c#")]
    [InlineData("json")]
    [InlineData("bash")]
    [InlineData("sh")]
    [InlineData("JSON")]
    public void ALanguageAFenceActuallyNames_HasAGrammar(string language)
        => Assert.NotNull(MarkdownCodeHighlighter.ScopeFor(language));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-language")]
    public void ALanguageNothingKnows_HasNone(string? language)
        => Assert.Null(MarkdownCodeHighlighter.ScopeFor(language));

    [Fact]
    public void AKnownLanguage_ComesBackColoured()
    {
        IReadOnlyList<MarkdownCodeToken> tokens = Tokens("public sealed class Note", "csharp");

        Assert.Contains(tokens, token => token.Text == "public");
        Assert.Contains(tokens, token => token.Foreground is not null);
    }

    [Fact]
    public void AnUnknownLanguage_ComesBackAsPlainLines()
    {
        IReadOnlyList<IReadOnlyList<MarkdownCodeToken>> lines =
            MarkdownCodeHighlighter.Highlight("no language at all\nsecond line", "not-a-language", ThemeVariant.Dark);

        Assert.Equal(2, lines.Count);
        Assert.All(lines, line => Assert.Single(line));
        Assert.All(lines, line => Assert.Null(line[0].Foreground));
        Assert.Equal("no language at all", lines[0][0].Text);
    }

    [Fact]
    public void EveryLine_SurvivesTokenizing()
    {
        IReadOnlyList<IReadOnlyList<MarkdownCodeToken>> lines =
            MarkdownCodeHighlighter.Highlight("{\n  \"a\": 1,\n\n  \"b\": 2\n}", "json", ThemeVariant.Dark);

        // Blank lines included: a fence is code, and dropping the empty line in the middle of it would
        // change what the reader is looking at.
        Assert.Equal(5, lines.Count);
        Assert.Empty(lines[2]);
    }

    [Fact]
    public void AConstructThatSpansLines_StaysItselfOnTheSecondOne()
    {
        IReadOnlyList<IReadOnlyList<MarkdownCodeToken>> lines = MarkdownCodeHighlighter.Highlight(
            """
            /* a comment
               that carries on */
            var x = 1;
            """,
            "csharp",
            ThemeVariant.Dark);

        // The tokenizer's state is carried from line to line. Without it the second line starts again
        // as code and comes out in the colours of whatever it happens to look like.
        Color? comment = lines[0].Last(token => !string.IsNullOrWhiteSpace(token.Text)).Foreground;

        Assert.NotNull(comment);
        Assert.Equal(comment, lines[1][^1].Foreground);
    }

    [Fact]
    public void TheTwoVariants_ColourTheSameTokenDifferently()
    {
        Color? dark = Keyword(ThemeVariant.Dark);
        Color? light = Keyword(ThemeVariant.Light);

        Assert.NotNull(dark);
        Assert.NotNull(light);
        Assert.NotEqual(dark, light);
    }

    [AvaloniaFact]
    public void AFence_IsRenderedAsACodeBlockCarryingItsLanguage()
    {
        MarkdownCodeBlock block = PreviewTree.Block<MarkdownCodeBlock>(CSharpFence);

        Assert.Equal("csharp", block.Language);
        Assert.Contains("public sealed class Note", block.Code, System.StringComparison.Ordinal);
        Assert.Contains(MarkdownPreviewTheme.CodeBlockClass, block.Classes);
    }

    [AvaloniaFact]
    public void AnInfoStringWithMoreThanALanguage_KeepsOnlyTheLanguage()
    {
        MarkdownCodeBlock block = PreviewTree.Block<MarkdownCodeBlock>("```json title=settings.json\n{}\n```");

        Assert.Equal("json", block.Language);
    }

    [AvaloniaFact]
    public void AFenceWithNoLanguage_IsStillACodeBlock()
    {
        MarkdownCodeBlock block = PreviewTree.Block<MarkdownCodeBlock>("```\nno language at all\n```");

        Assert.Null(block.Language);

        // Foreground is left unset rather than set to a colour, so the code inherits the preview's own
        // body colour instead of a grammar's idea of what plain text looks like.
        Assert.All(Runs(block), run => Assert.False(run.IsSet(TextElement.ForegroundProperty)));
    }

    [AvaloniaFact]
    public void AnIndentedBlock_IsCodeToo()
    {
        MarkdownCodeBlock block = PreviewTree.Block<MarkdownCodeBlock>("    var x = 1;");

        Assert.Null(block.Language);
        Assert.Contains("var x = 1;", block.Code, System.StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void TheRenderedFence_IsMonospaceAndDoesNotWrap()
    {
        SelectableTextBlock text = Assert.Single(
            PreviewTree.Descendants<SelectableTextBlock>(
                PreviewTree.Block<MarkdownCodeBlock>(CSharpFence, typography: PreviewTree.NamedFamilies)));

        Assert.Equal(new FontFamily(PreviewTree.NamedFamilies.CodeFontFamily), text.FontFamily);

        // Wrapped code says something the code does not — a continuation reads as a statement — so a
        // long line goes behind the block's own scroll bar instead.
        Assert.Equal(TextWrapping.NoWrap, text.TextWrapping);

        ScrollViewer scroller = Assert.Single(
            PreviewTree.Descendants<ScrollViewer>(PreviewTree.Block<MarkdownCodeBlock>(CSharpFence)));

        Assert.Equal(ScrollBarVisibility.Auto, scroller.HorizontalScrollBarVisibility);
    }

    [AvaloniaFact]
    public void TheRenderedFence_KeepsALineBreakPerSourceLine()
    {
        MarkdownCodeBlock block = PreviewTree.Block<MarkdownCodeBlock>(CSharpFence);
        SelectableTextBlock text = Assert.Single(PreviewTree.Descendants<SelectableTextBlock>(block));

        Assert.Equal(3, PreviewTree.Inlines<LineBreak>(text).Count);
    }

    [AvaloniaFact]
    public void SwitchingVariant_RecoloursTheFenceInPlace()
    {
        ThemeVariant? requested = Application.Current!.RequestedThemeVariant;

        try
        {
            Application.Current.RequestedThemeVariant = ThemeVariant.Dark;

            MarkdownPreview preview = PreviewTree.Preview(CSharpFence);
            MarkdownCodeBlock block = Assert.IsType<MarkdownCodeBlock>(Assert.Single(preview.Blocks));

            IReadOnlyList<IBrush?> dark = [.. Runs(block).Select(run => run.Foreground)];

            Application.Current.RequestedThemeVariant = ThemeVariant.Light;

            // Token colours come out of a TextMate theme as literal values, so they are the one thing
            // in the preview a DynamicResource cannot keep in step — the block re-reads them itself.
            IReadOnlyList<IBrush?> light = [.. Runs(block).Select(run => run.Foreground)];

            Assert.NotEqual(
                dark.Select(brush => brush?.ToString()),
                light.Select(brush => brush?.ToString()));

            // Recoloured, not rebuilt: the same block is still the one on screen.
            Assert.Same(block, Assert.Single(preview.Blocks));
        }
        finally
        {
            Application.Current.RequestedThemeVariant = requested;
        }
    }

    [AvaloniaFact]
    public void WhatTheThemeMarksBold_IsRenderedAtTheNormalWeightWithItsColourKept()
    {
        // Not vacuous, and asserted for both variants because the block re-reads its colours from
        // whichever one the window is in: if Dark+ and Light+ ever stopped marking a scope bold there
        // would be nothing left for the renderer to ignore, and this test would be proving nothing.
        Assert.Contains(Tokens(BoldSource, "markdown", ThemeVariant.Dark), token => token.IsBold);
        Assert.Contains(Tokens(BoldSource, "markdown", ThemeVariant.Light), token => token.IsBold);

        IReadOnlyList<Run> runs = Runs(PreviewTree.Block<MarkdownCodeBlock>(MarkdownFence));

        // The weight is dropped on purpose: the themes mark whole scopes bold, so a fence came out
        // heavier than the same text in the editor, which paints its tokens through another path.
        Assert.All(runs, run => Assert.False(run.IsSet(TextElement.FontWeightProperty)));
        Assert.All(runs, run => Assert.Equal(FontWeight.Normal, run.FontWeight));

        // Colour is not what was dropped — in a preview nobody types into, it is what carries the
        // syntax on its own.
        Assert.Contains(runs, run => run.IsSet(TextElement.ForegroundProperty));
    }

    [AvaloniaFact]
    public void TheBlocksAroundAFence_StillRender()
    {
        IReadOnlyList<Control> blocks = PreviewTree.Blocks($"Before.\n\n{CSharpFence}\n\nAfter.");

        Assert.Equal(3, blocks.Count);
        Assert.Equal("Before.", PreviewTree.Text(blocks[0]));
        Assert.Equal("After.", PreviewTree.Text(blocks[2]));
    }

    private static IReadOnlyList<MarkdownCodeToken> Tokens(
        string code,
        string language,
        ThemeVariant? variant = null)
        => [.. MarkdownCodeHighlighter
            .Highlight(code, language, variant ?? ThemeVariant.Dark)
            .SelectMany(line => line)];

    private static Color? Keyword(ThemeVariant variant)
        => MarkdownCodeHighlighter
            .Highlight("public sealed class Note", "csharp", variant)[0]
            .First(token => token.Text == "public")
            .Foreground;

    private static IReadOnlyList<Run> Runs(MarkdownCodeBlock block)
        => PreviewTree.Inlines<Run>(Assert.Single(PreviewTree.Descendants<SelectableTextBlock>(block)));
}
