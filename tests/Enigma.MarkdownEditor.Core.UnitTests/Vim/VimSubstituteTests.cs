using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// <c>:s</c>: the range, the delimiter, the flags, the replacement's escapes, the message, and where
/// the caret lands.
/// </summary>
/// <remarks>
/// <para>
/// The range is where the grammar is, so one buffer of four identical lines with the caret on the
/// third is run through every address form, and the answer is read off which lines changed. The
/// replacement is .NET's, and the cases spell the difference from vim's out — <c>$1</c> rather than
/// <c>\1</c>, and an <c>&amp;</c> that is only an ampersand.
/// </para>
/// <para>
/// One write, one undo step, and the caret on the first non-blank of the last line that changed are
/// vim's rules and are asserted in nearly every case, because they are what a user notices after the
/// substitution has run.
/// </para>
/// </remarks>
public sealed class VimSubstituteTests
{
    private const string FourLines = "a\na\n|a\na";

    [Fact]
    public void ASubstitute_ReplacesTheFirstMatchOnTheLine()
    {
        VimRun run = VimScript.Run("|a a", ":s/a/b/<CR>");

        Assert.Equal("|b a", run.Text);
        Assert.Equal("1 substitution on 1 line", run.Status);
        Assert.Null(run.CommandLine);
        Assert.Equal(VimMode.Normal, run.Mode);
    }

    [Fact]
    public void TheGFlag_ReplacesEveryMatchOnTheLine()
    {
        VimRun run = VimScript.Run("|a a a", ":s/a/b/g<CR>");

        Assert.Equal("|b b b", run.Text);
        Assert.Equal("3 substitutions on 1 line", run.Status);
    }

    [Theory]
    [InlineData(":2s/a/b/<CR>", "a\n|b\na\na")]
    [InlineData(":1,2s/a/b/<CR>", "b\n|b\na\na")]
    [InlineData(":.s/a/b/<CR>", "a\na\n|b\na")]
    [InlineData(":.,$s/a/b/<CR>", "a\na\nb\n|b")]
    [InlineData(":$s/a/b/<CR>", "a\na\na\n|b")]
    [InlineData(":.,+1s/a/b/<CR>", "a\na\nb\n|b")]
    [InlineData(":-2,.s/a/b/<CR>", "b\nb\n|b\na")]
    [InlineData(":+1s/a/b/<CR>", "a\na\na\n|b")]
    [InlineData(":%s/a/b/<CR>", "b\nb\nb\n|b")]
    // Backwards, clamped, and spaced out: the same lines.
    [InlineData(":2,1s/a/b/<CR>", "b\n|b\na\na")]
    [InlineData(":99s/a/b/<CR>", "a\na\na\n|b")]
    [InlineData(":3,99s/a/b/<CR>", "a\na\nb\n|b")]
    [InlineData(": 1 , 2 s/a/b/<CR>", "b\n|b\na\na")]
    public void TheRange_SaysWhichLines(string keys, string after)
    {
        VimRun run = VimScript.Run(FourLines, keys);

        Assert.Equal(after, run.Text);
    }

    [Fact]
    public void TheVisualMarks_NameTheLastSelection()
    {
        // Lines 1 and 2 were selected; the caret is elsewhere when the command runs.
        VimRun run = VimScript.Run("|a\na\na\na", "Vj<Esc>G:'<,'>s/a/b/<CR>");

        Assert.Equal("b\n|b\na\na", run.Text);
        Assert.Equal("2 substitutions on 2 lines", run.Status);
    }

    [Fact]
    public void TheMessage_CountsMatchesAndLines()
    {
        VimRun run = VimScript.Run("|a\na a\nc", ":%s/a/b/g<CR>");

        Assert.Equal("b\n|b b\nc", run.Text);
        Assert.Equal("3 substitutions on 2 lines", run.Status);
    }

