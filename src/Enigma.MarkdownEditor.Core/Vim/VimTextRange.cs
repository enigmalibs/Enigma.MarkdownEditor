namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// The stretch of the buffer an operator works on.
/// </summary>
/// <param name="Start">Where it begins.</param>
/// <param name="End">Where it ends, one past the last character it covers.</param>
/// <param name="Kind">Whether it is a stretch of characters or a run of whole lines.</param>
/// <remarks>
/// <para>
/// Two offsets and nothing else, deliberately. A linewise range is expressed as the offsets of its
/// <em>content</em> — the first line's start to the last line's end, terminator excluded — rather
/// than as a pair of line numbers, so that yanking is the same operation for both kinds and only the
/// deletion has to think about which line terminator goes with the text.
/// </para>
/// <para>
/// Internal because it is the shape of an answer between <see cref="VimRanges"/> and
/// <see cref="VimEngine"/>, not part of what the editor sees.
/// </para>
/// </remarks>
internal readonly record struct VimTextRange(int Start, int End, VimRegisterKind Kind)
{
    /// <summary>
    /// Gets how many characters the range covers.
    /// </summary>
    public int Length => End - Start;

    /// <summary>
    /// Gets a value indicating whether the range covers nothing.
    /// </summary>
    /// <remarks>
    /// True of a legitimate linewise range — one empty line has no content — so only a characterwise
    /// range being empty means "this operator has nothing to do".
    /// </remarks>
    public bool IsEmpty => End <= Start;

    /// <summary>
    /// Gets a value indicating whether the range is a run of whole lines.
    /// </summary>
    public bool IsLinewise => Kind == VimRegisterKind.Linewise;
}
