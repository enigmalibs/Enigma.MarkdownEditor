using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// The three operators over the eleven motions: what comes out, what is left, and where the caret
/// ends up.
/// </summary>
/// <remarks>
/// This is where the grammar earns its keep — and where vim's two irregularities live. <c>cw</c> on a
/// word changes the word and not the space after it, and <c>dw</c> on the last word of a line stops
/// at the line's end rather than pulling the next line up. Both are only true of a motion used with
/// an operator, which is why neither can be seen from the motion tests.
/// </remarks>
public sealed class VimOperatorTests
{
    [Theory]
    // The whole line, and the terminator that goes with it.
    [InlineData("a|bc\ndef", "dd", "|def")]
    [InlineData("abc\nd|ef", "dd", "|abc")]
    [InlineData("abc\nd|ef\nghi", "dd", "abc\n|ghi")]
    // The only line there is: what is left is the empty line a buffer always has.
    [InlineData("a|bc", "dd", "|")]
    // An empty line in the middle goes without taking the line below with it.
    [InlineData("abc\n|\ndef", "dd", "abc\n|def")]
    // The caret lands on the first non-blank of whatever moved up.
    [InlineData("a|bc\n  def", "dd", "  |def")]
    public void Dd_TakesTheLineAndTheOneTerminatorThatGoesWithIt(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("a|bc\ndef", "yy", "a|bc\ndef")]
    [InlineData("abc\nd|ef", "2yy", "abc\nd|ef")]
    // A yank that reaches above the caret moves it to the first line it took; one that does not,
    // does not. That is what makes yanking a line a read-only act in every sense.
    [InlineData("abc\nd|ef", "yk", "|abc\ndef")]
    [InlineData("a|bc\ndef", "yj", "a|bc\ndef")]
    [InlineData("foo b|ar", "yb", "foo |bar")]
    [InlineData("|foo bar", "yw", "|foo bar")]
    public void Y_LeavesTheTextAloneAndOnlyMovesTheCaretBackwards(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("a|bc\ndef", "yy", "abc", VimRegisterKind.Linewise)]
    [InlineData("a|bc\ndef", "2yy", "abc\ndef", VimRegisterKind.Linewise)]
    [InlineData("a|bc\ndef", "Y", "abc", VimRegisterKind.Linewise)]
    [InlineData("a|bc\ndef", "dd", "abc", VimRegisterKind.Linewise)]
    [InlineData("a|bc\ndef", "dj", "abc\ndef", VimRegisterKind.Linewise)]
    [InlineData("abc\n|\ndef", "dd", "", VimRegisterKind.Linewise)]
    [InlineData("|foo bar", "dw", "foo ", VimRegisterKind.Characterwise)]
    [InlineData("|foo bar", "cw", "foo", VimRegisterKind.Characterwise)]
    [InlineData("ab|cdef", "D", "cdef", VimRegisterKind.Characterwise)]
    [InlineData("ab|cdef", "y$", "cdef", VimRegisterKind.Characterwise)]
    [InlineData("|abc", "x", "a", VimRegisterKind.Characterwise)]
    [InlineData("|abc", "3x", "abc", VimRegisterKind.Characterwise)]
    public void WhatAnOperatorTakes_GoesInTheRegisterWithItsKind(
        string before,
        string keys,
        string text,
        VimRegisterKind kind)
        => Assert.Equal(new VimRegister(text, kind), VimScript.Run(before, keys).Register);

