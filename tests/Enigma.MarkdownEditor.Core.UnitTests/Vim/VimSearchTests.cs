using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// <c>/</c>, <c>n</c> and <c>N</c>: where they land, when they go round the end of the buffer, and
/// what they say about it.
/// </summary>
/// <remarks>
/// <para>
/// The v1 shape of a search, which the regular expression underneath has to keep: a pattern of plain
/// characters is still found where it stands, case-sensitive as typed, inside a longer word, spaces
/// included. What the expression adds — <c>.</c>, <c>*</c>, the anchors, <c>\c</c>, the options —
/// is <see cref="VimRegexSearchTests"/>' business, and a search behind an operator or over a
/// selection is <see cref="VimSearchMotionTests"/>'.
/// </para>
/// <para>
/// The wrap is the other half. It is on by default, the caret's own match is skipped on the way out
/// and found on the way round, and both of those are things a user only ever learns from the message
/// — so the message is asserted with the position in nearly every case.
/// </para>
/// </remarks>
public sealed class VimSearchTests
{
    private const string HitBottom = "search hit BOTTOM, continuing at TOP";
    private const string HitTop = "search hit TOP, continuing at BOTTOM";

    [Theory]
    [InlineData("|foo bar foo", "/bar<CR>", "foo |bar foo")]
    [InlineData("|foo bar foo", "/foo<CR>", "foo bar |foo")]
    [InlineData("|abc\ndef\nghi", "/ghi<CR>", "abc\ndef\n|ghi")]
    // A substring, not a word: the caret lands inside 'xxfooxx'.
    [InlineData("|xxfooxx", "/foo<CR>", "xx|fooxx")]
    // Case-sensitive as typed, so the capitalised one is stepped over.
    [InlineData("|Foo foo", "/foo<CR>", "Foo |foo")]
    // Several characters, spaces included: everything up to Enter is the pattern.
    [InlineData("|one two three", "/two th<CR>", "one |two three")]
    public void ASearch_MovesTheCaretToTheFirstMatchAhead(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Null(run.Status);
        Assert.Null(run.CommandLine);
        Assert.Equal(VimMode.Normal, run.Mode);
    }

    [Fact]
    public void ASearchWithNoMatchAhead_GoesRoundAndSaysSo()
    {
        VimRun run = VimScript.Run("foo bar |foo", "/foo<CR>");

        Assert.Equal("|foo bar foo", run.Text);
        Assert.Equal(HitBottom, run.Status);
    }

    [Fact]
    public void SearchingForTheWordTheCaretIsOn_FindsItAgainRatherThanNothing()
    {
        // The only match is the one under the caret: skipped on the way out, found on the way round.
        VimRun run = VimScript.Run("|foo bar", "/foo<CR>");

        Assert.Equal("|foo bar", run.Text);
        Assert.Equal(HitBottom, run.Status);
    }

