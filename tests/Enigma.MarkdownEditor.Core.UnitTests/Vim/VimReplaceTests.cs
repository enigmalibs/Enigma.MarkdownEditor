using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// The two commands that change a character in place: <c>r</c> and <c>~</c>.
/// </summary>
/// <remarks>
/// Neither touches the register, which is what makes them the small change: nothing was taken out, so
/// there is nothing to put back. <c>r</c> is also the only command in the contract that reads the
/// <em>next</em> key literally — <c>rd</c> writes a <c>d</c> rather than starting a delete — and the
/// only one that refuses rather than doing as much of its count as it can.
/// </remarks>
public sealed class VimReplaceTests
{
    [Theory]
    [InlineData("a|bc", "rX", "a|Xc")]
    [InlineData("|abc", "rX", "|Xbc")]
    // At the end of the line, where the caret is on the last character rather than past it.
    [InlineData("ab|c", "rX", "ab|X")]
    [InlineData("|abc", "2rX", "X|Xc")]
    [InlineData("|abc", "3rX", "XX|X")]
    // The key after 'r' is a character to write, whatever else it would have spelled.
    [InlineData("a|bc", "rd", "a|dc")]
    [InlineData("a|bc", "r3", "a|3c")]
    [InlineData("a|bc", "r~", "a|~c")]
    public void R_WritesTheCharacterThatFollowsIt(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // Vim refuses rather than doing as much as it can, so a mistyped count cannot half-destroy a word.
    [InlineData("ab|c", "2rX", "ab|c")]
    [InlineData("|abc", "4rX", "|abc")]
    [InlineData("|", "rX", "|")]
    [InlineData("abc\n|\ndef", "rX", "abc\n|\ndef")]
    public void RWithNowhereToWrite_LeavesTheLineAlone(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("|abc", "~", "A|bc")]
    [InlineData("|ABC", "~", "a|BC")]
    [InlineData("|abc", "3~", "AB|C")]
    // Past what it swapped, then clamped by normal mode's rule: '~' held down stops at the line's end.
    [InlineData("ab|c", "~", "ab|C")]
    [InlineData("|abc", "9~", "AB|C")]
    // What has no case keeps none, and the caret still steps over it.
    [InlineData("|a1B", "3~", "A1|b")]
    [InlineData("|", "~", "|")]
    [InlineData("abc\n|\ndef", "~", "abc\n|\ndef")]
    public void Tilde_SwapsTheCaseUnderTheCaretAndStepsPastIt(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("a|bc", "rX")]
    [InlineData("|abc", "3~")]
    public void NeitherOfThem_TouchesTheRegister(string before, string keys)
        => Assert.Null(VimScript.Run(before, keys).Register);

    [Fact]
    public void EscapeAfterR_CancelsItRatherThanWritingAnything()
    {
        VimRun run = VimScript.Run("a|bc", "r<Esc>");

        Assert.Equal("a|bc", run.Text);
        Assert.False(run.Engine.HasPendingSequence);
    }

    [Fact]
    public void AKeyWithNoCharacterAfterR_CancelsItToo()
    {
        // An 'r' left armed after a stray Page Down would write the next letter typed into the
        // document instead of running the command it spells.
        VimRun run = VimScript.Run("a|bc", "r<Other>l");

        Assert.Equal("ab|c", run.Text);
        Assert.False(run.Engine.HasPendingSequence);
    }

    [Fact]
    public void RStillWaitingForItsCharacter_HasChangedNothingYet()
    {
        VimRun run = VimScript.Run("a|bc", "r");

        Assert.Equal("a|bc", run.Text);
        Assert.True(run.Engine.HasPendingSequence);
    }

    [Theory]
    [InlineData("|abc", "3rX")]
    [InlineData("|abc", "3~")]
    public void EachOfThem_IsOneUndoStepHoweverManyCharactersItTouched(string before, string keys)
        => Assert.Equal(1, VimScript.Run(before, keys).Buffer.UndoSteps);
}
