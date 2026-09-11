namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// Where <c>}</c>, <c>{</c>, <c>)</c> and <c>(</c> land.
/// </summary>
/// <remarks>
/// <para>
/// Kept apart from the engine for the same reason <see cref="VimWords"/> is: this answers a question
/// about the text — "where does the next paragraph start?" — and the engine answers what to do about
/// it. Both the bare motion and the range behind an operator go through the one scan, so <c>}</c>
/// and <c>d}</c> cannot disagree about where a paragraph ends.
/// </para>
/// <para>
/// A paragraph boundary is a line with nothing on it at all. Not a line of blanks — vim's rule, and
/// the right one for markdown, where a line holding two trailing spaces is a hard break inside a
/// paragraph rather than a gap between two.
/// </para>
/// <para>
/// A sentence is vim's definition, and this is a transcription of vim's own scan rather than a
/// simplification of it: a sentence ends at a <c>.</c>, <c>!</c> or <c>?</c> followed by the end of
/// the line or by a space or a tab, with any run of <c>)</c> <c>]</c> <c>"</c> <c>'</c> allowed in
/// between, and a paragraph boundary is a sentence boundary too. The simplifications that suggest
/// themselves all break on the second example anyone tries — <c>e.g. this</c>, a quote closing after
/// the full stop, a sentence that runs across a line break — so the walk is the original's, position
/// by position.
/// </para>
/// <para>
/// A count is honoured inside the scan rather than by repeating it, because vim's answer to a count
/// that runs out is not "as far as it got": <c>3}</c> with one paragraph left moves nowhere at all,
/// which is what makes a count safe to type blind.
/// </para>
/// </remarks>
internal static class VimParagraphs
{
    private const char Nul = '\0';

    /// <summary>
    /// Works out where <c>}</c> or <c>{</c> goes.
    /// </summary>
    /// <param name="buffer">The buffer to scan.</param>
    /// <param name="caret">Where the caret is.</param>
    /// <param name="forward">Which way to look.</param>
    /// <param name="count">How many paragraphs along, never less than one.</param>
    /// <returns>
    /// The offset of the blank line the motion stops on; the end of the buffer or its start when it
    /// runs out of paragraphs on the last count; <see langword="null"/> when it runs out on an
    /// earlier one, which is a motion that does not move.
    /// </returns>
    /// <remarks>
    /// The end of the buffer is returned as the offset <em>past</em> the last character. As a caret
    /// position normal mode pulls it back onto the character; as the end of a range it is what makes
    /// <c>d}</c> on the last paragraph take the last character with it, which is vim's behaviour —
    /// the motion turns inclusive there.
    /// </remarks>
    public static int? Paragraph(IVimBuffer buffer, int caret, bool forward, int count)
    {
        int number = buffer.GetLineByOffset(caret).Number;

        for (int step = 0; step < count; step++)
        {
            bool sawText = false;

            while (true)
            {
                VimLine line = buffer.GetLine(number);

                if (!line.IsEmpty)
                {
                    sawText = true;
                }
                else if (sawText)
                {
                    break;
                }

                if (forward ? (number == buffer.LineCount) : (number == 1))
                {
                    // Out of lines. On the last count that is where the motion stops; on an earlier
                    // one it is a count that cannot be honoured, and the motion goes nowhere.
                    return (step == (count - 1))
                        ? (forward ? buffer.TextLength : 0)
                        : null;
                }

                number += forward ? 1 : -1;
            }
        }

        return buffer.GetLine(number).Offset;
    }

    /// <summary>
    /// Works out where <c>)</c> or <c>(</c> goes.
    /// </summary>
    /// <param name="buffer">The buffer to scan.</param>
    /// <param name="caret">Where the caret is.</param>
    /// <param name="forward">Which way to look.</param>
    /// <param name="count">How many sentences along, never less than one.</param>
    /// <returns>
    /// The offset of the sentence start the motion stops on, or <see langword="null"/> when the
    /// count runs out before the last step, which is a motion that does not move.
    /// </returns>
    public static int? Sentence(IVimBuffer buffer, int caret, bool forward, int count)
    {
        int pos = caret;

        while (count-- > 0)
        {
            bool noSkip = false;

            if (CharAt(buffer, pos) == Nul)
            {
                // On an empty line: leave the run of them first. Forwards, the first character
                // after them is the answer outright — a paragraph start is a sentence start.
                do
                {
                    if (Step(buffer, ref pos, forward) == -1)
                    {
                        break;
                    }
                }
                while (CharAt(buffer, pos) == Nul);

                if (forward)
                {
                    SkipBlanks(buffer, ref pos);

                    continue;
                }
            }
            else if (!forward)
            {
                // One back before looking, so that '(' at the start of a sentence finds the one
                // before it rather than itself.
                Decl(buffer, ref pos);
            }

            RewindOverSentenceEnd(buffer, ref pos, forward);

            int startLine = buffer.GetLineByOffset(pos).Number;

            while (true)
            {
                char character = CharAt(buffer, pos);

                if (character == Nul)
                {
                    // An empty line: a paragraph boundary, and so a sentence boundary. Backwards,
                    // the sentence being looked for starts on the line after it.
                    if (!forward && (buffer.GetLineByOffset(pos).Number != startLine))
                    {
                        pos = buffer.GetLine(buffer.GetLineByOffset(pos).Number + 1).Offset;
                    }

                    break;
                }

                if ((character is '.' or '!' or '?') && EndsSentenceAt(buffer, pos, out int after))
                {
                    pos = after;

                    if (CharAt(buffer, pos) == Nul)
                    {
                        // The end of a line: the sentence after it starts on the next one.
                        Inc(buffer, ref pos);
                    }

                    break;
                }

                if (Step(buffer, ref pos, forward) == -1)
                {
                    if (count > 0)
                    {
                        return null;
                    }

                    noSkip = true;

                    break;
                }
            }

            if (!noSkip)
            {
                SkipBlanks(buffer, ref pos);
            }
        }

        return pos;
    }

