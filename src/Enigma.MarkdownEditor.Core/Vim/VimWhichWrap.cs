namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// Where <c>Space</c> and <c>Backspace</c> land: one character along, and across a line break when
/// there is no character left on the line.
/// </summary>
/// <remarks>
/// Named after vim's <c>whichwrap</c> option, whose default is what this implements: the space bar
/// and Backspace cross line breaks and <c>h</c> and <c>l</c> do not. One step at a time, so the
/// engine's own count loop drives both the bare motion and the range behind an operator through the
/// same two functions.
/// </remarks>
internal static class VimWhichWrap
{
    /// <summary>
    /// Where <c>Space</c> goes: one character right, or the next line's first character from the
    /// last one — the next line's start when the caret's line is empty.
    /// </summary>
    /// <param name="buffer">The buffer to step through.</param>
    /// <param name="offset">Where the caret is now.</param>
    /// <returns>The offset one step on, or the caret's own at the end of the buffer.</returns>
    public static int Right(IVimBuffer buffer, int offset)
    {
        VimLine line = buffer.GetLineByOffset(offset);

        if (offset < line.LastCaretOffset)
        {
            return offset + 1;
        }

        return line.DelimiterLength > 0 ? line.EndOffset + line.DelimiterLength : offset;
    }

    /// <summary>
    /// Where <c>Backspace</c> goes: one character left, or the previous line's last character from
    /// the first one.
    /// </summary>
    /// <param name="buffer">The buffer to step through.</param>
    /// <param name="offset">Where the caret is now.</param>
    /// <returns>The offset one step back, or the caret's own at the start of the buffer.</returns>
    public static int Left(IVimBuffer buffer, int offset)
    {
        VimLine line = buffer.GetLineByOffset(offset);

        if (offset > line.Offset)
        {
            return offset - 1;
        }

        return line.Number > 1 ? buffer.GetLine(line.Number - 1).LastCaretOffset : offset;
    }
}
