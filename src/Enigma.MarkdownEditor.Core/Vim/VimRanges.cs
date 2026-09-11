using System;

namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// Which characters a motion covers when an operator is waiting on it.
/// </summary>
/// <remarks>
/// <para>
/// Kept apart from the engine for the same reason <see cref="VimWords"/> is: this answers a question
/// about the text — "what does <c>d3w</c> reach?" — and the engine answers what to do about it. It
/// is also where the difference between a motion and a range lives, which is not a detail: <c>l</c>
/// as a motion stops on the last character of a line, but <c>dl</c> on that character has to delete
/// it, so the range runs one further than the caret ever would.
/// </para>
/// <para>
/// A visual selection is a range too — <see cref="Selection"/> — and for the same reason: what
/// <c>v</c> and <c>V</c> cover is a question about the text, and the engine answers what the operator
/// does with it.
/// </para>
/// <para>
/// Several of vim's rules only exist here, because each is about a motion used with an operator
/// rather than on its own. <c>cw</c> on a non-blank changes to the <em>end</em> of the word, like
/// <c>ce</c>, so that changing a word does not swallow the space after it — the quirk vim's own
/// documentation apologises for and every vim user relies on. <c>w</c> stops at the end of a line
/// rather than running on to the next line's first word, so that deleting the last word of a line
/// does not join it to the line below. And an exclusive motion whose end lands in the first column
/// of a later line is pulled back to the end of the line before — or, when it also started at or
/// before the first non-blank, becomes linewise — which is what makes <c>d}</c> from the first
/// column of a paragraph take whole lines and leave the blank line after them alone. That last rule
/// is applied to the paragraph motions, to the two that cross a line break on purpose, and to the
/// searches — the motions whose end can land there.
/// </para>
/// </remarks>
internal static class VimRanges
{
    /// <summary>
    /// Works out what an operator's motion covers.
    /// </summary>
    /// <param name="buffer">The buffer to measure.</param>
    /// <param name="caret">Where the caret is, already pulled inside the line by the engine.</param>
    /// <param name="operator">The operator waiting on the motion; <c>cw</c> reads differently to <c>dw</c>.</param>
    /// <param name="motion">The motion that was typed.</param>
    /// <param name="count">How many times, never less than one.</param>
    /// <param name="hasCount">
    /// Whether the count was typed. <c>G</c>, <c>gg</c> and <c>%</c> are the motions that mean a
    /// different place with a count than without one, so they need to know rather than to guess from
    /// a 1.
    /// </param>
    /// <param name="find">
    /// The find the motion stands for, when it is one of the six — already resolved out of <c>;</c>
    /// and <c>,</c> by the engine, which is where the memory of the last find lives.
    /// <see langword="null"/> for every other motion, and for a <c>;</c> typed before any find.
    /// </param>
    /// <returns>The range, or <see langword="null"/> for a motion that cannot form one.</returns>
    public static VimTextRange? Resolve(
        IVimBuffer buffer,
        int caret,
        VimOperator @operator,
        VimMotion motion,
        int count,
        bool hasCount,
        VimFindRequest? find = null)
    {
        VimLine line = buffer.GetLineByOffset(caret);

        // Before the switch, because a find is the one motion whose name does not say where it goes:
        // 'RepeatFind' is four different motions depending on what was typed last.
        if (find is { } request)
        {
            return FindRange(buffer, caret, request, count);
        }

        return motion switch
        {
            VimMotion.WholeLine or VimMotion.CurrentLineFirstNonBlank => Lines(buffer, line.Number, (line.Number + count) - 1),
            VimMotion.Down or VimMotion.LineDownFirstNonBlank => Lines(buffer, line.Number, line.Number + count),
            VimMotion.Up or VimMotion.LineUpFirstNonBlank => Lines(buffer, line.Number, line.Number - count),
            VimMotion.FirstLine => Lines(buffer, line.Number, hasCount ? count : 1),
            VimMotion.LastLine => Lines(buffer, line.Number, hasCount ? count : buffer.LineCount),

            // The screen motions name a line the way 'G' does, and are linewise the same way: 'dL'
            // takes from the caret's line to the bottom of the screen, whole lines.
            VimMotion.ScreenTop => Lines(buffer, line.Number, VimViewport.TopLine(buffer, count)),
            VimMotion.ScreenMiddle => Lines(buffer, line.Number, VimViewport.MiddleLine(buffer)),
            VimMotion.ScreenBottom => Lines(buffer, line.Number, VimViewport.BottomLine(buffer, count)),

            // With a count '%' is a line motion; without one it is a jump to a bracket, and inclusive
            // of it, so 'd%' takes both brackets with everything between them.
            VimMotion.MatchingBracket when hasCount => Lines(buffer, line.Number, PercentLine(buffer, count)),
            VimMotion.MatchingBracket => VimBrackets.Match(buffer, caret) is { } match
                ? Characters(Math.Min(caret, match), Inclusive(buffer, Math.Max(caret, match)))
                : null,

            VimMotion.LineStart => Characters(line.Offset, caret),
            VimMotion.FirstNonBlank => Characters(line.FirstNonBlankOffset, caret),
            VimMotion.Column => Characters(caret, ColumnOffset(line, count)),
            VimMotion.LineEnd => Characters(caret, LineBelow(buffer, line, count - 1).EndOffset),
            VimMotion.Left => Characters(Math.Max(line.Offset, caret - count), caret),

            // One further than the motion itself would go: 'l' stops on the last character of the
            // line, 'dl' on that character deletes it.
            VimMotion.Right => Characters(caret, Math.Min(line.EndOffset, caret + count)),

            VimMotion.RightWrapping => RightWrapping(buffer, caret, count),
            VimMotion.LeftWrapping => ExclusiveLinewise(buffer, Characters(Walk(buffer, caret, count, VimWhichWrap.Left), caret)),

            VimMotion.WordBackward => Characters(Walk(buffer, caret, count, VimWords.PreviousStart), caret),
            VimMotion.WordBackwardBig => Characters(Walk(buffer, caret, count, VimWords.PreviousStartBig), caret),
            VimMotion.WordEnd => Characters(caret, Inclusive(buffer, Walk(buffer, caret, count, VimWords.End))),
            VimMotion.WordEndBig => Characters(caret, Inclusive(buffer, Walk(buffer, caret, count, VimWords.EndBig))),
            VimMotion.WordForward => WordForward(buffer, caret, @operator, count, VimWords.NextStart, VimWords.End),
            VimMotion.WordForwardBig => WordForward(buffer, caret, @operator, count, VimWords.NextStartBig, VimWords.EndBig),

            VimMotion.ParagraphForward => Paragraph(buffer, caret, forward: true, count),
            VimMotion.ParagraphBackward => Paragraph(buffer, caret, forward: false, count),
            VimMotion.SentenceForward => VimParagraphs.Sentence(buffer, caret, forward: true, count) is { } next
                ? Characters(caret, next)
                : null,
            VimMotion.SentenceBackward => VimParagraphs.Sentence(buffer, caret, forward: false, count) is { } previous
                ? Characters(previous, caret)
                : null,

            _ => null,
        };
    }

