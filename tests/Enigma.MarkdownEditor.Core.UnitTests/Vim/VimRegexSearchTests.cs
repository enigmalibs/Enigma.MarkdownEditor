using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// What the regular expression under <c>/</c> adds: the dialect, the case rules, the direction
/// <c>?</c> and <c>n</c> keep, <c>wrapscan</c>, <c>*</c> and <c>#</c>, and what a pattern that will
/// not compile says.
/// </summary>
/// <remarks>
/// <para>
/// The dialect is .NET's, and the cases here say so by using what vim would spell differently —
/// <c>\b</c>, <c>(a)</c>, <c>{2}</c> — so that a reader who knows vim's <c>\&lt;</c> and <c>\(</c>
/// sees the difference written down rather than discovers it.
/// </para>
/// <para>
/// Case is the one part taken from vim, and it has an order: <c>\c</c> in the pattern beats the
/// options, <c>smartcase</c> only ever turns <c>ignorecase</c> off, and <c>*</c> is exempt from
/// <c>smartcase</c>. Every rule in that order has a case with the options set the other way, because
/// a case rule that is only tested in one direction is half a rule.
/// </para>
/// </remarks>
public sealed class VimRegexSearchTests
{
    private const string HitBottom = "search hit BOTTOM, continuing at TOP";
    private const string HitTop = "search hit TOP, continuing at BOTTOM";

    [Theory]
    [InlineData("|xx foo", "/f.o<CR>", "xx |foo")]
    [InlineData("|xx fo", "/fo*<CR>", "xx |fo")]
    [InlineData("|xx foo", "/[aeiou]{2}<CR>", "xx f|oo")]
    [InlineData("|xx bar foo", "/foo|bar<CR>", "xx |bar foo")]
    // Word boundaries are .NET's '\b', not vim's '\<' — 'there' is stepped over.
    [InlineData("|the there the", "/\\bthe\\b<CR>", "the there |the")]
    // The anchors are a line's ends, as they are in vim, because the expression is compiled multiline.
    [InlineData("|abc\nbar", "/^bar<CR>", "abc\n|bar")]
    [InlineData("|abc\nbar", "/c$<CR>", "ab|c\nbar")]
    // A pattern may cross a line break, which no line-at-a-time search could see.
    [InlineData("|abc\nbar", "/c\\nb<CR>", "ab|c\nbar")]
    public void APattern_IsARegularExpressionInDotNetsDialect(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Null(run.Status);
    }

    [Fact]
    public void MatchesDoNotOverlap_WhichIsVimsRuleToo()
    {
        // Standing on the first 'aa' of 'aaaa', the next 'aa' begins after it, not one character on.
        Assert.Equal("aa|aa", VimScript.Run("|aaaa", "/aa<CR>").Text);
    }

    [Theory]
    // An empty match is stepped over by one character, so a search never stands still.
    [InlineData("|abc", "/x*<CR>", "a|bc")]
    [InlineData("|abc", "/x*<CR>n", "ab|c")]
    [InlineData("|abc\ndef", "/^<CR>", "abc\n|def")]
    [InlineData("|abc def", "/\\b<CR>", "abc| def")]
    public void AnEmptyMatch_StillMovesTheCaret(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("|x foo", "/FOO\\c<CR>", "x |foo")]
    [InlineData("|x foo", "/\\cFOO<CR>", "x |foo")]
    // '\c' wins over '\C' when both are written, as it does in vim.
    [InlineData("|x foo", "/FOO\\c\\C<CR>", "x |foo")]
    public void BackslashC_ForcesIgnoreCaseForThisSearch(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Null(run.Status);
    }

    [Fact]
    public void BackslashCapitalC_ForcesMatchCaseEvenWithIgnoreCaseOn()
    {
        VimRun run = VimScript.Run("|x Foo foo", "/foo\\C<CR>", new VimOptions { IgnoreCase = true });

        Assert.Equal("x Foo |foo", run.Text);
    }

    [Fact]
    public void IgnoreCase_MatchesWhateverTheCaseOfTheText()
        => Assert.Equal("x |Foo", VimScript.Run("|x Foo", "/foo<CR>", new VimOptions { IgnoreCase = true }).Text);

