using System;

namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// What <c>iw</c>, <c>a(</c> and the rest of the text objects cover.
/// </summary>
/// <remarks>
/// <para>
/// Kept apart from the engine for the same reason <see cref="VimWords"/> and <see cref="VimFind"/>
/// are: this answers a question about the text — "where does the word under the caret start and
/// end?" — and the engine answers what to do about it. Both callers go through the one resolver, so
/// <c>diw</c> and <c>viw</c> cannot disagree about what a word is.
/// </para>
/// <para>
/// Three rules, one per family, and each of them is about how far the object is allowed to reach.
/// A word stays on the caret's line, because <c>aw</c>'s fallback to the blanks <em>before</em> the
/// word is only meaningful if the blanks after it stop at the line's end — and a WORD is the same
/// object read with <see cref="VimWords.ClassOfBig"/>, so <c>iW</c> cannot come to mean something
/// <c>W</c> does not. A bracket pair reaches as far as the buffer does and counts nesting, because
/// <c>ci{</c> over a fenced block is most of the reason to have it; the nesting walk itself is
/// <see cref="VimBrackets"/>', shared with <c>%</c>. A quote pair stays on the line and does not
/// nest, because nothing in the text says which of two identical characters opens and which closes —
/// they are paired off from the start of the line instead.
/// </para>
/// <para>
/// Every range is characterwise, including one that happens to cover whole lines. Vim promotes
/// <c>i{</c> to linewise when the braces are alone on their lines; that is not done here, and the
/// difference shows in where <c>p</c> puts the text back afterwards.
/// </para>
/// <para>
/// A range that covers nothing is not the same answer as no range at all. <c>i(</c> on <c>()</c>
/// covers nothing and is still a match, which is what lets <c>ci(</c> put the caret between the
/// parentheses; <c>i(</c> with no parentheses round it is <see langword="null"/>, and the engine
/// answers that with silence.
/// </para>
/// </remarks>
internal static class VimTextObjects
{
    /// <summary>
    /// Works out what a text object covers.
    /// </summary>
    /// <param name="buffer">The buffer to measure.</param>
    /// <param name="caret">Where the caret is, already pulled inside the line by the engine.</param>
    /// <param name="textObject">Which object was typed.</param>
    /// <param name="count">
    /// How many times, never less than one. Honoured by the word objects and ignored by the others —
    /// see <see cref="VimTextObject.InnerParen"/>.
    /// </param>
    /// <returns>The range, or <see langword="null"/> when there is no such object round the caret.</returns>
    public static VimTextRange? Resolve(IVimBuffer buffer, int caret, VimTextObject textObject, int count)
    {
        // Read off once rather than written into all sixteen arms below: 'i' and 'a' differ only in
        // whether what surrounds the text comes with it, which every resolver takes as a flag.
        bool around = textObject is VimTextObject.AroundWord
            or VimTextObject.AroundBigWord
            or VimTextObject.AroundParen
            or VimTextObject.AroundBracket
            or VimTextObject.AroundBrace
            or VimTextObject.AroundAngle
            or VimTextObject.AroundDoubleQuote
            or VimTextObject.AroundSingleQuote
            or VimTextObject.AroundBacktick;

        return textObject switch
        {
            VimTextObject.InnerWord or VimTextObject.AroundWord => Word(buffer, caret, count, around, VimWords.ClassOf),
            VimTextObject.InnerBigWord or VimTextObject.AroundBigWord => Word(buffer, caret, count, around, VimWords.ClassOfBig),

            VimTextObject.InnerParen or VimTextObject.AroundParen => Delimited(buffer, caret, '(', ')', around),
            VimTextObject.InnerBracket or VimTextObject.AroundBracket => Delimited(buffer, caret, '[', ']', around),
            VimTextObject.InnerBrace or VimTextObject.AroundBrace => Delimited(buffer, caret, '{', '}', around),
            VimTextObject.InnerAngle or VimTextObject.AroundAngle => Delimited(buffer, caret, '<', '>', around),

            VimTextObject.InnerDoubleQuote or VimTextObject.AroundDoubleQuote => Quoted(buffer, caret, '"', around),
            VimTextObject.InnerSingleQuote or VimTextObject.AroundSingleQuote => Quoted(buffer, caret, '\'', around),
            VimTextObject.InnerBacktick or VimTextObject.AroundBacktick => Quoted(buffer, caret, '`', around),

            _ => null,
        };
    }

    /// <summary>
    /// What <c>iw</c> and <c>aw</c> cover — and, read with the two-class classifier, what <c>iW</c>
    /// and <c>aW</c> do.
    /// </summary>
    /// <remarks>
    /// Never <see langword="null"/>: there is always a run under the caret, and on an empty line the
    /// run is empty — a range that covers nothing, which is a match with nothing in it rather than a
    /// miss. That is what makes <c>ciw</c> on an empty line enter insert mode, exactly as <c>c0</c>
    /// at the start of a line does.
    /// </remarks>
    private static VimTextRange? Word(
        IVimBuffer buffer,
        int caret,
        int count,
        bool around,
        Func<char, VimWords.CharacterClass> classOf)
    {
        VimLine line = buffer.GetLineByOffset(caret);
        int start = RunStart(buffer, line, caret, classOf);
        int end = caret;

        if (!around)
        {
            // 'iw' counts runs rather than words, which is vim's own reading and the reason 'd3iw' is
            // word, blanks, word rather than three words.
            for (int step = 0; (step < count) && (end < line.EndOffset); step++)
            {
                end = RunEnd(buffer, line, end, classOf);
            }

            return Characters(start, end);
        }

        bool tookBlanks = false;

        // 'aw' counts words with the blanks that follow them — or, from a run of blanks, the blanks
        // with the word that follows them. Both readings are the same two runs taken in order, which
        // is what makes 'daw' work from either side of the space.
        for (int step = 0; (step < count) && (end < line.EndOffset); step++)
        {
            bool fromBlanks = IsBlank(buffer, end, classOf);

            end = RunEnd(buffer, line, end, classOf);
            tookBlanks = fromBlanks;

            if ((end < line.EndOffset) && (fromBlanks || IsBlank(buffer, end, classOf)))
            {
                tookBlanks = IsBlank(buffer, end, classOf);
                end = RunEnd(buffer, line, end, classOf);
            }
        }

        // Vim's fallback: a word with no blanks after it takes the ones before it instead, so 'daw'
        // on the last word of a line does not leave the separator that used to precede it.
        if (!tookBlanks)
        {
            start = BlanksBefore(buffer, line, start, classOf);
        }

        return Characters(start, end);
    }

