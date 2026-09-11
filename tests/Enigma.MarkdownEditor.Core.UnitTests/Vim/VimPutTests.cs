using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// Where <c>p</c> and <c>P</c> put what the register holds.
/// </summary>
/// <remarks>
/// One command with two behaviours, and the register decides which. Characters go back beside the
/// caret, inside the line; lines go back as lines, above or below the one the caret is on. Every case
/// here takes the text out first — with <c>x</c>, <c>dd</c> or <c>yy</c> — because the kind of the
/// register is set by the command that filled it, and testing the put without the take would be
/// testing half of it.
/// </remarks>
public sealed class VimPutTests
{
    [Theory]
    // The classic: 'xp' swaps the character under the caret with the one after it.
    [InlineData("a|bc", "xp", "ac|b")]
    [InlineData("a|bc", "xP", "a|bc")]
    [InlineData("|abc", "xp", "b|ac")]
    // At the end of a line there is nowhere past the caret to go, so 'p' lands at the line's end.
    [InlineData("ab|c", "xp", "ab|c")]
    [InlineData("a|bc", "x2p", "acb|b")]
    [InlineData("a|bc", "x3P", "abb|bc")]
    public void ACharacterwiseRegister_GoesBackInsideTheLine(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("a|bc\ndef", "yyp", "abc\n|abc\ndef")]
    [InlineData("a|bc\ndef", "yyP", "|abc\nabc\ndef")]
    // On the last line there is no terminator to put the text after, so the put brings one.
    [InlineData("abc\nd|ef", "yyp", "abc\ndef\n|def")]
    [InlineData("abc\nd|ef", "yyP", "abc\n|def\ndef")]
    [InlineData("a|bc\ndef", "ddP", "|abc\ndef")]
    [InlineData("abc\nd|ef\nghi", "ddP", "abc\n|def\nghi")]
    [InlineData("a|bc\ndef", "yy2p", "abc\n|abc\nabc\ndef")]
    [InlineData("a|bc\ndef\nghi", "2yyp", "abc\n|abc\ndef\ndef\nghi")]
    // The caret lands on the first non-blank of the first line put back, indent and all.
    [InlineData("  a|bc\ndef", "yyp", "  abc\n  |abc\ndef")]
    public void ALinewiseRegister_GoesBackAsWholeLines(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // The buffer is empty afterwards, so the put has to make the line it needs.
    [InlineData("a|bc", "ddp", "\n|abc")]
    [InlineData("a|bc", "ddP", "|abc\n")]
    [InlineData("a|bc\ndef", "dd2p", "def\n|abc\nabc")]
    public void PuttingLinesBackIntoAnAlmostEmptyBuffer_StillMakesWholeLines(
        string before,
        string keys,
        string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("p")]
    [InlineData("P")]
    [InlineData("3p")]
    public void PuttingWithNothingYanked_SaysSoAndChangesNothing(string keys)
    {
        VimRun run = VimScript.Run("a|bc", keys);

        Assert.Equal("a|bc", run.Text);
        Assert.Equal("Nothing in the register", run.Buffer.Status);
        Assert.Equal(0, run.Buffer.UndoSteps);
    }

    [Fact]
    public void APutOfAnEmptyLine_PutsAnEmptyLineRatherThanNothing()
    {
        // 'dd' on an empty line fills the register with a line that happens to have no text on it,
        // which is a different thing from a register nobody has filled.
        VimRun run = VimScript.Run("abc\n|\ndef", "ddp");

        Assert.Equal(VimRegister.Lines(string.Empty), run.Register);

        // The line came out of the middle and went back at the end, where the caret had landed.
        Assert.Equal("abc\ndef\n|", run.Text);
    }

    [Fact]
    public void APut_IsOneUndoStepHoweverManyCopiesItMakes()
    {
        FakeVimBuffer buffer = VimScript.Run("a|bc\ndef", "yy5p").Buffer;

        Assert.Equal(1, buffer.UndoSteps);
        Assert.Equal(0, buffer.OpenUndoGroups);
    }

    [Fact]
    public void TheRegister_OutlivesTheCommandsThatDidNotTouchIt()
    {
        // Only a delete, a change or a yank fills it. Everything else leaves it exactly as it was,
        // which is what lets a yank at the top of a file be put back at the bottom.
        VimRun run = VimScript.Run("a|bc\ndef", "yyGjjhl$0");

        Assert.Equal(VimRegister.Lines("abc"), run.Register);
    }
}
