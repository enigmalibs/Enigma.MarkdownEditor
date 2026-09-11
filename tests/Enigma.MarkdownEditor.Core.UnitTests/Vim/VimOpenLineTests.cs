using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// <c>o</c> and <c>O</c> over a markdown item: the marker carried onto the new line, the way Enter
/// carries it in insert mode.
/// </summary>
/// <remarks>
/// The rules themselves are <c>ListContinuation</c>'s and are covered there over strings; what is
/// asserted here is the engine's half — that the right one of the two is asked, that the caret lands
/// after the marker rather than before it, that a count and <c>.</c> each write the marker again, and
/// that the whole thing is still one undo step.
/// </remarks>
public sealed class VimOpenLineTests
{
    [Theory]
    [InlineData("- it|em", "- item\n- |")]
    [InlineData("* it|em", "* item\n* |")]
    [InlineData("+ it|em", "+ item\n+ |")]
    [InlineData("1. it|em", "1. item\n2. |")]
    [InlineData("9. it|em", "9. item\n10. |")]
    [InlineData("42) it|em", "42) item\n43) |")]
    [InlineData("> quo|ted", "> quoted\n> |")]
    [InlineData("- [ ] to|do", "- [ ] todo\n- [ ] |")]
    // A done item is continued with an empty box: continuing a tick with another tick is never what
    // was meant.
    [InlineData("- [x] do|ne", "- [x] done\n- [ ] |")]
    [InlineData("  - nes|ted", "  - nested\n  - |")]
    [InlineData("> - quoted i|tem", "> - quoted item\n> - |")]
    // The spacing after the marker is the user's, tab included.
    [InlineData("-\tit|em", "-\titem\n-\t|")]
    public void O_ContinuesTheItemBelowIt(string before, string after)
    {
        VimRun run = VimScript.Run(before, "o");

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Insert, run.Mode);
    }

    [Theory]
    [InlineData("- it|em", "- |\n- item")]
    [InlineData("> quo|ted", "> |\n> quoted")]
    [InlineData("- [x] do|ne", "- [ ] |\n- [x] done")]
    [InlineData("  - nes|ted", "  - |\n  - nested")]
    // The item's own marker, not the next one: the new line goes before this item and takes its place
    // in the list, so counting on would number it after the one it precedes.
    [InlineData("2. b|ar", "2. |\n2. bar")]
    [InlineData("1. fo|o", "1. |\n1. foo")]
    public void CapitalO_ContinuesTheItemAboveItWithItsOwnMarker(string before, string after)
    {
        VimRun run = VimScript.Run(before, "O");

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Insert, run.Mode);
    }

    [Theory]
    [InlineData("# a head|ing", "o", "# a heading\n|")]
    [InlineData("# a head|ing", "O", "|\n# a heading")]
    [InlineData("just a l|ine", "o", "just a line\n|")]
    [InlineData("--|-", "o", "---\n|")]
    [InlineData("**bo|ld** at the start", "o", "**bold** at the start\n|")]
    [InlineData("-|x", "o", "-x\n|")]
    [InlineData("|", "o", "\n|")]
    public void ALineThatIsNotAnItem_OpensPlain(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("- |", "o", "- \n|")]
    [InlineData("- |", "O", "|\n- ")]
    [InlineData("1. |", "o", "1. \n|")]
    [InlineData("> |", "o", "> \n|")]
    [InlineData("- [ ] |", "o", "- [ ] \n|")]
    public void AnEmptyItem_OpensPlainAndIsLeftAlone(string before, string keys, string after)
    {
        // Enter clears an empty item, because the caret is on it and the user is ending the list. 'o'
        // never edits the line it is on — that is vim — so the analogue is to continue nothing.
        Assert.Equal(after, VimScript.Run(before, keys).Text);
    }

    [Theory]
    [InlineData("- it|em", "oX", "- item\n- X|")]
    [InlineData("- it|em", "OX", "- X|\n- item")]
    [InlineData("1. it|em", "oX<Esc>", "1. item\n2. |X")]
    public void WhatIsTypedNext_LandsAfterTheMarker(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void ACount_WritesTheMarkerOncePerLineAndCountsOn()
    {
        // The marker is re-derived from the line just written, so three copies walk down the list
        // exactly as three separate 'o's would.
        Assert.Equal(
            "1. item\n2. x\n3. x\n4. |x",
            VimScript.Run("1. it|em", "3ox<Esc>l").Text);
    }

    [Fact]
    public void ACountOverABullet_RepeatsTheBullet()
        => Assert.Equal("- item\n- x\n- |x", VimScript.Run("- it|em", "2ox<Esc>l").Text);

    [Fact]
    public void Dot_OpensAnotherContinuedItem()
        => Assert.Equal("- item\n- x\n- |x", VimScript.Run("- it|em", "ox<Esc>.l").Text);

    [Fact]
    public void OneUndo_TakesTheMarkerBackWithWhatWasTypedOnIt()
    {
        // The marker is written inside the insert session's undo group, so it cannot be left behind
        // as an orphan bullet by a single 'u'.
        VimRun run = VimScript.Run("- it|em", "oX<Esc>u");

        Assert.Equal("- it|em", run.Text);
        Assert.Equal(1, run.Buffer.UndoGroupsOpened);
    }

    [Fact]
    public void OpeningAContinuedLine_IsStillOneWriteToTheBuffer()
    {
        // The engine's rule: one command, one Replace, one undo step. The marker rides in the same
        // write as the line break rather than following it.
        Assert.Equal(1, VimScript.Run("- it|em", "o").Buffer.Replacements);
    }

    [Fact]
    public void TheMarkerCarriesNoTerminatorOfItsOwn()
    {
        // A CRLF buffer keeps CRLF, and the marker does not smuggle a line feed in behind it — which
        // is the one way this could have mixed two kinds of ending into one file.
        FakeVimBuffer buffer = new("- item") { NewLine = "\r\n", CaretOffset = 3 };
        VimEngine engine = new(buffer);

        foreach (VimKey key in VimKeyNotation.Parse("o"))
        {
            engine.HandleKey(key);
        }

        Assert.Equal("- item\r\n- ", buffer.Text);
    }
}
