using System.Text;
using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// The commands that read or move the view: <c>H</c> <c>M</c> <c>L</c>, the six scroll keys vim
/// spells with Control, the two page keys, and the <c>z</c> family.
/// </summary>
/// <remarks>
/// <para>
/// Every case here runs over a window the fake buffer simulates — so many lines tall, scrolled to
/// such a line — because that is the one thing about these commands the engine cannot know from the
/// text. What is asserted is where the caret went and what the engine asked the view to do, in that
/// order: the real editor is then trusted with exactly those instructions and nothing more.
/// </para>
/// <para>
/// The buffers are numbered lines, indented two spaces, so that a case can name a line by its number
/// and still tell a landing on the first non-blank (column two) from a column that was kept.
/// </para>
/// </remarks>
public sealed class VimScreenMotionTests
{
    // A thirty-line buffer seen through a ten-line window, which is what most cases start from.
    private const int Lines = 30;
    private const int Window = 10;

    [Theory]
    // The top, middle and bottom of a window scrolled part way down the buffer.
    [InlineData("H", 11, 15, 11)]
    [InlineData("M", 11, 15, 15)]
    [InlineData("L", 11, 15, 20)]
    // Counts on H and L walk in from the edge; M ignores its count, as vim's does.
    [InlineData("3H", 11, 15, 13)]
    [InlineData("3L", 11, 15, 18)]
    [InlineData("3M", 11, 15, 15)]
    // H with a count too large goes past the window and is clamped to the buffer, vim's asymmetry:
    // L with one never leaves the window.
    [InlineData("25H", 11, 15, 30)]
    [InlineData("25L", 11, 15, 11)]
    // At the bottom of the buffer the window is only part full, and M is the middle of what shows.
    [InlineData("H", 25, 27, 25)]
    [InlineData("M", 25, 27, 27)]
    [InlineData("L", 25, 27, 30)]
    // Over an even number of lines the middle is the upper of the two.
    [InlineData("M", 1, 1, 5)]
    public void HMAndL_GoToTheTopMiddleAndBottomOfTheScreen(string keys, int firstVisible, int caretLine, int expected)
    {
        VimRun run = Screen(keys, firstVisible, caretLine);

        Assert.Equal(expected, run.CaretLine);
        Assert.Equal(2, run.CaretColumn);
        Assert.Empty(run.Buffer.Scrolls);
    }

    [Theory]
    [InlineData("H", 1)]
    [InlineData("M", 3)]
    [InlineData("L", 5)]
    public void InABufferShorterThanTheWindow_TheScreenIsTheBuffer(string keys, int expected)
    {
        VimRun run = VimScript.Run(Numbered(5, caretLine: 1), keys, Window);

        Assert.Equal(expected, run.CaretLine);
    }

    [Theory]
    // Linewise, whichever way the range runs: dL takes the caret's line to the bottom, dH up to the top.
    [InlineData("dL", 15, 15, "  line 21", 24)]
    [InlineData("dH", 15, 11, "  line 16", 25)]
    [InlineData("dM", 13, 13, "  line 16", 27)]
    [InlineData("d3H", 15, 13, "  line 16", 27)]
    public void AnOperatorOverAScreenMotion_TakesWholeLines(string keys, int caretLine, int landing, string lineThere, int linesLeft)
    {
        VimRun run = Screen(keys, firstVisible: 11, caretLine);

        Assert.Equal(linesLeft, run.Buffer.LineCount);
        Assert.Equal(landing, run.CaretLine);
        Assert.Equal(lineThere, run.Buffer.GetLine(landing).Text);
        Assert.Equal(VimRegisterKind.Linewise, run.Register?.Kind);
    }

    [Fact]
    public void AYankOverAScreenMotion_FillsTheRegisterWithTheLines()
    {
        VimRun run = Screen("yL", firstVisible: 11, caretLine: 18);

        Assert.Equal(VimRegister.Lines("  line 18\n  line 19\n  line 20"), run.Register);
        Assert.Equal(18, run.CaretLine);
    }