    [Theory]
    // The blanks between two words on one line belong to the motion.
    [InlineData("|foo bar", "dw", "|bar")]
    [InlineData("|foo   bar", "dw", "|bar")]
    // The line break at the end of one does not: the last word of a line goes without the line.
    [InlineData("foo |bar\nbaz", "dw", "foo| \nbaz")]
    [InlineData("|foo bar\nbaz qux", "2dw", "|\nbaz qux")]
    [InlineData("|foo\n\nbar", "dw", "|\n\nbar")]
    // A caret on the blanks themselves changes nothing about the rule.
    [InlineData("|  foo", "dw", "|foo")]
    public void Dw_StopsAtTheEndOfALineRatherThanPullingTheNextOneUp(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("ab|cd", "d0", "|cd")]
    // Nothing to the left of the caret: a command with nothing to do.
    [InlineData("|abcd", "d0", "|abcd")]
    [InlineData("ab|cdef", "d$", "a|b")]
    [InlineData("|abc", "d$", "|")]
    [InlineData("ab|c", "dh", "a|c")]
    [InlineData("|abc", "dh", "|abc")]
    // 'l' as a motion stops on the last character of the line; 'dl' on that character deletes it.
    [InlineData("a|bc", "dl", "a|c")]
    [InlineData("ab|c", "dl", "a|b")]
    [InlineData("foo b|ar", "db", "foo |ar")]
    [InlineData("|foo bar", "de", "| bar")]
    // 'e' with nothing left ahead of it still covers the character under the caret.
    [InlineData("fo|o", "de", "f|o")]
    public void AnOperatorOverACharacterwiseMotion_TakesExactlyWhatItReaches(
        string before,
        string keys,
        string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("a|bc\ndef\nghi", "dj", "|ghi")]
    [InlineData("abc\nd|ef\nghi", "dk", "|ghi")]
    [InlineData("a|bc\ndef\nghi", "dG", "|")]
    [InlineData("abc\ndef\ng|hi", "dgg", "|")]
    [InlineData("a|bc\ndef\nghi", "d2G", "|ghi")]
    [InlineData("abc\ndef\ng|hi", "d2gg", "|abc")]
    // A linewise motion that runs out of document clamps, like every other count does.
    [InlineData("abc\nd|ef", "dj", "|abc")]
    [InlineData("a|bc\ndef", "dk", "|def")]
    public void AnOperatorOverALinewiseMotion_TakesWholeLines(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // The quirk: on a word, 'cw' is 'ce' — the word changes and the space after it stays.
    [InlineData("|foo bar", "cw", "| bar")]
    [InlineData("|foo bar", "ce", "| bar")]
    [InlineData("|foo   bar", "cw", "|   bar")]
    // On a blank there is no word to change, so 'cw' changes the blanks, exactly as 'dw' would.
    [InlineData("|  foo", "cw", "|foo")]
    public void Cw_ChangesTheWordAndNotTheSpaceAfterIt(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Insert, run.Mode);
    }

    [Theory]
    // The line's text goes and the line stays, which is the difference between 'cc' and 'dd'.
    [InlineData("abc\nd|ef\nghi", "ccX<Esc>", "abc\n|X\nghi")]
    [InlineData("a|bc", "ccX<Esc>", "|X")]
    [InlineData("a|bc\ndef\nghi", "2ccX<Esc>", "|X\nghi")]
    // 'S' is not in the contract, but 'C' is, and it is 'c$'.
    [InlineData("ab|cdef", "CX<Esc>", "ab|X")]
    [InlineData("|abc", "CX<Esc>", "|X")]
    public void TheChangeFamily_ReplacesWhatItTookWithWhatIsTypedNext(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
    }

    [Theory]
    [InlineData("ab|cdef", "D", "a|b")]
    [InlineData("a|bc\ndef\nghi", "2D", "|a\nghi")]
    [InlineData("a|bc\ndef", "Y", "a|bc\ndef")]
    public void TheCapitalsSpellTheOperatorTheyStandFor(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // An operator followed by something that is not a motion is a sequence, not two commands.
    // 'di' opens a text object rather than dying, so what dies is the key that names none.
    [InlineData("a|bc", "diz", "Not a command: diz")]
    [InlineData("a|bc", "dy", "Not a command: dy")]
    [InlineData("a|bc", "dz", "Not a command: dz")]
    [InlineData("a|bc", "3dz", "Not a command: 3dz")]
    public void AnOperatorWithoutAMotion_IsNamedInFullAndDoesNothing(string before, string keys, string message)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(before, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.Equal(message, run.Buffer.Status);
        Assert.Null(run.Register);
    }

    [Fact]
    public void AnOperatorStillWaitingForItsMotion_HasChangedNothingYet()
    {
        VimRun run = VimScript.Run("a|bc\ndef", "3d");

        Assert.Equal("a|bc\ndef", run.Text);
        Assert.True(run.Engine.HasPendingSequence);
    }

    [Fact]
    public void EscapeAfterAnOperator_ThrowsItAwayRatherThanLettingTheNextKeyFinishIt()
    {
        // Without the cancel this would be 'dd' and the line would be gone.
        VimRun run = VimScript.Run("a|bc\ndef", "d<Esc>d");

        Assert.Equal("a|bc\ndef", run.Text);
        Assert.True(run.Engine.HasPendingSequence);
    }

    [Fact]
    public void NothingHasBeenYanked_UntilAnOperatorSaysSo()
        => Assert.Null(VimScript.Run("a|bc", "wbj$").Register);

    [Fact]
    public void AYank_DoesNotOpenAnUndoGroupBecauseItChangesNothing()
    {
        FakeVimBuffer buffer = VimScript.Run("a|bc\ndef", "yyyj").Buffer;

        Assert.Equal(0, buffer.UndoGroupsOpened);
        Assert.Equal(0, buffer.UndoSteps);
    }
}
