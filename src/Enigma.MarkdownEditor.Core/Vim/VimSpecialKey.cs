namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// A key that types no character.
/// </summary>
/// <remarks>
/// Only the keys the engine gives a meaning to are named. Everything else the editor reports — a
/// function key, Delete, Insert — arrives as <see cref="Other"/>, which normal mode swallows: a key
/// with no vim meaning must still not fall through to the text editor underneath and edit the
/// document.
/// </remarks>
public enum VimSpecialKey
{
    /// <summary>
    /// Not a special key: the <see cref="VimKey"/> carries a character.
    /// </summary>
    None,

    /// <summary>
    /// Escape — back to normal mode, and cancel anything half-typed.
    /// </summary>
    Escape,

    /// <summary>
    /// Left arrow, the same motion as <c>h</c>.
    /// </summary>
    Left,

    /// <summary>
    /// Right arrow, the same motion as <c>l</c>.
    /// </summary>
    Right,

    /// <summary>
    /// Up arrow, the same motion as <c>k</c>.
    /// </summary>
    Up,

    /// <summary>
    /// Down arrow, the same motion as <c>j</c>.
    /// </summary>
    Down,

    /// <summary>
    /// Enter — runs what is on the command line; in a command mode, the same motion as <c>+</c>.
    /// </summary>
    /// <remarks>
    /// Two meanings by position, both vim's: while a command line is open the key finishes it, and
    /// otherwise it is a motion to the first non-blank of the line below. In insert mode it is the
    /// editor's, and splits the line.
    /// </remarks>
    Enter,

    /// <summary>
    /// Backspace — rubs out the last character of the command line, and closes it when there is
    /// nothing left to rub out; in a command mode, one character left, crossing a line break.
    /// </summary>
    /// <remarks>
    /// The same two meanings by position as <see cref="Enter"/>. Outside the command line it is
    /// <c>h</c> with vim's <c>whichwrap</c> allowed to cross a line break, which is what the key does
    /// there by default and <c>h</c> does not.
    /// </remarks>
    Backspace,

    /// <summary>
    /// Home — the same motion as <c>0</c>.
    /// </summary>
    Home,

    /// <summary>
    /// End — the same motion as <c>$</c>.
    /// </summary>
    End,

    /// <summary>
    /// Tab — named so that a command mode can swallow it and insert mode can hand it back to the
    /// editor to indent with.
    /// </summary>
    /// <remarks>
    /// Vim jumps forward through the jump list with it. There is no jump list here, so in a command
    /// mode the key means nothing and, like <see cref="Other"/>, is taken without a word rather than
    /// let through to indent a line nobody is typing in.
    /// </remarks>
    Tab,

    /// <summary>
    /// Page Up — a page backwards, the same command as <c>Ctrl+B</c>.
    /// </summary>
    /// <remarks>
    /// In insert mode both page keys are the editor's, as they are in vim, and the engine turns them
    /// down there.
    /// </remarks>
    PageUp,

    /// <summary>
    /// Page Down — a page forwards, the same command as <c>Ctrl+F</c>.
    /// </summary>
    PageDown,

    /// <summary>
    /// A key this engine has no meaning for.
    /// </summary>
    Other,
}