    /// <summary>
    /// Works out what a visual selection covers.
    /// </summary>
    /// <param name="buffer">The buffer to measure.</param>
    /// <param name="anchor">Where the selection was started, the end that does not move.</param>
    /// <param name="caret">Where the caret is now, the end the motions move.</param>
    /// <param name="linewise">
    /// Whether the selection is <c>V</c>'s rather than <c>v</c>'s: whole lines, whatever column
    /// either end is in.
    /// </param>
    /// <returns>The range, from whichever end comes first in the buffer.</returns>
    /// <remarks>
    /// Which end is the anchor stops mattering here: a selection made backwards covers exactly what
    /// the same selection made forwards does, and the caret's own position is what the engine puts
    /// back afterwards.
    /// </remarks>
    public static VimTextRange Selection(IVimBuffer buffer, int anchor, int caret, bool linewise)
    {
        if (linewise)
        {
            return Lines(buffer, buffer.GetLineByOffset(anchor).Number, buffer.GetLineByOffset(caret).Number);
        }

        // Inclusive of the character under the caret, which is vim's own rule and the reason 'vd' on
        // one character deletes it rather than nothing. On an empty line the one character it can
        // cover is the line break, so 'vd' there joins the two lines — again vim's answer.
        return Characters(Math.Min(anchor, caret), Inclusive(buffer, Math.Max(anchor, caret)));
    }