    [Theory]
    // With both on, a capital in the pattern makes the search exact, and its absence leaves it loose.
    [InlineData("|x foo Foo", "/Foo<CR>", "x foo |Foo")]
    [InlineData("|x foo Foo", "/foo<CR>", "x |foo Foo")]
    // A capital inside an escape is a class, not a capital: '\W' does not turn smartcase on.
    [InlineData("|x FOO", "/\\Wfoo<CR>", "x| FOO")]
    public void SmartCase_ReadsACapitalInThePatternAsMeaningIt(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys, new VimOptions { IgnoreCase = true, SmartCase = true });

        Assert.Equal(after, run.Text);
    }

    [Fact]
    public void SmartCase_DoesNothingWhenIgnoreCaseIsOff()
    {
        VimRun run = VimScript.Run("|x Foo foo", "/foo<CR>", new VimOptions { SmartCase = true });

        Assert.Equal("x Foo |foo", run.Text);
    }

    [Fact]
    public void AnInvalidPattern_IsNamedAndLeavesTheCaret()
    {
        VimRun run = VimScript.Run("|foo bar", "/[<CR>");

        Assert.Equal("|foo bar", run.Text);
        Assert.Equal("Invalid pattern: Unterminated [] set.", run.Status);
    }

    [Fact]
    public void AnInvalidPattern_IsNotRemembered()
    {
        // There is nothing 'n' could do with a pattern that does not compile, so it does not become
        // the last search.
        VimRun run = VimScript.Run("|foo bar", "/[<CR>n");

        Assert.Equal("No previous search pattern", run.Status);
    }

    [Fact]
    public void APatternThatRunsOutOfTime_IsReportedRatherThanWaitedFor()
    {
        // The textbook backtracking blow-up, over a string long enough that it cannot finish in a
        // second: nested quantifiers that can share their letters, followed by a character that is
        // not there.
        VimRun run = VimScript.Run("|" + new string('a', 40) + "c", "/(a+)+b<CR>");

        Assert.Equal("Invalid pattern: Match timed out after one second.", run.Status);
        Assert.Equal(0, run.Buffer.CaretOffset);
    }

    [Theory]
    [InlineData("foo bar |x", "?foo<CR>", "|foo bar x", null)]
    [InlineData("foo bar |foo", "?foo<CR>", "|foo bar foo", null)]
    // Nothing behind the caret: round the end, and said so.
    [InlineData("|foo bar", "?bar<CR>", "foo |bar", HitTop)]
    [InlineData("|foo bar", "?foo<CR>", "|foo bar", HitTop)]
    public void QuestionMark_SearchesBackwards(string before, string keys, string after, string? status)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(status, run.Status);
    }

    [Theory]
    // 'n' keeps the direction of the last search and 'N' reverses it, whichever way that was.
    [InlineData("foo foo |x", "?foo<CR>n", "|foo foo x")]
    [InlineData("foo foo |x", "?foo<CR>nN", "foo |foo x")]
    [InlineData("|foo foo foo", "/foo<CR>n", "foo foo |foo")]
    [InlineData("|foo foo foo", "/foo<CR>N", "|foo foo foo")]
    // 'N' does not turn the search round: 'n' after it still goes the original way.
    [InlineData("|foo foo foo", "/foo<CR>Nn", "foo |foo foo")]
    [InlineData("foo foo |x", "?foo<CR>Nn", "foo |foo x")]
    // A bare '/' or '?' reuses the pattern and sets the direction anew.
    [InlineData("foo foo |x", "?foo<CR>n/<CR>", "foo |foo x")]
    [InlineData("|foo foo foo", "/foo<CR>?<CR>", "|foo foo foo")]
    public void NAndCapitalN_FollowTheDirectionOfTheLastSearch(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void ACountOnABackwardSearch_WalksThatManyMatchesBack()
        => Assert.Equal("foo |foo foo x", VimScript.Run("foo foo foo |x", "2?foo<CR>").Text);

    [Fact]
    public void ACountOnASearch_LandsOnThatMatch()
        => Assert.Equal("x foo foo |foo", VimScript.Run("|x foo foo foo", "3/foo<CR>").Text);

    [Theory]
    [InlineData("|foo bar foo", "/foo<CR>n", "foo bar |foo", "search hit BOTTOM without match for: foo")]
    [InlineData("foo bar |foo", "?foo<CR>n", "|foo bar foo", "search hit TOP without match for: foo")]
    // Standing on the only match, with nothing ahead, is the same failure.
    [InlineData("|foo bar", "/foo<CR>", "|foo bar", "search hit BOTTOM without match for: foo")]
    // A count that runs out is the same failure too, and the caret stays where it started.
    [InlineData("|foo foo", "3/foo<CR>", "|foo foo", "search hit BOTTOM without match for: foo")]
    public void WithWrapScanOff_ASearchStopsAtTheEndAndSaysWhichEnd(
        string before,
        string keys,
        string after,
        string message)
    {
        VimRun run = VimScript.Run(before, keys, new VimOptions { WrapScan = false });

        Assert.Equal(after, run.Text);
        Assert.Equal(message, run.Status);
    }

    [Fact]
    public void WithWrapScanOff_APatternThatIsNowhere_IsStillNotFound()
    {
        VimRun run = VimScript.Run("|foo bar", "/zap<CR>", new VimOptions { WrapScan = false });

        Assert.Equal("Pattern not found: zap", run.Status);
    }

    [Fact]
    public void WithWrapScanOff_ASearchThatNeedNotWrap_IsSilent()
    {
        VimRun run = VimScript.Run("|foo bar foo", "/foo<CR>", new VimOptions { WrapScan = false });

        Assert.Equal("foo bar |foo", run.Text);
        Assert.Null(run.Status);
    }

    [Theory]
    [InlineData("|foo bar foo", "*", "foo bar |foo")]
    [InlineData("foo bar |foo", "#", "|foo bar foo")]
    // A whole word: 'there' is not 'the'.
    [InlineData("|the there the", "*", "the there |the")]
    [InlineData("the there |the", "#", "|the there the")]
    // From the middle of the word, and from a blank before it: the word under or after the caret,
    // and the search starts at its first character, so that word itself is the one skipped.
    [InlineData("f|oo bar foo", "*", "foo bar |foo")]
    [InlineData("foo| bar foo bar", "*", "foo bar foo |bar")]
    // A keyword later on the line is preferred to the punctuation under the caret.
    [InlineData("|-> foo -> foo", "*", "-> foo -> |foo")]
    [InlineData("|(a) (a)", "*", "(a) (|a)")]
    // With no keyword left on the line, the run of punctuation under the caret, as it stands.
    [InlineData("x |-> ->", "*", "x -> |->")]
    // A count.
    [InlineData("|foo foo foo foo", "2*", "foo foo |foo foo")]
    public void StarAndHash_SearchForTheWordUnderTheCaret(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Null(run.Status);
    }

    [Fact]
    public void StarWithNothingLeftOnTheLine_DoesNothingWithoutAWord()
    {
        VimRun run = VimScript.Run("abc |  ", "*");

        Assert.Equal("abc |  ", run.Text);
        Assert.Null(run.Status);
    }

    [Fact]
    public void Star_SetsThePatternNRepeats()
        => Assert.Equal("foo foo |foo", VimScript.Run("|foo foo foo", "*n").Text);

    [Fact]
    public void StarOnTheOnlyOccurrence_GoesRoundToItself()
    {
        VimRun run = VimScript.Run("|foo bar", "*");

        Assert.Equal("|foo bar", run.Text);
        Assert.Equal(HitBottom, run.Status);
    }

    [Fact]
    public void Star_IgnoresSmartCase_AsVimDoes()
    {
        // A capital in a word taken off the page was not typed, so it says nothing about what was
        // meant: with ignorecase on, '*' on 'Foo' finds 'foo' — and so does the 'n' after it.
        VimOptions options = new() { IgnoreCase = true, SmartCase = true };

        Assert.Equal("Foo |foo Foo", VimScript.Run("|Foo foo Foo", "*", options).Text);
        Assert.Equal("Foo foo |Foo", VimScript.Run("|Foo foo Foo", "*n", options).Text);

        // Where the same pattern typed after '/' is exact.
        Assert.Equal("Foo foo |Foo", VimScript.Run("|Foo foo Foo", "/Foo<CR>", options).Text);
    }

    [Fact]
    public void ASearchThatFindsNothing_StopsAMacro()
    {
        // The recording deletes the next 'z'. The second replay finds none, and stops there rather
        // than deleting whatever the caret happened to be on.
        VimRun run = VimScript.Run("|az az", "qa/z<CR>xq@a@a");

        Assert.Equal("a |a", run.Text);
        Assert.Equal("Pattern not found: z", run.Status);
    }
}
