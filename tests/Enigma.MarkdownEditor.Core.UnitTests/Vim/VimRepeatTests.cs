using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// <c>.</c> — the last change, done again.
/// </summary>
/// <remarks>
/// Two things have to hold for every kind of change: that <c>.</c> does it again, and that it is one
/// undo step when it does. The kinds are the whole surface — an operator over a motion or object, the
/// single-key edits, the puts, the joins, an insert with the text typed in it, and a visual
/// operation replayed over the same size — and a handful of things <c>.</c> must <em>not</em> repeat:
/// a motion, a yank, undo, an ex command.
/// </remarks>
public sealed class VimRepeatTests
{
    [Theory]
    [InlineData("|abcde", "x.", "|cde")]
    [InlineData("|foo bar baz", "dw.", "|baz")]
    [InlineData("|abc", "~.", "AB|c")]
    // 'r' leaves the caret on the character it wrote, so a move comes first for the repeat to land
    // anywhere new.
    [InlineData("a|bcd", "rxl.", "ax|xd")]
    [InlineData("|foo\nbar\nbaz", "ddj.", "|bar")]
    [InlineData("|a\nb\nc\nd", "J.", "a b| c\nd")]
    public void Dot_RepeatsTheLastChange(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void Dot_RepeatsAnOperatorOverAnObject()
        => Assert.Equal("() () (|)", VimScript.Run("(|foo) (bar) (baz)", "di(f(.f(.").Text);

    [Fact]
    public void Dot_RepeatsAPut()
        => Assert.Equal("abc\nabc\n|abc", VimScript.Run("|abc", "yyp.").Text);

    [Theory]
    [InlineData("a|bc", "ix<Esc>.", "a|xxbc")]
    [InlineData("|abc", "A!<Esc>.", "abc!|!")]
    [InlineData("|abc", "ox<Esc>.", "abc\nx\n|x")]
    public void Dot_RepeatsAnInsertWithTheTextTypedInIt(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("a|bc", "cwXY<Esc>.", "aXX|Y")]
    [InlineData("|foo bar", "ciwzz<Esc>w.", "zz z|z")]
    public void Dot_RepeatsAChangeWithItsText(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // A count on '.' replaces the original; a bare '.' after it reuses the replacement, not the count
    // the change was first typed with.
    [InlineData("|foo bar baz qux", "dw3.", "|")]
    [InlineData("|a b c d e f", "dw2..", "|f")]
    public void ACountOnDot_ReplacesTheOriginalCount(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("a|bc", "3ix<Esc>", "axx|xbc")]
    [InlineData("|abc", "3A!<Esc>", "abc!!|!")]
    [InlineData("a|bc", "2ox<Esc>", "abc\nx\n|x")]
    public void ACountOnAnInsertCommand_RepeatsItsText(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void ACountedInsert_IsOneUndoStep()
    {
        VimRun run = VimScript.Run("a|bc", "3ix<Esc>u");

        Assert.Equal("a|bc", run.Text);
        Assert.Equal(0, run.Buffer.UndoSteps);
    }

    [Fact]
    public void ADotReplay_IsExactlyOneUndoStep()
    {
        VimRun run = VimScript.Run("a|bcde", "x.u");

        // The '.' is taken back on its own, leaving the first 'x' done.
        Assert.Equal("a|cde", run.Text);
    }

    [Fact]
    public void ADotReplayOfAnInsert_ComesBackInOneStep()
    {
        // 'ix<Esc>.' inserts two x's in two steps; 'u' takes back only the one the '.' added.
        VimRun run = VimScript.Run("a|bc", "ix<Esc>.u");

        Assert.Equal("a|xbc", run.Text);
    }

    [Theory]
    // The excluded kinds: '.' after a motion or a yank repeats the change before it, not the motion
    // or the yank.
    [InlineData("|abcde", "xl.", "b|de")]
    [InlineData("|abcde", "xyy.", "|cde")]
    public void Dot_DoesNotRepeatAMotionOrAYank(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void DotWithNothingToRepeat_DoesNothing()
    {
        VimRun run = VimScript.Run("a|bc", ".");

        Assert.Equal("a|bc", run.Text);
        Assert.Null(run.Status);
    }

    [Theory]
    // A visual operation is replayed over the same number of lines or characters, from the caret.
    [InlineData("|abcdef", "vlx.", "|ef")]
    [InlineData("|a\nb\nc\nd", "Vjd.", "|")]
    public void Dot_RepeatsAVisualOperationOverTheSameExtent(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void ADotWithACount_IsKeptForTheNextBareDot()
    {
        // '2.' after 'dw' is '2dw'; the bare '.' after it is '2dw' again, so four words go after the
        // first, not three.
        VimRun run = VimScript.Run("|a b c d e f", "dw2..");

        Assert.Equal("|f", run.Text);
    }
}
