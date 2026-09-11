using System;

namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// The arithmetic of the view: which lines are on screen, and how far a page or half a page is.
/// </summary>
/// <remarks>
/// <para>
/// Kept apart from the engine for the same reason <see cref="VimRanges"/> is: these are answers about
/// where the view stands — "which line is third from the bottom?" — and both the caret motions
/// (<c>H</c> <c>M</c> <c>L</c>) and the ranges behind an operator (<c>dL</c>) need the same answers,
/// so they are worked out once, here, from the two numbers <see cref="IVimBuffer"/> reports.
/// </para>
/// <para>
/// Vim's own formulas throughout. <c>M</c> is the middle of the lines actually shown, which for a
/// buffer shorter than the window is the middle of the buffer; a page for <c>Ctrl+F</c> is the window
/// less two lines, so that the last two lines of one screen are the first two of the next; and half a
/// page for <c>Ctrl+D</c> is half the window, which is vim's default for its <c>scroll</c> option.
/// </para>
/// </remarks>
internal static class VimViewport
{
    /// <summary>
    /// The last line on screen: the first visible line plus the view's height, or the last line of the
    /// buffer when the buffer ends before the view does.
    /// </summary>
    public static int LastVisibleLine(IVimBuffer buffer)
        => Math.Min((buffer.FirstVisibleLine + buffer.VisibleLineCount) - 1, buffer.LineCount);

    /// <summary>
    /// The line <c>H</c> names: the top of the view, or the <paramref name="count"/>th line down from it.
    /// </summary>
    /// <remarks>
    /// Clamped to the buffer's last line and not to the bottom of the view, which is vim's rule:
    /// <c>99H</c> in a long file goes past the window and the view follows.
    /// </remarks>
    public static int TopLine(IVimBuffer buffer, int count)
        => Math.Min((buffer.FirstVisibleLine + count) - 1, buffer.LineCount);

    /// <summary>
    /// The line <c>M</c> names: the middle of the lines on screen.
    /// </summary>
    /// <remarks>
    /// Over an odd number of lines the one in the middle; over an even number the upper of the two,
    /// which is where vim's own count of "half the lines used" stops.
    /// </remarks>
    public static int MiddleLine(IVimBuffer buffer)
    {
        int first = buffer.FirstVisibleLine;
        int shown = (LastVisibleLine(buffer) - first) + 1;

        return first + ((shown - 1) / 2);
    }

    /// <summary>
    /// The line <c>L</c> names: the bottom of the view, or the <paramref name="count"/>th line up from it.
    /// </summary>
    /// <remarks>
    /// Clamped to the top of the view, not the top of the buffer — the other half of vim's asymmetry
    /// with <c>H</c>: a count on <c>L</c> never leaves the screen.
    /// </remarks>
    public static int BottomLine(IVimBuffer buffer, int count)
        => Math.Max((LastVisibleLine(buffer) - count) + 1, buffer.FirstVisibleLine);

    /// <summary>
    /// How many lines <c>Ctrl+D</c> and <c>Ctrl+U</c> scroll when no count says otherwise: half the
    /// view, never less than one.
    /// </summary>
    public static int HalfPage(IVimBuffer buffer) => Math.Max(buffer.VisibleLineCount / 2, 1);

    /// <summary>
    /// How many lines <c>Ctrl+F</c> and <c>Ctrl+B</c> scroll for one page: the view less two lines of
    /// overlap, never less than one.
    /// </summary>
    public static int Page(IVimBuffer buffer) => Math.Max(buffer.VisibleLineCount - 2, 1);

    /// <summary>
    /// The furthest down the view can go without showing empty space under the buffer's last line —
    /// where <c>Ctrl+D</c> stops scrolling and starts moving only the caret.
    /// </summary>
    public static int LastTopLineWithoutGap(IVimBuffer buffer)
        => Math.Max((buffer.LineCount - buffer.VisibleLineCount) + 1, 1);
}
