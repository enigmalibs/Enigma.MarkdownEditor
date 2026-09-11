using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// The digits in front of a command, on every command that takes them.
/// </summary>
/// <remarks>
/// A count is one rule applied everywhere rather than a feature of any one command, so these cases
/// are about the rule: it multiplies the motion, it survives an operator in front of it, it clamps
/// rather than failing when the document runs out, and <c>0</c> is a digit only once another one has
/// been typed — which is what leaves it free to be the motion to the start of the line.
/// </remarks>
public sealed class VimCountTests
{
    [Theory]
    [InlineData("|abcdef", "3l", "abc|def")]
    [InlineData("abcde|f", "3h", "ab|cdef")]
    [InlineData("|a\nb\nc\nd", "2j", "a\nb\n|c\nd")]
    [InlineData("a\nb\nc\n|d", "2k", "a\n|b\nc\nd")]
    [InlineData("|foo bar baz", "2w", "foo bar |baz")]
    [InlineData("foo bar b|az", "2b", "foo |bar baz")]
    [InlineData("|foo bar", "2e", "foo ba|r")]
    // '$' with a count reaches the end of a line below, which is what makes '3D' delete to there.
    [InlineData("|abc\ndefgh", "2$", "abc\ndefg|h")]
    // 'G' and 'gg' are the two motions that mean a line number rather than a distance.
    [InlineData("a\nb\n|c\nd", "2G", "a\n|b\nc\nd")]
    [InlineData("a\nbb\nccc\n|d", "2gg", "a\n|bb\nccc\nd")]
    [InlineData("a\nb\n|c", "1gg", "|a\nb\nc")]
    public void ACount_MultipliesTheMotion(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // Ten lines is not ten keystrokes' worth of digits: the count is read as a number, not a repeat.
    [InlineData("|a\nb\nc\nd\ne\nf\ng\nh\ni\nj\nk", "10j", "a\nb\nc\nd\ne\nf\ng\nh\ni\nj\n|k")]
    [InlineData("|abcdefghijkl", "10l", "abcdefghij|kl")]
    public void ACountOfMoreThanOneDigit_IsOneNumber(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("|a\nb\nc", "9j", "a\nb\n|c")]
    [InlineData("a\nb\n|c", "9k", "|a\nb\nc")]
    [InlineData("|a\nb\nc", "9G", "a\nb\n|c")]
    [InlineData("|abc", "9l", "ab|c")]
    [InlineData("ab|c", "9h", "|abc")]
    [InlineData("abc|de", "9x", "ab|c")]
    [InlineData("|abc", "9dd", "|")]
    [InlineData("a|bc\ndef", "9dj", "|")]
    public void ACountThatOvershootsTheDocument_ClampsRatherThanFailing(
        string before,
        string keys,
        string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("|abcde", "3x", "|de")]
    [InlineData("|abc\ndef", "9x", "|\ndef")]
    [InlineData("a|bc\ndef\nghi\njkl", "2dd", "|ghi\njkl")]
    [InlineData("|foo bar baz", "d2w", "|baz")]
    [InlineData("|a b c d e f g", "2d3w", "|g")]
    public void ACount_SurvivesAnOperatorInFrontOfIt(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // '0' on its own is the motion to the start of the line.
    [InlineData("ab|cd", "0", "|abcd")]
    [InlineData("ab|cd", "d0", "|cd")]
    // Behind another digit it is a digit: '10' is ten, not one and then the start of the line.
    [InlineData("|abcdefghijkl", "10l", "abcdefghij|kl")]
    [InlineData("ab|cd", "10d0", "|cd")]
    public void Zero_IsADigitOnlyOnceAnotherOneHasBeenTyped(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void ACountOnItsOwn_IsNotACommandYet()
    {
        VimRun run = VimScript.Run("|abcdef", "12");

        Assert.Equal("|abcdef", run.Text);
        Assert.True(run.Engine.HasPendingSequence);
    }

    [Fact]
    public void EscapeAfterACount_ThrowsItAway()
    {
        // Without the cancel this would be '3l' and land three characters along.
        VimRun run = VimScript.Run("|abcdef", "3<Esc>l");

        Assert.Equal("a|bcdef", run.Text);
    }

    [Fact]
    public void ACountThatIsFinished_DoesNotCarryIntoTheNextCommand()
        => Assert.Equal("abc|def", VimScript.Run("|abcdef", "2ll").Text);
}
