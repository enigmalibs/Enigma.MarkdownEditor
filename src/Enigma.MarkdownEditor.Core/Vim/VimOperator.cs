namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// What an operator does to the stretch of text its motion picks out.
/// </summary>
/// <remarks>
/// <para>
/// The verb half of vim's <c>[count] operator [count] motion</c> grammar, and the reason that grammar
/// is worth having: eight operators over the whole list of motions and text objects is a few hundred
/// commands nobody had to write down one at a time. Kept apart from <see cref="VimAction"/> because
/// an operator is not a command on its own — <c>d</c> means nothing until a motion says what to
/// delete, which is exactly the state the parser sits in between the two keystrokes.
/// </para>
/// <para>
/// The first three fill the register; the other five do not. A shift or a case change is something
/// done <em>to</em> the text where it stands rather than something taken out of it, and vim leaves the
/// register alone for exactly that reason.
/// </para>
/// </remarks>
public enum VimOperator
{
    /// <summary>
    /// No operator. What every command that is not one carries.
    /// </summary>
    None,

    /// <summary>
    /// <c>d</c> — take the text out and put it in the register.
    /// </summary>
    Delete,

    /// <summary>
    /// <c>y</c> — put the text in the register and leave it where it is.
    /// </summary>
    Yank,

    /// <summary>
    /// <c>c</c> — take the text out, put it in the register, and start typing in its place.
    /// </summary>
    Change,

    /// <summary>
    /// <c>&gt;</c> — indent the lines the range touches by one <c>shiftwidth</c>.
    /// </summary>
    /// <remarks>
    /// Always over whole lines, whatever the motion: <c>&gt;w</c> shifts the line the word is on, not
    /// the word. Doubled, <c>&gt;&gt;</c>, it is the caret's line and the count-1 below it.
    /// </remarks>
    ShiftRight,

    /// <summary>
    /// <c>&lt;</c> — take up to one <c>shiftwidth</c> of indent off the lines the range touches.
    /// </summary>
    ShiftLeft,

    /// <summary>
    /// <c>gu</c> — lowercase the text.
    /// </summary>
    /// <remarks>
    /// The three case operators are the ones spelled with a <c>g</c> prefix, and their doubled forms
    /// accept either spelling: <c>guu</c> and <c>gugu</c> are the same command over the line.
    /// </remarks>
    Lowercase,

    /// <summary>
    /// <c>gU</c> — uppercase the text.
    /// </summary>
    Uppercase,

    /// <summary>
    /// <c>g~</c> — swap the case of every letter in the text.
    /// </summary>
    ToggleCase,
}
