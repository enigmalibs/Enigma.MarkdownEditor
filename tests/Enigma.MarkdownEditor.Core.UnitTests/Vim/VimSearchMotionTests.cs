using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// A search as a motion: behind an operator, over a selection, and repeated by <c>.</c>.
/// </summary>
/// <remarks>
/// <para>
/// The command line arrives late — <c>d/foo</c> is a <c>d</c> waiting on a motion whose target is
/// still being typed — so the cases here are about what carries across that gap: the operator, the
/// count, the register, and the selection that was on screen when <c>/</c> was pressed. And about
/// what does not: <c>Esc</c> on the line abandons the operator with it.
/// </para>
/// <para>
/// The range is exclusive in both directions, as vim's is, and follows the exclusive-linewise rule
/// the paragraph motions follow, so <c>d/^#</c> from the first column takes whole lines. Each of
/// those is a case here, because each is the difference between a delete that stops where the user
/// expects and one that takes a line break with it.
/// </para>
/// </remarks>
public sealed class VimSearchMotionTests
{
    [Theory]
    // Exclusive: up to the match and not including it.
    [InlineData("|foo bar", "d/bar<CR>", "|bar")]
    [InlineData("|abc\nxbar", "d/bar<CR>", "|bar")]
    // Backwards: from the match up to but not including the caret's own character, like 'dF'.
    [InlineData("foo bar |x", "d?foo<CR>", "|x")]
    [InlineData("foo bar |x", "d?bar<CR>", "foo |x")]
    // 'n', 'N', '*' and '#' behind an operator.
    [InlineData("|foo bar foo bar", "/bar<CR>0dn", "|bar foo bar")]
    [InlineData("foo bar foo |bar", "/bar<CR>$dN", "foo bar foo |r")]
    [InlineData("|foo bar foo", "d*", "|foo")]
    [InlineData("foo bar |foo", "d#", "|foo")]
    // A count lands on that match.
    [InlineData("|x foo foo foo", "d2/foo<CR>", "|foo foo")]
    [InlineData("|x foo foo foo", "2d/foo<CR>", "|foo foo")]
    public void AnOperatorOverASearch_ActsUpToTheMatch(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.Null(run.CommandLine);
    }

    [Fact]
    public void ADeleteToASearch_FillsTheRegisterAndIsOneUndoStep()
    {
        VimRun run = VimScript.Run("|foo bar", "d/bar<CR>");

        Assert.Equal("foo ", run.Register?.Text);
        Assert.Equal(VimRegisterKind.Characterwise, run.Register?.Kind);
        Assert.Equal(1, run.Buffer.UndoSteps);
    }

    [Fact]
    public void AYankToASearch_LeavesTheTextAndMovesToTheStart()
    {
        VimRun run = VimScript.Run("foo bar |x", "y?foo<CR>");

        Assert.Equal("|foo bar x", run.Text);
        Assert.Equal("foo bar ", run.Register?.Text);
        Assert.Equal(0, run.Buffer.UndoSteps);
    }

    [Fact]
    public void AChangeToASearch_EntersInsertModeWhereTheTextWas()
    {
        VimRun run = VimScript.Run("|foo bar", "c/bar<CR>X<Esc>");

        Assert.Equal("|Xbar", run.Text);
        Assert.Equal(1, run.Buffer.UndoSteps);
    }

