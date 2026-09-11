using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// The shift operators <c>&gt;</c> and <c>&lt;</c>: doubled, over motions and objects, over a
/// selection, and with the two options that decide what an indent is written as.
/// </summary>
/// <remarks>
/// The rule most of these hold is that a shift is always linewise — <c>&gt;w</c> shifts the line, not
/// the word — and that the indent is rewritten from scratch in columns: a tab counts as one
/// <c>shiftwidth</c>, and the total comes back out as spaces or as tabs-then-spaces according to
/// <c>expandtab</c>. The register is never touched, and one shift is one undo whatever the count.
/// </remarks>
public sealed class VimShiftTests
{
    [Theory]
    [InlineData("|abc", ">>", "    |abc")]
    [InlineData("ab|c", ">>", "    |abc")]
    [InlineData("  |abc", ">>", "      |abc")]
    [InlineData("|a\nb\nc", "2>>", "    |a\n    b\nc")]
    [InlineData("|a\nb\nc", "9>>", "    |a\n    b\n    c")]
    // An empty line stays empty; a line of blanks is a line and is shifted.
    [InlineData("|a\n\nb", "3>>", "    |a\n\n    b")]
    [InlineData("|a\n \nb", "2>>", "    |a\n     \nb")]
    public void ShiftRightDoubled_IndentsTheLinesByOneWidth(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("    |abc", "<lt><lt>", "|abc")]
    [InlineData("        |abc", "<lt><lt>", "    |abc")]
    [InlineData("      ab|c", "<lt><lt>", "  |abc")]
    // Less than a width of indent: what is there goes.
    [InlineData("  |abc", "<lt><lt>", "|abc")]
    [InlineData(" |abc", "<lt><lt>", "|abc")]
    [InlineData("    |a\n    b\nc", "2<lt><lt>", "|a\nb\nc")]
    [InlineData("    |a\n\n    b", "3<lt><lt>", "|a\n\nb")]
    public void ShiftLeftDoubled_RemovesUpToOneWidthOfIndent(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void ShiftLeftOnALineWithNoIndent_ChangesNothingAndCostsNoUndoStep()
    {
        VimRun run = VimScript.Run("|abc\ndef", "<lt><lt>");

        Assert.Equal("|abc\ndef", run.Text);
        Assert.Equal(0, run.Buffer.UndoSteps);
    }

    [Theory]
    // Always linewise, whatever the motion: the lines the range touches are what move.
    [InlineData("|a\nb\nc", ">j", "    |a\n    b\nc")]
    [InlineData("a\n|b\nc", ">k", "    |a\n    b\nc")]
    [InlineData("ab|c def", ">w", "    |abc def")]
    [InlineData("ab|c def", ">l", "    |abc def")]
    [InlineData("ab|c", ">iw", "    |abc")]
    [InlineData("|a\nb\nc", ">G", "    |a\n    b\n    c")]
    [InlineData("a\nb\n|c", ">gg", "    |a\n    b\n    c")]
    [InlineData("|a\nb\n\nc", ">}", "    |a\n    b\n\nc")]
    [InlineData("    |a\n    b\nc", "<lt>j", "|a\nb\nc")]
    [InlineData("    a\n    |b\nc", "<lt>k", "|a\nb\nc")]
    public void AShiftOverAMotionOrObject_IsLinewiseOverTheLinesItTouches(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void AShiftOverAnEmptyRange_StillShiftsTheLine()
        => Assert.Equal("    |abc", VimScript.Run("|abc", ">0").Text);

    [Theory]
    [InlineData("|a\nb\nc", "Vj>", "    |a\n    b\nc")]
    [InlineData("|a\nb", "vj>", "    |a\n    b")]
    [InlineData("a\n|b", "Vk>", "    |a\n    b")]
    [InlineData("    |a\n    b", "Vj<lt>", "|a\nb")]
    // Over a selection the count is the shift's own: three widths at once.
    [InlineData("|a\nb", "V3>", "            |a\nb")]
    [InlineData("        |a", "V2<lt>", "|a")]
    public void AShiftOverASelection_ShiftsTheSelectedLinesCountTimes(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.Null(run.Buffer.Selection);
    }

    [Theory]
    // Without 'expandtab' the indent is tabs for the whole widths and spaces for the rest, a tab
    // being one width wide.
    [InlineData("|abc", ">>", "\t|abc")]
    [InlineData("  |abc", ">>", "\t  |abc")]
    [InlineData("\t|abc", ">>", "\t\t|abc")]
    [InlineData("\t  |abc", ">>", "\t\t  |abc")]
    [InlineData("\t|abc", "<lt><lt>", "|abc")]
    [InlineData("\t  |abc", "<lt><lt>", "  |abc")]
    [InlineData("      |abc", "<lt><lt>", "  |abc")]
    public void WithoutExpandTab_TheIndentIsWrittenInTabs(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys, new VimOptions { ShiftWidth = 4, ExpandTab = false }).Text);

    [Theory]
    // With 'expandtab' the indent is rewritten as spaces, tabs already there included.
    [InlineData("\t|abc", ">>", "        |abc")]
    [InlineData("\t|abc", "<lt><lt>", "|abc")]
    // A tab after two spaces reaches the next tab stop rather than adding a whole width.
    [InlineData("  \t|abc", "<lt><lt>", "|abc")]
    [InlineData("  \t|abc", ">>", "        |abc")]
    public void WithExpandTab_TabsAlreadyThereAreRewrittenAsSpaces(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData(2, "|abc", ">>", "  |abc")]
    [InlineData(2, "    |abc", "<lt><lt>", "  |abc")]
    [InlineData(8, "|abc", ">>", "        |abc")]
    [InlineData(3, "|abc", "2>>", "   |abc")]
    public void TheShiftWidthOption_IsHowFarALineMoves(int width, string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys, new VimOptions { ShiftWidth = width }).Text);

