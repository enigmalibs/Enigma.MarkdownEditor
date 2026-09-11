using System;
using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// The commands that act on a selection without being <c>d</c>, <c>y</c> or <c>c</c>: the spellings
/// of those three (<c>x</c> <c>X</c> <c>D</c> <c>Y</c> <c>s</c> <c>C</c> <c>S</c>), the put over a
/// selection, <c>r</c> over every character of it, the case trio, the shifts, the joins, and the two
/// that move the selection itself — <c>o</c> and <c>gv</c>.
/// </summary>
/// <remarks>
/// Every case is run in both visual modes where both make sense, forwards and backwards, on one line
/// and across lines, and against the last line of the buffer, because that is where the terminator
/// rules differ. Every command but <c>o</c> and <c>gv</c> leaves visual mode; where it leaves the
/// caret is part of each case, because it differs by command — the start of what was selected for
/// most, the seam for a join, the put's own landing for a put.
/// </remarks>
public sealed class VimVisualCommandTests
{
    [Theory]
    [InlineData("|abc", "vx", "|bc")]
    [InlineData("|abc", "vlx", "|c")]
    [InlineData("ab|c", "vhx", "|a")]
    [InlineData("a|bc\ndef", "vjx", "a|f")]
    // Linewise, 'x' is still 'd', which over whole lines is 'dd'.
    [InlineData("a|bc\ndef", "Vx", "|def")]
    [InlineData("abc\nd|ef", "Vkx", "|")]
    public void X_DeletesTheSelectionExactlyAsDDoes(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.Null(run.Buffer.Selection);
    }

    [Theory]
    // Whole lines whatever the mode: a characterwise selection inside one line takes the line.
    [InlineData("a|bc\ndef", "vX", "|def")]
    [InlineData("a|bc\ndef", "vD", "|def")]
    [InlineData("a|bc\ndef\nghi", "vjD", "|ghi")]
    [InlineData("a|bc\ndef", "VD", "|def")]
    [InlineData("abc\nd|ef", "vkD", "|")]
    // The last line goes with the terminator before it, as 'dd' takes it.
    [InlineData("abc\nd|ef", "vX", "|abc")]
    // The caret lands on the first non-blank of the line that moved up.
    [InlineData("abc\n  d|ef\n  ghi", "vD", "abc\n  |ghi")]
    // The only line there is: what is left is the empty line a buffer always has.
    [InlineData("a|bc", "vD", "|")]
    public void CapitalXAndD_DeleteTheSelectedLinesWhateverTheMode(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("a|bc\ndef", "vD", "abc")]
    [InlineData("a|bc\ndef", "vjX", "abc\ndef")]
    [InlineData("|abc", "vlx", "ab")]
    public void WhatALinewiseSpellingTakes_GoesInTheRegisterAsLines(string before, string keys, string text)
    {
        VimRegister expected = keys.Contains('x', StringComparison.Ordinal)
            ? VimRegister.Characters(text)
            : VimRegister.Lines(text);

        Assert.Equal(expected, VimScript.Run(before, keys).Register);
    }