    [Fact]
    public void AChangeOverAScreenMotion_EmptiesTheLinesAndEntersInsertMode()
    {
        VimRun run = Screen("cL", firstVisible: 11, caretLine: 19);

        Assert.Equal(VimMode.Insert, run.Mode);
        Assert.Equal(29, run.Buffer.LineCount);
        Assert.Equal(string.Empty, run.Buffer.GetLine(19).Text);
    }

    [Theory]
    [InlineData("vL", 15, 20)]
    [InlineData("vH", 15, 11)]
    [InlineData("VLd", 15, 15)]
    public void InAVisualMode_AScreenMotionMovesTheCaretEnd(string keys, int caretLine, int landing)
    {
        VimRun run = Screen(keys, firstVisible: 11, caretLine);

        Assert.Equal(landing, run.CaretLine);
    }

    [Fact]
    public void VL_ThenDelete_TakesTheSelectedLines()
    {
        VimRun run = Screen("VLd", firstVisible: 11, caretLine: 15);

        Assert.Equal(24, run.Buffer.LineCount);
        Assert.Equal("  line 21", run.Buffer.GetLine(15).Text);
    }

    [Theory]
    // Half a window down: the view and the caret both move five lines, and the column is kept.
    [InlineData("<C-d>", 1, 3, 6, 8)]
    // A count is the number of lines instead, for this command alone.
    [InlineData("3<C-d>", 1, 3, 4, 6)]
    // The view stops where the last line reaches the bottom; the caret goes on.
    [InlineData("<C-d>", 18, 20, 21, 25)]
    [InlineData("<C-d>", 21, 25, 21, 30)]
    // Half a window up, and the mirror of all of it at the top.
    [InlineData("<C-u>", 11, 15, 6, 10)]
    [InlineData("3<C-u>", 11, 15, 8, 12)]
    [InlineData("<C-u>", 3, 8, 1, 3)]
    [InlineData("<C-u>", 1, 3, 1, 1)]
    public void CtrlDAndCtrlU_ScrollHalfAWindowAndMoveTheCaretWithIt(
        string keys,
        int firstVisible,
        int caretLine,
        int expectedFirst,
        int expectedCaret)
    {
        VimRun run = Screen(keys, firstVisible, caretLine, caretColumn: 7);

        Assert.Equal(expectedFirst, run.FirstVisibleLine);
        Assert.Equal(expectedCaret, run.CaretLine);
        Assert.Equal(7, run.CaretColumn);
    }

    [Fact]
    public void CtrlD_AsksForTheNewTopLineAtTheTop()
    {
        VimRun run = Screen("<C-d>", firstVisible: 1, caretLine: 3);

        Assert.Equal([(6, VimScrollAlignment.Top)], run.Buffer.Scrolls);
    }

    [Fact]
    public void CtrlD_WithTheViewAlreadyAtTheEnd_MovesOnlyTheCaret()
    {
        VimRun run = Screen("<C-d>", firstVisible: 21, caretLine: 25);

        Assert.Empty(run.Buffer.Scrolls);
        Assert.Equal(30, run.CaretLine);
    }

    [Theory]
    [InlineData("<C-d>", 21, 30)]
    [InlineData("<C-u>", 1, 1)]
    public void CtrlDOnTheLastLineAndCtrlUOnTheFirst_DoNothing(string keys, int firstVisible, int caretLine)
    {
        VimRun run = Screen(keys, firstVisible, caretLine, caretColumn: 7);

        Assert.Empty(run.Buffer.Scrolls);
        Assert.Equal(caretLine, run.CaretLine);
        Assert.Equal(7, run.CaretColumn);
        Assert.Null(run.Status);
    }

    [Fact]
    public void CtrlD_KeepsTheColumnThroughAShortLine_LikeJAndK()
    {
        // Line 3 is short; Ctrl+D from column 7 lands on its end, and Ctrl+U comes back to column 7.
        VimRun run = VimScript.Run("  line 1\n  line |2\nx\n  line 4\n  line 5\n  line 6", "<C-d><C-u>", viewportLines: 2);

        Assert.Equal(2, run.CaretLine);
        Assert.Equal(7, run.CaretColumn);
    }

