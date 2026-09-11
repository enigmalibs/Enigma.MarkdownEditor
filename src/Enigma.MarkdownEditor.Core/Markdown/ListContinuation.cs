using System;
using System.Collections.Generic;

namespace Enigma.MarkdownEditor.Core.Markdown;

/// <summary>
/// What Enter, Tab and Shift+Tab do inside a markdown list, as decisions over plain strings.
/// </summary>
/// <remarks>
/// <para>
/// None of this touches the editor. Each method is given the line the caret is on — and, for the two
/// indentation ones, the lines above it — and answers with a <see cref="TextEdit"/> or with
/// <see langword="null"/> for "this is not ours, let the editor do what it always does". That is what
/// makes the whole continuation matrix testable without a text control, and what keeps the rules in
/// one place rather than spread through a key handler.
/// </para>
/// <para>
/// Indentation is compared in columns rather than characters, so a tab-indented file nests the same
/// way a space-indented one does; rewritten indentation is always spaces, because that is the form
/// every markdown parser agrees on.
/// </para>
/// </remarks>
public static class ListContinuation
{
    /// <summary>
    /// How far Tab indents an item that has nothing above it to line up with.
    /// </summary>
    public const int FallbackIndentWidth = 2;

    // CommonMark's own cap. It also keeps the number an int, which is what the continuation counts on.
    private const int MaximumOrderedDigits = 9;

    /// <summary>
    /// Takes a line apart as a list, task or quote item.
    /// </summary>
    /// <param name="line">The line's text, without its terminator.</param>
    /// <returns>The parts, or <see langword="null"/> when the line is not an item at all.</returns>
    /// <remarks>
    /// A marker only counts when white-space or the end of the line follows it, which is what keeps
    /// <c>**bold**</c>, <c>---</c> and <c>-x</c> from being read as list items.
    /// </remarks>
    public static ListItemPrefix? Parse(string? line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return null;
        }

        int index = SkipSpaces(line, 0);
        string indent = line[..index];

        int quoteStart = index;

        while ((index < line.Length) && (line[index] == '>'))
        {
            index++;

            // The one space after a marker belongs to the quote rather than to the content, so a
            // quote continues as "> " rather than as ">".
            if ((index < line.Length) && (line[index] == ' '))
            {
                index++;
            }
        }

        string quote = line[quoteStart..index];
        string marker = ReadMarker(line, ref index);

        if ((marker.Length == 0) && (quote.Length == 0))
        {
            return null;
        }

        int spacingStart = index;
        index = SkipSpaces(line, index);
        string markerSpacing = line[spacingStart..index];

        string checkbox = ReadCheckbox(line, marker, markerSpacing, ref index);
        int checkboxSpacingStart = index;

        if (checkbox.Length > 0)
        {
            index = SkipSpaces(line, index);
        }

