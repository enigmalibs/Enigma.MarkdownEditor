using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Enigma.MarkdownEditor.Core.Markdown;

/// <summary>
/// A markdown document as the preview pane reads it: the same text, without the markup that produced
/// it. What a copy out of the rendered pane puts on the clipboard.
/// </summary>
/// <remarks>
/// <para>
/// Re-parsed rather than read back off the control tree, and deliberately: the visuals carry a table
/// as nested grids, a fence as coloured token runs and a task as a templated checkbox, so recovering
/// text from them would mean encoding the renderer's structure in a second place. The AST is the same
/// input the renderer itself was handed.
/// </para>
/// <para>
/// It mirrors the renderer's drop rules block for block, so what is copied is what is on screen: the
/// front-matter chip and a thematic rule are drawn but say nothing, an HTML block reads as the literal
/// text it was written as, and a block kind the preview does not draw contributes nothing at all. That
/// correspondence is what <see cref="Blocks"/> exists to let a test assert — see its remarks.
/// </para>
/// <para>
/// Markup goes: a heading is bare, emphasis and code spans keep their text and lose their delimiters,
/// a link keeps its label. What the reader can see stays: a list item keeps the marker drawn beside it
/// and its nesting, a task keeps its box as <c>[ ]</c> or <c>[x]</c>, a fence keeps its lines exactly
/// and loses only the fence itself, an image leaves <c>[alt text]</c> where it sat, and a table row is
/// tab-separated so a paste into a spreadsheet lands one cell per column.
/// </para>
/// </remarks>
public static class MarkdownPlainText
{
    // Two spaces per level of nesting. The renderer indents by a marker column whose width is a
    // measurement, which is not a thing plain text has; what has to survive is that a nested item
    // reads as nested.
    private const int IndentWidth = 2;

    /// <summary>
    /// Renders <paramref name="markdown"/> as the plain text a copy of the whole pane produces.
    /// </summary>
    /// <param name="markdown">The document text. May be empty.</param>
    /// <returns>
    /// The text, blocks separated by one blank line, with <see cref="Environment.NewLine"/> between
    /// lines. Empty when the document renders nothing at all.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="markdown"/> is <see langword="null"/>.</exception>
    public static string Render(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        List<string> parts = [];

        foreach (string block in Blocks(markdown))
        {
            // The blocks that are drawn but say nothing — the chip and the rules — would otherwise
            // each come out as a blank line of their own.
            if (block.Length > 0)
            {
                parts.Add(block);
            }
        }

        return string.Join(Environment.NewLine + Environment.NewLine, parts);
    }

    /// <summary>
    /// The text of every block the preview draws for <paramref name="markdown"/>, in document order.
    /// </summary>
    /// <param name="markdown">The document text. May be empty.</param>
    /// <returns>
    /// One entry per drawn block. An entry is empty for a block that is drawn but carries no text of
    /// its own — the front-matter chip and a thematic rule — and a block kind the preview does not
    /// draw at all has no entry.
    /// </returns>
    /// <remarks>
    /// The projection <see cref="Render"/> is built from, exposed because it is the only honest way to
    /// assert that this and the renderer still agree: a block kind added to one and not the other
    /// changes this list's length or order, which is what the drift test reads. Application code
    /// wanting the clipboard's text wants <see cref="Render"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="markdown"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<string> Blocks(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        MarkdownDocument document = MarkdownPipelineFactory.Parse(markdown);
        List<string> blocks = [];

        foreach (Block block in document)
        {
            if (BlockText(block) is { } text)
            {
                // Built with '\n' throughout and normalized once here, at the one point every block's
                // text passes through, rather than at each of the dozen places one is appended.
                blocks.Add(text.ReplaceLineEndings());
            }
        }