    [Fact]
    public void TheEngineKeepsTheOptionsItWasGiven()
    {
        FakeVimBuffer buffer = VimScript.NewBuffer("|abc");
        VimOptions options = new() { ShiftWidth = 2 };
        VimEngine engine = new(buffer, options);

        Assert.Same(options, engine.Options);

        // Changed after the fact, the change is what the next shift reads: this is how the editor
        // seeds them and how ':set' will edit them.
        options.ShiftWidth = 3;
        engine.HandleKey(VimKey.Typed('>'));
        engine.HandleKey(VimKey.Typed('>'));

        Assert.Equal("   abc", buffer.Text);
    }

    [Fact]
    public void AnEngineWithNoOptions_UsesTheDefaults()
    {
        VimEngine engine = new(new FakeVimBuffer("abc"));

        Assert.Equal(4, engine.Options.ShiftWidth);
        Assert.True(engine.Options.ExpandTab);
    }

    [Fact]
    public void AShift_LeavesTheRegisterAlone()
    {
        VimRun run = VimScript.Run("|abc\ndef", "yy>>");

        Assert.Equal("    |abc\ndef", run.Text);
        Assert.Equal(VimRegister.Lines("abc"), run.Register);
    }

    [Fact]
    public void AShift_CostsOneUndoStepWhateverTheCount()
    {
        VimRun run = VimScript.Run("|a\nb\nc", "3>>");

        Assert.Equal(1, run.Buffer.UndoSteps);
        Assert.Null(run.Status);
        Assert.Equal("|a\nb\nc", VimScript.Run("|a\nb\nc", "3>>u").Text);
    }

    [Fact]
    public void AShiftOnTheLastLine_HasNoLineBreakToPreserveAndNeedsNone()
        => Assert.Equal("a\n    |b", VimScript.Run("a\n|b", ">>").Text);

    [Fact]
    public void Escape_CancelsAHalfTypedShift()
    {
        VimRun run = VimScript.Run("|abc", "><Esc>");

        Assert.False(run.Engine.HasPendingSequence);
        Assert.Equal("|abc", run.Text);
    }
}