    /// <summary>
    /// What an operator over a search covers: from the caret to the match, or from the match to the
    /// caret, exclusive either way.
    /// </summary>
    /// <param name="buffer">The buffer to measure.</param>
    /// <param name="caret">Where the caret is.</param>
    /// <param name="target">Where the search landed, already found by the engine.</param>
    /// <returns>The range.</returns>
    /// <remarks>
    /// Exclusive in both directions, as vim's search motions are: <c>d/foo</c> stops short of the
    /// <c>f</c>, and <c>d?foo</c> takes from the <c>f</c> up to but not including the caret's own
    /// character — the same shape as <c>dF</c>. The exclusive-linewise rules apply, so <c>d/^#</c>
    /// from the first column takes whole lines up to the heading and leaves the heading alone.
    /// </remarks>
    public static VimTextRange Search(IVimBuffer buffer, int caret, int target)
        => ExclusiveLinewise(buffer, Characters(caret, target));

    /// <summary>
    /// The line <c>N%</c> names: vim's own formula, rounding up.
    /// </summary>
    /// <param name="buffer">The buffer to measure.</param>
    /// <param name="percent">The count that was typed.</param>
    /// <returns>The line number, never past the last line.</returns>
    public static int PercentLine(IVimBuffer buffer, int percent)
        => Math.Clamp(((percent * buffer.LineCount) + 99) / 100, 1, buffer.LineCount);

    /// <summary>
    /// The offset <c>|</c> names on a line: column <paramref name="column"/> counted from one, and
    /// never past the last character.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="column">The count that was typed, one when none was.</param>
    /// <returns>The offset.</returns>
    public static int ColumnOffset(VimLine line, int column)
        => line.Offset + Math.Min(column - 1, Math.Max(0, line.Length - 1));

    /// <summary>
    /// What an operator over a find covers.
    /// </summary>
    /// <remarks>
    /// Vim's two rules about direction, and nothing else. <c>f</c> and <c>t</c> are inclusive, so
    /// <c>df,</c> takes the comma with it and <c>dt,</c> stops on the character before it — one
    /// formula, because <c>t</c>'s target is already the short one. <c>F</c> and <c>T</c> are
    /// exclusive of the caret's own character, so <c>dF,</c> deletes back to and including the comma
    /// and leaves the character the caret was on.
    /// </remarks>
    private static VimTextRange? FindRange(IVimBuffer buffer, int caret, VimFindRequest request, int count)
    {
        if (VimFind.Target(buffer, caret, request, count) is not { } target)
        {
            return null;
        }

        return request.IsForward ? Characters(caret, target + 1) : Characters(target, caret);
    }

    private static VimTextRange WordForward(
        IVimBuffer buffer,
        int caret,
        VimOperator @operator,
        int count,
        Func<IVimBuffer, int, int> nextStart,
        Func<IVimBuffer, int, int> end)
    {
        // The 'cw' quirk. On a blank it is left alone: there is no word under the caret to change,
        // so 'cw' there changes the run of white-space, exactly as 'dw' would delete it.
        if ((@operator == VimOperator.Change) && (caret < buffer.TextLength) && !char.IsWhiteSpace(buffer.CharAt(caret)))
        {
            return Characters(caret, Inclusive(buffer, Walk(buffer, caret, count, end)));
        }

        return Characters(caret, ClipAtLineEnd(buffer, caret, Walk(buffer, caret, count, nextStart)));
    }

    /// <summary>
    /// What an operator over <c>Space</c> covers.
    /// </summary>
    /// <remarks>
    /// Exclusive, like the motion — except when the walk runs into the end of the buffer, where vim
    /// makes it inclusive so that <c>d&lt;Space&gt;</c> on the last character still deletes it, as
    /// <c>dl</c> does. The line-break rules then pull an end that landed in a first column back onto
    /// the line before, which is what makes <c>d&lt;Space&gt;</c> on the last character of any other
    /// line delete that character and not the break after it.
    /// </remarks>
    private static VimTextRange RightWrapping(IVimBuffer buffer, int caret, int count)
    {
        int at = caret;
        bool stuck = false;

        for (int index = 0; index < count; index++)
        {
            int next = VimWhichWrap.Right(buffer, at);

            if (next == at)
            {
                stuck = true;
                break;
            }

            at = next;
        }

        int end = (stuck && (at < buffer.GetLineByOffset(at).EndOffset)) ? at + 1 : at;

        return ExclusiveLinewise(buffer, Characters(caret, end));
    }

