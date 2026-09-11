// Using directives stay at file scope, above the namespace declaration, throughout this project:
// inside a namespace beginning with "Enigma.", the simple name "Avalonia" resolves against the
// referenced Enigma.Avalonia.* assemblies instead of Avalonia itself.
using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia.Media;
using Avalonia.Styling;
using Enigma.MarkdownEditor.Desktop.Views;
using TextMateSharp.Grammars;
using TextMateSharp.Registry;
using TextMateSharp.Themes;

// Both namespaces define a FontStyle, and only Avalonia's is used bare in this file.
using TextMateFontStyle = TextMateSharp.Themes.FontStyle;

namespace Enigma.MarkdownEditor.Desktop.Controls;

/// <summary>
/// Colours the contents of a fenced code block, using the same TextMate grammars and themes the
/// editor pane is highlighted with.
/// </summary>
/// <remarks>
/// <para>
/// The same stack as <see cref="EditorAppearance"/>, and deliberately the same themes: a fence in the
/// preview and the same fence in the editor two panes to its left have to be the same colours, or the
/// reader is looking at two opinions about their own document.
/// </para>
/// <para>
/// Everything here is cached for the life of the process and shared. Building a
/// <see cref="RegistryOptions"/> reads the grammar index, and loading a grammar parses it — tens of
/// milliseconds each, once, behind a preview that re-renders on a timer. The caches are guarded by a
/// lock because a grammar tokenizes through mutable state of its own, and being wrong about that is a
/// crash rather than a wrong colour.
/// </para>
/// <para>
/// Nothing here throws for a document's sake. An unknown language, a grammar that fails to load and a
/// tokenizer that gives up all come out the same way: the code, uncoloured, in the preview's own body
/// colour.
/// </para>
/// </remarks>
public static class MarkdownCodeHighlighter
{
    // Long enough that no realistic line of a note reaches it, short enough that a pathological one
    // cannot hold the render. TextMateSharp returns what it has when the budget runs out.
    private static readonly TimeSpan LineBudget = TimeSpan.FromMilliseconds(500);

    private static readonly Lock Gate = new();
    private static readonly Dictionary<ThemeName, Palette> Palettes = [];

    // Including the misses. A fence whose language nothing knows is the case that costs the most to
    // answer — every grammar definition is scanned for an alias — and a preview re-renders on a timer.
    private static readonly Dictionary<string, string?> Scopes = new(StringComparer.Ordinal);

    /// <summary>
    /// The TextMate scope the grammar for <paramref name="language"/> is registered under.
    /// </summary>
    /// <param name="language">
    /// The word after the fence — <c>csharp</c>, <c>cs</c>, <c>c#</c>, <c>json</c>, <c>bash</c>.
    /// </param>
    /// <returns>The scope name, or <see langword="null"/> when nothing matches.</returns>
    /// <remarks>
    /// Three lookups, because a fence is written by a person rather than by a language id: the id
    /// itself (<c>csharp</c>), the language whose file extension it looks like (<c>cs</c>, <c>sh</c>),
    /// and finally the aliases a language declares (<c>c#</c>, <c>bash</c>). The first that answers
    /// wins.
    /// </remarks>
    public static string? ScopeFor(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return null;
        }

        string name = language.Trim().ToLowerInvariant();