    [Theory]
    [InlineData("a|bc\ndef", "vY", "|abc\ndef", "abc")]
    [InlineData("a|bc\ndef", "vjY", "|abc\ndef", "abc\ndef")]
    [InlineData("abc\nd|ef", "vkY", "|abc\ndef", "abc\ndef")]
    [InlineData("a|bc\ndef", "VY", "|abc\ndef", "abc")]
    // Linewise, the start of the selection is the first non-blank of its first line.
    [InlineData("  a|bc\ndef", "vY", "  |abc\ndef", "  abc")]
    public void CapitalY_YanksTheSelectedLinesAndLeavesTheTextAlone(
        string before,
        string keys,
        string after,
        string yanked)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimRegister.Lines(yanked), run.Register);
        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.Equal(0, run.Buffer.UndoSteps);
    }

    [Theory]
    // 's' is 'c' over the selection.
    [InlineData("|abc", "vsX<Esc>", "|Xbc")]
    [InlineData("|abc", "vlsX<Esc>", "|Xc")]
    [InlineData("ab|c", "vhsX<Esc>", "a|X")]
    [InlineData("a|bc\ndef", "vjsX<Esc>", "a|Xf")]
    // 'C' and 'S' are 'cc' over the selected lines, whatever the mode: the text goes and one line stays.
    [InlineData("a|bc\ndef", "vCX<Esc>", "|X\ndef")]
    [InlineData("a|bc\ndef", "vSX<Esc>", "|X\ndef")]
    [InlineData("a|bc\ndef\nghi", "vjCX<Esc>", "|X\nghi")]
    [InlineData("a|bc\ndef", "VSX<Esc>", "|X\ndef")]
    [InlineData("abc\nd|ef", "vkCX<Esc>", "|X")]
    public void SAndTheCapitals_ChangeTheSelectionOrItsLines(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
    }

    [Theory]
    [InlineData("|abc", "vls", "ab", VimRegisterKind.Characterwise)]
    [InlineData("a|bc\ndef", "vC", "abc", VimRegisterKind.Linewise)]
    [InlineData("a|bc\ndef", "vjS", "abc\ndef", VimRegisterKind.Linewise)]
    public void WhatAChangeSpellingTakes_GoesInTheRegisterWithItsKind(
        string before,
        string keys,
        string text,
        VimRegisterKind kind)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(new VimRegister(text, kind), run.Register);
        Assert.Equal(VimMode.Insert, run.Mode);
        Assert.Null(run.Buffer.Selection);
    }

    [Fact]
    public void AVisualChangeSpellingAndWhatWasTyped_ComeBackInOneStep()
    {
        VimRun run = VimScript.Run("a|bc", "vlsXY<Esc>u");

        Assert.Equal("a|bc", run.Text);
        Assert.Equal(0, run.Buffer.UndoSteps);
    }

    [Theory]
    // Characters over characters go inline where the selection was, caret on the last one put.
    [InlineData("|foo bar", "yiwwviwp", "foo fo|o")]
    [InlineData("|foo bar", "yiwwviwP", "foo fo|o")]
    // Made backwards, the same.
    [InlineData("|foo bar", "yiw$vhhp", "foo fo|o")]
    // At the end of a line: where the selection began, not where the delete left the caret.
    [InlineData("|abc", "yl$vp", "ab|a")]
    [InlineData("|abc", "yl$v2p", "aba|a")]
    // Across a line break, the break goes with the rest of the selection.
    [InlineData("|ab\ncd", "ylvjp", "|ad")]
    // The one selection that covers nothing takes nothing out, and the put still goes in.
    [InlineData("abc\n|", "ggylGvp", "abc\n|a")]
    public void P_OverACharacterwiseSelection_PutsCharactersInline(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.Null(run.Buffer.Selection);
    }

    [Theory]
    // Lines into the middle of a line split it, and go in between the halves on lines of their own.
    [InlineData("abc def\n|X", "yykllvlp", "ab\n|X\ndef\nX")]
    [InlineData("abc def\n|X", "yykllvl2p", "ab\n|X\nX\ndef\nX")]
    // At the end of the line the second half is empty, and stays as an empty line — vim's answer.
    [InlineData("abc\n|X", "yykllvp", "ab\n|X\n\nX")]
    // At the start, the first half is.
    [InlineData("|abc def\nX", "jyykviwp", "\n|X\n def\nX")]
    public void P_OverACharacterwiseSelection_PutsLinesOnLinesOfTheirOwn(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // Lines over lines go where the lines were.
    [InlineData("|foo\nbar\nbaz", "yyjVp", "foo\n|foo\nbaz")]
    [InlineData("|foo\nbar\nbaz", "yyjVjp", "foo\n|foo")]
    [InlineData("|foo\nbar\nbaz", "yyjVP", "foo\n|foo\nbaz")]
    // Characters over lines become a line: the word replaces the line rather than being spliced into it.
    [InlineData("|foo\nbar\nbaz", "yiwjVp", "foo\n|foo\nbaz")]
    [InlineData("|foo\nbar\nbaz", "yiwjV2p", "foo\n|foo\nfoo\nbaz")]
    // On the last line, which has no terminator to put the lines before.
    [InlineData("|foo\nbar", "yiwjVp", "foo\n|foo")]
    // A characterwise register with a line break in it becomes as many lines.
    [InlineData("a|b\ncd\nX", "vjhyGVp", "ab\ncd\n|b\nc")]
    // Over the whole buffer nothing is left to put beside, and no empty line is left over either.
    [InlineData("|foo", "yiwVp", "|foo")]
    [InlineData("|foo\nbar", "yyVjp", "|foo")]
    // The caret lands on the first non-blank of the first line put, indent and all.
    [InlineData("  |foo\nbar", "yyjVp", "  foo\n  |foo")]
    public void P_OverALinewiseSelection_PutsWholeLinesWhereTheLinesWere(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
    }

    [Theory]
    // 'p' leaves what it replaced in the register, with the selection's own kind ...
    [InlineData("|foo bar", "yiwwviwp", "bar", VimRegisterKind.Characterwise)]
    [InlineData("|foo\nbar\nbaz", "yiwjVp", "bar", VimRegisterKind.Linewise)]
    [InlineData("|foo\nbar\nbaz", "yyjVp", "bar", VimRegisterKind.Linewise)]
    [InlineData("|ab\ncd", "ylvjp", "ab\nc", VimRegisterKind.Characterwise)]
    // ... and 'P' leaves the register exactly as it was.
    [InlineData("|foo bar", "yiwwviwP", "foo", VimRegisterKind.Characterwise)]
    [InlineData("|foo\nbar\nbaz", "yyjVP", "foo", VimRegisterKind.Linewise)]
    [InlineData("|foo\nbar\nbaz", "yiwjVP", "foo", VimRegisterKind.Characterwise)]
    public void P_LeavesTheReplacedTextInTheRegister_AndCapitalPLeavesTheRegisterAlone(
        string before,
        string keys,
        string text,
        VimRegisterKind kind)
        => Assert.Equal(new VimRegister(text, kind), VimScript.Run(before, keys).Register);

    [Fact]
    public void P_Twice_SwapsTwoWords()
    {
        // The reason 'p' fills the register with what it replaced: the first put takes 'bar' out, and
        // the second puts it back where 'foo' was.
        Assert.Equal("ba|r foo", VimScript.Run("|foo bar", "yiwwviwp0viwp").Text);
    }

    [Theory]
    [InlineData("p")]
    [InlineData("P")]
    [InlineData("2p")]
    public void P_WithNothingInTheRegister_SaysSoAndKeepsTheSelection(string keys)
    {
        VimRun run = VimScript.Run("a|bc", "vl" + keys);

        Assert.Equal("Nothing in the register", run.Status);
        Assert.Equal(VimMode.Visual, run.Mode);
        Assert.Equal("a[b|c]", run.Highlight);
        Assert.Equal(0, run.Buffer.UndoSteps);
    }

    [Theory]
    [InlineData("|foo bar", "yiwwviwp")]
    [InlineData("|foo\nbar\nbaz", "yyjVp")]
    [InlineData("abc def\n|X", "yykllvlp")]
    public void AVisualPut_IsOneUndoStepForTheDeleteAndThePutTogether(string before, string keys)
    {
        FakeVimBuffer buffer = VimScript.Run(before, keys).Buffer;

        Assert.Equal(1, buffer.UndoSteps);
        Assert.Equal(0, buffer.OpenUndoGroups);

        FakeVimBuffer undone = VimScript.Run(before, keys + "u").Buffer;

        Assert.Equal(before.Replace("|", string.Empty, StringComparison.Ordinal), undone.Text);
        Assert.Equal(0, undone.UndoSteps);
    }

    [Theory]
    [InlineData("|abc", "vlrx", "|xxc")]
    [InlineData("a|bc", "vrx", "a|xc")]
    [InlineData("ab|c", "vhrx", "a|xx")]
    // The line break is not a character to overwrite; everything either side of it is.
    [InlineData("a|bc\ndef", "vjrx", "a|xx\nxxf")]
    [InlineData("a|bc\n  def", "Vjrx", "|xxx\nxxxxx")]
    // An empty line has nothing to overwrite and stays empty.
    [InlineData("a|bc\n\ndef", "Vjjrx", "|xxx\n\nxxx")]
    // A blank is a character like any other, and the caret still goes to the start.
    [InlineData("|abc", "Vr ", "|   ")]
    // A count means nothing over a selection: the selection says how many.
    [InlineData("|abc", "v3rx", "|xbc")]
    // Nothing selected, nothing written.
    [InlineData("abc\n|", "vrx", "abc\n|")]
    public void R_OverwritesEverySelectedCharacter(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.Null(run.Buffer.Selection);
    }

    [Fact]
    public void R_OverASelection_IsOneUndoStepAndLeavesTheRegisterAlone()
    {
        VimRun run = VimScript.Run("|abc", "ylvlrx");

        Assert.Equal(VimRegister.Characters("a"), run.Register);
        Assert.Equal(1, run.Buffer.UndoSteps);
        Assert.Equal("abc", VimScript.Run("|abc", "vlrxu").Buffer.Text);
    }

    [Fact]
    public void R_OverASelection_WaitsForItsCharacterAndEscCancelsIt()
    {
        VimRun armed = VimScript.Run("a|bc", "vlr");

        Assert.True(armed.Engine.HasPendingSequence);
        Assert.Equal(VimMode.Visual, armed.Mode);
        Assert.Equal("abc", armed.Buffer.Text);

        VimRun cancelled = VimScript.Run("a|bc", "vlr<Esc>");

        Assert.False(cancelled.Engine.HasPendingSequence);
        Assert.Equal(VimMode.Normal, cancelled.Mode);
        Assert.Equal("abc", cancelled.Buffer.Text);
    }

    [Theory]
    [InlineData("|Foo Bar", "ve~", "|fOO Bar")]
    [InlineData("|Foo Bar", "veu", "|foo Bar")]
    [InlineData("|Foo Bar", "veU", "|FOO Bar")]
    [InlineData("Foo Ba|r", "vbU", "Foo |BAR")]
    [InlineData("F|oo\nBar", "vj~", "F|OO\nbAr")]
    // Linewise, the caret goes to the first column of the first line — where 'gu' over a selection
    // and vim's own case operators leave it — rather than to the first non-blank.
    [InlineData("  |Foo\nBar", "VjU", "|  FOO\nBAR")]
    [InlineData("abc\nD|EF", "Vku", "|abc\ndef")]
    public void TildeUAndCapitalU_ChangeTheCaseOfTheSelection(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.Null(run.Buffer.Selection);
    }

    [Fact]
    public void ACaseChangeOverASelection_IsOneUndoStepAndLeavesTheRegisterAlone()
    {
        VimRun run = VimScript.Run("|Foo", "ylveU");

        Assert.Equal("|FOO", run.Text);
        Assert.Equal(VimRegister.Characters("F"), run.Register);
        Assert.Equal(1, run.Buffer.UndoSteps);
        Assert.Equal("Foo", VimScript.Run("|Foo", "veUu").Buffer.Text);
    }

    [Fact]
    public void U_OverASelection_IsNotUndo()
    {
        // Vim's own rule, and the one deliberate loss: while there is a selection neither undo nor
        // redo is reachable. The 'x' stays done, and the 'u' lowercases the character it selected.
        VimRun run = VimScript.Run("|FOO", "xvu");

        Assert.Equal("|oO", run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
    }

    [Theory]
    [InlineData("|a\nb", "vj>", "    |a\n    b")]
    [InlineData("a\n|b", "vk>", "    |a\n    b")]
    [InlineData("|a\nb", "Vj2>", "        |a\n        b")]
    [InlineData("    |a\n    b", "vj<lt>", "|a\nb")]
    [InlineData("a\n|b", "V>", "a\n    |b")]
    public void TheShifts_MoveTheSelectedLinesCountWidths(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
    }

    [Theory]
    // A selection inside one line still joins two: vim's minimum.
    [InlineData("a|bc\ndef", "vJ", "abc| def")]
    [InlineData("a|bc\ndef\nghi", "vjJ", "abc| def\nghi")]
    [InlineData("a|bc\ndef\nghi", "VjjJ", "abc def| ghi")]
    [InlineData("abc\nd|ef", "vkJ", "abc| def")]
    // 'gJ' puts the lines together exactly as they are.
    [InlineData("a|bc\n  def", "VjgJ", "abc|  def")]
    [InlineData("a|bc\ndef", "vgJ", "abc|def")]
    public void JAndGJ_JoinTheSelectedLinesAndLandOnTheSeam(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.Null(run.Buffer.Selection);
    }

    [Theory]
    // On the last line there is nothing to join: the selection goes, the text stays, without a word.
    [InlineData("abc\nd|ef", "vJ", "abc\nd|ef")]
    [InlineData("abc\n  d|ef", "VJ", "abc\n  |def")]
    public void AJoinOnTheLastLine_LeavesVisualModeAndNothingElse(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.Null(run.Status);
        Assert.Equal(0, run.Buffer.UndoSteps);
    }

    [Fact]
    public void AJoinOverASelection_IsOneUndoStepAndLeavesTheRegisterAlone()
    {
        VimRun run = VimScript.Run("|abc\ndef\nghi", "ylVjjJ");

        Assert.Equal(VimRegister.Characters("a"), run.Register);
        Assert.Equal(1, run.Buffer.UndoSteps);
        Assert.Equal("abc\ndef\nghi", VimScript.Run("|abc\ndef\nghi", "VjjJu").Buffer.Text);
    }

    [Theory]
    [InlineData("a|bc", "vlo", "a[|bc]")]
    [InlineData("a|bc", "vloo", "a[b|c]")]
    // After the swap the motions move what used to be the anchor.
    [InlineData("a|bc", "vlol", "ab[|c]")]
    [InlineData("a|bc", "vloh", "[|abc]")]
    [InlineData("a|bc\ndef", "Vjo", "[a|bc\ndef]")]
    [InlineData("a|bc\ndef", "Vjoj", "abc\n[d|ef]")]
    public void O_SwapsTheAnchorAndTheCaretAndStaysInVisualMode(string before, string keys, string highlight)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(highlight, run.Highlight);
        Assert.Equal(keys.Contains('V', StringComparison.Ordinal) ? VimMode.VisualLine : VimMode.Visual, run.Mode);
        Assert.Equal(before.Replace("|", string.Empty, StringComparison.Ordinal), run.Buffer.Text);
    }

    [Theory]
    // After Esc, after 'v' again, after an operator: the same ends, the same mode.
    [InlineData("a|bc", "vl<Esc>gv", "a[b|c]")]
    [InlineData("a|bc", "vlvgv", "a[b|c]")]
    [InlineData("a|bc\ndef", "vjygv", "a[bc\nd|e]f")]
    // Made backwards, it comes back backwards.
    [InlineData("ab|c", "vhygv", "a[|bc]")]
    public void GV_InNormalMode_ReselectsWhatWasLastSelected(string before, string keys, string highlight)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(highlight, run.Highlight);
        Assert.Equal(VimMode.Visual, run.Mode);
    }

    [Theory]
    [InlineData("a|bc\ndef", "Vjygv", "[abc\nd|ef]")]
    // The lines moved under the selection, and it still covers them: the ends are remembered as lines
    // and columns, not as offsets.
    [InlineData("|ab\ncd", "Vj>gv", "[    ab\n|    cd]")]
    public void GV_AfterALinewiseSelection_ReselectsTheSameLines(string before, string keys, string highlight)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(highlight, run.Highlight);
        Assert.Equal(VimMode.VisualLine, run.Mode);
    }

    [Fact]
    public void GV_WhereTheSelectionNoLongerExists_SelectsWhatIsStillThere()
    {
        // The two ends were on the b and the e; both lines have gone into one, and both ends are pulled
        // onto the nearest character that is still there.
        Assert.Equal("a[|f]", VimScript.Run("a|bc\ndef", "vjdgv").Highlight);
    }

    [Fact]
    public void GV_InAVisualMode_SwapsTheSelectionWithThePreviousOne()
    {
        VimRun swapped = VimScript.Run("|abc def", "viw<Esc>wviwgv");

        Assert.Equal("[ab|c] def", swapped.Highlight);
        Assert.Equal(VimMode.Visual, swapped.Mode);

        // And back again: the one swapped out is now the previous one.
        Assert.Equal("abc [de|f]", VimScript.Run("|abc def", "viw<Esc>wviwgvgv").Highlight);
    }

    [Fact]
    public void GV_WithNothingToGoBackTo_DoesNothing()
    {
        VimRun normal = VimScript.Run("a|bc", "gv");

        Assert.Equal(VimMode.Normal, normal.Mode);
        Assert.Null(normal.Buffer.Selection);
        Assert.Null(normal.Status);

        VimRun visual = VimScript.Run("a|bc", "vlgv");

        Assert.Equal("a[b|c]", visual.Highlight);
        Assert.Equal(VimMode.Visual, visual.Mode);
    }

    [Fact]
    public void GV_ForgetsTheSelectionWhenTheEngineIsReset()
    {
        // A selection is a place in a document, and after a reset the document may be another one.
        FakeVimBuffer buffer = VimScript.NewBuffer("a|bc");
        VimEngine engine = new(buffer);

        engine.HandleKey(VimKey.Typed('v'));
        engine.HandleKey(VimKey.Typed('l'));
        engine.HandleKey(VimKey.Named(VimSpecialKey.Escape));
        engine.Reset();

        engine.HandleKey(VimKey.Typed('g'));
        engine.HandleKey(VimKey.Typed('v'));

        Assert.Equal(VimMode.Normal, engine.Mode);
        Assert.Null(buffer.Selection);
    }

    [Fact]
    public void GV_BehindAnOperator_IsNotACommand()
    {
        VimRun run = VimScript.Run("a|bc", "vl<Esc>dgv");

        Assert.Equal("abc", run.Buffer.Text);
        Assert.Equal("Not a command: dgv", run.Status);
        Assert.Equal(VimMode.Normal, run.Mode);
    }
}