    [Fact]
    public void CtrlD_InABufferShorterThanTheWindow_GoesToTheLastLine()
    {
        // Half a ten-line window is five lines, which is past the end of a three-line buffer: the
        // caret lands on the last line, as it does in vim, rather than moving half the buffer.
        VimRun run = VimScript.Run(Numbered(3, caretLine: 1), "<C-d>", Window);

        Assert.Equal(3, run.CaretLine);
        Assert.Empty(run.Buffer.Scrolls);
    }

    [Theory]
    // A page is the window less two lines, so the last two lines of one screen top the next; the
    // caret goes to the first non-blank of the new top line, and the view is aligned on that line.
    [InlineData("<C-f>", 1, 3, 9, 9)]
    [InlineData("<PageDown>", 1, 3, 9, 9)]
    [InlineData("2<C-f>", 1, 3, 17, 17)]
    // Near the end the view can go as far as the last line alone at the top, as vim's does.
    [InlineData("<C-f>", 21, 25, 29, 29)]
    [InlineData("<C-f>", 29, 29, 30, 30)]
    public void CtrlF_ScrollsAPageAndPutsTheCaretOnTheNewTopLine(
        string keys,
        int firstVisible,
        int caretLine,
        int expectedFirst,
        int expectedCaret)
    {
        VimRun run = Screen(keys, firstVisible, caretLine, caretColumn: 7);

        Assert.Equal(expectedFirst, run.FirstVisibleLine);
        Assert.Equal(expectedCaret, run.CaretLine);
        Assert.Equal(2, run.CaretColumn);
        Assert.Equal([(expectedFirst, VimScrollAlignment.Top)], run.Buffer.Scrolls);
    }

    [Theory]
    // Going back, the caret goes to the first non-blank of the new bottom line, and it is that line
    // the view is aligned on — at the bottom — so that the caret is on screen whatever the editor's
    // line heights make of the lines above it.
    [InlineData("<C-b>", 17, 20, 9, 18)]
    [InlineData("<PageUp>", 17, 20, 9, 18)]
    [InlineData("2<C-b>", 21, 25, 5, 14)]
    [InlineData("<C-b>", 5, 8, 1, 10)]
    public void CtrlB_ScrollsAPageBackAndPutsTheCaretOnTheNewBottomLine(
        string keys,
        int firstVisible,
        int caretLine,
        int expectedFirst,
        int expectedCaret)
    {
        VimRun run = Screen(keys, firstVisible, caretLine, caretColumn: 7);

        Assert.Equal(expectedFirst, run.FirstVisibleLine);
        Assert.Equal(expectedCaret, run.CaretLine);
        Assert.Equal(2, run.CaretColumn);
        Assert.Equal([(expectedCaret, VimScrollAlignment.Bottom)], run.Buffer.Scrolls);
    }

    [Theory]
    [InlineData("<C-f>", 30, 30)]
    [InlineData("<PageDown>", 30, 30)]
    [InlineData("<C-b>", 1, 5)]
    [InlineData("<PageUp>", 1, 5)]
    public void APageBeyondEitherEnd_DoesNothing(string keys, int firstVisible, int caretLine)
    {
        VimRun run = Screen(keys, firstVisible, caretLine, caretColumn: 7);

        Assert.Empty(run.Buffer.Scrolls);
        Assert.Equal(caretLine, run.CaretLine);
        Assert.Equal(7, run.CaretColumn);
        Assert.Null(run.Status);
    }

    [Fact]
    public void CtrlF_InABufferShorterThanTheWindow_PutsTheLastLineAtTheTop()
    {
        VimRun run = VimScript.Run(Numbered(5, caretLine: 1), "<C-f>", Window);

        Assert.Equal(5, run.FirstVisibleLine);
        Assert.Equal(5, run.CaretLine);
    }

    [Theory]
    // The view moves and the caret stays, column and all; the new top line is what the view is
    // aligned on.
    [InlineData("<C-e>", 11, 15, 12, 15)]
    [InlineData("3<C-e>", 11, 15, 14, 15)]
    [InlineData("<C-y>", 11, 15, 10, 15)]
    [InlineData("3<C-y>", 11, 15, 8, 15)]
    // Unless the caret would leave over the top, when it is pulled onto the new top line.
    [InlineData("5<C-e>", 11, 15, 16, 16)]
    // The last line may come to the top, and the view stops there.
    [InlineData("5<C-e>", 28, 29, 30, 30)]
    public void CtrlEAndCtrlY_ScrollTheViewAndLeaveTheCaretUnlessItWouldLeave(
        string keys,
        int firstVisible,
        int caretLine,
        int expectedFirst,
        int expectedCaret)
    {
        VimRun run = Screen(keys, firstVisible, caretLine, caretColumn: 7);

        Assert.Equal(expectedFirst, run.FirstVisibleLine);
        Assert.Equal(expectedCaret, run.CaretLine);
        Assert.Equal(7, run.CaretColumn);
        Assert.Equal([(expectedFirst, VimScrollAlignment.Top)], run.Buffer.Scrolls);
    }

