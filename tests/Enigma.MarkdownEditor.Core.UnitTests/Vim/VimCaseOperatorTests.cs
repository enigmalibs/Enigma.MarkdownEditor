using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// The case operators <c>gu</c>, <c>gU</c> and <c>g~</c>: doubled in both spellings, over motions
/// and objects, over a selection, and what they leave alone.
/// </summary>
/// <remarks>
/// Two things distinguish these from the three operators that came before them. They fill no
/// register — nothing is taken out — and they leave the caret at the <em>start</em> of the range,
/// which for a doubled form is the first column of the line rather than its first non-blank. Both are
/// vim's, and both are held here.
/// </remarks>
public sealed class VimCaseOperatorTests
{
    [Theory]
    [InlineData("|Foo Bar", "guu", "|foo bar")]
    [InlineData("|Foo Bar", "gugu", "|foo bar")]
    [InlineData("|foo bar", "gUU", "|FOO BAR")]
    [InlineData("|foo bar", "gUgU", "|FOO BAR")]
    [InlineData("|Foo bAR", "g~~", "|fOO Bar")]
    [InlineData("|Foo bAR", "g~g~", "|fOO Bar")]
    // The caret goes to the first column, wherever it was.
    [InlineData("Foo |Bar", "guu", "|foo bar")]
    [InlineData("  Foo |Bar", "gUU", "|  FOO BAR")]
    // A count is lines.
    [InlineData("|Foo\nBar\nBaz", "2gUU", "|FOO\nBAR\nBaz")]
    [InlineData("|Foo\nBar\nBaz", "gU2U", "|FOO\nBAR\nBaz")]
    [InlineData("|Foo\nBar", "9guu", "|foo\nbar")]
    // Letters only: digits and punctuation are left as they are.
    [InlineData("|a1-b", "gUU", "|A1-B")]
    [InlineData("|", "gUU", "|")]
    [InlineData("|\nabc", "gUU", "|\nabc")]
    public void ADoubledCaseOperator_ChangesTheCaseOfWholeLines(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("|Foo Bar", "gUw", "|FOO Bar")]
    [InlineData("|foo bar", "gUe", "|FOO bar")]
    [InlineData("Foo |Bar", "gUb", "|FOO Bar")]
    [InlineData("|Foo Bar", "gU$", "|FOO BAR")]
    [InlineData("Foo |Bar", "gu0", "|foo Bar")]
    [InlineData("|foo", "3gUl", "|FOO")]
    [InlineData("|foo", "gU3l", "|FOO")]
    [InlineData("|foo.bar baz", "gUW", "|FOO.BAR baz")]
    [InlineData("|foo\nbar\nbaz", "gUj", "|FOO\nBAR\nbaz")]
    [InlineData("|foo\nbar\n\nbaz", "gU}", "|FOO\nBAR\n\nbaz")]
    [InlineData("|foo bar. baz", "g~)", "|FOO BAR. baz")]
    [InlineData("a|bc", "gUfc", "a|BC")]
    public void ACaseOperatorOverAMotion_ChangesWhatItReaches(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("|Foo Bar", "g~iw", "|fOO Bar")]
    [InlineData("foo b|ar", "gUiw", "foo |BAR")]
    [InlineData("foo b|ar baz", "gUaw", "foo |BAR baz")]
    [InlineData("Foo (b|ar) Baz", "gUi(", "Foo (|BAR) Baz")]
    [InlineData("Foo \"b|ar\" Baz", "gUa\"", "Foo |\"BAR\" Baz")]
    // The range starts at the line break after the brace, which is no place for a normal-mode caret,
    // so it is pulled back onto the brace — where vim leaves it too.
    [InlineData("{\nf|oo\n}", "gUi{", "|{\nFOO\n}")]
    public void ACaseOperatorOverATextObject_ChangesWhatItCovers(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("|foo bar", "vegU", "|FOO bar")]
    [InlineData("foo ba|r", "vbgU", "foo |BAR")]
    [InlineData("|Foo\nBar", "Vjgu", "|foo\nbar")]
    [InlineData("|Foo bAR", "v$g~", "|fOO Bar")]
    public void ACaseOperatorOverASelection_ChangesItAndLeavesVisualMode(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.Null(run.Buffer.Selection);
    }

    [Theory]
    // An empty characterwise range has nothing to change and changes nothing.
    [InlineData("|foo", "gU0", "|foo")]
    [InlineData("|foo", "gUh", "|foo")]
    public void ACaseOperatorOverNothing_DoesNothing(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(0, run.Buffer.UndoSteps);
    }

    [Fact]
    public void ACaseOperator_LeavesTheRegisterAlone()
    {
        VimRun run = VimScript.Run("|abc\ndef", "yygUU");

        Assert.Equal("|ABC\ndef", run.Text);
        Assert.Equal(VimRegister.Lines("abc"), run.Register);
    }

    [Fact]
    public void ACaseOperator_CostsOneUndoStepWhateverTheCount()
    {
        VimRun run = VimScript.Run("|foo\nbar", "2gUU");

        Assert.Equal(1, run.Buffer.UndoSteps);
        Assert.Null(run.Status);
        Assert.Equal("|foo\nbar", VimScript.Run("|foo\nbar", "2gUUu").Text);
    }

    [Fact]
    public void ACaseOperator_StaysInNormalMode()
        => Assert.Equal(VimMode.Normal, VimScript.Run("|foo", "gUU").Mode);

    [Theory]
    [InlineData("|foo", "gu")]
    [InlineData("|foo", "gU")]
    [InlineData("|foo", "g~")]
    [InlineData("|foo", "3gU")]
    public void AHalfTypedCaseOperator_IsPendingUntilItsMotionArrives(string before, string keys)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.True(run.Engine.HasPendingSequence);
        Assert.Equal(before, run.Text);
        Assert.Null(run.Status);
    }

    [Fact]
    public void Escape_CancelsAHalfTypedCaseOperator()
    {
        VimRun run = VimScript.Run("|foo", "gU<Esc>x");

        Assert.False(run.Engine.HasPendingSequence);
        Assert.Equal("|oo", run.Text);
    }

    [Theory]
    // The wrong second half is a sequence that never became a command, named in full.
    [InlineData("|foo", "gUu", "Not a command: gUu")]
    [InlineData("|foo", "gux", "Not a command: gux")]
    [InlineData("|foo", "g~U", "Not a command: g~U")]
    [InlineData("|foo", "gx", "Not a command: gx")]
    public void TheWrongKeyAfterACaseOperator_IsNamedAndChangesNothing(string before, string keys, string status)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(status, run.Status);
        Assert.Equal(before, run.Text);
        Assert.False(run.Engine.HasPendingSequence);
    }

    [Fact]
    public void TildeAlone_IsStillTheOneCharacterToggle()
        => Assert.Equal("F|oo", VimScript.Run("|foo", "~").Text);

    [Fact]
    public void UAlone_IsStillUndo()
        => Assert.Equal("|abc", VimScript.Run("|abc", "xu").Text);

    [Theory]
    // Over a selection the bare letters are the operators: 'u' is 'gu', 'U' is 'gU' and '~' is 'g~',
    // vim's own spellings. The matrix is in VimVisualCommandTests; this pins the three readings.
    [InlineData("|Foo", "veu", "|foo")]
    [InlineData("|Foo", "veU", "|FOO")]
    [InlineData("|Foo", "ve~", "|fOO")]
    public void InAVisualMode_TheBareLettersAreTheCaseOperators(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);
}
