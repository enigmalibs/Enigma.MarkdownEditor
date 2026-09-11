namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// One line of the buffer, as much of it as the engine ever needs.
/// </summary>
/// <param name="Number">The 1-based line number.</param>
/// <param name="Offset">Where the line starts, as an offset into the whole buffer.</param>
/// <param name="Text">The line's text, without its terminator.</param>
/// <param name="DelimiterLength">How long its terminator is; zero for the last line of the buffer.</param>
/// <remarks>
/// Handed out whole rather than queried a property at a time: a motion asks about a line three or
/// four times, and one snapshot is both cheaper and impossible to read inconsistently.
/// </remarks>
public readonly record struct VimLine(int Number, int Offset, string Text, int DelimiterLength)
{
    /// <summary>
    /// Gets the line's length, without its terminator.
    /// </summary>
    public int Length => Text.Length;

    /// <summary>
    /// Gets the offset just past the line's last character, which is where its terminator begins.
    /// </summary>
    public int EndOffset => Offset + Text.Length;

    /// <summary>
    /// Gets a value indicating whether the line has nothing on it.
    /// </summary>
    public bool IsEmpty => Text.Length == 0;

    /// <summary>
    /// Gets the furthest offset normal mode lets the caret sit at on this line.
    /// </summary>
    /// <remarks>
    /// The last character, not the end of the line — that is the rule that makes <c>x</c> at the end
    /// of a line delete something and <c>l</c> stop one short of the terminator. An empty line is the
    /// exception every editor has to make: there is no character to sit on, so the caret sits where
    /// the line starts.
    /// </remarks>
    public int LastCaretOffset => Text.Length == 0 ? Offset : EndOffset - 1;

    /// <summary>
    /// Gets the offset of the line's first character that is not a space or a tab.
    /// </summary>
    /// <remarks>
    /// Where <c>I</c>, <c>gg</c> and <c>G</c> leave the caret. A line that is all white-space has no
    /// such character, and answers with its last caret position rather than running past it.
    /// </remarks>
    public int FirstNonBlankOffset
    {
        get
        {
            for (int index = 0; index < Text.Length; index++)
            {
                if ((Text[index] != ' ') && (Text[index] != '\t'))
                {
                    return Offset + index;
                }
            }

            return LastCaretOffset;
        }
    }
}
