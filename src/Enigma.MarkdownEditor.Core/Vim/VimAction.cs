namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// What a parsed keystroke asks the engine to do.
/// </summary>
/// <remarks>
/// The verbs. A move is one value here rather than the enum being a list of motions, and for the same
/// reason <see cref="Operate"/> is one value rather than a list of operator-and-motion pairs: what to
/// do and where to do it are two questions, and keeping them apart is what lets eight operators cover
/// thirty-odd motions and objects without a few hundred names.
/// </remarks>
public enum VimAction
{
    /// <summary>
    /// Move the caret along the command's motion.
    /// </summary>
    Move,

    /// <summary>
    /// <c>i</c> — insert where the caret already is.
    /// </summary>
    InsertBeforeCaret,

    /// <summary>
    /// <c>I</c> — insert at the line's first non-blank character.
    /// </summary>
    InsertAtLineStart,

    /// <summary>
    /// <c>a</c> — insert one character further on.
    /// </summary>
    InsertAfterCaret,

    /// <summary>
    /// <c>A</c> — insert at the end of the line.
    /// </summary>
    InsertAtLineEnd,

    /// <summary>
    /// <c>o</c> — open a new line below this one and insert on it.
    /// </summary>
    /// <remarks>
    /// On a markdown list, task or quote line the new line starts with the item continued — the same
    /// rule Enter follows in insert mode, and the same code: <c>ListContinuation.OpenBelow</c>.
    /// </remarks>
    OpenLineBelow,

    /// <summary>
    /// <c>O</c> — open a new line above this one and insert on it.
    /// </summary>
    /// <remarks>
    /// The item is continued here too, with <em>this</em> item's own marker rather than the next one:
    /// the new line goes before it and takes its place in the list.
    /// </remarks>
    OpenLineAbove,

    /// <summary>
    /// <c>x</c> — delete the character under the caret, and the count-1 after it.
    /// </summary>
    DeleteCharacter,

    /// <summary>
    /// An operator over a motion — <c>dw</c>, <c>3yy</c>, <c>cc</c>, <c>D</c> — or, in a visual mode,
    /// over the selection, which is what a command carrying an operator and
    /// <see cref="VimMotion.None"/> means.
    /// </summary>
    Operate,

    /// <summary>
    /// <c>p</c> — put the register back after the caret, or below the caret's line.
    /// </summary>
    PutAfter,

    /// <summary>
    /// <c>P</c> — put the register back at the caret, or above the caret's line.
    /// </summary>
    PutBefore,

    /// <summary>
    /// <c>r</c> — replace the character under the caret with the one that follows the command, or, in
    /// a visual mode, every selected character with it.
    /// </summary>
    ReplaceCharacter,

    /// <summary>
    /// <c>p</c> and <c>P</c> in a visual mode — the register goes where the selection was.
    /// </summary>
    /// <remarks>
    /// One action for both, with the key carried as the command's argument the way <c>:</c> and
    /// <c>/</c> are on <see cref="BeginCommandLine"/>, because the two differ in one thing only:
    /// <c>p</c> leaves what it replaced in the register, so that a second <c>vp</c> puts the first
    /// selection back, and <c>P</c> leaves the register exactly as it was. The shape of the put is the
    /// register's and the selection's to decide — characters go inline, lines go on lines of their own,
    /// and anything put over whole lines becomes whole lines.
    /// </remarks>
    ReplaceSelection,

    /// <summary>
    /// <c>~</c> — swap the case of the character under the caret and step past it.
    /// </summary>
    ToggleCase,

    /// <summary>
    /// <c>J</c> — join the caret's line with the count-1 below it, at least one, a space between; or,
    /// in a visual mode, join the selected lines, two at the least.
    /// </summary>
    /// <remarks>
    /// The following line's leading blanks go and one space comes, except where vim puts none: a line
    /// that already ends in a blank, a next line that starts with <c>)</c>, and an empty line on
    /// either side of the join. The register is left alone — nothing is taken out, only the line
    /// breaks.
    /// </remarks>
    Join,

    /// <summary>
    /// <c>gJ</c> — the same join with nothing removed and nothing inserted.
    /// </summary>
    JoinRaw,

    /// <summary>
    /// <c>ZZ</c> — save and close, the same request <c>:wq</c> makes.
    /// </summary>
    WriteQuit,

    /// <summary>
    /// <c>ZQ</c> — close without saving, the same request <c>:q!</c> makes.
    /// </summary>
    QuitDiscard,

    /// <summary>
    /// <c>v</c> — start selecting characters, switch to characterwise from linewise, or stop selecting
    /// when it is already what the engine is doing.
    /// </summary>
    VisualCharacterwise,

    /// <summary>
    /// <c>V</c> — the same for whole lines.
    /// </summary>
    VisualLinewise,

    /// <summary>
    /// <c>o</c> in a visual mode — the caret goes to the anchor and the anchor to the caret, so that
    /// the motions move the other end of the selection.
    /// </summary>
    SwapVisualEnds,

    /// <summary>
    /// <c>gv</c> — select again what was last selected, in the mode it was selected in; or, typed
    /// while selecting, swap the selection on screen with the one before it.
    /// </summary>
    ReselectVisual,

