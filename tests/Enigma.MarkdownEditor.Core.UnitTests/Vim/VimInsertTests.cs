using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// The six ways into insert mode, the one way out, and <c>x</c>.
/// </summary>
/// <remarks>
/// The harness types every key the engine passes through, exactly as the editor would, so a case like
/// <c>"iX&lt;Esc&gt;"</c> goes through the whole round trip: enter insert, let the character past,
/// come back to normal mode with the caret where vim leaves it.
/// </remarks>
public sealed class VimInsertTests
{
    [Theory]
    [InlineData("a|bc", "i", "a|bc")]
    [InlineData("a|bc", "I", "|abc")]
    [InlineData("  a|bc", "I", "  |abc")]
    [InlineData("a|bc", "a", "ab|c")]
    [InlineData("a|bc", "A", "abc|")]
    // 'a' at the end of a line is the whole reason insert mode lets the caret past the last character.
    [InlineData("ab|c", "a", "abc|")]
    [InlineData("|", "i", "|")]
    [InlineData("|", "A", "|")]
    public void EnteringInsertMode_PutsTheCaretWhereTheCommandSays(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Insert, run.Mode);
    }

    [Theory]
    [InlineData("a|bc", "iX", "aX|bc")]
    [InlineData("a|bc", "IX", "X|abc")]
    [InlineData("a|bc", "aX", "abX|c")]
    [InlineData("a|bc", "AX", "abcX|")]
    [InlineData("a|bc", "ixyz", "axyz|bc")]
    public void WhatIsTypedInInsertMode_LandsInTheDocument(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("a|bc", "oX", "abc\nX|")]
    [InlineData("a|bc", "OX", "X|\nabc")]
    [InlineData("abc\nd|ef", "o", "abc\ndef\n|")]
    [InlineData("abc\nd|ef", "O", "abc\n|\ndef")]
    [InlineData("|", "oX", "\nX|")]
    // A line that is not a markdown item is opened plain. The item case — where the marker is carried
    // onto the new line, as Enter does in insert mode — is FEATURE-3505's, in VimOpenLineTests.
    [InlineData("# a head|ing", "oX", "# a heading\nX|")]
    public void OAndCapitalO_OpenALineAndInsertOnIt(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Insert, run.Mode);
    }

    [Theory]
    // Esc steps the caret back onto the character that was just typed, the way vim does.
    [InlineData("a|bc", "iX<Esc>", "a|Xbc")]
    [InlineData("a|bc", "AX<Esc>", "abc|X")]
    [InlineData("a|bc", "oX<Esc>", "abc\n|X")]
    [InlineData("a|bc", "ixyz<Esc>", "axy|zbc")]
    // Nothing was typed, so there is nothing to step back onto at the start of a line.
    [InlineData("|abc", "i<Esc>", "|abc")]
    [InlineData("a|bc", "i<Esc>", "|abc")]
    [InlineData("|", "i<Esc>", "|")]
    public void Escape_ReturnsToNormalModeAndStepsTheCaretBack(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
    }

    [Theory]
    [InlineData("a|bc", "x", "a|c")]
    [InlineData("|abc", "x", "|bc")]
    [InlineData("ab|c", "x", "a|b")]
    [InlineData("|abc", "xxx", "|")]
    [InlineData("|a", "x", "|")]
    // Nothing under the caret, and in particular the line below is not pulled up onto this one.
    [InlineData("|\nabc", "x", "|\nabc")]
    [InlineData("|", "x", "|")]
    // Deleting the last character of a line moves the caret back onto the new one.
    [InlineData("a|b\ncd", "x", "|a\ncd")]
    public void X_DeletesTheCharacterUnderTheCaret(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void AnInsertSession_IsOneUndoGroupFromTheCommandToEscape()
    {
        FakeVimBuffer buffer = VimScript.Run("a|bc", "ixyz<Esc>").Buffer;

        Assert.Equal(1, buffer.UndoGroupsOpened);
        Assert.Equal(0, buffer.OpenUndoGroups);
    }

    [Fact]
    public void AnOpenLineAndWhatIsTypedOnIt_AreTheSameUndoGroup()
    {
        FakeVimBuffer buffer = VimScript.Run("a|bc", "oxyz<Esc>").Buffer;

        Assert.Equal(1, buffer.UndoGroupsOpened);
        Assert.Equal(0, buffer.OpenUndoGroups);
    }

    [Fact]
    public void TwoInsertSessions_AreTwoUndoGroups()
    {
        FakeVimBuffer buffer = VimScript.Run("a|bc", "iX<Esc>AY<Esc>").Buffer;

        Assert.Equal(2, buffer.UndoGroupsOpened);
        Assert.Equal(0, buffer.OpenUndoGroups);
    }

    [Fact]
    public void AnInsertSessionStillOpen_HasNotClosedItsUndoGroup()
        => Assert.Equal(1, VimScript.Run("a|bc", "iX").Buffer.OpenUndoGroups);

    [Fact]
    public void ACommandThatIsNotAnInsert_OpensNoUndoGroup()
        => Assert.Equal(0, VimScript.Run("a|bc", "xlh").Buffer.UndoGroupsOpened);
}