    [Theory]
    // Vim's exclusive-linewise rule. A match in the first column of a later line, from at or before
    // the first non-blank: whole lines, and the line the match is on is left alone.
    [InlineData("|abc\ndef\nxyz", "d/^x<CR>", "|xyz")]
    // From mid-line: to the end of the line before the match, and the line break stays.
    [InlineData("a|bc\ndef\nxyz", "d/^x<CR>", "|a\nxyz")]
    public void ASearchThatLandsInTheFirstColumn_FollowsTheExclusiveLinewiseRule(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void ALinewiseDeleteToASearch_FillsTheRegisterWithLines()
    {
        VimRun run = VimScript.Run("|abc\ndef\nxyz", "d/^x<CR>");

        Assert.Equal(VimRegisterKind.Linewise, run.Register?.Kind);
        Assert.Equal("abc\ndef", run.Register?.Text);
    }

    [Fact]
    public void ARegisterPrefix_RidesAcrossTheCommandLine()
    {
        VimRun run = VimScript.Run("|foo bar", "\"ad/bar<CR>");

        Assert.Equal("|bar", run.Text);
        Assert.Equal("foo ", run.RegisterNamed('a')?.Text);
    }

    [Theory]
    // '.' runs the same search again from where the caret is now — vim's rule, and what makes
    // 'd/,' then '.' walk through a list.
    [InlineData("|a1 a2 a3 a4", "d/a<CR>.", "|a3 a4")]
    [InlineData("|a1 a2 a3 a4", "d/a<CR>..", "|a4")]
    [InlineData("|foo x foo x foo", "/foo<CR>0dn.", "|foo")]
    [InlineData("|foo x foo x foo", "d*.", "|foo")]
    // With a new count.
    [InlineData("|a1 a2 a3 a4 a5", "d/a<CR>2.", "|a4 a5")]
    public void Dot_RepeatsAnOperatorOverASearch(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void Dot_RepeatsAChangeToASearchWithItsText()
    {
        VimRun run = VimScript.Run("|a1 a2 a3", "c/a<CR>x<Esc>w.");

        // 'c/a' from the first 'a1' changes 'a1 ' to 'x'; 'w' moves onto 'a2'; '.' changes 'a2 ' to 'x'.
        Assert.Equal("x|xa3", run.Text);
    }

    [Fact]
    public void ASearchThatFindsNothing_LeavesTheOperatorUndone()
    {
        VimRun run = VimScript.Run("|foo bar", "d/zap<CR>");

        Assert.Equal("|foo bar", run.Text);
        Assert.Equal("Pattern not found: zap", run.Status);
        Assert.Null(run.Register);
        Assert.Equal(0, run.Buffer.UndoSteps);
    }

    [Fact]
    public void AnOperatorThenASlash_OpensTheCommandLine()
    {
        VimRun run = VimScript.Run("|foo bar", "d/ba");

        Assert.Equal("/ba", run.CommandLine);
        Assert.True(run.Engine.HasPendingSequence);
        Assert.Equal("foo bar", run.Buffer.Text);
    }

    [Fact]
    public void EscapeOnTheLine_AbandonsTheOperatorWithIt()
    {
        // The 'd' does not survive the Esc: 'x' afterwards is a plain 'x', not the motion 'd' was
        // waiting for.
        VimRun run = VimScript.Run("|foo bar", "d/ba<Esc>x");

        Assert.Equal("|oo bar", run.Text);
        Assert.Null(run.CommandLine);
        Assert.False(run.Engine.HasPendingSequence);
    }

    [Theory]
    // In a visual mode the search moves the caret, and the selection follows it.
    [InlineData("|foo bar", "v/bar<CR>", "[foo |b]ar", "foo b")]
    [InlineData("foo bar |x", "v?foo<CR>", "[|foo bar x]", "foo bar x")]
    [InlineData("|foo bar foo", "/bar<CR>0vn", "[foo |b]ar foo", "foo b")]
    [InlineData("|foo bar foo", "/foo<CR>vN", "[|foo bar f]oo", "foo bar f")]
    [InlineData("|foo bar foo", "v*", "[foo bar |f]oo", "foo bar f")]
    [InlineData("foo bar |foo", "v#", "[|foo bar f]oo", "foo bar f")]
    public void InAVisualMode_ASearchExtendsTheSelection(string before, string keys, string highlight, string selected)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(highlight, run.Highlight);
        Assert.Equal(selected, run.SelectedText);
        Assert.Equal(VimMode.Visual, run.Mode);
    }

    [Fact]
    public void InLinewiseVisual_ASearchExtendsTheSelectionByLines()
    {
        VimRun run = VimScript.Run("|abc\nbar\nxyz", "V/bar<CR>");

        Assert.Equal(VimMode.VisualLine, run.Mode);
        Assert.Equal("abc\nbar\n", run.SelectedText);
    }

    [Fact]
    public void InAVisualMode_TheSelectionStaysWhileTheLineIsTyped()
    {
        VimRun run = VimScript.Run("|foo bar", "v/ba");

        Assert.Equal("/ba", run.CommandLine);
        Assert.Equal(VimMode.Visual, run.Mode);
        Assert.Equal("f", run.SelectedText);
    }

    [Fact]
    public void InAVisualMode_EscapeOnTheLine_KeepsTheSelection()
    {
        VimRun run = VimScript.Run("|foo bar", "vl/ba<Esc>");

        Assert.Null(run.CommandLine);
        Assert.Equal(VimMode.Visual, run.Mode);
        Assert.Equal("fo", run.SelectedText);
    }

    [Fact]
    public void InAVisualMode_ASearchThatFindsNothing_KeepsTheSelectionAndSaysSo()
    {
        VimRun run = VimScript.Run("|foo bar", "vl/zap<CR>");

        Assert.Equal("Pattern not found: zap", run.Status);
        Assert.Equal(VimMode.Visual, run.Mode);
        Assert.Equal("fo", run.SelectedText);
    }

    [Fact]
    public void ASelectionExtendedByASearch_IsWhatTheOperatorActsOn()
        => Assert.Equal("|ar", VimScript.Run("|foo bar", "v/bar<CR>d").Text);

    [Fact]
    public void ASearchWhileSelecting_IsWhatDotRemembersAsTheExtent()
    {
        // 'v/,' then 'd' took five characters; '.' takes five more from the caret.
        Assert.Equal("|c", VimScript.Run("|a1, b2, c", "v/,<CR>ld.").Text);
    }
}
