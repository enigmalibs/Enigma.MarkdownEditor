using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// Where every motion in the v1 contract leaves the caret, including at the edges nobody aims for on
/// purpose: the ends of lines, empty lines, and both ends of the document.
/// </summary>
/// <remarks>
/// Written in the harness's notation — text with a <c>|</c> where the caret is, keys, and the text the
/// caret should end up in. The rule most of these are really testing is normal mode's: the caret sits
/// on a character and never past the last one, which is what makes the end of a line a position rather
/// than a gap.
/// </remarks>
public sealed class VimMotionTests
{
    [Theory]
    [InlineData("a|bc", "h", "|abc")]
    [InlineData("|abc", "h", "|abc")]
    [InlineData("ab|c", "hh", "|abc")]
    [InlineData("|abc", "l", "a|bc")]
    [InlineData("ab|c", "l", "ab|c")]
    [InlineData("|abc", "lll", "ab|c")]
    [InlineData("a|bc", "<Left>", "|abc")]
    [InlineData("|abc", "<Right>", "a|bc")]
    [InlineData("|", "h", "|")]
    [InlineData("|", "l", "|")]
    public void HAndL_MoveAlongTheLineAndStopAtItsEnds(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // Nothing the engine does leaves a caret here, but a mouse click in the empty space past the end
    // of a line does, so a command has to start by pulling it back onto the last character.
    [InlineData("abc|", "h", "a|bc")]
    [InlineData("abc|", "l", "ab|c")]
    [InlineData("abc|", "0", "|abc")]
    [InlineData("abc|", "x", "a|b")]
    public void ACaretPastTheLastCharacter_IsPulledBackBeforeACommandRuns(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("a|bc\ndef", "j", "abc\nd|ef")]
    [InlineData("abc\nd|ef", "k", "a|bc\ndef")]
    [InlineData("a|bc\ndef", "k", "a|bc\ndef")]
    [InlineData("abc\nd|ef", "j", "abc\nd|ef")]
    [InlineData("a|bc\ndef", "<Down>", "abc\nd|ef")]
    [InlineData("abc\nd|ef", "<Up>", "a|bc\ndef")]
    public void JAndK_ChangeLineAndStopAtTheDocumentEdges(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // Through a line too short for the column, and out the other side at the column it started in.
    [InlineData("ab|cd\nx\nefgh", "jj", "abcd\nx\nef|gh")]
    // The short line itself puts the caret on its last character, not past it.
    [InlineData("ab|cd\nx\nefgh", "j", "abcd\n|x\nefgh")]
    // An empty line has no character to sit on, so the caret sits where the line starts.
    [InlineData("a|bc\n\ndef", "j", "abc\n|\ndef")]
    [InlineData("a|bc\n\ndef", "jj", "abc\n\nd|ef")]
    // Held against the last line, the column survives the keystrokes that went nowhere.
    [InlineData("ab|cd\nx", "jjjk", "ab|cd\nx")]
    public void AVerticalRun_RemembersTheColumnItStartedIn(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("ab|c", "0", "|abc")]
    [InlineData("|abc", "0", "|abc")]
    [InlineData("|abc", "$", "ab|c")]
    [InlineData("ab|c", "$", "ab|c")]
    [InlineData("|  abc", "$", "  ab|c")]
    [InlineData("|", "$", "|")]
    [InlineData("|\nabc", "$", "|\nabc")]
    public void ZeroAndDollar_GoToTheEndsOfTheLine(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // Vim's own quirk, and a useful one: after $, j and k walk down the ends of the lines.
    [InlineData("|abc\nde\nfghi", "$j", "abc\nd|e\nfghi")]
    [InlineData("|abc\nde\nfghi", "$jj", "abc\nde\nfgh|i")]
    public void DollarThenAVerticalMotion_StaysAtTheEndOfEachLine(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("|foo bar baz", "w", "foo |bar baz")]
    [InlineData("|foo bar baz", "ww", "foo bar |baz")]
    [InlineData("f|oo bar", "w", "foo |bar")]
    // A run of punctuation is a word of its own, which is what makes w useful in code as well as prose.
    [InlineData("|foo.bar", "w", "foo|.bar")]
    [InlineData("foo|.bar", "w", "foo.|bar")]
    // An underscore is a word character, so this is one word rather than three.
    [InlineData("|foo_bar baz", "w", "foo_bar |baz")]
    [InlineData("|foo   bar", "w", "foo   |bar")]
    // Across a line break, and the last word has nowhere further to go.
    [InlineData("a|bc\ndef", "w", "abc\n|def")]
    [InlineData("foo |bar", "w", "foo ba|r")]
    [InlineData("|", "w", "|")]
    public void W_GoesToTheStartOfTheNextWord(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // An empty line is a word in its own right — which is what walks a markdown file paragraph by
    // paragraph rather than running through the blank lines between them.
    [InlineData("a|bc\n\ndef", "w", "abc\n|\ndef")]
    [InlineData("a|bc\n\ndef", "ww", "abc\n\n|def")]
    [InlineData("abc\n\n|def", "b", "abc\n|\ndef")]
    public void AnEmptyLine_IsAWordOfItsOwn(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("foo bar |baz", "b", "foo |bar baz")]
    [InlineData("foo bar |baz", "bb", "|foo bar baz")]
    [InlineData("foo ba|r", "b", "foo |bar")]
    [InlineData("|foo bar", "b", "|foo bar")]
    [InlineData("foo.|bar", "b", "foo|.bar")]
    [InlineData("abc\n|def", "b", "|abc\ndef")]
    [InlineData("|", "b", "|")]
    public void B_GoesToTheStartOfThePreviousWord(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("|foo bar", "e", "fo|o bar")]
    [InlineData("fo|o bar", "e", "foo ba|r")]
    [InlineData("|foo bar", "ee", "foo ba|r")]
    [InlineData("|foo.bar", "e", "fo|o.bar")]
    // Nothing left to end on: e stays where it is rather than running off the document.
    [InlineData("foo ba|r", "e", "foo ba|r")]
    // Unlike w and b, e does not stop on an empty line: there is no character there to end on.
    [InlineData("ab|c\n\ndef", "e", "abc\n\nde|f")]
    [InlineData("|", "e", "|")]
    public void E_GoesToTheEndOfTheWord(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("abc\nd|ef", "gg", "|abc\ndef")]
    [InlineData("  a|bc", "gg", "  |abc")]
    [InlineData("|abc\ndef", "G", "abc\n|def")]
    [InlineData("|abc\n  def", "G", "abc\n  |def")]
    [InlineData("|abc", "G", "|abc")]
    [InlineData("|abc", "gg", "|abc")]
    [InlineData("|", "G", "|")]
    [InlineData("|", "gg", "|")]
    // A line of nothing but spaces has no first non-blank character, so the caret stops on its last.
    [InlineData("|abc\n   ", "G", "abc\n  | ")]
    public void GgAndG_GoToTheFirstNonBlankOfTheFirstAndLastLine(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void AHalfTypedG_DoesNothingUntilItsSecondKey()
    {
        VimRun run = VimScript.Run("abc\nd|ef", "g");

        Assert.Equal("abc\nd|ef", run.Text);
        Assert.True(run.Engine.HasPendingSequence);
    }

    [Fact]
    public void EveryMotion_AsksForTheCaretToBeBroughtIntoView()
        => Assert.Equal(3, VimScript.Run("|abc", "llh").Buffer.ScrollRequests);
}