    /// <summary>
    /// <c>:</c>, <c>/</c> and <c>?</c> — open the command line, with the key that opened it as the
    /// command's argument and as the line's first character.
    /// </summary>
    /// <remarks>
    /// One action for all three, because from here they are the same thing: a line of text is
    /// collected until Enter, and what its first character was decides whether the line is a search
    /// or an ex command. Nothing between those two moments differs. The two search keys are motions
    /// as well as commands, so behind an operator or over a selection they carry the operator and the
    /// count with them, and what Enter runs is <c>d/foo</c> rather than <c>/foo</c> after a <c>d</c>
    /// that went nowhere.
    /// </remarks>
    BeginCommandLine,

    /// <summary>
    /// <c>u</c> — take back the last change. Only in normal mode: over a selection <c>u</c> is the
    /// lowercase operator, as it is in vim.
    /// </summary>
    Undo,

    /// <summary>
    /// <c>Ctrl+R</c> — put back what <see cref="Undo"/> took.
    /// </summary>
    Redo,

    /// <summary>
    /// <c>.</c> — do the last change again, with a new count if one was typed.
    /// </summary>
    /// <remarks>
    /// A change is anything that edits the buffer on purpose: an operator over a motion or an object
    /// (a yank excepted), the single-key edits, a put, an insert with what was typed in it, a
    /// command over a selection. A motion, a yank, undo and redo, an ex command, a recording and a
    /// replay are not changes and never become what <c>.</c> repeats.
    /// </remarks>
    Repeat,

    /// <summary>
    /// <c>q{a-zA-Z}</c> — start recording every key into a register, until the next bare <c>q</c>.
    /// </summary>
    /// <remarks>
    /// The register is the command's argument. The <c>q</c> that stops the recording never reaches the
    /// parser: the engine takes it before, because whether a <c>q</c> starts or stops is a question
    /// about the engine's state and not about the keys.
    /// </remarks>
    RecordMacro,

    /// <summary>
    /// <c>@{a-zA-Z0"}</c> — run the keys in a register, <em>count</em> times.
    /// </summary>
    PlayMacro,

    /// <summary>
    /// <c>@@</c> — run again the register that was last run.
    /// </summary>
    PlayLastMacro,

    /// <summary>
    /// <c>Ctrl+D</c> — scroll the view half a screen down, or <em>count</em> lines, and move the
    /// caret the same number of lines, keeping its column.
    /// </summary>
    /// <remarks>
    /// The twelve scroll commands are the only ones that move the view on purpose — every other
    /// command moves the caret and lets the view follow. None of them is a change, so <c>.</c> never
    /// repeats one; every one of them works over a selection, which grows or shrinks with the caret
    /// as it does for a motion. The view stops scrolling where the buffer's last line reaches the
    /// bottom of the screen, as vim's does, and the caret goes on alone; on the last line already,
    /// nothing happens.
    /// </remarks>
    ScrollHalfPageDown,

    /// <summary>
    /// <c>Ctrl+U</c> — the same, up.
    /// </summary>
    ScrollHalfPageUp,

    /// <summary>
    /// <c>Ctrl+F</c> and <c>Page Down</c> — scroll <em>count</em> pages down, a page being the screen
    /// less two lines, and put the caret on the first non-blank of the new top line.
    /// </summary>
    /// <remarks>
    /// Vim's page can end with the buffer's last line alone at the top of the screen, and so can
    /// this one where the editor allows it; with the last line already at the top there is nowhere
    /// left to go and nothing happens.
    /// </remarks>
    ScrollPageDown,

    /// <summary>
    /// <c>Ctrl+B</c> and <c>Page Up</c> — the same, up, with the caret on the first non-blank of the
    /// new bottom line.
    /// </summary>
    ScrollPageUp,

    /// <summary>
    /// <c>Ctrl+E</c> — scroll the view <em>count</em> lines down and leave the caret where it is,
    /// unless it would leave the screen, in which case it is pulled onto the nearest line still
    /// shown, keeping its column.
    /// </summary>
    ScrollLineDown,

    /// <summary>
    /// <c>Ctrl+Y</c> — the same, up.
    /// </summary>
    ScrollLineUp,

    /// <summary>
    /// <c>zt</c> — scroll so that the caret's line is at the top of the screen; the caret stays.
    /// </summary>
    /// <remarks>
    /// With a count, the six <c>z</c> commands act on the line the count names instead and move the
    /// caret to it — <c>3zt</c> is "line 3 at the top" — which is vim's rule for all six.
    /// </remarks>
    ScrollCaretTop,

    /// <summary>
    /// <c>zz</c> — the same, with the line in the middle of the screen.
    /// </summary>
    ScrollCaretCenter,

    /// <summary>
    /// <c>zb</c> — the same, with the line at the bottom of the screen.
    /// </summary>
    ScrollCaretBottom,

    /// <summary>
    /// <c>z</c> and <c>Enter</c> — <see cref="ScrollCaretTop"/>, and the caret goes to the line's
    /// first non-blank.
    /// </summary>
    ScrollCaretTopToFirstNonBlank,

    /// <summary>
    /// <c>z.</c> — <see cref="ScrollCaretCenter"/>, and the caret goes to the line's first non-blank.
    /// </summary>
    ScrollCaretCenterToFirstNonBlank,

    /// <summary>
    /// <c>z-</c> — <see cref="ScrollCaretBottom"/>, and the caret goes to the line's first non-blank.
    /// </summary>
    ScrollCaretBottomToFirstNonBlank,
}
