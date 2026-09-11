using System;
using System.Collections.Generic;
using System.Linq;
using Enigma.MarkdownEditor.Desktop.Views;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Views;

/// <summary>
/// What the editor actually colours, taken from the grammar rather than from the screen.
/// </summary>
/// <remarks>
/// <para>
/// The pane hands one scope name to TextMate and lets the grammar do the rest, so everything the
/// user sees is decided here: whether markdown structure is recognised, whether a fenced block is
/// handed to the language it names, and what happens when it names one that does not exist. Driving
/// the grammar directly is the only way to assert any of that without a window.
/// </para>
/// <para>
/// It is third-party artwork being asserted on, deliberately. The grammars arrive with
/// TextMateSharp.Grammars, and a bump that drops one would otherwise show up as a code block that
/// quietly stopped being coloured.
/// </para>
/// </remarks>
public sealed class MarkdownHighlightingTests
{
    private const string EmbeddedPrefix = "meta.embedded.block.";

    [Fact]
    public void TheMarkdownExtension_ResolvesToTheMarkdownScope()
    {
        RegistryOptions options = new(ThemeName.DarkPlus);

        Assert.Equal("text.html.markdown", options.GetScopeByExtension(EditorAppearance.MarkdownExtension));
    }

    [Fact]
    public void TheMarkdownScope_LoadsAGrammar()
    {
        IGrammar grammar = LoadMarkdownGrammar();

        Assert.Equal("Markdown", grammar.GetName());
    }

    [Theory]
    [InlineData("# Heading", "entity.name.section.markdown")]
    [InlineData("Some **bold** text.", "markup.bold.markdown")]
    [InlineData("Some `code` inline.", "markup.inline.raw.string.markdown")]
    [InlineData("> quoted", "markup.quote.markdown")]
    [InlineData("- item", "markup.list.unnumbered.markdown")]
    [InlineData("1. item", "markup.list.numbered.markdown")]
    public void MarkdownStructure_IsRecognised(string line, string expectedScope)
        => Assert.Contains(expectedScope, ScopesOf(line));

    [Theory]
    [InlineData("csharp", "public class Foo { }", "csharp")]
    [InlineData("json", "{ \"a\": 1 }", "json")]
    [InlineData("bash", "echo hi", "shellscript")]
    public void AFencedBlock_IsColouredAsTheLanguageItNames(string fence, string code, string language)
    {
        IReadOnlyCollection<string> scopes = ScopesOf($"```{fence}", code, "```");

        Assert.Contains(EmbeddedPrefix + language, scopes);
    }

    [Fact]
    public void AFencedBlockInALanguageNobodyHas_DegradesToPlainText()
    {
        IReadOnlyCollection<string> scopes = ScopesOf("```wubbalubba", "dub dub", "```");

        // Still inside a code block — it is the language that is missing, not the fence.
        Assert.Contains("markup.fenced_code.block.markdown", scopes);
        Assert.DoesNotContain(scopes, scope => scope.StartsWith(EmbeddedPrefix, StringComparison.Ordinal));
    }

    private static IGrammar LoadMarkdownGrammar()
    {
        RegistryOptions options = new(ThemeName.DarkPlus);
        Registry registry = new(options);

        return registry.LoadGrammar(options.GetScopeByExtension(EditorAppearance.MarkdownExtension));
    }

    /// <summary>
    /// Tokenizes the lines as one document and returns every scope any token carried, which is what
    /// the theme is looked up by and therefore what decides whether something is coloured at all.
    /// </summary>
    private static IReadOnlyCollection<string> ScopesOf(params string[] lines)
    {
        IGrammar grammar = LoadMarkdownGrammar();
        HashSet<string> scopes = [];
        IStateStack? state = null;

        foreach (string line in lines)
        {
            // The state carried between lines is the whole point for a fenced block: the opening
            // fence is what puts the grammar into the embedded language for the lines beneath it.
            ITokenizeLineResult result = grammar.TokenizeLine(line, state, TimeSpan.MaxValue);
            state = result.RuleStack;

            scopes.UnionWith(result.Tokens.SelectMany(token => token.Scopes));
        }

        return scopes;
    }
}
