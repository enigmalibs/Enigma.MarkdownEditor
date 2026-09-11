using System;

namespace Enigma.MarkdownEditor.Core.Search;

/// <summary>
/// One match: which file, where in it, and the piece of text to show for it.
/// </summary>
/// <remarks>
/// <para>
/// Two coordinate systems, deliberately. <see cref="Line"/> and <see cref="Column"/> address the file
/// as the editor sees it — one-based, over the whole line — and are what puts the caret on the match
/// when the hit is opened. <see cref="MatchStart"/> addresses <see cref="LineText"/>, which is only an
/// excerpt of that line, and is what the result list highlights.
/// </para>
/// <para>
/// The excerpt exists because a line has no length limit and a hit list does: a minified file's single
/// half-megabyte line, held five hundred times over, is the difference between a search and an
/// out-of-memory. It is capped at <see cref="MaxLineTextLength"/> characters around the match, with
/// the whitespace that indents the line dropped — that is width the pane cannot spare, and none of it
/// is what was searched for.
/// </para>
/// </remarks>
public sealed class WorkspaceSearchHit
{
    /// <summary>
    /// The longest excerpt kept for a hit. Enough for a sentence of context either side of a match in
    /// a pane a few hundred pixels wide, and short enough that five hundred of them cost nothing.
    /// </summary>
    public const int MaxLineTextLength = 200;

    // How much of the line before the match is kept when the line is too long to keep whole. Enough to
    // see what the match is part of; small enough that the match itself is never pushed out of sight.
    private const int LeadingContext = 40;

    private WorkspaceSearchHit(
        string filePath,
        WorkspaceSearchHitKind kind,
        int line,
        int column,
        string lineText,
        int matchStart,
        int matchLength)
    {
        FilePath = filePath;
        Kind = kind;
        Line = line;
        Column = column;
        LineText = lineText;
        MatchStart = matchStart;
        MatchLength = matchLength;
    }

    /// <summary>
    /// Gets the absolute path of the file the match is in.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Gets what matched: the file's name, or a line of its text.
    /// </summary>
    public WorkspaceSearchHitKind Kind { get; }

    /// <summary>
    /// Gets the one-based line the match is on. Always <c>1</c> for a
    /// <see cref="WorkspaceSearchHitKind.FileName"/> hit, which opens the file at its top.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// Gets the one-based column the match starts at, counted over the whole line rather than over
    /// <see cref="LineText"/>. Always <c>1</c> for a <see cref="WorkspaceSearchHitKind.FileName"/> hit.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Gets the text to show for this hit: an excerpt of the matching line, or the file's name.
    /// </summary>
    public string LineText { get; }

    /// <summary>
    /// Gets where the match starts within <see cref="LineText"/>, zero-based.
    /// </summary>
    public int MatchStart { get; }

    /// <summary>
    /// Gets how many characters matched, clamped to what <see cref="LineText"/> actually holds.
    /// </summary>
    public int MatchLength { get; }

    /// <summary>
    /// Builds a hit on a line of a file's text.
    /// </summary>
    /// <param name="filePath">The file's absolute path.</param>
    /// <param name="line">The one-based line the match is on.</param>
    /// <param name="lineText">The whole line, which the excerpt is cut from.</param>
    /// <param name="matchIndex">Where the match starts in <paramref name="lineText"/>, zero-based.</param>
    /// <param name="matchLength">How many characters matched.</param>
    /// <returns>The hit.</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Either string is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="line"/> is below one, or <paramref name="matchIndex"/> or
    /// <paramref name="matchLength"/> is negative.
    /// </exception>
    public static WorkspaceSearchHit InContent(
        string filePath,
        int line,
        string lineText,
        int matchIndex,
        int matchLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(lineText);
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(matchIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(matchLength);

        (string text, int start, int length) = Excerpt(lineText, matchIndex, matchLength);

        return new WorkspaceSearchHit(
            filePath,
            WorkspaceSearchHitKind.Content,
            line,
            matchIndex + 1,
            text,
            start,
            length);
    }

    /// <summary>
    /// Builds a hit on a file's own name.
    /// </summary>
    /// <param name="filePath">The file's absolute path.</param>
    /// <param name="fileName">The file's name, which is what the result list shows.</param>
    /// <param name="matchIndex">Where the match starts in <paramref name="fileName"/>, zero-based.</param>
    /// <param name="matchLength">How many characters matched.</param>
    /// <returns>The hit, addressing the top of the file.</returns>
    /// <exception cref="ArgumentException">Either string is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Either string is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="matchIndex"/> or <paramref name="matchLength"/> is negative.
    /// </exception>
    public static WorkspaceSearchHit InFileName(
        string filePath,
        string fileName,
        int matchIndex,
        int matchLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentOutOfRangeException.ThrowIfNegative(matchIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(matchLength);

        (string text, int start, int length) = Excerpt(fileName, matchIndex, matchLength);

        // Line and column one: what matched is the name, and the file has to open somewhere.
        return new WorkspaceSearchHit(filePath, WorkspaceSearchHitKind.FileName, 1, 1, text, start, length);
    }

    private static (string Text, int Start, int Length) Excerpt(string line, int matchIndex, int matchLength)
    {
        // A line that fits is kept whole; only a long one is windowed, and then around the match
        // rather than from the start, which is where the match usually is not.
        int start = line.Length > MaxLineTextLength ? Math.Max(0, matchIndex - LeadingContext) : 0;

        // Indentation is dropped, but never into the match: a match that IS whitespace has to survive.
        int indentLimit = Math.Min(matchIndex, line.Length);

        while ((start < indentLimit) && char.IsWhiteSpace(line[start]))
        {
            start++;
        }

        string text = line.Substring(start, Math.Min(MaxLineTextLength, line.Length - start));
        int excerptStart = Math.Min(matchIndex - start, text.Length);

        return (text, excerptStart, Math.Clamp(matchLength, 0, text.Length - excerptStart));
    }
}
