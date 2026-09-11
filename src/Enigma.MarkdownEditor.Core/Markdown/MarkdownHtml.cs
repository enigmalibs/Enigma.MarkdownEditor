using System;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Enigma.MarkdownEditor.Core.Markdown;

/// <summary>
/// A markdown document as HTML, for the rich-text flavour a copy out of the preview pane puts on the
/// clipboard beside the plain text.
/// </summary>
/// <remarks>
/// <para>
/// The same pipeline the pane renders from, so the two agree on what the markup means: a table is a
/// table in both, a task list has its boxes in both, and an extension the pane does not run cannot
/// change what is pasted. What differs is only the target — Avalonia visuals there, HTML here.
/// </para>
/// <para>
/// <b>Deliberately unstyled.</b> No fonts, no sizes, no colours, no stylesheet. The application being
/// pasted into applies its own, which is the only correct answer when the pane may be in dark theme
/// and the document receiving the paste is white — carrying the pane's own colours across would land
/// pale grey text on white paper. Structure is what a rich paste is for; appearance belongs to
/// wherever it lands.
/// </para>
/// <para>
/// The one thing rewritten is an image link. A relative <c>src</c> means nothing once the HTML has
/// left this process, so a link that resolves to a file on this machine is made an absolute
/// <c>file://</c> URI — through <see cref="MarkdownImageSource"/>, the same resolution the renderer
/// draws from, rather than a second set of rules that could disagree with it.
/// </para>
/// </remarks>
public static class MarkdownHtml
{
    /// <summary>
    /// Renders <paramref name="markdown"/> as the HTML a copy of the whole pane produces.
    /// </summary>
    /// <param name="markdown">The document text. May be empty.</param>
    /// <param name="basePath">
    /// The folder the document lives in, which relative image links are resolved against. A
    /// <see langword="null"/> or empty value leaves them exactly as the document wrote them — an
    /// unsaved buffer has no folder, and an absolute link invented from the working directory would
    /// point somewhere the reader never meant.
    /// </param>
    /// <returns>
    /// The HTML, as a fragment rather than a whole page: no <c>&lt;html&gt;</c>, no
    /// <c>&lt;head&gt;</c>, no styling. Empty when the document renders nothing at all.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="markdown"/> is <see langword="null"/>.</exception>
    public static string Render(string markdown, string? basePath)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        MarkdownDocument document = MarkdownPipelineFactory.Parse(markdown);

        Absolutize(document, basePath);

        return Markdig.Markdown.ToHtml(document, MarkdownPipelineFactory.Shared);
    }

    // Rewritten on the AST before rendering rather than on the rendered string afterwards: a regex
    // over the output would have to tell an <img src> from the same characters inside a code block or
    // an attribute of a literal HTML block, and Markdig has already made that distinction for us.
    private static void Absolutize(MarkdownDocument document, string? basePath)
    {
        foreach (LinkInline link in document.Descendants<LinkInline>())
        {
            if (!link.IsImage)
            {
                continue;
            }

            MarkdownImageSource source = MarkdownImageSource.Resolve(basePath, link.Url);

            // Local is the only kind worth rewriting. A remote link already travels, and an
            // unresolvable one has no file to point at — writing a guess would turn a link the reader
            // can still read into one that silently points at nothing.
            //
            // Whether the file is there right now is deliberately not checked: the pane draws a
            // placeholder for a missing image and still copies the link, and a file that appears
            // later makes the pasted document right without the paste having to be repeated.
            if ((source.Kind == MarkdownImageKind.Local) && (source.LocalPath is { Length: > 0 } path))
            {
                link.Url = new Uri(path).AbsoluteUri;
            }
        }
    }
}
