using System;
using System.Collections.Generic;
using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// The two answers the engine gives the editor — "I took that key" and "that one is yours" — and the
/// state it keeps between them.
/// </summary>
/// <remarks>
/// This is the half of the contract the view depends on. Normal mode has to swallow a key it has no
/// meaning for, or the character would be typed into a document nobody is typing into; insert mode
/// has to let everything past, or an input method composing a character would find half of it
/// missing.
/// </remarks>
public sealed class VimModeTests
{
    [Fact]
    public void TheEngine_StartsInNormalMode()
        => Assert.Equal(VimMode.Normal, new VimEngine(new FakeVimBuffer("abc")).Mode);

    [Fact]
    public void ItsBuffer_IsRequired()
        => Assert.Throws<ArgumentNullException>(() => new VimEngine(null!));

    [Theory]
    [InlineData('h')]
    [InlineData('x')]
    [InlineData('G')]
    // A key with no meaning at all is swallowed too: letting it through would type it.
    [InlineData('z')]
    [InlineData('7')]
    public void NormalMode_TakesEveryBareKey(char character)
        => Assert.True(new VimEngine(new FakeVimBuffer("abc")).HandleKey(VimKey.Typed(character)));

    [Theory]
    [InlineData('s')]
    [InlineData('w')]
    [InlineData('v')]
    public void NormalMode_LeavesTheWindowsOwnShortcutsAlone(char character)
    {
        VimEngine engine = new(new FakeVimBuffer("abc"));

        Assert.False(engine.HandleKey(VimKey.Typed(character, VimModifiers.Control)));
        Assert.False(engine.HandleKey(VimKey.Typed(character, VimModifiers.Alt)));
    }

    [Fact]
    public void InsertMode_LetsEveryKeyButEscapeThrough()
    {
        VimEngine engine = new(new FakeVimBuffer("abc"));
        engine.HandleKey(VimKey.Typed('i'));

        Assert.False(engine.HandleKey(VimKey.Typed('x')));
        Assert.False(engine.HandleKey(VimKey.Typed('G')));
        Assert.False(engine.HandleKey(VimKey.Typed('s', VimModifiers.Control)));
        Assert.False(engine.HandleKey(VimKey.Named(VimSpecialKey.Left)));
        Assert.False(engine.HandleKey(VimKey.Named(VimSpecialKey.Other)));

        Assert.True(engine.HandleKey(VimKey.Named(VimSpecialKey.Escape)));
        Assert.Equal(VimMode.Normal, engine.Mode);
    }

    [Fact]
    public void AKeyWithNoVimMeaning_IsSwallowedWithoutAWord()
    {
        FakeVimBuffer buffer = new("abc");
        VimEngine engine = new(buffer);

        Assert.True(engine.HandleKey(VimKey.Named(VimSpecialKey.Other)));
        Assert.Null(buffer.Status);
    }

    [Fact]
    public void AKeyThatCouldHaveBeenACommand_SaysSoOnTheStatusBar()
        => Assert.Equal("Not a command: Q", VimScript.Run("|abc", "Q").Buffer.Status);

    [Fact]
    public void ADeadTwoKeySequence_IsNamedInFull()
        => Assert.Equal("Not a command: gx", VimScript.Run("|abc", "gx").Buffer.Status);

    [Fact]
    public void TheNextCommandThatWorks_TakesTheMessageBackOff()
        => Assert.Null(VimScript.Run("|abc", "Ql").Buffer.Status);

    [Fact]
    public void Escape_CancelsAHalfTypedSequence()
    {
        VimRun run = VimScript.Run("abc\nd|ef", "g<Esc>");

        Assert.False(run.Engine.HasPendingSequence);
        Assert.Null(run.Buffer.Status);
    }

    [Fact]
    public void ASequenceCancelledByEscape_DoesNotFinishOnTheNextKey()
    {
        // Without the cancel this would be 'gg' and jump to the top.
        VimRun run = VimScript.Run("abc\nd|ef", "g<Esc>g");

        Assert.Equal("abc\nd|ef", run.Text);
        Assert.True(run.Engine.HasPendingSequence);
    }

    [Fact]
    public void ModeChanged_IsRaisedOnceForEachSwitch()
    {
        VimEngine engine = new(new FakeVimBuffer("abc"));
        List<VimMode> raised = [];
        engine.ModeChanged += (_, mode) => raised.Add(mode);

        engine.HandleKey(VimKey.Typed('i'));
        engine.HandleKey(VimKey.Typed('i'));
        engine.HandleKey(VimKey.Named(VimSpecialKey.Escape));
        engine.HandleKey(VimKey.Named(VimSpecialKey.Escape));

        Assert.Equal(new[] { VimMode.Insert, VimMode.Normal }, raised);
    }

    [Fact]
    public void Reset_ClosesAnOpenInsertSessionAndComesBackToNormalMode()
    {
        FakeVimBuffer buffer = VimScript.NewBuffer("a|bc");
        VimEngine engine = new(buffer);

        engine.HandleKey(VimKey.Typed('A'));
        engine.Reset();

        Assert.Equal(VimMode.Normal, engine.Mode);
        Assert.Equal(0, buffer.OpenUndoGroups);

        // The caret was at the end of the line for insert mode; normal mode does not allow that.
        Assert.Equal("ab|c", VimScript.Render(buffer));
    }

    [Fact]
    public void Reset_ThrowsAwayAHalfTypedSequenceAndTheMessageOnScreen()
    {
        FakeVimBuffer buffer = VimScript.NewBuffer("|abc");
        VimEngine engine = new(buffer);

        engine.HandleKey(VimKey.Typed('z'));
        engine.HandleKey(VimKey.Typed('g'));
        engine.Reset();

        Assert.False(engine.HasPendingSequence);
        Assert.Null(buffer.Status);
    }
}
