using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// The two normal-mode spellings of a save-and-close, <c>ZZ</c> and <c>ZQ</c>, and the ex spellings
/// that join them: <c>:q!</c>, <c>:x</c> and <c>:wq!</c>.
/// </summary>
/// <remarks>
/// All five leave the engine as a request, exactly as <c>:wq</c> does, and none of them touches the
/// buffer. What the cases hold is which request each one raises, that it is raised once, and that
/// <c>Z</c> on its own is a half-typed sequence <c>Esc</c> cancels.
/// </remarks>
public sealed class VimQuitTests
{
    [Theory]
    [InlineData("ZZ", VimExCommand.WriteQuit)]
    [InlineData("ZQ", VimExCommand.QuitDiscard)]
    [InlineData("3ZZ", VimExCommand.WriteQuit)]
    [InlineData(":q!<CR>", VimExCommand.QuitDiscard)]
    [InlineData(":x<CR>", VimExCommand.WriteQuit)]
    [InlineData(":wq!<CR>", VimExCommand.WriteQuit)]
    [InlineData(":wq<CR>", VimExCommand.WriteQuit)]
    public void TheQuitSpellings_RaiseTheirRequestOnceAndTouchNothing(string keys, VimExCommand expected)
    {
        VimRun run = VimScript.Run("a|bc\ndef", keys);

        Assert.Equal(expected, Assert.Single(run.ExCommands));
        Assert.Equal("a|bc\ndef", run.Text);
        Assert.Null(run.Status);
        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.False(run.Engine.HasPendingSequence);
    }

    [Fact]
    public void ALoneZ_IsPending()
    {
        VimRun run = VimScript.Run("|abc", "Z");

        Assert.True(run.Engine.HasPendingSequence);
        Assert.Empty(run.ExCommands);
        Assert.Null(run.Status);
    }

    [Fact]
    public void Escape_CancelsAHalfTypedZ()
    {
        VimRun run = VimScript.Run("|abc", "Z<Esc>x");

        Assert.Empty(run.ExCommands);
        Assert.Equal("|bc", run.Text);
    }

    [Theory]
    [InlineData("Zx", "Not a command: Zx")]
    [InlineData("Zz", "Not a command: Zz")]
    [InlineData("dZ", "Not a command: dZ")]
    public void ZFollowedByAnythingElse_IsNamedAndNothingIsAskedFor(string keys, string status)
    {
        VimRun run = VimScript.Run("|abc", keys);

        Assert.Equal(status, run.Status);
        Assert.Empty(run.ExCommands);
        Assert.False(run.Engine.HasPendingSequence);
    }

    [Fact]
    public void InAVisualMode_ZIsNotACommandAndTheSelectionStays()
    {
        VimRun run = VimScript.Run("a|bc", "vZZ");

        Assert.Equal("Not a command: Z", run.Status);
        Assert.Empty(run.ExCommands);
        Assert.Equal(VimMode.Visual, run.Mode);
        Assert.Equal("b", run.SelectedText);
    }

    [Fact]
    public void InInsertMode_ZZIsJustText()
    {
        VimRun run = VimScript.Run("|abc", "iZZ");

        Assert.Equal("ZZ|abc", run.Text);
        Assert.Empty(run.ExCommands);
    }

    [Theory]
    [InlineData(":wa<CR>", "Not an editor command: wa")]
    [InlineData(":qa<CR>", "Not an editor command: qa")]
    [InlineData(":w!<CR>", "Not an editor command: w!")]
    [InlineData(":e<CR>", "Not an editor command: e")]
    public void TheExCommandsStillOut_AreStillRefusedByName(string keys, string status)
    {
        VimRun run = VimScript.Run("|abc", keys);

        Assert.Equal(status, run.Status);
        Assert.Empty(run.ExCommands);
    }
}