    [Theory]
    // The delimiter is whatever non-alphanumeric character follows the 's'.
    [InlineData("|a/b", ":s#a/b#x#<CR>", "|x")]
    [InlineData("|a b", ":s,a,x,<CR>", "|x b")]
    [InlineData("|a b", ":s+a+x+<CR>", "|x b")]
    // A backslash puts the delimiter in the pattern or the replacement.
    [InlineData("|a/b", ":s/a\\/b/x/<CR>", "|x")]
    [InlineData("|a", ":s/a/x\\/y/<CR>", "|x/y")]
    // The closing delimiter is optional, and so is the replacement.
    [InlineData("|a a", ":s/a/b<CR>", "|b a")]
    [InlineData("|a b", ":s/a<CR>", " |b")]
    public void TheDelimiter_IsAnyPunctuation(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("|ab", ":s/(a)(b)/$2$1/<CR>", "|ba")]
    [InlineData("|a", ":s/a/[$0]/<CR>", "|[a]")]
    [InlineData("|a", ":s/a/$$/<CR>", "|$")]
    // '&' is an ampersand, not the match — the one place the replacement is not vim's.
    [InlineData("|a", ":s/a/&/<CR>", "|&")]
    // Vim's escapes: a line break, a tab, a backslash.
    [InlineData("|a b", ":s/ /\\r/<CR>", "a\n|b")]
    [InlineData("|a b", ":s/ /\\t/<CR>", "|a\tb")]
    [InlineData("|a", ":s/a/\\\\/<CR>", "|\\")]
    // Any other backslash is left as it is.
    [InlineData("|a", ":s/a/\\n/<CR>", "|\\n")]
    public void TheReplacement_IsDotNetsWithVimsEscapes(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void ALineBreakInTheReplacement_LeavesTheCaretOnTheLastPiece()
    {
        VimRun run = VimScript.Run("|a b c\nd", ":s/ /\\r/g<CR>");

        Assert.Equal("a\nb\n|c\nd", run.Text);
        Assert.Equal("2 substitutions on 1 line", run.Status);
    }

    [Fact]
    public void TheCaret_LandsOnTheFirstNonBlankOfTheLastLineThatChanged()
    {
        VimRun run = VimScript.Run("|a\n  a\nx", ":%s/a/bb/<CR>");

        Assert.Equal("bb\n  |bb\nx", run.Text);
    }

    [Theory]
    [InlineData("|a", ":s/A/b/i<CR>", "|b", null)]
    [InlineData("|a", ":s/A/b/gi<CR>", "|b", null)]
    // 'I' forces an exact match even with ignorecase on.
    [InlineData("|a", ":s/A/b/I<CR>", "|a", "Pattern not found: A")]
    public void TheCaseFlags_SettleTheCaseForThisCommand(string before, string keys, string after, string? failure)
    {
        VimRun run = VimScript.Run(before, keys, new VimOptions { IgnoreCase = true });

        Assert.Equal(after, run.Text);

        if (failure is not null)
        {
            Assert.Equal(failure, run.Status);
        }
    }

    [Fact]
    public void TheIgnoreCaseOption_AppliesToASubstitute()
        => Assert.Equal("|b", VimScript.Run("|a", ":s/A/b/<CR>", new VimOptions { IgnoreCase = true }).Text);

    [Fact]
    public void ACharacterThatIsNotAFlag_IsNamedAsTrailing()
    {
        VimRun run = VimScript.Run("|a", ":s/a/b/x<CR>");

        Assert.Equal("|a", run.Text);
        Assert.Equal("Trailing characters: x", run.Status);
    }

    [Fact]
    public void AnEmptyPattern_ReusesTheLastSearch()
    {
        VimRun run = VimScript.Run("|x a", "/a<CR>:s//b/<CR>");

        Assert.Equal("|x b", run.Text);
    }

    [Fact]
    public void AnEmptyPatternBeforeAnySearch_HasNothingToReuse()
    {
        VimRun run = VimScript.Run("|x a", ":s//b/<CR>");

        Assert.Equal("|x a", run.Text);
        Assert.Equal("No previous search pattern", run.Status);
    }

    [Fact]
    public void ThePattern_BecomesTheLastSearchPattern()
        => Assert.Equal("b x |a", VimScript.Run("|a x a", ":s/a/b/<CR>n").Text);

    [Fact]
    public void APatternThatMatchesNothing_SaysSoAndIsStillRemembered()
    {
        VimRun run = VimScript.Run("|a", ":s/z/b/<CR>n");

        Assert.Equal("|a", run.Text);
        Assert.Equal("Pattern not found: z", run.Status);
        Assert.Equal(0, run.Buffer.UndoSteps);
    }

    [Fact]
    public void AnInvalidPattern_IsNamed()
    {
        VimRun run = VimScript.Run("|a", ":s/[/b/<CR>");

        Assert.Equal("|a", run.Text);
        Assert.Equal("Invalid pattern: Unterminated [] set.", run.Status);
    }

    [Fact]
    public void ASubstituteOverManyLines_IsOneUndoStep()
    {
        VimRun run = VimScript.Run("|a\na\na", ":%s/a/b/<CR>");

        Assert.Equal(1, run.Buffer.UndoSteps);
        Assert.Equal("b\nb\n|b", run.Text);

        VimRun undone = VimScript.Run("|a\na\na", ":%s/a/b/<CR>u");

        Assert.Equal("a\na\na", undone.Buffer.Text);
    }

    [Fact]
    public void ASubstituteThatChangesNothingInTheMiddle_WritesBackTheLinesBetween()
    {
        // Line 2 has no match and sits between two that do: it is part of the one write and comes
        // back exactly as it was.
        VimRun run = VimScript.Run("|a\nx\na", ":%s/a/b/<CR>");

        Assert.Equal("b\nx\n|b", run.Text);
        Assert.Equal("2 substitutions on 2 lines", run.Status);
    }

    [Theory]
    // The anchors are each line's ends, because the substitute runs a line at a time.
    [InlineData("|a\nb", ":%s/^/- /<CR>", "- a\n|- b")]
    [InlineData("|a\nb", ":%s/$/!/<CR>", "a!\n|b!")]
    // An empty match at every position, as .NET reads it — and as vim does.
    [InlineData("|ab", ":s/x*/-/g<CR>", "|-a-b-")]
    public void TheAnchorsAndEmptyMatches_ReadPerLine(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // A bare ':s' repeats the last substitute in vim, which is '&' and out of scope here; a letter
    // where the delimiter should be is another command's name.
    [InlineData(":s<CR>", "Not an editor command: s")]
    [InlineData(":sxaxbx<CR>", "Not an editor command: sxaxbx")]
    public void ASubstituteWithNoDelimiter_IsNotACommand(string keys, string message)
    {
        VimRun run = VimScript.Run("|a", keys);

        Assert.Equal(message, run.Status);
        Assert.Equal("|a", run.Text);
    }

    [Fact]
    public void TheLongName_IsReadToo()
        => Assert.Equal("|b", VimScript.Run("|a", ":substitute/a/b/<CR>").Text);

    [Fact]
    public void ASubstitute_IsNotWhatDotRepeats()
    {
        // 'x' is the last change, and stays it across the ':s'.
        Assert.Equal("|c", VimScript.Run("|xab", "x:s/b/c/<CR>.").Text);
    }

    [Fact]
    public void ASubstitute_BringsTheCaretIntoView()
    {
        VimRun run = VimScript.Run("|a\n\n\n\na", ":$s/a/b/<CR>");

        Assert.True(run.Buffer.ScrollRequests > 0);
    }
}
