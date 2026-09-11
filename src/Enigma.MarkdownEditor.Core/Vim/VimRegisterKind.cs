namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// Whether what is in the register is a stretch of characters or a run of whole lines.
/// </summary>
/// <remarks>
/// The one thing about a register that changes what <c>p</c> does with it, and the reason a register
/// is a pair rather than a string. Text taken out by <c>dd</c>, <c>yy</c>, <c>cc</c> or <c>Y</c> is
/// lines and goes back as lines, above or below the one the caret is on; text taken out by <c>x</c>,
/// <c>dw</c> or <c>cw</c> is characters and goes back inside the line, beside the caret.
/// </remarks>
public enum VimRegisterKind
{
    /// <summary>
    /// A stretch of characters, put back beside the caret.
    /// </summary>
    Characterwise,

    /// <summary>
    /// Whole lines, put back above or below the caret's line.
    /// </summary>
    Linewise,
}
