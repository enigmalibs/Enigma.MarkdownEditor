namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// One line address of an ex range.
/// </summary>
/// <param name="Kind">What it counts from.</param>
/// <param name="Line">The number, for <see cref="VimExAddressKind.Line"/>; unused otherwise.</param>
/// <param name="Offset">
/// What was added or taken away after it — <c>+3</c>, <c>-1</c>, a bare <c>+</c> for one — summed.
/// </param>
/// <remarks>
/// A bare offset, <c>+3</c> with nothing before it, counts from the caret's line, as vim's does; the
/// parser writes that down as <see cref="VimExAddressKind.Current"/> so the engine has one rule.
/// </remarks>
internal readonly record struct VimExAddress(VimExAddressKind Kind, int Line, int Offset)
{
    /// <summary>
    /// <c>$</c> — the last line.
    /// </summary>
    public static VimExAddress Last => new(VimExAddressKind.Last, 0, 0);

    /// <summary>
    /// A line by its number.
    /// </summary>
    /// <param name="line">The number as typed.</param>
    /// <returns>The address.</returns>
    public static VimExAddress Number(int line) => new(VimExAddressKind.Line, line, 0);
}
