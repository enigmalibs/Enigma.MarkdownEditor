namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// Which mode the vim engine is in, and therefore what a keystroke means.
/// </summary>
/// <remarks>
/// Four modes, and only three kinds of keystroke: in <see cref="Insert"/> a key is text, and in every
/// other mode it is a command. What separates the two visual modes from <see cref="Normal"/> is not
/// what the keys mean but what an operator acts on — a selection that is already there, rather than a
/// motion typed after it.
/// </remarks>
public enum VimMode
{
    /// <summary>
    /// Keys are commands. Nothing typed reaches the document.
    /// </summary>
    Normal,

    /// <summary>
    /// Keys are text. Everything except <c>Esc</c> reaches the document untouched.
    /// </summary>
    Insert,

    /// <summary>
    /// <c>v</c> — keys are commands, and there is a stretch of characters under the selection for an
    /// operator to act on. Motions move the caret and the selection follows it.
    /// </summary>
    Visual,

    /// <summary>
    /// <c>V</c> — the same, over whole lines: the selection covers every line between the anchor's and
    /// the caret's, whatever column either is in.
    /// </summary>
    VisualLine,
}
