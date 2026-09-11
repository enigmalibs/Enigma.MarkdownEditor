using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// The character-find family: <c>f</c>, <c>F</c>, <c>t</c>, <c>T</c> and the two keys that repeat
/// them, as motions, behind an operator and in a visual mode.
/// </summary>
/// <remarks>
/// The rule most of these are really testing is that a find is a jump within one line. Everything
/// else about the family follows from it: why it is worth having next to <c>/</c>, why a count that
/// overshoots does nothing at all, and why a miss is silent.
/// </remarks>
public sealed class VimFindTests
{
    [Theory]
    [InlineData("|foo bar", "fb", "foo |bar")]
    [InlineData("|a.b.c", "f.", "a|.b.c")]
    [InlineData("|a.b.c", "2f.", "a.b|.c")]
    [InlineData("|xaxaxaxa", "3fa", "xaxax|axa")]
    // The scan starts one past the caret, so the character it is already sitting on is not a match.
    [InlineData("|aaa", "fa", "a|aa")]
    public void F_GoesForwardOntoTheCharacter(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("foo |bar", "Fo", "fo|o bar")]
    [InlineData("a.b.|c", "F.", "a.b|.c")]
    [InlineData("a.b.|c", "2F.", "a|.b.c")]
    [InlineData("aaa|", "Fa", "a|aa")]
    public void CapitalF_GoesBackwardOntoTheCharacter(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("|foo bar", "tb", "foo| bar")]
    [InlineData("|a.b.c", "2t.", "a.|b.c")]
    // The character is already next to the caret, so stopping short of it is standing still.
    [InlineData("|a.b.c", "t.", "|a.b.c")]
    public void T_StopsOneShortOfTheCharacter(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("foo b|ar", "To", "foo| bar")]
    [InlineData("a.b.|c", "2T.", "a.|b.c")]
    [InlineData("a.b.|c", "T.", "a.b.|c")]
    public void CapitalT_StopsOneShortGoingBackwards(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // The character is on the next line, which is exactly where a find does not look.
    [InlineData("|abc\nxyz", "fz", "|abc\nxyz")]
    [InlineData("|abc\nxyz", "tz", "|abc\nxyz")]
    [InlineData("abc\nx|yz", "Fa", "abc\nx|yz")]
    [InlineData("abc\nx|yz", "Ta", "abc\nx|yz")]
    // A count buys no way across either: the second '.' is on the line below.
    [InlineData("|a.b\nc.d", "2f.", "|a.b\nc.d")]
    public void AFind_NeverCrossesALineBreak(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("|abc", "fz")]
    [InlineData("|abc", "Fz")]
    [InlineData("|abc", "tz")]
    [InlineData("|abc", "Tz")]
    // Three dots on the line and a count asking for a fourth: vim settles for none of them rather
    // than for the third, which is what makes a count safe to type without looking.
    [InlineData("|a.b.c.d", "4f.")]
    public void AFindThatMatchesNothing_SaysNothingAndChangesNothing(string before, string keys)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(before, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.Null(run.Status);
        Assert.False(run.Engine.HasPendingSequence);
    }

    [Theory]
    [InlineData("|a.b.c.d", "f.;", "a.b|.c.d")]
    [InlineData("|a.b.c.d", "f.;;", "a.b.c|.d")]
    [InlineData("|a.b.c.d", "f.2;", "a.b.c|.d")]
    [InlineData("a.b.c.|d", "F.;", "a.b|.c.d")]
    // The repeat runs out where the line does, and leaves the caret on the last one that worked.
    [InlineData("|a.b", "f.;", "a|.b")]
    public void Semicolon_RepeatsTheLastFindTheSameWayRound(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // After 't' the caret is already next to the match, so a repeat has to step over it first —
    // otherwise it would find the same character again and go nowhere, for as long as the key is held.
    [InlineData("|a.b.c.d", "t.;", "a.|b.c.d")]
    [InlineData("|a.b.c.d", "t.;;", "a.b.|c.d")]
    [InlineData("a.b.c.|d", "T.;", "a.b.|c.d")]
    // The same step, mirrored: a ',' that resolves to a backward till starts one character earlier.
    [InlineData("a.b.|c.d", "t.,", "a.|b.c.d")]
    public void ARepeatedTill_MakesProgressRatherThanStandingStill(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("|a.b.c.d", "f.f.,", "a|.b.c.d")]
    [InlineData("|a.b.c.d", "f.f.f.,", "a.b|.c.d")]
    [InlineData("a.b.c.|d", "F.F.,", "a.b.c|.d")]
    public void Comma_RepeatsTheLastFindTheOtherWayRound(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void Comma_ReversesAFindWithoutBecomingOne()
    {
        // Two commas that find nothing, and then a ';' that still goes forwards: ',' reads the
        // memory the other way round and never writes it.
        Assert.Equal("a.b|.c.d", VimScript.Run("|a.b.c.d", "f.,,;").Text);
    }

    [Theory]
    [InlineData(";")]
    [InlineData(",")]
    [InlineData("2;")]
    public void SemicolonOrCommaBeforeAnyFind_DoesNothingAndSaysNothing(string keys)
    {
        VimRun run = VimScript.Run("a|.b.c", keys);

        // Not the "No previous search pattern" answer 'n' gets: '/' is a mode the user visibly
        // entered and may think is still loaded, and ';' has no such surface to be wrong about.
        Assert.Equal("a|.b.c", run.Text);
        Assert.Null(run.Status);
        Assert.Equal(VimMode.Normal, run.Mode);
    }

    [Theory]
    // Forward finds are inclusive: 'df,' takes the comma, 'dt,' stops on the character before it.
    [InlineData("|a,b,c", "df,", "|b,c")]
    [InlineData("|a,b,c", "dt,", "|,b,c")]
    [InlineData("|a,b,c", "d2f,", "|c")]
    // Backward ones leave the character the caret was on and take the target with them.
    [InlineData("a,b,|c", "dF,", "a,b|c")]
    [InlineData("a,b,c|d", "dT,", "a,b,|d")]
    [InlineData("a,b,|c", "d2F,", "a|c")]
    public void AnOperatorOverAFind_CoversWhatTheDirectionSays(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("|a,b,c", "yf,", "a,", "|a,b,c")]
    [InlineData("|a,b,c", "yt,", "a", "|a,b,c")]
    // A yank leaves the caret at the start of what it took, which for a backward find is the target.
    [InlineData("a,b,|c", "yF,", ",", "a,b|,c")]
    public void AYankOverAFind_FillsTheRegisterWithWhatTheFindCovered(
        string before,
        string keys,
        string text,
        string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(new VimRegister(text, VimRegisterKind.Characterwise), run.Register);
        Assert.Equal(after, run.Text);
    }

    [Theory]
    [InlineData("|a,b,c", "cf,", "|b,c")]
    [InlineData("|a,b,c", "ct,", "|,b,c")]
    public void AChangeOverAFind_TakesTheTextAndEntersInsertMode(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Insert, run.Mode);
    }

    [Theory]
    [InlineData("|a,b,c", "df.")]
    [InlineData("|a,b,c", "d3f,")]
    [InlineData("|a,b,c", "d;")]
    public void AnOperatorOverAFindThatMissed_IsAbandonedWithoutAWord(string before, string keys)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(before, run.Text);
        Assert.Null(run.Register);
        Assert.Null(run.Status);
        Assert.Equal(VimMode.Normal, run.Mode);
    }

    [Fact]
    public void AFindBehindAnOperator_IsStillWhatSemicolonRepeats()
    {
        // 'df,' deletes through the first comma; the ';' that follows repeats the find on what is
        // left, because the memory is the find's and not the operator's.
        Assert.Equal("b|,c", VimScript.Run("|a,b,c", "df,;").Text);
    }

    [Theory]
    // Both counts multiply, exactly as they do for every other motion: '2d3f-' is 'd6f-'.
    [InlineData("|a-b-c-d-e-f-g", "2d3f-", "|g")]
    [InlineData("|a-b-c-d-e-f-g", "3f-", "a-b-c|-d-e-f-g")]
    [InlineData("|a-b-c-d-e-f-g", "d2t-", "|-c-d-e-f-g")]
    public void ACountOnEitherSide_CountsOccurrences(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("|a.b.c", "vf.", "[a|.]b.c")]
    [InlineData("|a.b.c", "vt.", "[|a].b.c")]
    [InlineData("a.b.|c", "vF.", "a.b[|.c]")]
    [InlineData("|a.b.c", "vf.;", "[a.b|.]c")]
    public void AFindInAVisualMode_MovesTheEndOfTheSelection(string before, string keys, string highlight)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(highlight, run.Highlight);
        Assert.Equal(VimMode.Visual, run.Mode);
    }

