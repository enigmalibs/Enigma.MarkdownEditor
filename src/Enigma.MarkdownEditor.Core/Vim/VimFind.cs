namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// Where <c>f</c>, <c>F</c>, <c>t</c> and <c>T</c> land.
/// </summary>
/// <remarks>
/// <para>
/// Kept apart from the engine for the same reason <see cref="VimWords"/> is: this answers a question
/// about the text — "where is the third comma to the right?" — and the engine answers what to do
/// about it. Both the bare motion and the range behind an operator go through the one scan here, so
/// that <c>fx</c> and <c>dfx</c> cannot disagree about which <c>x</c> they mean.
/// </para>
/// <para>
/// The scan never leaves the caret's line, in either direction and whatever the count. That is the
/// whole difference between a find and a search, and it is enforced here rather than by the callers
/// so that there is one place it can be true.
/// </para>
/// <para>
/// A count that runs out of matches finds nothing at all: <c>9fa</c> on a line with three
/// <c>a</c>s does not settle for the third. Vim's rule, and the one that makes a count safe to type
/// blind — a find that overshoots leaves the caret where it was rather than somewhere near.
/// </para>
/// </remarks>
internal static class VimFind
{
    /// <summary>
    /// Works out where a find lands.
    /// </summary>
    /// <param name="buffer">The buffer to scan.</param>
    /// <param name="caret">Where the caret is, already pulled inside the line by the engine.</param>
    /// <param name="request">Which find to run, already resolved out of <c>;</c> or <c>,</c>.</param>
    /// <param name="count">How many occurrences along, never less than one.</param>
    /// <returns>
    /// The offset the caret goes to, or <see langword="null"/> when the line does not hold that many
    /// occurrences.
    /// </returns>
    public static int? Target(IVimBuffer buffer, int caret, VimFindRequest request, int count)
    {
        VimLine line = buffer.GetLineByOffset(caret);

        // The one thing a repeat does differently. After 't' the caret sits on the character before
        // the match, so a scan starting next to it would find that same match and move nowhere; the
        // repeat steps over it first. Only a till needs this — 'f' already leaves the caret on the
        // match, and the scan's own first step is past it.
        int at = (request.Repeat && request.IsTill)
            ? (request.IsForward ? caret + 1 : caret - 1)
            : caret;

        for (int step = 0; step < count; step++)
        {
            at = request.IsForward
                ? NextOccurrence(buffer, at + 1, line.EndOffset, request.Character)
                : PreviousOccurrence(buffer, at - 1, line.Offset, request.Character);

            if (at < 0)
            {
                return null;
            }
        }

        // Applied once, after the last step, so that '3ta' is the third 'a' stopped short of rather
        // than three short steps.
        if (request.IsTill)
        {
            at += request.IsForward ? -1 : 1;
        }

        return at;
    }

    private static int NextOccurrence(IVimBuffer buffer, int from, int end, char character)
    {
        for (int index = from; index < end; index++)
        {
            if (buffer.CharAt(index) == character)
            {
                return index;
            }
        }

        return -1;
    }

    private static int PreviousOccurrence(IVimBuffer buffer, int from, int start, char character)
    {
        for (int index = from; index >= start; index--)
        {
            if (buffer.CharAt(index) == character)
            {
                return index;
            }
        }

        return -1;
    }
}