    /// <summary>
    /// What an operator over <c>}</c> or <c>{</c> covers.
    /// </summary>
    private static VimTextRange? Paragraph(IVimBuffer buffer, int caret, bool forward, int count)
    {
        if (VimParagraphs.Paragraph(buffer, caret, forward, count) is not { } target)
        {
            return null;
        }

        return ExclusiveLinewise(buffer, Characters(caret, target));
    }

    /// <summary>
    /// Applies vim's two rules for an exclusive motion whose end lands in the first column of a
    /// later line.
    /// </summary>
    /// <remarks>
    /// The end is moved back to the end of the line before it, so that the motion does not take the
    /// line break with it; and when the range also started at or before the first non-blank of its
    /// line, it becomes linewise instead. The second rule is what makes <c>d}</c> from a paragraph's
    /// first column delete its lines and stop short of the blank line, and the first is what makes
    /// <c>d}</c> from mid-line stop at the end of the paragraph rather than joining it to the blank
    /// line after it. Neither applies to a range that ends inside a line, or that covers nothing.
    /// </remarks>
    private static VimTextRange ExclusiveLinewise(IVimBuffer buffer, VimTextRange range)
    {
        if (range.IsEmpty)
        {
            return range;
        }

        VimLine last = buffer.GetLineByOffset(range.End);
        VimLine first = buffer.GetLineByOffset(range.Start);

        if ((range.End != last.Offset) || (last.Number == first.Number))
        {
            return range;
        }

        if (range.Start <= first.FirstNonBlankOffset)
        {
            return Lines(buffer, first.Number, last.Number - 1);
        }

        return Characters(range.Start, buffer.GetLine(last.Number - 1).EndOffset);
    }

    /// <summary>
    /// Pulls a <c>w</c> target back to the end of its line when the only thing between the last word
    /// and the target is a line break.
    /// </summary>
    /// <remarks>
    /// The blanks <em>between</em> two words on one line belong to the motion — <c>dw</c> on
    /// <c>foo   bar</c> takes the spaces with it — so a run of blanks is only dropped when it carries
    /// a line break. That one test is the whole of vim's rule.
    /// </remarks>
    private static int ClipAtLineEnd(IVimBuffer buffer, int caret, int target)
    {
        int back = target;

        while ((back > caret) && char.IsWhiteSpace(buffer.CharAt(back - 1)))
        {
            back--;
        }

        for (int index = back; index < target; index++)
        {
            if ((buffer.CharAt(index) == '\n') || (buffer.CharAt(index) == '\r'))
            {
                return back;
            }
        }

        return target;
    }

    private static VimLine LineBelow(IVimBuffer buffer, VimLine line, int distance)
        => buffer.GetLine(Math.Min(line.Number + distance, buffer.LineCount));

    private static int Inclusive(IVimBuffer buffer, int target) => Math.Min(target + 1, buffer.TextLength);

    private static int Walk(IVimBuffer buffer, int caret, int count, Func<IVimBuffer, int, int> step)
    {
        for (int index = 0; index < count; index++)
        {
            caret = step(buffer, caret);
        }

        return caret;
    }

    private static VimTextRange Lines(IVimBuffer buffer, int one, int other)
    {
        int first = Math.Clamp(Math.Min(one, other), 1, buffer.LineCount);
        int last = Math.Clamp(Math.Max(one, other), 1, buffer.LineCount);

        return new VimTextRange(
            buffer.GetLine(first).Offset,
            buffer.GetLine(last).EndOffset,
            VimRegisterKind.Linewise);
    }

    private static VimTextRange Characters(int one, int other)
        => new(Math.Min(one, other), Math.Max(one, other), VimRegisterKind.Characterwise);
}