    [Theory]
    // A caret that would leave over the bottom is pulled onto the new bottom line, and the view is
    // aligned on that line at the bottom — the line the caret is on is then exactly on screen.
    [InlineData("<C-y>", 11, 20, 10, 19)]
    [InlineData("5<C-y>", 11, 20, 6, 15)]
    public void CtrlY_PullsACaretThatWouldLeaveOntoTheBottomLine(
        string keys,
        int firstVisible,
        int caretLine,
        int expectedFirst,
        int expectedCaret)
    {
        VimRun run = Screen(keys, firstVisible, caretLine, caretColumn: 7);

        Assert.Equal(expectedFirst, run.FirstVisibleLine);
        Assert.Equal(expectedCaret, run.CaretLine);
        Assert.Equal(7, run.CaretColumn);
        Assert.Equal([(expectedCaret, VimScrollAlignment.Bottom)], run.Buffer.Scrolls);
    }

    [Theory]
    [InlineData("<C-e>", 30, 30)]
    [InlineData("<C-y>", 1, 3)]
    public void CtrlEAtTheEndAndCtrlYAtTheTop_DoNothing(string keys, int firstVisible, int caretLine)
    {
        VimRun run = Screen(keys, firstVisible, caretLine);

        Assert.Empty(run.Buffer.Scrolls);
        Assert.Equal(caretLine, run.CaretLine);
        Assert.Null(run.Status);
    }

    [Fact]
    public void CtrlE_PullsTheCaretOntoAShortLineAndRemembersTheColumn()
    {
        // The caret is pulled from line 1 onto the short line 2; the column it had comes back on
        // the next line down, exactly as it would after 'j'.
        VimRun run = VimScript.Run("  line |1\nx\n  line 3", "<C-e>j", viewportLines: 2);

        Assert.Equal(2, run.FirstVisibleLine);
        Assert.Equal(3, run.CaretLine);
        Assert.Equal(7, run.CaretColumn);
    }

    [Theory]
    // The caret's line goes to the top, the middle or the bottom, and the caret stays put.
    [InlineData("zt", VimScrollAlignment.Top, 15)]
    [InlineData("zz", VimScrollAlignment.Center, 11)]
    [InlineData("zb", VimScrollAlignment.Bottom, 6)]
    public void ZtZzAndZb_ScrollTheCaretsLineIntoPlaceAndLeaveTheCaret(string keys, VimScrollAlignment alignment, int expectedFirst)
    {
        VimRun run = Screen(keys, firstVisible: 1, caretLine: 15, caretColumn: 7);

        Assert.Equal([(15, alignment)], run.Buffer.Scrolls);
        Assert.Equal(expectedFirst, run.FirstVisibleLine);
        Assert.Equal(15, run.CaretLine);
        Assert.Equal(7, run.CaretColumn);
    }

    [Theory]
    // The same three, and the caret goes to the line's first non-blank.
    [InlineData("z<CR>", VimScrollAlignment.Top, 15)]
    [InlineData("z.", VimScrollAlignment.Center, 11)]
    [InlineData("z-", VimScrollAlignment.Bottom, 6)]
    public void ZEnterZDotAndZMinus_ScrollAndMoveTheCaretToTheFirstNonBlank(string keys, VimScrollAlignment alignment, int expectedFirst)
    {
        VimRun run = Screen(keys, firstVisible: 1, caretLine: 15, caretColumn: 7);

        Assert.Equal([(15, alignment)], run.Buffer.Scrolls);
        Assert.Equal(expectedFirst, run.FirstVisibleLine);
        Assert.Equal(15, run.CaretLine);
        Assert.Equal(2, run.CaretColumn);
    }

