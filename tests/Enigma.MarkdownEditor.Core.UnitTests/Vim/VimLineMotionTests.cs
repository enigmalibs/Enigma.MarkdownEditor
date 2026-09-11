using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// The motions that move along or between lines by something other than a word: <c>^</c>, <c>+</c>,
/// <c>-</c>, <c>_</c>, <c>|</c>, <c>Home</c>, <c>End</c>, and the three keys that cross a line break
/// — <c>Space</c>, <c>Backspace</c> and <c>Enter</c>.
/// </summary>
/// <remarks>
/// <para>
/// Three of these — Enter, Backspace and Space — used to mean nothing in normal mode, and the cases
/// that said so have been rewritten here to say what they mean now. Every one of them is a motion in
/// vim, and every one of them still does in insert mode exactly what the editor did with it before.
/// </para>
/// <para>
/// The rule the wrapping pair is really testing is vim's exclusive-linewise one: a motion that
/// crosses onto the first column of the next line is pulled back to the end of the line before when
/// an operator is waiting on it, which is what makes <c>d&lt;Space&gt;</c> on the last character of a
/// line the same as <c>x</c> rather than a join.
/// </para>
/// </remarks>
public sealed class VimLineMotionTests
{
    [Theory]
    [InlineData("  ab|c", "^", "  |abc")]
    [InlineData("|  abc", "^", "  |abc")]
    [InlineData("ab|c", "^", "|abc")]
    [InlineData("\t\tab|c", "^", "\t\t|abc")]
    // A line of nothing but blanks has no first non-blank, so the caret stops on its last one.
    [InlineData("|   ", "^", "  | ")]
    [InlineData("|", "^", "|")]
    // A count means nothing to '^', which is vim's own rule.
    [InlineData("  ab|c\n  def", "3^", "  |abc\n  def")]
    public void Caret_GoesToTheFirstNonBlank(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // Exclusive: the character under the caret stays.
    [InlineData("  ab|c", "d^", "  |c")]
    [InlineData("  ab|c", "c^", "  |c")]
    // Nothing between the caret and the first non-blank: a command with nothing to do.
    [InlineData("  |abc", "d^", "  |abc")]
    // From before the first non-blank the motion goes forward, and the indent is what it covers.
    [InlineData("|  abc", "d^", "|abc")]
    public void AnOperatorOverCaret_TakesBackToTheFirstNonBlank(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void AYankOverCaret_FillsTheRegisterAndMovesToTheStartOfTheRange()
    {
        VimRun run = VimScript.Run("  ab|c", "y^");

        Assert.Equal("  |abc", run.Text);
        Assert.Equal(VimRegister.Characters("ab"), run.Register);
    }

    [Theory]
    [InlineData("a|bc\n  def", "+", "abc\n  |def")]
    [InlineData("a|bc\n  def", "<CR>", "abc\n  |def")]
    [InlineData("a|bc\ndef\nghi", "2+", "abc\ndef\n|ghi")]
    // Clamped at the last line, and still on its first non-blank.
    [InlineData("abc\n  d|ef", "+", "abc\n  |def")]
    [InlineData("a|bc\ndef", "9+", "abc\n|def")]
    public void PlusAndEnter_GoToTheFirstNonBlankOfTheLineBelow(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("  abc\nd|ef", "-", "  |abc\ndef")]
    [InlineData("abc\ndef\ng|hi", "2-", "|abc\ndef\nghi")]
    [InlineData("a|bc\ndef", "-", "|abc\ndef")]
    public void Minus_GoesToTheFirstNonBlankOfTheLineAbove(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("  ab|c", "_", "  |abc")]
    [InlineData("a|bc\n  def", "2_", "abc\n  |def")]
    [InlineData("a|bc\ndef", "9_", "abc\n|def")]
    public void Underscore_GoesToTheFirstNonBlankOfThisLineOrACountBelow(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // All three are linewise behind an operator: '+' is the same range as 'j', '-' as 'k', and '_'
    // as the doubled operator.
    [InlineData("a|bc\ndef\nghi", "d+", "|ghi")]
    [InlineData("a|bc\ndef\nghi", "d<CR>", "|ghi")]
    [InlineData("abc\nd|ef", "d-", "|")]
    [InlineData("a|bc\ndef", "d_", "|def")]
    [InlineData("a|bc\ndef\nghi", "d2_", "|ghi")]
    [InlineData("abc\nd|ef\nghi", "c_", "abc\n|\nghi")]
    public void AnOperatorOverALineMotion_TakesWholeLines(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("a|bc\ndef\nghi", "y+", "abc\ndef")]
    [InlineData("a|bc", "y_", "abc")]
    [InlineData("abc\nd|ef", "y-", "abc\ndef")]
    public void AYankOverALineMotion_FillsTheRegisterLinewise(string before, string keys, string text)
        => Assert.Equal(VimRegister.Lines(text), VimScript.Run(before, keys).Register);

    [Theory]
    [InlineData("abc|def", "|", "|abcdef")]
    [InlineData("|abcdef", "4|", "abc|def")]
    [InlineData("|abcdef", "1|", "|abcdef")]
    // Past the end of the line the column is clamped to the last character.
    [InlineData("|abc", "9|", "ab|c")]
    [InlineData("|", "3|", "|")]
    public void Bar_GoesToTheColumn(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // Exclusive, in either direction.
    [InlineData("|abcdef", "d4|", "|def")]
    [InlineData("abcd|ef", "d2|", "a|ef")]
    [InlineData("ab|cdef", "d3|", "ab|cdef")]
    public void AnOperatorOverBar_TakesToTheColumn(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("ab|c", "<Home>", "|abc")]
    [InlineData("|abc", "<End>", "ab|c")]
    [InlineData("|  abc", "<Home>", "|  abc")]
    [InlineData("ab|c", "d<Home>", "|c")]
    [InlineData("|abc", "d<End>", "|")]
    [InlineData("a|bc", "y<End>", "a|bc")]
    // End is '$' to the letter: after it, j walks down the ends of the lines.
    [InlineData("|abc\nde\nfghi", "<End>j", "abc\nd|e\nfghi")]
    public void HomeAndEnd_AreZeroAndDollar(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // In insert mode both keys are the editor's, which the harness plays as a caret move.
    [InlineData("a|bc", "i<Home>x", "x|abc")]
    [InlineData("a|bc", "i<End>x", "abcx|")]
    public void InInsertMode_HomeAndEndAreStillTheEditors(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Insert, run.Mode);
    }

    [Theory]
    [InlineData("|abc", " ", "a|bc")]
    [InlineData("|abc", "2 ", "ab|c")]
    // Onto the next line's first character from the last one, and from an empty line.
    [InlineData("ab|c\ndef", " ", "abc\n|def")]
    [InlineData("abc\n|\ndef", " ", "abc\n\n|def")]
    [InlineData("a|bc\ndef", "3 ", "abc\nd|ef")]
    // The end of the buffer is where it stops.
    [InlineData("ab|c", " ", "ab|c")]
    [InlineData("ab|c", "9 ", "ab|c")]
    [InlineData("|", " ", "|")]
    public void Space_MovesRightAndCrossesALineBreak(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("a|bc", "<BS>", "|abc")]
    [InlineData("ab|c", "2<BS>", "|abc")]
    // Onto the previous line's last character from the first one, and onto an empty line.
    [InlineData("abc\n|def", "<BS>", "ab|c\ndef")]
    [InlineData("abc\n\n|def", "<BS>", "abc\n|\ndef")]
    [InlineData("abc\n|def", "2<BS>", "a|bc\ndef")]
    // The start of the buffer is where it stops.
    [InlineData("|abc", "<BS>", "|abc")]
    [InlineData("|abc", "9<BS>", "|abc")]
    public void Backspace_MovesLeftAndCrossesALineBreak(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // Inside a line it is 'dl'.
    [InlineData("a|bc", "d ", "a|c")]
    [InlineData("|abc", "2d ", "|c")]
    // On the last character of a line the motion crosses the break, and vim's rule pulls the range
    // back to the end of the line — so the character goes and the break stays, exactly as with 'x'.
    [InlineData("ab|c\ndef", "d ", "a|b\ndef")]
    // At the end of the buffer there is nowhere to cross to, and vim makes the motion inclusive
    // instead, so the last character still goes.
    [InlineData("ab|c", "d ", "a|b")]
    [InlineData("a|bc", "5d ", "|a")]
    // An empty line, though, has nothing to pull back to, and the same rule turns the range
    // linewise: the empty line itself goes.
    [InlineData("abc\n|\ndef", "d ", "abc\n|def")]
    public void AnOperatorOverSpace_TakesTheCharacterAndNeverTheLineBreak(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // Inside a line it is 'dh'.
    [InlineData("ab|c", "d<BS>", "a|c")]
    [InlineData("ab|c", "2d<BS>", "|c")]
    // From the first column the motion lands on the previous line's last character, and the range
    // from there to a first column is pulled back to that line's end: its last character goes and
    // the two lines stay apart — vim's own answer.
    [InlineData("abc\n|def", "d<BS>", "a|b\ndef")]
    // At the start of the buffer there is nothing to take.
    [InlineData("|abc", "d<BS>", "|abc")]
    public void AnOperatorOverBackspace_TakesTheCharacterBefore(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("a|bc", "i<CR>", "a\n|bc")]
    [InlineData("ab|c", "i<BS>", "a|c")]
    [InlineData("a|bc", "i x", "a x|bc")]
    public void InInsertMode_EnterBackspaceAndSpaceAreStillTheEditors(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Insert, run.Mode);
    }

    [Fact]
    public void Tab_IsStillSwallowedInNormalModeWithoutAWord()
    {
        VimRun run = VimScript.Run("|abc", "<Tab>");

        Assert.Equal("|abc", run.Text);
        Assert.Null(run.Status);
        Assert.Equal(VimMode.Normal, run.Mode);
    }

    [Fact]
    public void Tab_CancelsWhatIsHalfTyped()
    {
        // 'd', then a key that means nothing, then 'd'. Not a 'dd': the first operator went with the
        // key that reset the sequence, which is what every meaningless key does.
        VimRun run = VimScript.Run("|abc\ndef", "d<Tab>d");

        Assert.Equal("abc\ndef", run.Buffer.Text);
        Assert.True(run.Engine.HasPendingSequence);
    }

    [Fact]
    public void Tab_IsStillTheEditorsInInsertMode()
    {
        VimRun run = VimScript.Run("|abc", "i<Tab>");

        Assert.Equal("\t|abc", run.Text);
        Assert.Equal(VimMode.Insert, run.Mode);
    }

    [Theory]
    [InlineData("a|bc\ndef", "v  d", "a|ef")]
    [InlineData("abc\n|def", "v<BS>d", "ab|ef")]
    [InlineData("  ab|cd", "v^d", "  |d")]
    [InlineData("|abcdef", "v3|d", "|def")]
    public void TheLineMotions_ExtendAVisualSelection(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);
}