    /// <summary>
    /// What a bracket object covers: the nearest pair the caret is inside, or the one it is on.
    /// </summary>
    /// <remarks>
    /// A caret sitting on a delimiter belongs to that delimiter's pair rather than to whatever
    /// encloses it, which is what makes <c>di(</c> with the caret on the <c>(</c> mean the obvious
    /// thing. Everywhere else the pair is found by scanning outwards in both directions, each scan
    /// counting the pairs it passes so that <c>ci(</c> inside <c>((ab))</c> takes the inner one.
    /// </remarks>
    private static VimTextRange? Delimited(IVimBuffer buffer, int caret, char open, char close, bool around)
    {
        int? opening;
        int? closing;

        if (CharacterAt(buffer, caret) == open)
        {
            opening = caret;
            closing = VimBrackets.NextUnmatched(buffer, caret + 1, open, close);
        }
        else if (CharacterAt(buffer, caret) == close)
        {
            opening = VimBrackets.PreviousUnmatched(buffer, caret - 1, open, close);
            closing = caret;
        }
        else
        {
            opening = VimBrackets.PreviousUnmatched(buffer, caret - 1, open, close);
            closing = VimBrackets.NextUnmatched(buffer, caret + 1, open, close);
        }

        if ((opening is not { } first) || (closing is not { } last))
        {
            return null;
        }

        return around ? Characters(first, last + 1) : Characters(first + 1, last);
    }

    /// <summary>
    /// What a quote object covers: the pair the caret falls in, counting from the start of the line.
    /// </summary>
    /// <remarks>
    /// The caret has to be within a pair, delimiters included. Vim also jumps forward to the next
    /// quoted run when the caret is before the first quote on the line; that is not done here, so a
    /// caret outside every pair is a miss.
    /// </remarks>
    private static VimTextRange? Quoted(IVimBuffer buffer, int caret, char quote, bool around)
    {
        VimLine line = buffer.GetLineByOffset(caret);

        for (int index = line.Offset; index < line.EndOffset; index++)
        {
            if (buffer.CharAt(index) != quote)
            {
                continue;
            }

            // An unpaired quote opens nothing, and there is nothing further along the line that could
            // pair with anything either.
            if (Next(buffer, index + 1, line.EndOffset, quote) is not { } closing)
            {
                return null;
            }

            if ((caret >= index) && (caret <= closing))
            {
                return around ? Characters(index, closing + 1) : Characters(index + 1, closing);
            }

            index = closing;
        }

        return null;
    }

    private static int? Next(IVimBuffer buffer, int from, int end, char character)
    {
        for (int index = from; index < end; index++)
        {
            if (buffer.CharAt(index) == character)
            {
                return index;
            }
        }

        return null;
    }

    /// <summary>
    /// Walks back to the first character of the run the offset is in, never off the line.
    /// </summary>
    private static int RunStart(IVimBuffer buffer, VimLine line, int offset, Func<char, VimWords.CharacterClass> classOf)
    {
        if (offset >= line.EndOffset)
        {
            return offset;
        }

        VimWords.CharacterClass run = classOf(buffer.CharAt(offset));
        int index = offset;

        while ((index > line.Offset) && (classOf(buffer.CharAt(index - 1)) == run))
        {
            index--;
        }

        return index;
    }

    /// <summary>
    /// Walks forward to one past the last character of the run the offset is in, never off the line.
    /// </summary>
    private static int RunEnd(IVimBuffer buffer, VimLine line, int offset, Func<char, VimWords.CharacterClass> classOf)
    {
        VimWords.CharacterClass run = classOf(buffer.CharAt(offset));
        int index = offset;

        while ((index < line.EndOffset) && (classOf(buffer.CharAt(index)) == run))
        {
            index++;
        }

        return index;
    }

    private static int BlanksBefore(IVimBuffer buffer, VimLine line, int start, Func<char, VimWords.CharacterClass> classOf)
    {
        int index = start;

        while ((index > line.Offset) && (classOf(buffer.CharAt(index - 1)) == VimWords.CharacterClass.Blank))
        {
            index--;
        }

        return index;
    }

    private static bool IsBlank(IVimBuffer buffer, int offset, Func<char, VimWords.CharacterClass> classOf)
        => classOf(buffer.CharAt(offset)) == VimWords.CharacterClass.Blank;

    private static char? CharacterAt(IVimBuffer buffer, int offset)
        => ((offset >= 0) && (offset < buffer.TextLength)) ? buffer.CharAt(offset) : null;

    private static VimTextRange Characters(int one, int other)
        => new(Math.Min(one, other), Math.Max(one, other), VimRegisterKind.Characterwise);
}
