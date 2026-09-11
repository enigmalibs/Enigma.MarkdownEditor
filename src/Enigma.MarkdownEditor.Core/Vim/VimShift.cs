using System;
using System.Text;

namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// What <c>&gt;</c> and <c>&lt;</c> write in place of the lines they shift.
/// </summary>
/// <remarks>
/// <para>
/// Kept apart from the engine so that the engine keeps its one rule about changing the buffer: one
/// <see cref="IVimBuffer.Replace"/> per command. The lines are rewritten here as a single string and
/// the engine writes it over them in one call, which is what makes <c>3&gt;&gt;</c> one <c>u</c>.
/// </para>
/// <para>
/// An indent is measured in columns and rewritten from scratch, the way vim's own shift does: what
/// the line had is added up, the shift is applied, and the total is written back as spaces or as
/// tabs-then-spaces according to <see cref="VimOptions.ExpandTab"/>. A tab counts as
/// <see cref="VimOptions.ShiftWidth"/> columns, because that is what the editor draws it as.
/// </para>
/// </remarks>
internal static class VimShift
{
    /// <summary>
    /// Rewrites a run of lines shifted by <paramref name="steps"/> shift-widths.
    /// </summary>
    /// <param name="buffer">The buffer the lines are in.</param>
    /// <param name="firstLine">The first line to shift, by number.</param>
    /// <param name="lastLine">The last line to shift, by number.</param>
    /// <param name="steps">How many shift-widths, negative to shift left.</param>
    /// <param name="options">Where the width and the tab rule come from.</param>
    /// <returns>The span to replace and the text to replace it with, terminators included.</returns>
    /// <remarks>
    /// An empty line stays empty, which is vim's rule: <c>&gt;</c> does not leave a line of spaces
    /// behind where there was nothing. A line of nothing but blanks is not empty and is shifted like
    /// any other.
    /// </remarks>
    public static (int Start, int Length, string Text) Shift(
        IVimBuffer buffer,
        int firstLine,
        int lastLine,
        int steps,
        VimOptions options)
    {
        int width = Math.Max(1, options.ShiftWidth);
        StringBuilder text = new();

        VimLine first = buffer.GetLine(firstLine);
        VimLine last = buffer.GetLine(lastLine);

        for (int number = firstLine; number <= lastLine; number++)
        {
            VimLine line = buffer.GetLine(number);

            if (!line.IsEmpty)
            {
                int leading = LeadingBlanks(line.Text);
                int columns = Math.Max(0, Columns(line.Text, leading, width) + (steps * width));

                text.Append(Indent(columns, width, options.ExpandTab)).Append(line.Text, leading, line.Text.Length - leading);
            }

            if (number < lastLine)
            {
                text.Append(buffer.GetText(line.EndOffset, line.DelimiterLength));
            }
        }

        return (first.Offset, last.EndOffset - first.Offset, text.ToString());
    }

    private static int LeadingBlanks(string text)
    {
        int index = 0;

        while ((index < text.Length) && (text[index] is ' ' or '\t'))
        {
            index++;
        }

        return index;
    }

    private static int Columns(string text, int leading, int width)
    {
        int columns = 0;

        for (int index = 0; index < leading; index++)
        {
            // A tab advances to the next tab stop rather than by a fixed amount, so a tab after two
            // spaces is worth two columns and a tab on its own is worth the full width.
            columns += text[index] == '\t' ? width - (columns % width) : 1;
        }

        return columns;
    }

    private static string Indent(int columns, int width, bool expandTab)
    {
        if (expandTab)
        {
            return new string(' ', columns);
        }

        return new string('\t', columns / width) + new string(' ', columns % width);
    }
}
