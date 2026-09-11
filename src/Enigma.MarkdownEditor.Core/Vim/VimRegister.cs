namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// What a register holds: text and whether it is characters or whole lines.
/// </summary>
/// <param name="Text">
/// The text itself. For <see cref="VimRegisterKind.Linewise"/> content it is the lines joined by the
/// buffer's own terminator, <em>without</em> a trailing one — the terminator a put needs is added
/// where the text lands, which is what keeps a register taken from the last line of a file from
/// pasting differently to one taken from the middle.
/// </param>
/// <param name="Kind">Whether it is characters or whole lines.</param>
/// <remarks>
/// The content of one register, whichever one — the unnamed, a named one, the yank register or a
/// macro, which holds keys rather than text and is characterwise. The store that gives them names is
/// <see cref="VimRegisters"/>. It is a value rather than a mutable object so that a test can name a
/// whole expected register in one expression, which is how the operator matrix asserts on what was
/// taken as well as on what is left.
/// </remarks>
public readonly record struct VimRegister(string Text, VimRegisterKind Kind)
{
    /// <summary>
    /// A register holding a stretch of characters.
    /// </summary>
    /// <param name="text">The characters.</param>
    /// <returns>The register.</returns>
    public static VimRegister Characters(string text) => new(text, VimRegisterKind.Characterwise);

    /// <summary>
    /// A register holding whole lines.
    /// </summary>
    /// <param name="text">The lines, joined by the buffer's terminator and without a trailing one.</param>
    /// <returns>The register.</returns>
    public static VimRegister Lines(string text) => new(text, VimRegisterKind.Linewise);
}