    [Fact]
    public void AHalfTypedFind_IsPendingUntilItsCharacterArrives()
    {
        VimRun run = VimScript.Run("|a.b.c", "df");

        Assert.True(run.Engine.HasPendingSequence);
        Assert.Equal("|a.b.c", run.Text);
    }

    [Fact]
    public void Esc_CancelsAHalfTypedFind()
    {
        VimRun run = VimScript.Run("|a.b.c", "df<Esc>");

        Assert.False(run.Engine.HasPendingSequence);
        Assert.Equal("|a.b.c", run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
    }

    [Fact]
    public void TheCharacterAfterAFind_IsTakenLiterallyRatherThanAsACommand()
    {
        // 'fd' looks for a 'd'; it does not start a delete, the same way 'rd' writes one.
        Assert.Equal("abc |def", VimScript.Run("|abc def", "fd").Text);
        Assert.Equal("abc| def", VimScript.Run("|abc def", "td").Text);
        Assert.Equal("x|yz", VimScript.Run("|xyz", "fy").Text);
    }

    [Fact]
    public void ANamedKeyWhereAFindsCharacterShouldBe_KillsTheSequence()
    {
        KeySequenceParser parser = new();
        parser.Feed(VimKey.Typed('f'));

        VimParseResult result = parser.Feed(VimKey.Named(VimSpecialKey.Left));

        Assert.Equal(VimParseStatus.Invalid, result.Status);
        Assert.False(parser.HasPendingSequence);
    }

    [Fact]
    public void AFindOpener_IsPendingAndSaysWhatHasBeenTyped()
    {
        KeySequenceParser parser = new();

        VimParseResult result = parser.Feed(VimKey.Typed('t'));

        Assert.Equal(VimParseStatus.Pending, result.Status);
        Assert.True(parser.HasPendingSequence);
        Assert.Equal("t", parser.PendingSequence);
    }

    [Fact]
    public void AFindBehindAnOperator_ParsesAsThatOperatorOverIt()
    {
        KeySequenceParser parser = new();
        parser.Feed(VimKey.Typed('d'));
        parser.Feed(VimKey.Typed('f'));

        VimParseResult result = parser.Feed(VimKey.Typed(','));

        Assert.Equal(VimParseStatus.Complete, result.Status);
        Assert.Equal(
            new VimCommand(VimAction.Operate, VimMotion.FindForward, 0, VimOperator.Delete, ','),
            result.Command);
    }

    [Fact]
    public void TheLastFind_SurvivesAReset()
    {
        FakeVimBuffer buffer = VimScript.NewBuffer("|a.b.c");
        VimEngine engine = new(buffer);

        engine.HandleKey(VimKey.Typed('f'));
        engine.HandleKey(VimKey.Typed('.'));

        // What the editor calls when another document is swapped into the control. Vim's own find
        // outlives the buffer it was typed over, the same way the register and the pattern do.
        engine.Reset();

        engine.HandleKey(VimKey.Typed(';'));

        Assert.Equal("a.b|.c", VimScript.Render(buffer));
    }

    [Fact]
    public void AFindThatMissed_IsStillWhatSemicolonRepeats()
    {
        FakeVimBuffer buffer = VimScript.NewBuffer("|abc\nxzy");
        VimEngine engine = new(buffer);

        // No 'z' on this line, so the find goes nowhere — but it is remembered anyway, which is what
        // makes the ';' on the line below the useful key it is.
        engine.HandleKey(VimKey.Typed('f'));
        engine.HandleKey(VimKey.Typed('z'));

        Assert.Equal("|abc\nxzy", VimScript.Render(buffer));

        engine.HandleKey(VimKey.Typed('j'));
        engine.HandleKey(VimKey.Typed(';'));

        Assert.Equal("abc\nx|zy", VimScript.Render(buffer));
    }
}
