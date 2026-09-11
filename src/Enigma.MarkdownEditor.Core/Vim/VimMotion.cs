namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// Where a motion moves the caret to.
/// </summary>
/// <remarks>
/// Named separately from <see cref="VimAction"/> because in vim a motion is a noun: the same list
/// sits behind an operator, so that <c>dw</c> is <see cref="VimOperator.Delete"/> over
/// <see cref="WordForward"/> rather than a command of its own in one flat list. Every member here
/// is both a place to move the caret to and a stretch of text to operate on — except
/// <see cref="WholeLine"/>, which is only the second.
/// </remarks>
public enum VimMotion
{
    /// <summary>
    /// No motion. What every action that is not a move carries.
    /// </summary>
    None,

    /// <summary>
    /// <c>h</c> — one character left, never past the start of the line.
    /// </summary>
    Left,

    /// <summary>
    /// <c>l</c> — one character right, never past the last character of the line.
    /// </summary>
    Right,

    /// <summary>
    /// <c>k</c> — one line up, at the remembered column.
    /// </summary>
    Up,

    /// <summary>
    /// <c>j</c> — one line down, at the remembered column.
    /// </summary>
    Down,

    /// <summary>
    /// <c>0</c> — the first character of the line.
    /// </summary>
    LineStart,

    /// <summary>
    /// <c>$</c> — the last character of the line, and stay there through <c>j</c> and <c>k</c>.
    /// </summary>
    LineEnd,

    /// <summary>
    /// <c>w</c> — the start of the next word.
    /// </summary>
    WordForward,

    /// <summary>
    /// <c>b</c> — the start of the previous word.
    /// </summary>
    WordBackward,

    /// <summary>
    /// <c>e</c> — the end of this word, or of the next one when already on it.
    /// </summary>
    WordEnd,

    /// <summary>
    /// <c>W</c> — the start of the next WORD.
    /// </summary>
    /// <remarks>
    /// A WORD, in vim's capitals, is a run of anything that is not blank: <c>foo.bar</c> is one of
    /// them where it is three words. The three capital motions are otherwise the same shape as their
    /// lowercase ones — the same empty-line stop, the same rules behind an operator.
    /// </remarks>
    WordForwardBig,

    /// <summary>
    /// <c>B</c> — the start of the previous WORD.
    /// </summary>
    WordBackwardBig,

    /// <summary>
    /// <c>E</c> — the end of this WORD, or of the next one when already on it.
    /// </summary>
    WordEndBig,

    /// <summary>
    /// <c>^</c> — the first character of the line that is not a space or a tab.
    /// </summary>
    /// <remarks>
    /// Where <c>I</c> starts typing. A count means nothing to it, which is vim's own rule.
    /// </remarks>
    FirstNonBlank,

    /// <summary>
    /// <c>%</c> — the bracket that matches the one under the caret, or the one first found along the
    /// line from it.
    /// </summary>
    /// <remarks>
    /// The three bracket pairs, counting nesting and crossing lines. With a count it is a different
    /// motion altogether: <c>50%</c> is the line half way through the buffer.
    /// </remarks>
    MatchingBracket,

    /// <summary>
    /// <c>}</c> — the next blank line, or the end of the buffer.
    /// </summary>
    /// <remarks>
    /// A blank line here is one with nothing on it at all, which is vim's paragraph boundary. Behind
    /// an operator this is the one motion the engine applies vim's exclusive-linewise rule to, so
    /// <c>d}</c> from the first column of a paragraph takes whole lines.
    /// </remarks>
    ParagraphForward,

    /// <summary>
    /// <c>{</c> — the previous blank line, or the start of the buffer.
    /// </summary>
    ParagraphBackward,

    /// <summary>
    /// <c>)</c> — the start of the next sentence.
    /// </summary>
    /// <remarks>
    /// Vim's definition of one: it ends at a <c>.</c>, <c>!</c> or <c>?</c> followed by the end of
    /// the line or by a space or a tab, with any run of <c>)</c> <c>]</c> <c>"</c> <c>'</c> allowed
    /// in between, and a paragraph boundary is a sentence boundary too.
    /// </remarks>
    SentenceForward,

    /// <summary>
    /// <c>(</c> — the start of this sentence, or of the previous one when already on it.
    /// </summary>
    SentenceBackward,

    /// <summary>
    /// <c>+</c> and <c>Enter</c> — the first non-blank of the line below, count lines down.
    /// </summary>
    /// <remarks>
    /// Linewise, like <c>j</c>, but without the remembered column: it lands on the first non-blank
    /// rather than under where the caret was.
    /// </remarks>
    LineDownFirstNonBlank,

    /// <summary>
    /// <c>-</c> — the first non-blank of the line above, count lines up.
    /// </summary>
    LineUpFirstNonBlank,

    /// <summary>
    /// <c>_</c> — the first non-blank of this line, or of the line count-1 below it.
    /// </summary>
    /// <remarks>
    /// Linewise, which is what makes <c>d_</c> the same as <c>dd</c> and gives the doubled operators
    /// a spelled-out form.
    /// </remarks>
    CurrentLineFirstNonBlank,

    /// <summary>
    /// <c>|</c> — column <em>count</em> of the line, counted from one and clamped to its last
    /// character.
    /// </summary>
    Column,

