using System;

namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// Where <c>w</c>, <c>b</c> and <c>e</c> land — and where <c>W</c>, <c>B</c> and <c>E</c> do.
/// </summary>
/// <remarks>
/// <para>
/// Vim's three character classes, and nothing else: blanks, word characters (letters, digits and the
/// underscore) and everything else, which counts as punctuation. A word is a run of one non-blank
/// class, so <c>foo.bar</c> is three words and <c>foo_bar</c> is one — the behaviour that makes
/// <c>w</c> useful in prose and in code alike.
/// </para>
/// <para>
/// The capitals read the same text with two classes instead of three — blank and not blank — so that
/// <c>foo.bar</c> is one WORD. That is the whole of the difference, which is why the walk is written
/// once and handed the classifier: two copies of the same loop would come apart on an edge nobody
/// tested, and be wrong in a way nobody could describe.
/// </para>
/// <para>
/// An empty line is a word of its own. That is not a special case invented here: it is what lets
/// <c>w</c> walk down a markdown document paragraph by paragraph instead of running through the
/// blank lines that separate them.
/// </para>
/// </remarks>
internal static class VimWords
{
    /// <summary>
    /// Where <c>w</c> goes: the start of the next word.
    /// </summary>
    /// <param name="buffer">The buffer to walk.</param>
    /// <param name="offset">Where the caret is now.</param>
    /// <returns>The offset of the next word's first character, or the end of the buffer.</returns>
    public static int NextStart(IVimBuffer buffer, int offset) => NextStart(buffer, offset, ClassOf);

    /// <summary>
    /// Where <c>W</c> goes: the start of the next WORD.
    /// </summary>
    /// <param name="buffer">The buffer to walk.</param>
    /// <param name="offset">Where the caret is now.</param>
    /// <returns>The offset of the next WORD's first character, or the end of the buffer.</returns>
    public static int NextStartBig(IVimBuffer buffer, int offset) => NextStart(buffer, offset, ClassOfBig);

    /// <summary>
    /// Where <c>b</c> goes: the start of the previous word.
    /// </summary>
    /// <param name="buffer">The buffer to walk.</param>
    /// <param name="offset">Where the caret is now.</param>
    /// <returns>The offset of the previous word's first character, or the start of the buffer.</returns>
    public static int PreviousStart(IVimBuffer buffer, int offset) => PreviousStart(buffer, offset, ClassOf);

    /// <summary>
    /// Where <c>B</c> goes: the start of the previous WORD.
    /// </summary>
    /// <param name="buffer">The buffer to walk.</param>
    /// <param name="offset">Where the caret is now.</param>
    /// <returns>The offset of the previous WORD's first character, or the start of the buffer.</returns>
    public static int PreviousStartBig(IVimBuffer buffer, int offset) => PreviousStart(buffer, offset, ClassOfBig);

    /// <summary>
    /// Where <c>e</c> goes: the last character of this word, or of the next one when the caret is
    /// already on it.
    /// </summary>
    /// <param name="buffer">The buffer to walk.</param>
    /// <param name="offset">Where the caret is now.</param>
    /// <returns>The offset of that character, or the caret's own when there is no word left.</returns>
    /// <remarks>
    /// Unlike <c>w</c> and <c>b</c>, <c>e</c> does not stop on an empty line: there is no character
    /// there for it to end on, so it runs through to the next word that has one.
    /// </remarks>
    public static int End(IVimBuffer buffer, int offset) => End(buffer, offset, ClassOf);

    /// <summary>
    /// Where <c>E</c> goes: the last character of this WORD, or of the next one when the caret is
    /// already on it.
    /// </summary>
    /// <param name="buffer">The buffer to walk.</param>
    /// <param name="offset">Where the caret is now.</param>
    /// <returns>The offset of that character, or the caret's own when there is no WORD left.</returns>
    public static int EndBig(IVimBuffer buffer, int offset) => End(buffer, offset, ClassOfBig);

    /// <summary>
    /// Which of the three classes a character belongs to.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns>Its class.</returns>
    /// <remarks>
    /// Shared with <see cref="VimTextObjects"/> rather than kept private, because <c>iw</c> has to
    /// mean the same thing by "word" that <c>w</c> does: a rule with two implementations would come
    /// apart on <c>foo.bar</c> first, and be wrong in a way nobody could describe.
    /// </remarks>
    public static CharacterClass ClassOf(char character)
    {
        if (char.IsWhiteSpace(character))
        {
            return CharacterClass.Blank;
        }

        return (char.IsLetterOrDigit(character) || (character == '_'))
            ? CharacterClass.Word
            : CharacterClass.Punctuation;
    }

    /// <summary>
    /// Which of the two classes a character belongs to, as the capital motions see the text.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns><see cref="CharacterClass.Blank"/> for white space, <see cref="CharacterClass.Word"/> for anything else.</returns>
    public static CharacterClass ClassOfBig(char character)
        => char.IsWhiteSpace(character) ? CharacterClass.Blank : CharacterClass.Word;

    private static int NextStart(IVimBuffer buffer, int offset, Func<char, CharacterClass> classOf)
    {
        int length = buffer.TextLength;
        int index = offset;

        if (index >= length)
        {
            return length;
        }

        CharacterClass start = classOf(buffer.CharAt(index));

        if (start != CharacterClass.Blank)
        {
            while ((index < length) && (classOf(buffer.CharAt(index)) == start))
            {
                index++;
            }
        }

        while ((index < length) && (classOf(buffer.CharAt(index)) == CharacterClass.Blank))
        {
            index++;

            if (IsStartOfEmptyLine(buffer, index))
            {
                return index;
            }
        }

        return index;
    }

    private static int PreviousStart(IVimBuffer buffer, int offset, Func<char, CharacterClass> classOf)
    {
        int index = offset - 1;

        while ((index >= 0) && (classOf(buffer.CharAt(index)) == CharacterClass.Blank))
        {
            if (IsStartOfEmptyLine(buffer, index))
            {
                return index;
            }

            index--;
        }

        if (index < 0)
        {
            return 0;
        }

        CharacterClass word = classOf(buffer.CharAt(index));

        while ((index > 0) && (classOf(buffer.CharAt(index - 1)) == word))
        {
            index--;
        }

        return index;
    }

    private static int End(IVimBuffer buffer, int offset, Func<char, CharacterClass> classOf)
    {
        int length = buffer.TextLength;
        int index = offset + 1;

        while ((index < length) && (classOf(buffer.CharAt(index)) == CharacterClass.Blank))
        {
            index++;
        }

        if (index >= length)
        {
            return offset;
        }

        CharacterClass word = classOf(buffer.CharAt(index));

        while (((index + 1) < length) && (classOf(buffer.CharAt(index + 1)) == word))
        {
            index++;
        }

        return index;
    }

    private static bool IsStartOfEmptyLine(IVimBuffer buffer, int offset)
    {
        VimLine line = buffer.GetLineByOffset(offset);

        return (line.Offset == offset) && line.IsEmpty;
    }

    /// <summary>
    /// Vim's three character classes, which are the whole of what a word motion knows about the text.
    /// </summary>
    public enum CharacterClass
    {
        /// <summary>
        /// White space of any kind, a line break included.
        /// </summary>
        Blank,

        /// <summary>
        /// A letter, a digit or an underscore — which is why <c>foo_bar</c> is one word. For the
        /// capital motions, anything at all that is not blank.
        /// </summary>
        Word,

        /// <summary>
        /// Everything else — which is why <c>foo.bar</c> is three. Never produced for the capitals.
        /// </summary>
        Punctuation,
    }
}
