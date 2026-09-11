namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// Which stretch of text an <c>i</c> or an <c>a</c> names.
/// </summary>
/// <remarks>
/// <para>
/// A separate list from <see cref="VimMotion"/> because a text object is a different kind of noun.
/// Every motion is somewhere the caret can go — that is written into <see cref="VimMotion"/>'s own
/// contract — and there is nowhere for <c>iw</c> to move a caret to on its own: it says which text,
/// and it needs either an operator or a selection to mean anything at all.
/// </para>
/// <para>
/// The members come in pairs, one for each of the two keys vim spells them with: <c>i</c> takes what
/// is inside the delimiters, <c>a</c> takes the delimiters too — and, for a word, the blanks beside
/// it. That is the whole difference between the halves of every pair here.
/// </para>
/// </remarks>
public enum VimTextObject
{
    /// <summary>
    /// No text object. What every command that names one by a motion instead carries.
    /// </summary>
    None,

    /// <summary>
    /// <c>iw</c> — the word under the caret, or the run of blanks when the caret is on one.
    /// </summary>
    /// <remarks>
    /// A word is a run of one character class, the same three <see cref="VimWords"/> gives <c>w</c>,
    /// <c>b</c> and <c>e</c>: <c>foo.bar</c> is three of them and <c>foo_bar</c> is one. A count
    /// counts runs rather than words, so <c>d3iw</c> is word, blanks, word.
    /// </remarks>
    InnerWord,

    /// <summary>
    /// <c>aw</c> — that, plus the blanks after it, or the ones before it when there are none after.
    /// </summary>
    /// <remarks>
    /// The fallback is vim's, and it is what makes <c>daw</c> on the last word of a line leave no
    /// space behind where the separator used to be. A count counts words with their blanks.
    /// </remarks>
    AroundWord,

    /// <summary>
    /// <c>iW</c> — the WORD under the caret, or the run of blanks when the caret is on one.
    /// </summary>
    /// <remarks>
    /// The same object with vim's two-class reading of the text: blank and not blank, so that
    /// <c>foo.bar</c> is one WORD where <c>iw</c> sees three words. Counts as for <c>iw</c>.
    /// </remarks>
    InnerBigWord,

    /// <summary>
    /// <c>aW</c> — that, plus the blanks after it, or the ones before it when there are none after.
    /// </summary>
    AroundBigWord,

    /// <summary>
    /// <c>i(</c> — inside the enclosing <c>(</c> … <c>)</c>, the parentheses excluded.
    /// </summary>
    /// <remarks>
    /// The four bracket pairs count nesting and are not bound to a line: <c>ci{</c> over a fenced
    /// block is most of the reason to have them. What a count would mean — one nesting level further
    /// out — is not implemented, and is ignored rather than turned down.
    /// </remarks>
    InnerParen,

    /// <summary>
    /// <c>a(</c> — the same, the parentheses included.
    /// </summary>
    AroundParen,

    /// <summary>
    /// <c>i[</c> — inside the enclosing <c>[</c> … <c>]</c>.
    /// </summary>
    InnerBracket,

    /// <summary>
    /// <c>a[</c> — the same, the brackets included.
    /// </summary>
    AroundBracket,

    /// <summary>
    /// <c>i{</c> — inside the enclosing <c>{</c> … <c>}</c>.
    /// </summary>
    InnerBrace,

    /// <summary>
    /// <c>a{</c> — the same, the braces included.
    /// </summary>
    AroundBrace,

    /// <summary>
    /// <c>i&lt;</c> — inside the enclosing <c>&lt;</c> … <c>&gt;</c>.
    /// </summary>
    InnerAngle,

    /// <summary>
    /// <c>a&lt;</c> — the same, the angle brackets included.
    /// </summary>
    AroundAngle,

    /// <summary>
    /// <c>i"</c> — inside the surrounding pair of double quotes.
    /// </summary>
    /// <remarks>
    /// The three quote objects stay on the caret's own line and do not nest, because neither rule
    /// can be had: an apostrophe three paragraphs up would otherwise capture half the document, and
    /// nothing in the text says which of two identical characters opens and which closes. They are
    /// paired off from the start of the line instead. A count is ignored.
    /// </remarks>
    InnerDoubleQuote,

    /// <summary>
    /// <c>a"</c> — the same, the quotes included.
    /// </summary>
    AroundDoubleQuote,

    /// <summary>
    /// <c>i'</c> — inside the surrounding pair of single quotes.
    /// </summary>
    InnerSingleQuote,

    /// <summary>
    /// <c>a'</c> — the same, the quotes included.
    /// </summary>
    AroundSingleQuote,

    /// <summary>
    /// <c>i`</c> — inside the surrounding pair of backticks.
    /// </summary>
    InnerBacktick,

    /// <summary>
    /// <c>a`</c> — the same, the backticks included.
    /// </summary>
    AroundBacktick,
}