    [Theory]
    // With a count the line named goes into place and the caret goes to it: 'zt' keeps the column,
    // 'z<CR>' takes the first non-blank. A count past the end is the last line.
    [InlineData("20zt", 20, 7, VimScrollAlignment.Top)]
    [InlineData("20z<CR>", 20, 2, VimScrollAlignment.Top)]
    [InlineData("20zz", 20, 7, VimScrollAlignment.Center)]
    [InlineData("20z.", 20, 2, VimScrollAlignment.Center)]
    [InlineData("99zb", 30, 7, VimScrollAlignment.Bottom)]
    [InlineData("99z-", 30, 2, VimScrollAlignment.Bottom)]
    public void ACountOnZ_NamesTheLineAndMovesTheCaretToIt(string keys, int expectedCaret, int expectedColumn, VimScrollAlignment alignment)
    {
        VimRun run = Screen(keys, firstVisible: 1, caretLine: 15, caretColumn: 7);

        Assert.Equal([(expectedCaret, alignment)], run.Buffer.Scrolls);
        Assert.Equal(expectedCaret, run.CaretLine);
        Assert.Equal(expectedColumn, run.CaretColumn);
    }

    [Theory]
    // A line near the top cannot go to the bottom of the screen without showing something above the
    // first line, so the view stops at the first line; a line near the end can go to the middle or
    // the top with nothing under it, as vim's does.
    [InlineData("zb", 3, 1)]
    [InlineData("zz", 30, 26)]
    [InlineData("zt", 30, 30)]
    public void ZAtEitherEndOfTheBuffer_GoesAsFarAsTheViewCan(string keys, int caretLine, int expectedFirst)
    {
        VimRun run = Screen(keys, firstVisible: 11, caretLine);

        Assert.Equal(expectedFirst, run.FirstVisibleLine);
    }

    [Fact]
    public void Z_IsPendingUntilItsSecondKey_AndEscCancelsIt()
    {
        VimRun run = Screen("z", firstVisible: 1, caretLine: 15);

        Assert.True(run.Engine.HasPendingSequence);
        Assert.Empty(run.Buffer.Scrolls);

        VimRun cancelled = Screen("z<Esc>zz", firstVisible: 1, caretLine: 15);

        Assert.False(cancelled.Engine.HasPendingSequence);
        Assert.Equal([(15, VimScrollAlignment.Center)], cancelled.Buffer.Scrolls);
    }

    [Theory]
    [InlineData("zx", "zx")]
    [InlineData("dz", "dz")]
    [InlineData("d<C-d>", "d<C-d>")]
    [InlineData("d<PageDown>", "d<PageDown>")]
    [InlineData("r<C-f>", "r<C-f>")]
    public void ZAndAScrollKey_AreNothingBehindAnOperatorOrEachOther(string keys, string named)
    {
        VimRun run = Screen(keys, firstVisible: 11, caretLine: 15);

        Assert.Equal($"Not a command: {named}", run.Status);
        Assert.Empty(run.Buffer.Scrolls);
        Assert.Equal(Lines, run.Buffer.LineCount);
    }

    [Theory]
    [InlineData("v<C-d>", 3, 8)]
    [InlineData("v<C-f>", 3, 9)]
    [InlineData("v<PageDown>", 3, 9)]
    [InlineData("vj5<C-e>", 3, 6)]
    public void InAVisualMode_AScrollMovesTheCaretEndOfTheSelection(string keys, int caretLine, int expectedCaret)
    {
        VimRun run = Screen(keys, firstVisible: 1, caretLine);

        Assert.Equal(VimMode.Visual, run.Mode);
        Assert.Equal(expectedCaret, run.CaretLine);
        Assert.NotNull(run.Buffer.Selection);
        Assert.Equal(run.Buffer.GetLine(caretLine).Offset + 2, run.Buffer.Selection!.Value.Start);
    }

    [Fact]
    public void InAVisualMode_ZzKeepsTheSelectionAndScrolls()
    {
        VimRun run = Screen("vjzz", firstVisible: 1, caretLine: 15);

        Assert.Equal(VimMode.Visual, run.Mode);
        Assert.Equal([(16, VimScrollAlignment.Center)], run.Buffer.Scrolls);
        Assert.NotNull(run.Buffer.Selection);
    }