        return blocks;
    }

    // The same arms, in the same order, as MarkdownVisualBuilder.Visual(Block): null is a block the
    // preview draws nothing for, and an empty string is one it draws without any text in it. Front
    // matter stays ahead of the HTML arm because Markdig models it as an HtmlBlock, and a fence ahead
    // of the plain code arm because a fence is a CodeBlock.
    private static string? BlockText(Block block) => block switch
    {
        YamlFrontMatterBlock => string.Empty,
        HeadingBlock heading => InlineText(heading.Inline),
        ParagraphBlock paragraph => InlineText(paragraph.Inline),
        ListBlock list => ListText(list, depth: 0),
        QuoteBlock quote => QuoteText(quote),
        Table table => TableText(table),
        FencedCodeBlock fenced => fenced.Lines.ToString(),
        CodeBlock code => code.Lines.ToString(),
        ThematicBreakBlock => string.Empty,
        HtmlBlock html => html.Lines.ToString(),
        _ => null,
    };

    private static string ListText(ListBlock list, int depth)
    {
        List<string> items = [];

        foreach (Block child in list)
        {
            if (child is ListItemBlock item)
            {
                items.Add(ItemText(list, item, depth));
            }
        }

        return string.Join('\n', items);
    }

    private static string ItemText(ListBlock list, ListItemBlock item, int depth)
    {
        string marker = Marker(list, item);
        string indent = new(' ', depth * IndentWidth);

        // Under the item's own text rather than under its marker, which is where the pane wraps it:
        // the marker sits in a column of its own and the content starts after it.
        string continuation = indent + new string(' ', marker.Length + 1);

        StringBuilder text = new();
        text.Append(indent).Append(marker);

        bool onMarkerLine = true;

        foreach (Block child in item)
        {
            if (child is ListBlock nested)
            {
                // Its own depth carries its own indent, so it is appended exactly as it comes.
                text.Append('\n').Append(ListText(nested, depth + 1));
                onMarkerLine = false;

                continue;
            }

            if (BlockText(child) is not { Length: > 0 } part)
            {
                continue;
            }

            string wrapped = part.Replace("\n", "\n" + continuation, StringComparison.Ordinal);

            if (onMarkerLine)
            {
                text.Append(' ').Append(wrapped);
                onMarkerLine = false;
            }
            else
            {
                text.Append('\n').Append(continuation).Append(wrapped);
            }
        }

        return text.ToString();
    }

    // The marker the reader is looking at: the box for a task, because the box IS the marker in the
    // pane, and otherwise exactly what the renderer draws in the marker column.
    private static string Marker(ListBlock list, ListItemBlock item)
    {
        if (TaskOf(item) is { } task)
        {
            return task.Checked ? "[x]" : "[ ]";
        }

        return list.IsOrdered
            ? string.Create(CultureInfo.InvariantCulture, $"{item.Order}{list.OrderedDelimiter}")
            : "•";
    }

    // Copied from the renderer rather than shared: the renderer's copy is what decides whether a box
    // is drawn, and this one is what decides whether a box is written. That the two must agree is
    // what the drift test is for.
    private static TaskList? TaskOf(ListItemBlock item)
        => (item.Count > 0) && (item[0] is ParagraphBlock { Inline: { } inline })
            ? inline.FirstChild as TaskList
            : null;

    private static string QuoteText(QuoteBlock quote)
    {
        List<string> parts = [];

        foreach (Block child in quote)
        {
            if (BlockText(child) is { Length: > 0 } text)
            {
                parts.Add(text);
            }
        }

        return string.Join("\n\n", parts);
    }

    private static string TableText(Table table)
    {
        List<List<string>> rows = [];
        int width = 0;

        foreach (Block child in table)
        {
            if (child is not TableRow row)
            {
                continue;
            }

            List<string> cells = [];
            int next = 0;

            foreach (Block content in row)
            {
                if (content is not TableCell cell)
                {
                    continue;
                }

                // Placed by its own column, like the renderer places it in the grid: a row with fewer
                // cells than the header leaves the gap empty instead of shifting the rest left, which
                // is the whole point of separating the columns with tabs.
                int column = (cell.ColumnIndex >= 0) ? cell.ColumnIndex : next;

                while (cells.Count <= column)
                {
                    cells.Add(string.Empty);
                }

                cells[column] = CellText(cell);
                next = column + Math.Max(1, cell.ColumnSpan);
            }

            width = Math.Max(width, cells.Count);
            rows.Add(cells);
        }

        List<string> lines = [];

        foreach (List<string> cells in rows)
        {
            while (cells.Count < width)
            {
                cells.Add(string.Empty);
            }

            lines.Add(string.Join('\t', cells));
        }

        return string.Join('\n', lines);
    }

    private static string CellText(TableCell cell)
    {
        List<string> parts = [];

        foreach (Block child in cell)
        {
            if (BlockText(child) is { Length: > 0 } text)
            {
                parts.Add(text);
            }
        }

        // Flattened, and it has to be: a row is one tab-separated line, so a newline inside a cell
        // would end the row halfway through it.
        return string.Join(' ', parts).Replace("\n", " ", StringComparison.Ordinal);
    }

    private static string InlineText(ContainerInline? inlines)
    {
        StringBuilder text = new();
        AppendInlines(text, inlines);

        return text.ToString();
    }

    private static void AppendInlines(StringBuilder text, Inline? first)
    {
        bool afterTaskBox = false;

        for (Inline? current = first is ContainerInline container ? container.FirstChild : first;
             current is not null;
             current = current.NextSibling)
        {
            if (current is TaskList)
            {
                // The box is written as the item's marker, so the inline itself contributes nothing —
                // but the source wrote `- [x] done`, and the space after the box is still in the
                // literal that follows it. Left in, a task's text would sit one space further right
                // than an ordinary bullet's. The renderer trims it for the same reason.
                afterTaskBox = true;

                continue;
            }

            int before = text.Length;
            AppendInline(text, current);

            if (afterTaskBox)
            {
                afterTaskBox = false;

                while ((before < text.Length) && (text[before] == ' '))
                {
                    text.Remove(before, 1);
                }
            }
        }
    }

    // The same arms, in the same order, as MarkdownVisualBuilder.Convert(Inline) — an emphasis, a link
    // and an image are all ContainerInlines, so the general arm has to come last.
    private static void AppendInline(StringBuilder text, Inline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                text.Append(literal.Content.AsSpan());
                break;

            case EmphasisInline emphasis:
                // The delimiters go and the text stays: bold, italic and struck-through all read as
                // themselves on screen and have no plain-text spelling.
                AppendInlines(text, emphasis);
                break;

            case CodeInline code:
                text.Append(code.Content);
                break;

            case LinkInline { IsImage: true } image:
                // Where the picture was. The pane shows the image itself, or a bordered placeholder
                // naming the link; neither is text, and the alt text is what the author wrote for
                // exactly this case.
                text.Append('[');
                AppendInlines(text, image);
                text.Append(']');
                break;

            case LinkInline link:
                AppendLabel(text, link, link.Url);
                break;

            case AutolinkInline autolink:
                text.Append(autolink.Url);
                break;

            case LineBreakInline line:
                // A soft break is a space, because the pane wraps the paragraph to its own width
                // rather than to where the author ended the source line.
                text.Append(line.IsHard ? '\n' : ' ');
                break;

            case TaskList:
                break;

            case HtmlEntityInline entity:
                text.Append(entity.Transcoded.AsSpan());
                break;

            case HtmlInline html:
                // Raw HTML as the literal text it was written as, which is what the pane shows.
                text.Append(html.Tag);
                break;

            case ContainerInline nested:
                AppendInlines(text, nested);
                break;

            default:
                break;
        }
    }

    private static void AppendLabel(StringBuilder text, ContainerInline label, string? url)
    {
        int before = text.Length;
        AppendInlines(text, label);

        // A link written as a bare address carries no children — the address IS the label, which is
        // how the renderer draws it too.
        if (text.Length == before)
        {
            text.Append(url);
        }
    }
}