    [Theory]
    [InlineData("|foo bar", "/zap<CR>", "Pattern not found: zap")]
    [InlineData("|", "/foo<CR>", "Pattern not found: foo")]
    // Case matters by default, so a capital that is not in the text is not in the text.
    [InlineData("|foo bar", "/Foo<CR>", "Pattern not found: Foo")]
    public void APatternThatIsNowhere_LeavesTheCaretAndSaysSo(string before, string keys, string message)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(before, run.Text);
        Assert.Equal(message, run.Status);
    }

    [Theory]
    [InlineData("|foo bar foo", "/foo<CR>n", "|foo bar foo", HitBottom)]
    [InlineData("|foo bar foo", "/foo<CR>N", "|foo bar foo", null)]
    [InlineData("|foo bar foo", "/foo<CR>NN", "foo bar |foo", HitTop)]
    [InlineData("|foo foo foo foo", "/foo<CR>nn", "foo foo foo |foo", null)]
    public void NAndCapitalN_RepeatTheSearchForwardsAndBackwards(
        string before,
        string keys,
        string after,
        string? status)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(status, run.Status);
    }

    [Theory]
    [InlineData("|foo foo foo foo", "/foo<CR>2n", "foo foo foo |foo")]
    // Three steps from the second match is round the end of the buffer and back to the first.
    [InlineData("|foo foo foo foo", "/foo<CR>3n", "|foo foo foo foo")]
    [InlineData("|foo foo foo foo", "/foo<CR>nn2N", "foo |foo foo foo")]
    public void ACount_WalksThatManyMatchesAlong(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void ACountThatRunsOutOfMatches_LeavesTheCaretWhereItStarted()
    {
        VimRun run = VimScript.Run("|abc", "/z<CR>3n");

        Assert.Equal("|abc", run.Text);
        Assert.Equal("Pattern not found: z", run.Status);
    }

    [Fact]
    public void ACountedRepeatThatGoesRoundOnTheWay_StillSaysSo()
    {
        // Three matches and three steps from the first: the third goes round the end.
        VimRun run = VimScript.Run("|foo foo foo", "/foo<CR>2n");

        Assert.Equal("|foo foo foo", run.Text);
        Assert.Equal(HitBottom, run.Status);
    }

    [Theory]
    [InlineData("n")]
    [InlineData("N")]
    [InlineData("/<CR>")]
    public void RepeatingASearchThatWasNeverMade_SaysThereIsNothingToRepeat(string keys)
    {
        VimRun run = VimScript.Run("|foo bar", keys);

        Assert.Equal("|foo bar", run.Text);
        Assert.Equal("No previous search pattern", run.Status);
    }

    [Fact]
    public void AnEmptySearchLine_RepeatsTheLastPattern()
    {
        VimRun run = VimScript.Run("|foo bar foo", "/foo<CR>/<CR>");

        Assert.Equal("|foo bar foo", run.Text);
        Assert.Equal(HitBottom, run.Status);
    }

    [Fact]
    public void ARepeatAfterAnEdit_SearchesTheTextAsItIsNow()
    {
        // The pattern is kept, not the matches: 'bar' was nowhere when it was typed, and 'n' finds
        // the one that has been typed in since.
        VimRun run = VimScript.Run("|foo", "/bar<CR>Abar<Esc>n");

        Assert.Equal("foo|bar", run.Text);
        Assert.Equal(HitBottom, run.Status);
    }

    [Fact]
    public void ARepeatAfterADelete_DoesNotLandOnAnOffsetThatIsGone()
    {
        // 'bar' sat at offset 4 when it was found. The delete moves it to 0, and 'n' has to find it
        // there rather than walk back to where it used to be.
        VimRun run = VimScript.Run("|foo bar", "/bar<CR>0dwn");

        Assert.Equal("|bar", run.Text);
        Assert.Equal(HitBottom, run.Status);
    }

    [Fact]
    public void ASearch_ChangesNothingInTheDocument()
    {
        VimRun run = VimScript.Run("|foo bar foo", "/bar<CR>nN");

        Assert.Equal("foo bar foo", run.Buffer.Text);
        Assert.Equal(0, run.Buffer.UndoSteps);
    }

    [Fact]
    public void ASearch_BringsTheCaretIntoView()
    {
        FakeVimBuffer buffer = VimScript.NewBuffer("|foo\n\n\n\nbar");
        VimEngine engine = new(buffer);

        foreach (char key in "/bar")
        {
            engine.HandleKey(VimKey.Typed(key));
        }

        // Counted from after the line was typed, because opening it asked for a scroll of its own.
        int before = buffer.ScrollRequests;
        engine.HandleKey(VimKey.Named(VimSpecialKey.Enter));

        Assert.True(buffer.ScrollRequests > before);
    }

    [Fact]
    public void AMatchAtTheEndOfALine_IsAPlaceTheCaretIsAllowedToBe()
    {
        VimRun run = VimScript.Run("|abc\ndef", "/c<CR>");

        Assert.Equal("ab|c\ndef", run.Text);
    }

    [Fact]
    public void AfterASearch_JAndKStartFromTheColumnItLandedIn()
    {
        // '$' leaves behind a column of "the end of the line", which is what makes j and k walk down
        // the line ends. A search has to forget it, or the 'j' after one would jump to the end of the
        // line below instead of straight down from the match.
        VimRun run = VimScript.Run("|abcdef\nabcdef\nabcdef", "$/cd<CR>j");

        Assert.Equal("abcdef\nabcdef\nab|cdef", run.Text);
    }

    [Fact]
    public void ASearchThatGoesRound_ThenOneThatDoesNot_ClearsTheMessage()
    {
        VimRun run = VimScript.Run("|foo bar foo", "/foo<CR>n/bar<CR>");

        Assert.Equal("foo |bar foo", run.Text);
        Assert.Null(run.Status);
    }

    [Fact]
    public void TheSearchPattern_SurvivesAReset()
    {
        // What the editor calls on a tab switch. Vim's search pattern is not the buffer's, so 'n' in
        // the next document finds what '/' looked for in the last one.
        FakeVimBuffer buffer = VimScript.NewBuffer("|foo bar foo");
        VimEngine engine = new(buffer);

        foreach (VimKey key in VimKeyNotation.Parse("/bar<CR>"))
        {
            engine.HandleKey(key);
        }

        engine.Reset();
        buffer.CaretOffset = 0;
        engine.HandleKey(VimKey.Typed('n'));

        Assert.Equal("foo |bar foo", VimScript.Render(buffer));
    }
}