    [Theory]
    [InlineData("i<C-d>")]
    [InlineData("i<C-u>")]
    [InlineData("i<C-f>")]
    [InlineData("i<C-b>")]
    [InlineData("i<C-e>")]
    [InlineData("i<C-y>")]
    [InlineData("i<PageDown>")]
    [InlineData("i<PageUp>")]
    public void InInsertMode_TheScrollKeysAreTheEditors(string keys)
    {
        // The harness types nothing for a held key or a page key, as the editor's own handling is not
        // its to imitate; what is proved is that the engine turned the key down and stayed in insert
        // mode without scrolling.
        FakeVimBuffer buffer = VimScript.NewBuffer(Numbered(Lines, caretLine: 15));
        buffer.ViewportLines = Window;
        VimEngine engine = new(buffer);

        engine.HandleKey(VimKey.Typed('i'));
        VimKey key = VimKeyNotation.Parse(keys)[1];

        Assert.False(engine.HandleKey(key));
        Assert.Equal(VimMode.Insert, engine.Mode);
        Assert.Empty(buffer.Scrolls);
    }

    [Fact]
    public void AScroll_IsNotAChangeThatDotRepeats()
    {
        // 'x' takes the 'a'; Ctrl+D moves down a line; '.' repeats the 'x', not the scroll.
        VimRun run = VimScript.Run("|abc\ndef", "x<C-d>.");

        Assert.Equal("bc\n|ef", run.Text);
    }

    [Fact]
    public void AScroll_IsRecordedIntoAMacroAsItsKey()
    {
        VimRun run = Screen("qa<C-d>zzq", firstVisible: 1, caretLine: 3);

        Assert.Equal(VimRegister.Characters("<C-d>zz"), run.RegisterNamed('a'));
    }

    [Theory]
    // A command that put a line on an edge of the screen is not followed by "show the caret": the
    // editor's idea of showing the caret keeps a margin round it, which would pull 'zt' back off the
    // top and leave a held Ctrl+E scrolling a line down and a line back for ever.
    [InlineData("<C-f>")]
    [InlineData("<C-b>")]
    [InlineData("<PageDown>")]
    [InlineData("<C-e>")]
    [InlineData("<C-y>")]
    [InlineData("zt")]
    [InlineData("zz")]
    [InlineData("zb")]
    [InlineData("z<CR>")]
    public void ACommandThatPlacesTheView_DoesNotAskForTheCaretToBeShownAfterwards(string keys)
        => Assert.Equal(0, Screen(keys, firstVisible: 11, caretLine: 15).Buffer.ScrollRequests);

    [Theory]
    // The half-page scrolls move the caret through the middle of the screen rather than onto an
    // edge, so the usual request at the end of a command stays — a safety net for a wrapped line the
    // engine's count of lines did not see.
    [InlineData("<C-d>")]
    [InlineData("<C-u>")]
    [InlineData("H")]
    [InlineData("L")]
    public void AHalfPageScrollAndAScreenMotion_StillAskForTheCaretToBeShown(string keys)
        => Assert.Equal(1, Screen(keys, firstVisible: 11, caretLine: 15).Buffer.ScrollRequests);

    /// <summary>
    /// Runs keys over the thirty-line buffer seen through the ten-line window.
    /// </summary>
    private static VimRun Screen(string keys, int firstVisible, int caretLine, int caretColumn = 2)
        => VimScript.Run(Numbered(Lines, caretLine, caretColumn), keys, Window, firstVisible);

    /// <summary>
    /// A buffer of numbered lines — <c>  line 1</c>, <c>  line 2</c>, … — with the caret in one of them.
    /// </summary>
    private static string Numbered(int lines, int caretLine, int caretColumn = 2)
    {
        StringBuilder text = new();

        for (int number = 1; number <= lines; number++)
        {
            string line = $"  line {number}";

            if (number == caretLine)
            {
                line = line.Insert(caretColumn, VimScript.CaretMarker.ToString());
            }

            text.Append(line);

            if (number < lines)
            {
                text.Append('\n');
            }
        }

        return text.ToString();
    }
}