    /// <summary>
    /// Walks back over the blanks and the punctuation that can end a sentence, so that a scan started
    /// on a full stop or in the gap after one is looking at the sentence before it.
    /// </summary>
    private static void RewindOverSentenceEnd(IVimBuffer buffer, ref int pos, bool forward)
    {
        bool foundDot = false;

        while (true)
        {
            char character = CharAt(buffer, pos);

            if (!IsBlank(character) && !IsSentenceEnd(character) && !IsTrailing(character))
            {
                return;
            }

            int before = pos;

            if ((Decl(buffer, ref before) == -1) || (forward && buffer.GetLineByOffset(before).IsEmpty))
            {
                return;
            }

            if (foundDot)
            {
                return;
            }

            if (IsSentenceEnd(character))
            {
                foundDot = true;
            }

            char preceding = CharAt(buffer, before);

            if (IsTrailing(character) && !IsSentenceEnd(preceding) && !IsTrailing(preceding))
            {
                return;
            }

            Decl(buffer, ref pos);
        }
    }

    /// <summary>
    /// Decides whether the <c>.</c>, <c>!</c> or <c>?</c> at <paramref name="pos"/> ends a sentence,
    /// and where the text after it begins if it does.
    /// </summary>
    private static bool EndsSentenceAt(IVimBuffer buffer, int pos, out int after)
    {
        int probe = pos;

        while (true)
        {
            if (Inc(buffer, ref probe) == -1)
            {
                // The end of the buffer: nothing can follow, so the sentence ends here.
                after = probe;

                return true;
            }

            char next = CharAt(buffer, probe);

            if (IsTrailing(next))
            {
                continue;
            }

            after = probe;

            return IsBlank(next) || (next == Nul);
        }
    }

    private static void SkipBlanks(IVimBuffer buffer, ref int pos)
    {
        while (IsBlank(CharAt(buffer, pos)))
        {
            if (Incl(buffer, ref pos) == -1)
            {
                return;
            }
        }
    }

    private static int Step(IVimBuffer buffer, ref int pos, bool forward)
        => forward ? Incl(buffer, ref pos) : Decl(buffer, ref pos);

    /// <summary>
    /// Reads the character at a position, with the end of a line reading as <see cref="Nul"/>.
    /// </summary>
    /// <remarks>
    /// The one convention the whole scan rests on, and vim's own: a line's end is a position of its
    /// own that holds no character. It is where an empty line puts its only position, and what the
    /// scan tests for to find a paragraph boundary.
    /// </remarks>
    private static char CharAt(IVimBuffer buffer, int pos)
    {
        if (pos >= buffer.TextLength)
        {
            return Nul;
        }

        char character = buffer.CharAt(pos);

        return (character is '\n' or '\r') ? Nul : character;
    }

    /// <summary>
    /// One position forwards, the end of the line counting as a position.
    /// </summary>
    /// <returns>0 when still on the line's characters, 2 when now on its end, 1 when moved onto the next line, -1 at the end of the buffer.</returns>
    private static int Inc(IVimBuffer buffer, ref int pos)
    {
        VimLine line = buffer.GetLineByOffset(pos);

        if (pos < line.EndOffset)
        {
            pos++;

            return pos < line.EndOffset ? 0 : 2;
        }

        if (line.DelimiterLength > 0)
        {
            pos = line.EndOffset + line.DelimiterLength;

            return 1;
        }

        return -1;
    }

    /// <summary>
    /// One character forwards, skipping the end of a non-empty line.
    /// </summary>
    private static int Incl(IVimBuffer buffer, ref int pos)
    {
        int result = Inc(buffer, ref pos);

        if ((result >= 1) && (pos != buffer.GetLineByOffset(pos).Offset))
        {
            result = Inc(buffer, ref pos);
        }

        return result;
    }

    /// <summary>
    /// One position backwards, the end of the previous line counting as a position.
    /// </summary>
    /// <returns>0 when still on the line, 1 when moved onto the previous line's end, -1 at the start of the buffer.</returns>
    private static int Dec(IVimBuffer buffer, ref int pos)
    {
        VimLine line = buffer.GetLineByOffset(pos);

        if (pos > line.Offset)
        {
            pos--;

            return 0;
        }

        if (line.Number > 1)
        {
            pos = buffer.GetLine(line.Number - 1).EndOffset;

            return 1;
        }

        return -1;
    }

    /// <summary>
    /// One character backwards, skipping the end of a non-empty line.
    /// </summary>
    private static int Decl(IVimBuffer buffer, ref int pos)
    {
        int result = Dec(buffer, ref pos);

        if ((result == 1) && (pos != buffer.GetLineByOffset(pos).Offset))
        {
            result = Dec(buffer, ref pos);
        }

        return result;
    }

    private static bool IsBlank(char character) => character is ' ' or '\t';

    private static bool IsSentenceEnd(char character) => character is '.' or '!' or '?';

    private static bool IsTrailing(char character) => character is ')' or ']' or '"' or '\'';
}
