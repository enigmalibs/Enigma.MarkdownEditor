using System;
using System.Collections.Generic;

namespace Enigma.MarkdownEditor.Core.Markdown;

/// <summary>
/// The line-to-block index the preview's scroll sync is built on: given a source line, which rendered
/// block it belongs to and how far into it.
/// </summary>
/// <remarks>
/// <para>
/// Built from the first source line of each rendered block, in document order, and nothing else. That
/// is deliberately less than the AST knows: the renderer skips block kinds it does not draw, so the
/// only list that can be matched back to what is on screen is the one the render itself produced.
/// Keeping the map to a sequence of integers is also what lets it — and therefore the whole sync
/// calculation — be tested without a window.
/// </para>
/// <para>
/// The blocks are treated as a gapless partition of the document: each one owns the lines from its
/// own start up to the line before the next block's, and the last one owns everything left. The blank
/// lines between two blocks therefore belong to the one above them, which is what the reader sees —
/// the space under a paragraph is that paragraph's, not the next heading's.
/// </para>
/// <para>
/// Lines are one-based throughout, matching the editor's own caret and line numbers. Markdig counts
/// from zero, so the renderer adds one on the way in.
/// </para>
/// </remarks>
public sealed class MarkdownLineMap
{
    // Far larger than the rounding error of a division followed by its own multiplication, and far
    // smaller than the one line it must never add.
    private const double Tolerance = 1e-9;

    private readonly int[] _starts;
    private readonly int _lastLine;

    private MarkdownLineMap(int[] starts, int lastLine)
    {
        _starts = starts;
        _lastLine = lastLine;
    }

    /// <summary>
    /// The map of a document that rendered no blocks. Finds nothing.
    /// </summary>
    public static MarkdownLineMap Empty { get; } = new([], 0);

    /// <summary>
    /// How many blocks the map covers.
    /// </summary>
    public int Count => _starts.Length;

    /// <summary>
    /// Whether the map covers no blocks at all, and so can answer nothing.
    /// </summary>
    public bool IsEmpty => _starts.Length == 0;

    /// <summary>
    /// Builds the map for one render.
    /// </summary>
    /// <param name="startLines">
    /// The first source line of every rendered block, one-based, in document order. Must not
    /// decrease; two blocks may share a start line, which gives the first of them no lines of its own.
    /// </param>
    /// <param name="lastLine">
    /// The document's last line, one-based — what the final block runs to. A value below the final
    /// start line is raised to it.
    /// </param>
    /// <returns>The map, or <see cref="Empty"/> when <paramref name="startLines"/> is empty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="startLines"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A start line is below <c>1</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="startLines"/> decreases.</exception>
    public static MarkdownLineMap For(IReadOnlyList<int> startLines, int lastLine)
    {
        ArgumentNullException.ThrowIfNull(startLines);

        if (startLines.Count == 0)
        {
            return Empty;
        }

        int[] starts = new int[startLines.Count];

        for (int index = 0; index < startLines.Count; index++)
        {
            int start = startLines[index];

            ArgumentOutOfRangeException.ThrowIfLessThan(start, 1, nameof(startLines));

            // Rejected rather than sorted: document order is the caller's guarantee, and a sequence
            // that breaks it means the render and the map disagree about which block is which — an
            // answer that would be wrong rather than merely imprecise.
            if ((index > 0) && (start < starts[index - 1]))
            {
                throw new ArgumentException(
                    "The start lines must be in document order; they decrease.",
                    nameof(startLines));
            }

            starts[index] = start;
        }

        return new MarkdownLineMap(starts, Math.Max(lastLine, starts[^1]));
    }

    /// <summary>
    /// The first source line of one block.
    /// </summary>
    /// <param name="index">The block's position in the rendered sequence, zero-based.</param>
    /// <returns>The line, one-based.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the map.</exception>
    public int StartLineAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _starts.Length);

        return _starts[index];
    }

    /// <summary>
    /// The last source line one block owns — the line before the next block starts, or the document's
    /// last line for the final block.
    /// </summary>
    /// <param name="index">The block's position in the rendered sequence, zero-based.</param>
    /// <returns>The line, one-based. Never below <see cref="StartLineAt"/> of the same block.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the map.</exception>
    public int EndLineAt(int index)
    {
        int start = StartLineAt(index);

        return (index == (_starts.Length - 1))
            ? Math.Max(start, _lastLine)
            : Math.Max(start, _starts[index + 1] - 1);
    }

    /// <summary>
    /// Finds the block a source line belongs to.
    /// </summary>
    /// <param name="line">The source line, one-based.</param>
    /// <param name="position">The block and how far into it the line sits.</param>
    /// <returns>
    /// <see langword="true"/> unless the map is empty. A line above the first block's start answers
    /// with the first block, and one past the document's end with the last: a viewport can sit either
    /// side of the content, and refusing to answer there would leave the preview wherever it was.
    /// </returns>
    public bool TryFind(int line, out MarkdownLinePosition position)
    {
        if (_starts.Length == 0)
        {
            position = default;

            return false;
        }

        int index = IndexOf(line);
        int start = _starts[index];
        int length = (EndLineAt(index) - start) + 1;

        // Divided by the count of lines the block owns rather than by the distance between its ends,
        // so a single-line block is 0 rather than a division by zero, and the last line of a block
        // lands short of the block below instead of exactly on it.
        double fraction = (length <= 1) ? 0 : Math.Clamp((double)(line - start) / length, 0, 1);

        position = new MarkdownLinePosition(index, fraction);

        return true;
    }

    /// <summary>
    /// The source line a position means — the inverse of <see cref="TryFind"/>.
    /// </summary>
    /// <param name="position">The block and how far into it.</param>
    /// <returns>
    /// The line, one-based, clamped to the lines the block owns. <c>0</c> when the map is empty.
    /// </returns>
    /// <remarks>
    /// What keeps the preview's own scroll position across a re-render: the position is read off the
    /// visuals before the tree is thrown away, turned into a line here, and looked up again in the new
    /// map. A line survives an edit somewhere else in the document; a pixel offset does not.
    /// </remarks>
    public int LineAt(MarkdownLinePosition position)
    {
        if (_starts.Length == 0)
        {
            return 0;
        }

        int index = Math.Clamp(position.Index, 0, _starts.Length - 1);
        int start = _starts[index];
        int end = EndLineAt(index);
        int length = (end - start) + 1;

        // Nudged before it is truncated: the fractions this reverses were produced by dividing by
        // that same length, and a division and a multiplication that should cancel can land a
        // fraction of a bit below the whole number they started at — which truncation would then turn
        // into a line off by one.
        double lines = Math.Floor((Math.Clamp(position.Fraction, 0, 1) * length) + Tolerance);

        return Math.Clamp(start + (int)lines, start, end);
    }

    // The last block whose start is at or before the line, or the first block when the line is above
    // all of them. Binary search rather than a walk because this runs on every scroll event of an
    // editor that raises one per pixel.
    private int IndexOf(int line)
    {
        int low = 0;
        int high = _starts.Length - 1;
        int found = 0;

        while (low <= high)
        {
            int middle = low + ((high - low) / 2);

            if (_starts[middle] <= line)
            {
                found = middle;
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return found;
    }
}