    /// <summary>
    /// <c>Space</c> — one character right, and on to the next line's first character from the last
    /// one.
    /// </summary>
    /// <remarks>
    /// <c>l</c> with vim's <c>whichwrap</c> allowed to cross a line break, which is what the space
    /// bar does by default there and <c>l</c> does not.
    /// </remarks>
    RightWrapping,

    /// <summary>
    /// <c>Backspace</c> — one character left, and back to the previous line's last character from
    /// the first one.
    /// </summary>
    LeftWrapping,

    /// <summary>
    /// <c>f</c> — forward to the next occurrence of the command's character, on it.
    /// </summary>
    /// <remarks>
    /// The four find motions never leave the caret's own line. That is vim's rule and the reason the
    /// family is worth having next to <c>/</c>: a find is a jump within the line one keystroke long,
    /// and a search is everything else.
    /// </remarks>
    FindForward,

    /// <summary>
    /// <c>F</c> — backward to the previous occurrence of it, on it.
    /// </summary>
    FindBackward,

    /// <summary>
    /// <c>t</c> — forward to just before the next occurrence of it.
    /// </summary>
    TillForward,

    /// <summary>
    /// <c>T</c> — backward to just after the previous occurrence of it.
    /// </summary>
    TillBackward,

    /// <summary>
    /// <c>;</c> — the last of the four find motions again, the same way round.
    /// </summary>
    /// <remarks>
    /// A motion that carries no character of its own: what it looks for is what the last <c>f</c>,
    /// <c>F</c>, <c>t</c> or <c>T</c> looked for, which is state the engine keeps rather than
    /// something the keys spell.
    /// </remarks>
    RepeatFind,

    /// <summary>
    /// <c>,</c> — the same, the other way round.
    /// </summary>
    RepeatFindReverse,

    /// <summary>
    /// <c>/pattern</c> and Enter — forward to the next match of a regular expression, on its first
    /// character.
    /// </summary>
    /// <remarks>
    /// The one motion typed on the command line rather than as keys, which is why the parser never
    /// produces it: the keys give the engine the command that opens the line, and the engine makes
    /// this one of them when Enter closes it, with what was typed as the command's
    /// <see cref="VimCommand.Pattern"/>. Exclusive behind an operator, as every search in vim is —
    /// <c>d/foo</c> takes up to the <c>f</c> and leaves it — and subject to the same
    /// exclusive-linewise rule as the paragraph motions. What was looked for is remembered, and
    /// <see cref="SearchNext"/> reads the memory.
    /// </remarks>
    SearchForward,

    /// <summary>
    /// <c>?pattern</c> and Enter — backward to the previous match.
    /// </summary>
    SearchBackward,

    /// <summary>
    /// <c>n</c> — the last search again, the way it was going.
    /// </summary>
    /// <remarks>
    /// Which way is the last search's to say, not this key's: <c>n</c> after a <c>?</c> goes
    /// backwards, which is vim's rule and the reason a search has a direction of its own to remember.
    /// </remarks>
    SearchNext,

    /// <summary>
    /// <c>N</c> — the last search again, the other way round.
    /// </summary>
    SearchPrevious,

    /// <summary>
    /// <c>*</c> — forward to the next occurrence of the word under the caret, as a whole word.
    /// </summary>
    /// <remarks>
    /// The word is the keyword under or after the caret on its line, searched for between word
    /// boundaries; with no keyword left on the line it is the run of punctuation there instead,
    /// searched for as it stands. Either way it becomes the pattern <c>n</c> repeats, as if it had
    /// been typed after a <c>/</c>. A line with nothing left on it gives the motion nothing to look
    /// for, and it does nothing, without a word.
    /// </remarks>
    SearchWordForward,

    /// <summary>
    /// <c>#</c> — backward to the previous occurrence of the word under the caret.
    /// </summary>
    SearchWordBackward,

    /// <summary>
    /// <c>gg</c> — the first line's first non-blank character.
    /// </summary>
    FirstLine,

    /// <summary>
    /// <c>G</c> — the last line's first non-blank character, or the line a count names.
    /// </summary>
    LastLine,

    /// <summary>
    /// <c>H</c> — the first non-blank of the top line on screen, or of the line <em>count</em> down
    /// from it.
    /// </summary>
    /// <remarks>
    /// The three screen motions are the only ones that ask where the <em>view</em> is rather than
    /// where the text is, which is what <see cref="IVimBuffer.FirstVisibleLine"/> and
    /// <see cref="IVimBuffer.VisibleLineCount"/> are for. Linewise behind an operator, like every
    /// motion that names a line: <c>dL</c> takes from the caret's line to the bottom of the screen.
    /// </remarks>
    ScreenTop,

    /// <summary>
    /// <c>M</c> — the first non-blank of the middle line on screen, or of the middle of the buffer
    /// when the buffer is shorter than the screen. A count means nothing to it.
    /// </summary>
    ScreenMiddle,

    /// <summary>
    /// <c>L</c> — the first non-blank of the bottom line on screen, or of the line <em>count</em> up
    /// from it.
    /// </summary>
    ScreenBottom,

    /// <summary>
    /// The caret's own line, and the count-1 below it, as whole lines.
    /// </summary>
    /// <remarks>
    /// What an operator doubled up on itself means — <c>dd</c>, <c>yy</c>, <c>cc</c> — and what
    /// <c>Y</c> is spelled as. Not a caret motion: there is nowhere for a line to move the caret to,
    /// so this is the one member the parser never produces on its own.
    /// </remarks>
    WholeLine,
}
