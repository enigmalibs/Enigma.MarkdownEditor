using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// <c>J</c> and <c>gJ</c>: what goes between the joined lines, where the caret lands, and what a
/// count and the last line do.
/// </summary>
/// <remarks>
/// Most of the cases are about the space, because that is where vim has rules rather than a habit:
/// one space at the seam, none when the next line starts with <c>)</c>, none when the line already
/// ends in a blank, none when either side is empty, and none at all for <c>gJ</c>. The caret lands on
/// the seam — the space it put there, or the first character of what it appended.
/// </remarks>
public sealed class VimJoinTests
{
    [Theory]
    [InlineData("a|bc\ndef", "J", "abc| def")]
    [InlineData("|abc\n   def", "J", "abc| def")]
    [InlineData("|abc\n\tdef", "J", "abc| def")]
    // No space before a ')'.
    [InlineData("|abc\n)def", "J", "abc|)def")]
    // No extra space after a line that already ends in one, or in a tab.
    [InlineData("|abc \ndef", "J", "abc |def")]
    [InlineData("|abc\t\ndef", "J", "abc\t|def")]
    // No space beside an empty line, on either side of the seam — and a line of blanks joined on
    // brings nothing with it.
    [InlineData("|abc\n", "J", "ab|c")]
    [InlineData("|\nabc", "J", "|abc")]
    [InlineData("|abc\n   ", "J", "ab|c")]
    // Never two spaces after a full stop: 'joinspaces' is off.
    [InlineData("|One.\nTwo.", "J", "One.| Two.")]
    public void J_JoinsTheLineBelowWithOneSpaceAtTheSeam(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // A count is the number of lines that end up as one, and the caret lands on the last seam.
    [InlineData("|a\nb\nc\nd", "3J", "a b| c\nd")]
    [InlineData("|a\nb\nc\nd", "4J", "a b c| d")]
    // 'J' and '2J' are the same command: two lines is the minimum.
    [InlineData("|a\nb\nc", "2J", "a| b\nc")]
    [InlineData("|a\nb\nc", "1J", "a| b\nc")]
    // A count past the end joins what is there.
    [InlineData("|a\nb", "5J", "a| b")]
    public void ACountOnJ_JoinsThatManyLines(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("|abc\n   def", "gJ", "abc|   def")]
    [InlineData("|abc\ndef", "gJ", "abc|def")]
    [InlineData("|abc \ndef", "gJ", "abc |def")]
    [InlineData("|a\nb\nc", "3gJ", "ab|c")]
    [InlineData("|abc\n", "gJ", "ab|c")]
    public void GJ_JoinsWithoutAddingOrRemovingAnything(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("a\n|b", "J")]
    [InlineData("a\n|b", "5J")]
    [InlineData("a\n|b", "gJ")]
    [InlineData("|", "J")]
    public void OnTheLastLine_JoinDoesNothingAndSaysNothing(string before, string keys)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(before, run.Text);
        Assert.Null(run.Status);
        Assert.Equal(0, run.Buffer.UndoSteps);
    }

    [Fact]
    public void Join_LeavesTheRegisterAlone()
    {
        VimRun run = VimScript.Run("|abc\ndef", "yyJ");

        Assert.Equal("abc| def", run.Text);
        Assert.Equal(VimRegister.Lines("abc"), run.Register);
    }

    [Fact]
    public void Join_CostsOneUndoStepWhateverTheCount()
    {
        VimRun run = VimScript.Run("|a\nb\nc", "3J");

        Assert.Equal(1, run.Buffer.UndoSteps);
        Assert.Equal("|a\nb\nc", VimScript.Run("|a\nb\nc", "3Ju").Text);
    }

    [Fact]
    public void Join_ResetsTheRememberedColumn()
    {
        // After '$' j walks the ends of the lines; a join in between is a new position, and the j
        // after it goes straight down from the seam rather than to the end of the line below.
        VimRun run = VimScript.Run("|abcdef\nab\ncdefgh", "$Jj");

        Assert.Equal("abcdef ab\ncdefg|h", run.Text);
    }

    [Theory]
    // Over a selection the lines to join are the selected ones, two at the least — so a selection
    // inside one line still joins it to the next. The matrix is in VimVisualCommandTests.
    [InlineData("a|bc\ndef", "vJ", "abc| def")]
    [InlineData("a|bc\ndef", "vgJ", "abc|def")]
    [InlineData("a|bc\ndef\nghi", "VjjJ", "abc def| ghi")]
    public void InAVisualMode_JoinTakesTheSelectedLines(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
    }

    [Fact]
    public void InInsertMode_JIsJustAJ()
        => Assert.Equal("J|abc", VimScript.Run("|abc", "iJ").Text);
}