        return new ListItemPrefix(
            indent,
            quote,
            marker,
            markerSpacing,
            checkbox,
            line[checkboxSpacingStart..index],
            line[index..]);
    }

    /// <summary>
    /// Decides what Enter does on a list, task or quote line.
    /// </summary>
    /// <param name="line">The line the caret is on, without its terminator.</param>
    /// <param name="column">Where the caret sits in that line.</param>
    /// <returns>
    /// The edit that continues the item, the edit that clears an empty one, or <see langword="null"/>
    /// when the line is not an item or the caret is still inside its marker.
    /// </returns>
    /// <remarks>
    /// An empty item is not continued but removed, leaving a blank line: that is how a list is ended
    /// without reaching for the mouse, and it is why the marker is cleared rather than repeated.
    /// </remarks>
    public static TextEdit? OnEnter(string? line, int column)
    {
        if ((line is null) || (column < 0) || (column > line.Length))
        {
            return null;
        }

        if (Parse(line) is not { } prefix)
        {
            return null;
        }

        // Inside the marker itself. Splitting a line there would leave half a bullet behind, so this
        // is one of the places the editor's own Enter is the right answer.
        if (column < prefix.Length)
        {
            return null;
        }

        if (prefix.IsEmpty)
        {
            return new TextEdit(0, line.Length, string.Empty, 0);
        }

        string continuation = "\n" + prefix.Continuation;

        return new TextEdit(column, 0, continuation, column + continuation.Length);
    }

    /// <summary>
    /// Decides what a line opened below an item starts with — vim's <c>o</c>.
    /// </summary>
    /// <param name="line">The line the caret is on, without its terminator.</param>
    /// <returns>
    /// The text the new line starts with, or the empty string when there is nothing to continue.
    /// </returns>
    /// <remarks>
    /// A string rather than a <see cref="TextEdit"/>, unlike <see cref="OnEnter"/>: the caller is the
    /// vim engine, which opens the line itself and owns both the offsets and the caret, so an edit
    /// would carry two values it would only discard. An empty item answers with nothing — a list the
    /// user is ending is not a list to continue — and so does a line that is not an item at all,
    /// which leaves <c>o</c> doing exactly what it has always done.
    /// </remarks>
    public static string OpenBelow(string? line)
        => Parse(line) is { IsEmpty: false } prefix ? prefix.Continuation : string.Empty;

    /// <summary>
    /// Decides what a line opened above an item starts with — vim's <c>O</c>.
    /// </summary>
    /// <param name="line">The line the caret is on, without its terminator.</param>
    /// <returns>
    /// The text the new line starts with, or the empty string when there is nothing to continue.
    /// </returns>
    /// <remarks>
    /// <see cref="ListItemPrefix.Opening"/> rather than <see cref="ListItemPrefix.Continuation"/>:
    /// the new line goes <em>before</em> this item and takes its place in the list, so it carries the
    /// same marker rather than the next one.
    /// </remarks>
    public static string OpenAbove(string? line)
        => Parse(line) is { IsEmpty: false } prefix ? prefix.Opening : string.Empty;

    /// <summary>
    /// Decides what Tab does on a list, task or quote line: indent it under the item above.
    /// </summary>
    /// <param name="line">The line the caret is on, without its terminator.</param>
    /// <param name="column">Where the caret sits in that line.</param>
    /// <param name="precedingLines">
    /// The lines above it, nearest first. Enumerated lazily and only as far as the first answer, so
    /// the caller can walk the document backwards rather than materialize it.
    /// </param>
    /// <returns>The edit, or <see langword="null"/> when there is nothing to change.</returns>
    /// <remarks>
    /// The item lines up with the content of the nearest item above it that is no deeper than it is —
    /// 2 columns under <c>- foo</c>, 3 under <c>1. foo</c>, 6 under <c>- [ ] foo</c>. That is the
    /// indentation CommonMark reads as nesting. An item that is already a child of the one above it is
    /// therefore left alone: markdown has no deeper level to move it to without a sibling to nest
    /// under, and adding spaces anyway would only make the file wider without changing what it means.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="precedingLines"/> is <see langword="null"/>.</exception>
    public static TextEdit? OnIndent(string? line, int column, IEnumerable<string> precedingLines)
    {
        ArgumentNullException.ThrowIfNull(precedingLines);

        if ((line is null) || (Parse(line) is not { } prefix))
        {
            return null;
        }

        int target = prefix.IndentWidth + FallbackIndentWidth;

        foreach (ListItemPrefix candidate in ItemsAbove(precedingLines))
        {
            if (candidate.IndentWidth <= prefix.IndentWidth)
            {
                target = candidate.ContentColumn;
                break;
            }
        }

        return Reindent(prefix, column, target);
    }

    /// <summary>
    /// Decides what Shift+Tab does on a list, task or quote line: outdent it to the level above.
    /// </summary>
    /// <param name="line">The line the caret is on, without its terminator.</param>
    /// <param name="column">Where the caret sits in that line.</param>
    /// <param name="precedingLines">The lines above it, nearest first, enumerated lazily.</param>
    /// <returns>The edit, or <see langword="null"/> when the item is already flush left.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="precedingLines"/> is <see langword="null"/>.</exception>
    public static TextEdit? OnOutdent(string? line, int column, IEnumerable<string> precedingLines)
    {
        ArgumentNullException.ThrowIfNull(precedingLines);

        if ((line is null) || (Parse(line) is not { } prefix))
        {
            return null;
        }

        int target = 0;

        foreach (ListItemPrefix candidate in ItemsAbove(precedingLines))
        {
            if (candidate.IndentWidth < prefix.IndentWidth)
            {
                target = candidate.IndentWidth;
                break;
            }
        }

        return Reindent(prefix, column, target);
    }

    private static int SkipSpaces(string line, int index)
    {
        while ((index < line.Length) && ((line[index] == ' ') || (line[index] == '\t')))
        {
            index++;
        }

        return index;
    }

    private static bool EndsMarker(string line, int index)
        => (index >= line.Length) || (line[index] == ' ') || (line[index] == '\t');

    private static string ReadMarker(string line, ref int index)
    {
        if (index >= line.Length)
        {
            return string.Empty;
        }

        if (line[index] is '-' or '*' or '+')
        {
            if (!EndsMarker(line, index + 1))
            {
                return string.Empty;
            }

            string bullet = line[index..(index + 1)];
            index++;

            return bullet;
        }

        int digitsEnd = index;

        while ((digitsEnd < line.Length)
            && char.IsAsciiDigit(line[digitsEnd])
            && ((digitsEnd - index) < MaximumOrderedDigits))
        {
            digitsEnd++;
        }

        if ((digitsEnd == index) || (digitsEnd >= line.Length) || (line[digitsEnd] is not ('.' or ')')))
        {
            return string.Empty;
        }

        if (!EndsMarker(line, digitsEnd + 1))
        {
            return string.Empty;
        }

        string ordered = line[index..(digitsEnd + 1)];
        index = digitsEnd + 1;

        return ordered;
    }

    private static string ReadCheckbox(string line, string marker, string markerSpacing, ref int index)
    {
        // A box belongs to a list item and needs the marker's space in front of it, so a quote line
        // that happens to start with a bracket is left as the text it is.
        if ((marker.Length == 0) || (markerSpacing.Length == 0) || ((index + 2) >= line.Length))
        {
            return string.Empty;
        }

        if ((line[index] != '[') || (line[index + 2] != ']') || (line[index + 1] is not (' ' or 'x' or 'X')))
        {
            return string.Empty;
        }

        if (!EndsMarker(line, index + 3))
        {
            return string.Empty;
        }

        string checkbox = line[index..(index + 3)];
        index += 3;

        return checkbox;
    }

    // Walks up from the item, skipping the blank lines a loose list is full of, and stops at the first
    // line that is not an item at all — that line is where the list this item belongs to began.
    private static IEnumerable<ListItemPrefix> ItemsAbove(IEnumerable<string> precedingLines)
    {
        foreach (string line in precedingLines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (Parse(line) is not { } prefix)
            {
                yield break;
            }

            yield return prefix;
        }
    }

    private static TextEdit? Reindent(ListItemPrefix prefix, int column, int targetWidth)
    {
        string indent = new(' ', Math.Max(targetWidth, 0));

        if (string.Equals(indent, prefix.Indent, StringComparison.Ordinal))
        {
            return null;
        }

        // A caret that was inside the old indentation has nowhere of its own to be, so it lands where
        // the new indentation ends; anywhere else it keeps its distance from the item's text.
        int caret = column <= prefix.Indent.Length
            ? indent.Length
            : (column - prefix.Indent.Length) + indent.Length;

        return new TextEdit(0, prefix.Indent.Length, indent, caret);
    }
}
