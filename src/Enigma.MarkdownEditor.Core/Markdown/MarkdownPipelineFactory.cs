using System;
using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Syntax;

namespace Enigma.MarkdownEditor.Core.Markdown;

/// <summary>
/// The one Markdig pipeline the preview parses with, and the parse itself.
/// </summary>
/// <remarks>
/// <para>
/// One pipeline for the whole application, built once. A <c>MarkdownPipeline</c> is immutable and
/// thread-safe once built, and building one means instantiating every extension and re-ordering the
/// parser lists — work that has no business happening on each keystroke behind a live preview.
/// </para>
/// <para>
/// The extension set is GFM's core plus what a notes editor actually writes. It is deliberately
/// short: every extension adds parsers to the same lists, and one that nothing renders would only
/// change what the AST means without changing what the reader sees.
/// </para>
/// </remarks>
public static class MarkdownPipelineFactory
{
    /// <summary>
    /// The shared pipeline. Built on first use and reused for the life of the process.
    /// </summary>
    public static MarkdownPipeline Shared { get; } = Create();

    /// <summary>
    /// Builds a new pipeline configured exactly like <see cref="Shared"/>.
    /// </summary>
    /// <returns>The configured pipeline.</returns>
    /// <remarks>
    /// Public so a test can assert the configuration of a pipeline it built itself rather than only
    /// of the shared one. Application code wants <see cref="Shared"/>.
    /// </remarks>
    public static MarkdownPipeline Create()
        => new MarkdownPipelineBuilder()
            .UsePipeTables()
            .UseTaskLists()

            // Strikethrough alone. The other three EmphasisExtras — subscript, superscript and
            // inserted/marked text — are non-standard syntax that would start claiming `~`, `^` and
            // `++` in documents that meant them literally.
            .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
            .UseAutoLinks()
            .UseYamlFrontMatter()

            // What the scroll sync in PHASE03 is built on: without it, an inline's Line and Column
            // are relative to the block it sits in rather than to the document, so no rendered block
            // can be matched back to a line in the editor.
            .UsePreciseSourceLocation()
            .Build();

    /// <summary>
    /// Parses <paramref name="markdown"/> with <see cref="Shared"/>.
    /// </summary>
    /// <param name="markdown">The document text. May be empty.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="markdown"/> is <see langword="null"/>.</exception>
    public static MarkdownDocument Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        return Markdig.Markdown.Parse(markdown, Shared);
    }
}
