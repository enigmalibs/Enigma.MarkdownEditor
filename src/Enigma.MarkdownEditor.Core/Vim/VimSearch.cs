using System;
using System.Text.RegularExpressions;

namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// Where <c>/</c>, <c>?</c>, <c>n</c>, <c>N</c>, <c>*</c> and <c>#</c> land.
/// </summary>
/// <remarks>
/// <para>
/// A regular expression, compiled by <see cref="VimPattern"/>, run over the buffer's whole text taken
/// in one call. That is the one place this engine reads more than a line at a time, and it is the
/// right trade for a markdown note: the alternative is walking <see cref="IVimBuffer.CharAt"/> across
/// a document to re-implement what <see cref="Regex"/> already does, and a pattern may span a line
/// break, which no line-at-a-time scan could see.
/// </para>
/// <para>
/// The matches are the expression's own, taken in order from the start of the text and never
/// overlapping — which is how vim reads them too, with its default <c>cpoptions</c>: standing on the
/// first <c>aa</c> of <c>aaaa</c>, a search for <c>aa</c> lands on the third character, not the
/// second. An empty match — <c>^</c>, <c>\b</c>, <c>x*</c> — is stepped over by one character, so a
/// search never stands still.
/// </para>
/// <para>
/// It always wraps here; whether the wrap was allowed is the engine's question, because the answer is
/// an option and a message rather than a different search. So a search that finds nothing ahead of
/// the caret starts again from the other end, and only a pattern that is nowhere in the buffer at all
/// comes back empty-handed.
/// </para>
/// </remarks>
internal static class VimSearch
{
    /// <summary>
    /// Looks for the next match in one direction, wrapping round the end of the buffer.
    /// </summary>
    /// <param name="buffer">The buffer to search.</param>
    /// <param name="pattern">What to look for.</param>
    /// <param name="caret">Where the search starts from. The match never begins here on the first pass.</param>
    /// <param name="forward">Whether to search towards the end of the buffer or towards its start.</param>
    /// <returns>The match, or <see langword="null"/> when the pattern is nowhere in the buffer.</returns>
    /// <exception cref="RegexMatchTimeoutException">A match ran past <see cref="VimPattern.Timeout"/>.</exception>
    /// <remarks>
    /// The caret's own offset is excluded on the first pass and included on the wrapped one, which is
    /// vim's rule and the reason searching for the word you are standing on says "search hit BOTTOM"
    /// and leaves you where you were rather than reporting that it found nothing.
    /// </remarks>
    public static VimSearchHit? Find(IVimBuffer buffer, Regex pattern, int caret, bool forward)
    {
        string text = buffer.GetText(0, buffer.TextLength);

        int first = -1;
        int last = -1;
        int ahead = -1;
        int behind = -1;

        foreach (Match match in pattern.Matches(text))
        {
            int at = match.Index;

            if (first < 0)
            {
                first = at;
            }

            last = at;

            if ((at > caret) && (ahead < 0))
            {
                ahead = at;
            }

            if (at < caret)
            {
                behind = at;
            }
        }

        if (forward)
        {
            return ahead >= 0 ? new VimSearchHit(ahead, false) : Wrap(first);
        }

        return behind >= 0 ? new VimSearchHit(behind, false) : Wrap(last);
    }

    /// <summary>
    /// The word <c>*</c> and <c>#</c> search for: the keyword under or after the caret, or failing
    /// that the run of punctuation there.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="caret">Where the caret is.</param>
    /// <returns>
    /// Where the word starts and the pattern that finds it, or <see langword="null"/> when the line
    /// has nothing left on it from the caret on.
    /// </returns>
    /// <remarks>
    /// Vim's own two-step rule. A keyword — letters, digits and <c>_</c>, the same class <c>w</c>
    /// stops on — anywhere from the caret to the end of the line is preferred, and taken whole even
    /// when it began before the caret; only a line with no keyword left falls back to the non-blank
    /// run under or after the caret. A keyword is searched for between word boundaries, so <c>*</c>
    /// on <c>the</c> skips <c>there</c>; a run of punctuation is searched for as it stands, escaped,
    /// because a boundary means nothing next to a <c>)</c>.
    /// </remarks>
    public static VimSearchWord? WordUnderCaret(IVimBuffer buffer, int caret)
    {
        VimLine line = buffer.GetLineByOffset(caret);
        string text = line.Text;
        int column = Math.Clamp(caret - line.Offset, 0, text.Length);

        int index = column;

        while ((index < text.Length) && (VimWords.ClassOf(text[index]) != VimWords.CharacterClass.Word))
        {
            index++;
        }

        if (index < text.Length)
        {
            (int start, int end) = Run(text, index, VimWords.CharacterClass.Word);

            return new VimSearchWord(line.Offset + start, $@"\b{Regex.Escape(text[start..end])}\b");
        }

        index = column;

        while ((index < text.Length) && (VimWords.ClassOf(text[index]) == VimWords.CharacterClass.Blank))
        {
            index++;
        }

        if (index >= text.Length)
        {
            return null;
        }

        (int from, int to) = Run(text, index, VimWords.CharacterClass.Punctuation);

        return new VimSearchWord(line.Offset + from, Regex.Escape(text[from..to]));
    }

    private static VimSearchHit? Wrap(int offset) => offset >= 0 ? new VimSearchHit(offset, true) : null;

    /// <summary>
    /// The run of one class the character at <paramref name="index"/> belongs to.
    /// </summary>
    private static (int Start, int End) Run(string text, int index, VimWords.CharacterClass @class)
    {
        int start = index;
        int end = index + 1;

        while ((start > 0) && (VimWords.ClassOf(text[start - 1]) == @class))
        {
            start--;
        }

        while ((end < text.Length) && (VimWords.ClassOf(text[end]) == @class))
        {
            end++;
        }

        return (start, end);
    }
}