        lock (Gate)
        {
            if (Scopes.TryGetValue(name, out string? cached))
            {
                return cached;
            }

            // Either palette would answer: which grammars exist is a property of the package, not
            // of the theme laid over them.
            RegistryOptions options = PaletteFor(ThemeName.DarkPlus).Options;

            try
            {
                Scopes[name] = Resolve(options, name);
            }
            catch (Exception failure) when (failure is not OutOfMemoryException)
            {
                // Caught broadly for the reason the whole class is: the grammar index arrives from a
                // package, and an unknown language must cost the reader an uncoloured fence rather
                // than a bordered error where their code was.
                Scopes[name] = null;
            }

            return Scopes[name];
        }
    }

    private static string? Resolve(RegistryOptions options, string name)
    {
        if (options.GetScopeByLanguageId(name) is { Length: > 0 } byId)
        {
            return byId;
        }

        if (options.GetScopeByExtension("." + name) is { Length: > 0 } byExtension)
        {
            return byExtension;
        }

        foreach (Language candidate in options.GetAvailableLanguages())
        {
            if (Aliases(candidate, name) && (candidate.Id is { Length: > 0 } id))
            {
                return options.GetScopeByLanguageId(id);
            }
        }

        return null;
    }

    /// <summary>
    /// Splits <paramref name="code"/> into coloured runs, line by line.
    /// </summary>
    /// <param name="code">The contents of the block, without its fences.</param>
    /// <param name="language">The word after the fence. Unknown or absent gives plain lines.</param>
    /// <param name="variant">The variant the window is showing, which picks the TextMate theme.</param>
    /// <returns>One list of runs per line of <paramref name="code"/>, in order.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="code"/> or <paramref name="variant"/> is <see langword="null"/>.
    /// </exception>
    public static IReadOnlyList<IReadOnlyList<MarkdownCodeToken>> Highlight(
        string code,
        string? language,
        ThemeVariant variant)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(variant);

        string[] lines = Lines(code);
        string? scope = ScopeFor(language);

        if (scope is null)
        {
            return Plain(lines);
        }

        lock (Gate)
        {
            Palette palette = PaletteFor(EditorAppearance.ThemeNameFor(variant));

            try
            {
                IGrammar? grammar = palette.Grammar(scope);

                return grammar is null ? Plain(lines) : Tokenize(lines, grammar, palette);
            }
            catch (Exception failure) when (failure is not OutOfMemoryException)
            {
                // Same reason as above, one level down: a grammar that throws mid-document costs the
                // colours of one fence, not the note around it.
                return Plain(lines);
            }
        }
    }

    private static IReadOnlyList<IReadOnlyList<MarkdownCodeToken>> Tokenize(
        string[] lines,
        IGrammar grammar,
        Palette palette)
    {
        List<IReadOnlyList<MarkdownCodeToken>> highlighted = new(lines.Length);
        IStateStack? state = null;

        foreach (string line in lines)
        {
            ITokenizeLineResult result = grammar.TokenizeLine(line, state, LineBudget);

            // Carried from line to line, and that is the whole reason a fence is tokenized as a unit:
            // without it the second line of a multi-line string or comment starts again as code.
            state = result.RuleStack;

            List<MarkdownCodeToken> tokens = [];

            foreach (IToken token in result.Tokens)
            {
                int start = Math.Clamp(token.StartIndex, 0, line.Length);
                int end = Math.Clamp(token.EndIndex, start, line.Length);

                if (end > start)
                {
                    tokens.Add(palette.Token(line[start..end], token.Scopes));
                }
            }

            highlighted.Add(tokens);
        }

        return highlighted;
    }

    private static IReadOnlyList<IReadOnlyList<MarkdownCodeToken>> Plain(string[] lines)
    {
        List<IReadOnlyList<MarkdownCodeToken>> plain = new(lines.Length);

        foreach (string line in lines)
        {
            plain.Add(line.Length == 0 ? [] : [new MarkdownCodeToken(line, foreground: null)]);
        }

        return plain;
    }

    private static string[] Lines(string code)
    {
        // Markdig hands the block's lines back with the newline the source used, and usually one
        // trailing one; keeping it would draw an empty last line inside every fence.
        string trimmed = code.TrimEnd('\n', '\r');
        string[] lines = trimmed.Split('\n');

        for (int index = 0; index < lines.Length; index++)
        {
            lines[index] = lines[index].TrimEnd('\r');
        }

        return lines;
    }

    private static bool Aliases(Language language, string name)
    {
        if (language.Aliases is not { } aliases)
        {
            return false;
        }

        foreach (string alias in aliases)
        {
            if (string.Equals(alias, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static Palette PaletteFor(ThemeName name)
    {
        if (!Palettes.TryGetValue(name, out Palette? palette))
        {
            palette = new Palette(name);
            Palettes.Add(name, palette);
        }

        return palette;
    }

    /// <summary>
    /// One TextMate theme, its registry, and everything loaded through them so far.
    /// </summary>
    /// <remarks>
    /// A registry per theme rather than one registry re-themed: switching a registry's theme throws
    /// away every grammar's cached scope metadata, and the two variants would then invalidate each
    /// other every time the window changed.
    /// </remarks>
    private sealed class Palette
    {
        private readonly Registry _registry;
        private readonly Theme _theme;
        private readonly Dictionary<string, IGrammar?> _grammars = new(StringComparer.Ordinal);
        private readonly Dictionary<int, Color?> _colors = new();

        public Palette(ThemeName name)
        {
            Options = new RegistryOptions(name);
            _registry = new Registry(Options);
            _theme = _registry.GetTheme();
        }

        public RegistryOptions Options { get; }

        public IGrammar? Grammar(string scope)
        {
            if (!_grammars.TryGetValue(scope, out IGrammar? grammar))
            {
                grammar = _registry.LoadGrammar(scope);
                _grammars.Add(scope, grammar);
            }

            return grammar;
        }

        public MarkdownCodeToken Token(string text, IList<string> scopes)
        {
            int foreground = -1;
            TextMateFontStyle style = TextMateFontStyle.NotSet;

            foreach (ThemeTrieElementRule rule in _theme.Match(scopes))
            {
                if ((foreground == -1) && (rule.foreground > 0))
                {
                    foreground = rule.foreground;
                }

                if ((style == TextMateFontStyle.NotSet) && (rule.fontStyle > 0))
                {
                    style = rule.fontStyle;
                }
            }

            return new MarkdownCodeToken(
                text,
                ColorOf(foreground),
                (style & TextMateFontStyle.Bold) != 0,
                (style & TextMateFontStyle.Italic) != 0,
                (style & TextMateFontStyle.Underline) != 0);
        }

        private Color? ColorOf(int id)
        {
            if (id <= 0)
            {
                return null;
            }

            if (!_colors.TryGetValue(id, out Color? color))
            {
                color = Parse(_theme.GetColor(id));
                _colors.Add(id, color);
            }

            return color;
        }

        private static Color? Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            // A theme's colours are hex strings, and a malformed one is worth an uncoloured token
            // rather than an exception out of a render.
            return Color.TryParse(value, out Color parsed) ? parsed : null;
        }
    }
}
