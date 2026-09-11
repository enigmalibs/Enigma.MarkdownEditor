namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// What a line address in an ex range counts from.
/// </summary>
/// <remarks>
/// Symbolic rather than resolved, because the parser reads a finished line with no buffer in reach:
/// <c>.</c> is whichever line the caret is on when the command runs, and <c>'&lt;</c> is wherever the
/// last selection was. The engine turns each into a number, with the address's offset added.
/// </remarks>
internal enum VimExAddressKind
{
    /// <summary>
    /// A line number as typed — <c>42</c>.
    /// </summary>
    Line,

    /// <summary>
    /// <c>.</c> — the caret's line. Also what a bare <c>+3</c> counts from.
    /// </summary>
    Current,

    /// <summary>
    /// <c>$</c> — the last line of the buffer.
    /// </summary>
    Last,

    /// <summary>
    /// <c>'&lt;</c> — the first line of the last selection.
    /// </summary>
    VisualStart,

    /// <summary>
    /// <c>'&gt;</c> — the last line of it.
    /// </summary>
    VisualEnd,
}
