using System.Collections.Generic;
using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// The two visual modes: what they select, how a motion changes it, what the three operators do to
/// it, and every way out. The commands that act on a selection without being one of the three are in
/// <see cref="VimVisualCommandTests"/>.
/// </summary>
/// <remarks>
/// Selections read back in the harness's own notation — <c>a[b|c]d</c> is "b and c are selected, the
/// caret is on c" — because which end of a selection the caret is at is half of what these cases are
/// about. A selection made leftwards covers exactly what the same one made rightwards does, and the
/// operators have to leave the caret at its start either way.
/// </remarks>
public sealed class VimVisualTests
{
    [Theory]
    // Inclusive of the character under the caret, which is what makes 'vd' on one character delete it.
    [InlineData("a|bc", "v", "a[|b]c")]
    [InlineData("a|bc", "vl", "a[b|c]")]
    [InlineData("|abc", "v$", "[ab|c]")]
    [InlineData("|foo bar", "vw", "[foo |b]ar")]
    [InlineData("|foo bar", "ve", "[fo|o] bar")]
    // Made leftwards: the caret is at the start of what is selected rather than at its end.
    [InlineData("ab|c", "v0", "[|abc]")]
    [InlineData("ab|c", "vh", "a[|bc]")]
    [InlineData("abc\nd|ef", "vk", "a[|bc\nde]f")]
    public void V_SelectsTheCharactersBetweenTheAnchorAndTheCaret(string before, string keys, string highlight)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(highlight, run.Highlight);
        Assert.Equal(VimMode.Visual, run.Mode);
    }

    [Theory]
    [InlineData("a|bc\ndef", "V", "[a|bc\n]def")]
    [InlineData("a|bc\ndef", "Vj", "[abc\nd|ef]")]
    [InlineData("abc\nd|ef", "Vk", "[a|bc\ndef]")]
    // Whatever column either end is in: 'V' then '$' is still the same two lines.
    [InlineData("a|bc\ndef", "Vj$", "[abc\nde|f]")]
    [InlineData("abc\n|\ndef", "V", "abc\n[|\n]def")]
    // The last line of the buffer has no line break to put in the highlight.
    [InlineData("abc\nd|ef", "V", "abc\n[d|ef]")]
    public void CapitalV_SelectsWholeLinesAndTheLineBreaksBetweenThem(
        string before,
        string keys,
        string highlight)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(highlight, run.Highlight);
        Assert.Equal(VimMode.VisualLine, run.Mode);
    }

    [Theory]
    [InlineData("|abc\ndef\nghi", "v2j", "[abc\ndef\n|g]hi")]
    [InlineData("|abcdef", "v3l", "[abc|d]ef")]
    [InlineData("|abc\ndef\nghi", "V2j", "[abc\ndef\n|ghi]")]
    // A count that overshoots clamps, exactly as it does on a motion in normal mode.
    [InlineData("|abc\ndef", "v9j", "[abc\n|d]ef")]
    // The count on 'v' itself is read and ignored: re-selecting the size of the last selection is
    // part of the machinery '.' repeat needs, and that is on the v1 out list.
    [InlineData("a|bc", "2v", "a[|b]c")]
    public void ACountOnAMotion_ExtendsTheSelectionThatFarInOneGo(
        string before,
        string keys,
        string highlight)
        => Assert.Equal(highlight, VimScript.Run(before, keys).Highlight);

    [Theory]
    [InlineData("a|bc\ndef", "vjl$0hkgg")]
    [InlineData("a|bc\ndef", "Vjkj")]
    public void AMotionInAVisualMode_MovesTheCaretAndNothingElse(string before, string keys)
        => Assert.Equal("abc\ndef", VimScript.Run(before, keys).Buffer.Text);

    [Fact]
    public void TheKeyThatStartsASelection_IsTheKeyThatStopsIt()
    {
        VimRun charwise = VimScript.Run("a|bc", "vlv");

        Assert.Equal(VimMode.Normal, charwise.Mode);
        Assert.Null(charwise.Buffer.Selection);
        Assert.Equal("ab|c", charwise.Highlight);

        VimRun linewise = VimScript.Run("a|bc\ndef", "VjV");

        Assert.Equal(VimMode.Normal, linewise.Mode);
        Assert.Null(linewise.Buffer.Selection);
    }

    [Theory]
    // Switching between the two keeps the anchor, so 'V' widens what is already selected to whole
    // lines rather than starting again from the caret.
    [InlineData("a|bc\ndef", "vjV", "[abc\nd|ef]")]
    [InlineData("a|bc\ndef", "Vjv", "a[bc\nd|e]f")]
    public void SwitchingBetweenTheVisualModes_KeepsWhatWasAlreadySelected(
        string before,
        string keys,
        string highlight)
        => Assert.Equal(highlight, VimScript.Run(before, keys).Highlight);

    [Theory]
    [InlineData("a|bc", "vl<Esc>")]
    [InlineData("a|bc", "Vl<Esc>")]
    public void Escape_LeavesTheVisualModeAndTakesTheHighlightWithIt(string before, string keys)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.Null(run.Buffer.Selection);
        Assert.Equal("abc", run.Buffer.Text);
    }

    [Theory]
    [InlineData("|abc", "vd", "|bc")]
    [InlineData("|abc", "vld", "|c")]
    [InlineData("a|bc", "vd", "a|c")]
    [InlineData("|abc", "v$d", "|")]
    // Made leftwards, and the caret lands at the start of what went.
    [InlineData("ab|c", "vhd", "|a")]
    [InlineData("ab|c", "v0d", "|")]
    // Across a line break, which is a character like any other to a charwise selection.
    [InlineData("a|bc\ndef", "vjd", "a|f")]
    // On an empty line the only thing to select is the line break, so 'vd' there joins the two lines.
    [InlineData("abc\n|\ndef", "vd", "abc\n|def")]
    // Except at the end of the buffer, where there is not even one of those.
    [InlineData("abc\n|", "vd", "abc\n|")]
    public void D_OverACharacterwiseSelection_TakesExactlyWhatWasHighlighted(
        string before,
        string keys,
        string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.Null(run.Buffer.Selection);
    }

    [Theory]
    [InlineData("a|bc\ndef", "Vd", "|def")]
    [InlineData("abc\nd|ef", "Vd", "|abc")]
    [InlineData("abc\nd|ef\nghi", "Vd", "abc\n|ghi")]
    [InlineData("a|bc\ndef", "Vjd", "|")]
    [InlineData("abc\n  d|ef", "Vkd", "|")]
    // The only line there is: what is left is the empty line a buffer always has.
    [InlineData("a|bc", "Vd", "|")]
    // The caret lands on the first non-blank of whatever moved up, as it does after 'dd'.
    [InlineData("a|bc\n  def", "Vd", "  |def")]
    public void D_OverALinewiseSelection_TakesWholeLinesAndTheirTerminator(
        string before,
        string keys,
        string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("|abc", "vly", "ab", VimRegisterKind.Characterwise)]
    [InlineData("ab|c", "v0y", "abc", VimRegisterKind.Characterwise)]
    [InlineData("a|bc\ndef", "vjy", "bc\nde", VimRegisterKind.Characterwise)]
    [InlineData("a|bc\ndef", "Vy", "abc", VimRegisterKind.Linewise)]
    [InlineData("a|bc\ndef", "Vjy", "abc\ndef", VimRegisterKind.Linewise)]
    [InlineData("abc\nd|ef", "Vky", "abc\ndef", VimRegisterKind.Linewise)]
    // An empty line yanked linewise is an empty line, which is not the same as nothing.
    [InlineData("abc\n|\ndef", "Vy", "", VimRegisterKind.Linewise)]
    [InlineData("|abc", "vld", "ab", VimRegisterKind.Characterwise)]
    [InlineData("a|bc\ndef", "Vd", "abc", VimRegisterKind.Linewise)]
    [InlineData("|abc", "vlc", "ab", VimRegisterKind.Characterwise)]
    [InlineData("a|bc\ndef", "Vc", "abc", VimRegisterKind.Linewise)]
    public void WhatAVisualOperatorTakes_GoesInTheRegisterWithTheSelectionsOwnKind(
        string before,
        string keys,
        string text,
        VimRegisterKind kind)
        => Assert.Equal(new VimRegister(text, kind), VimScript.Run(before, keys).Register);

    [Theory]
    [InlineData("|abc", "vly", "|abc")]
    [InlineData("ab|c", "vhy", "a|bc")]
    [InlineData("ab|c", "v0y", "|abc")]
    [InlineData("a|bc\ndef", "vjy", "a|bc\ndef")]
    [InlineData("a|bc\ndef", "Vy", "|abc\ndef")]
    [InlineData("a|bc\ndef", "Vjy", "|abc\ndef")]
    [InlineData("abc\nd|ef", "Vky", "|abc\ndef")]
    // Linewise, "the start of the selection" is the first non-blank of its first line — where every
    // other linewise command in this engine leaves the caret.
    [InlineData("  |foo", "Vy", "  |foo")]
    [InlineData("abc\n  d|ef", "Vky", "|abc\n  def")]
    public void Y_LeavesTheTextAloneAndTheCaretAtTheStartOfWhatWasSelected(
        string before,
        string keys,
        string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.Null(run.Buffer.Selection);
    }

    [Theory]
    [InlineData("|abc", "vcX<Esc>", "|Xbc")]
    [InlineData("|abc", "vlcX<Esc>", "|Xc")]
    [InlineData("ab|c", "vhcX<Esc>", "a|X")]
    [InlineData("a|bc\ndef", "vjcX<Esc>", "a|Xf")]
    // Linewise, the lines' text goes and one line stays to type on — 'cc' over a selection.
    [InlineData("a|bc\ndef", "VcX<Esc>", "|X\ndef")]
    [InlineData("a|bc\ndef\nghi", "VjcX<Esc>", "|X\nghi")]
    public void C_ReplacesWhatWasSelectedWithWhatIsTypedNext(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
    }

    [Fact]
    public void C_EntersInsertModeWithTheHighlightAlreadyGone()
    {
        VimRun run = VimScript.Run("a|bc", "vlc");

        Assert.Equal(VimMode.Insert, run.Mode);
        Assert.Null(run.Buffer.Selection);
        Assert.Equal("a|", run.Text);
    }

    [Fact]
    public void AChangeOverASelectionThatCoversNothing_StillEntersInsertMode()
    {
        // 'v' on the last line of a buffer when it is empty: there is neither a character nor a line
        // break to take, and a change there is an insertion, exactly as 'c0' at the start of a line is.
        VimRun run = VimScript.Run("abc\n|", "vcX<Esc>");

        Assert.Equal("abc\n|X", run.Text);
        Assert.Null(run.Register);
    }

    [Fact]
    public void AVisualDelete_IsOneUndoStep()
    {
        VimRun run = VimScript.Run("a|bc\ndef", "Vjdu");

        Assert.Equal("|abc\ndef", run.Text);
        Assert.Equal(0, run.Buffer.UndoSteps);
    }

    [Fact]
    public void AVisualChangeAndWhatWasTypedInItsPlace_ComeBackInExactlyOneStep()
    {
        VimRun run = VimScript.Run("a|bc", "vlcXY<Esc>u");

        // Back where the change started, which is where vim puts the caret after an undo as well.
        Assert.Equal("a|bc", run.Text);
        Assert.Equal(0, run.Buffer.UndoSteps);
    }

    [Fact]
    public void AVisualYank_DoesNotOpenAnUndoGroupBecauseItChangesNothing()
    {
        FakeVimBuffer buffer = VimScript.Run("a|bc\ndef", "Vjy").Buffer;

        Assert.Equal(0, buffer.UndoGroupsOpened);
        Assert.Equal(0, buffer.UndoSteps);
    }

    [Fact]
    public void ALinewiseSelection_HighlightsTheLineBreakButDoesNotYankIt()
    {
        Assert.Equal("abc\n", VimScript.Run("a|bc\ndef", "V").SelectedText);
        Assert.Equal(VimRegister.Lines("abc"), VimScript.Run("a|bc\ndef", "Vy").Register);
    }

    [Theory]
    // Every one of these has a visual meaning in vim, and every one of them is still out of scope:
    // ':' opens the command line over the selected lines, and 'I', 'A', 'O' and 'R' are the block-mode
    // spellings. 'x', 'p', 'r', 'u', 'o', '~' and the capitals are no longer among them — they are the
    // visual commands — and neither is the search family, which extends the selection.
    [InlineData(":")]
    [InlineData("I")]
    [InlineData("A")]
    [InlineData("O")]
    [InlineData("R")]
    public void AKeyWithNoVisualMeaning_IsNamedAndTheSelectionStays(string keys)
    {
        VimRun run = VimScript.Run("a|bc\ndef", "vl" + keys);

        Assert.Equal("abc\ndef", run.Buffer.Text);
        Assert.Equal(VimMode.Visual, run.Mode);
        Assert.Equal("a[b|c]\ndef", run.Highlight);
        Assert.Equal($"Not a command: {keys}", run.Buffer.Status);
    }

    [Fact]
    public void RInAVisualMode_TakesTheNextKeyAsTheCharacterToWrite()
    {
        // The same 'r' as normal mode's, over every selected character rather than the caret's: the
        // 'z' is what the selection becomes, not a key of its own.
        VimRun run = VimScript.Run("a|bc", "vlrz");

        Assert.Equal("a|zz", run.Text);
        Assert.Null(run.Buffer.Status);
        Assert.False(run.Engine.HasPendingSequence);
        Assert.Equal(VimMode.Normal, run.Mode);
    }

    [Fact]
    public void CtrlR_IsNamedRatherThanRedoingWhileThereIsASelection()
    {
        // 'u' over a selection is the lowercase operator, as it is in vim, so neither half of undo is
        // reachable while selecting — and the redo key says so rather than doing nothing.
        VimRun run = VimScript.Run("a|bc", "v<C-r>");

        Assert.Equal("Not a command: <C-r>", run.Buffer.Status);
        Assert.Equal(VimMode.Visual, run.Mode);
        Assert.NotNull(run.Buffer.Selection);
    }

    [Fact]
    public void AKeyThatTypesNothingAndMeansNothing_IsSwallowedWithoutLeavingTheVisualMode()
    {
        VimRun run = VimScript.Run("a|bc", "vl<Other>");

        Assert.Equal(VimMode.Visual, run.Mode);
        Assert.Equal("a[b|c]", run.Highlight);
        Assert.Null(run.Buffer.Status);
    }

    [Fact]
    public void AWindowShortcut_IsStillNotTheEnginesInAVisualMode()
    {
        FakeVimBuffer buffer = VimScript.NewBuffer("a|bc");
        VimEngine engine = new(buffer);
        engine.HandleKey(VimKey.Typed('v'));

        Assert.False(engine.HandleKey(VimKey.Typed('s', VimModifiers.Control)));
        Assert.Equal(VimMode.Visual, engine.Mode);
        Assert.NotNull(buffer.Selection);
    }

    [Fact]
    public void Reset_LeavesTheVisualModeAndTheHighlightBehind()
    {
        FakeVimBuffer buffer = VimScript.NewBuffer("a|bc\ndef");
        VimEngine engine = new(buffer);

        engine.HandleKey(VimKey.Typed('V'));
        engine.HandleKey(VimKey.Typed('j'));

        Assert.NotNull(buffer.Selection);

        engine.Reset();

        Assert.Equal(VimMode.Normal, engine.Mode);
        Assert.Null(buffer.Selection);
    }

    [Fact]
    public void EscapeInNormalMode_DropsASelectionTheEngineDidNotMake()
    {
        FakeVimBuffer buffer = VimScript.NewBuffer("a|bc");
        VimEngine engine = new(buffer);

        // What a drag with the mouse leaves behind. Esc is the key vim uses to get out of every other
        // kind of state, and a highlight nothing is acting on any more is one of them.
        buffer.SetSelection(0, 2);
        engine.HandleKey(VimKey.Named(VimSpecialKey.Escape));

        Assert.Null(buffer.Selection);
    }

    [Fact]
    public void EveryModeChange_IsReportedSoTheStatusBarCanFollowIt()
    {
        FakeVimBuffer buffer = VimScript.NewBuffer("a|bc");
        VimEngine engine = new(buffer);
        List<VimMode> reported = [];

        engine.ModeChanged += (_, mode) => reported.Add(mode);

        engine.HandleKey(VimKey.Typed('v'));
        engine.HandleKey(VimKey.Typed('V'));
        engine.HandleKey(VimKey.Typed('c'));
        engine.HandleKey(VimKey.Named(VimSpecialKey.Escape));

        Assert.Equal([VimMode.Visual, VimMode.VisualLine, VimMode.Normal, VimMode.Insert, VimMode.Normal], reported);
    }
}
