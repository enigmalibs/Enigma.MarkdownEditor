using System;
using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// The text objects: <c>iw</c> and <c>aw</c>, the four bracket pairs and the three quote pairs,
/// behind <c>d</c>, <c>y</c> and <c>c</c> and in both visual modes.
/// </summary>
/// <remarks>
/// Most of these are really testing how far an object is allowed to reach, because that is the only
/// thing the three families disagree about: a word stays on its line, a bracket pair goes as far as
/// the buffer does and counts nesting, and a quote pair stays on the line and does not nest at all.
/// The rest of the file is about the one place the grammar collides with itself — <c>i</c> and
/// <c>a</c> are also the insert keys — and about a miss being silent.
/// </remarks>
public sealed class VimTextObjectTests
{
    [Theory]
    [InlineData("foo |bar baz", "diw", "foo | baz")]
    [InlineData("foo b|ar baz", "diw", "foo | baz")]
    [InlineData("|abc", "diw", "|")]
    // On a run of blanks the object is the blanks, which is vim's rule and what makes 'daw' work
    // from either side of a space.
    [InlineData("foo| bar", "diw", "foo|bar")]
    [InlineData("foo|   bar", "diw", "foo|bar")]
    // The caret lands on the last character the line still has, which is the space the word left.
    [InlineData("foo b|ar", "diw", "foo| ")]
    public void Iw_IsTheRunUnderTheCaret(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("foo |bar baz", "daw", "foo |baz")]
    [InlineData("|foo bar", "daw", "|bar")]
    // No blanks after the word, so the ones before it come instead: vim's fallback, and the reason
    // 'daw' on the last word of a line leaves no separator hanging.
    [InlineData("foo b|ar", "daw", "fo|o")]
    // From a run of blanks the pair is the other way round: the blanks and the word after them.
    [InlineData("foo| bar baz", "daw", "foo| baz")]
    // Neither side has blanks at all, because the next class along is punctuation.
    [InlineData("|foo.bar", "daw", "|.bar")]
    public void Aw_TakesTheBlanksBesideTheWordToo(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // The same three classes 'w', 'b' and 'e' use: 'foo.bar' is three words and 'foo_bar' is one.
    [InlineData("|foo.bar", "diw", "|.bar")]
    [InlineData("foo|.bar", "diw", "foo|bar")]
    [InlineData("|foo_bar", "diw", "|")]
    [InlineData("a|++b", "diw", "a|b")]
    public void AWordObject_UsesTheSameThreeClassesAsTheWordMotions(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // 'iw' counts runs, so three of them is word, blanks, word.
    [InlineData("|one two three", "d2iw", "|two three")]
    [InlineData("|one two three", "d3iw", "| three")]
    // 'aw' counts words with the blanks that follow them.
    [InlineData("|one two three", "d2aw", "|three")]
    // A count that overshoots takes what the line has, unlike a find, which takes nothing at all: a
    // word object is bounded by the line either way, so there is no wrong place for it to land.
    [InlineData("|one two", "d9iw", "|")]
    public void ACountOnAWordObject_TakesThatManyOfThem(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("x(a|b)y", "di(", "x(|)y")]
    [InlineData("x(a|b)y", "da(", "x|y")]
    [InlineData("[a|b]", "di[", "[|]")]
    [InlineData("[a|b]", "da[", "|")]
    [InlineData("{a|b}", "di{", "{|}")]
    [InlineData("<a|b>", "di<lt>", "<|>")]
    [InlineData("<a|b>", "da<lt>", "|")]
    public void ABracketObject_IsThePairRoundTheCaret(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // Nesting is counted from the caret outwards, in both directions, so the pair found is the
    // innermost one that encloses it.
    [InlineData("((a|b))", "di(", "((|))")]
    [InlineData("((a|b))", "da(", "(|)")]
    [InlineData("(a(b)|c)", "di(", "(|)")]
    // And a pair is not bound to a line, which is the whole reason to have 'ci{' over a fenced block.
    [InlineData("{\nf|oo\n}", "di{", "{|}")]
    [InlineData("{\nf|oo\n}", "da{", "|")]
    public void ABracketObject_CountsNestingAndSpansLines(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // A caret on a delimiter belongs to that delimiter's pair rather than to whatever encloses it.
    [InlineData("|(ab)", "di(", "(|)")]
    [InlineData("(ab|)", "di(", "(|)")]
    [InlineData("|((ab))", "di(", "(|)")]
    [InlineData("((ab)|)", "di(", "(|)")]
    public void ACaretOnADelimiter_BelongsToThatPair(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // Vim's spellings, all of them: either delimiter names the pair, and 'b' and 'B' are the
    // mnemonics for the round and the curly one.
    [InlineData("x(a|b)y", "di)", "x(|)y")]
    [InlineData("x(a|b)y", "dib", "x(|)y")]
    [InlineData("x(a|b)y", "dab", "x|y")]
    [InlineData("{a|b}", "di}", "{|}")]
    [InlineData("{a|b}", "diB", "{|}")]
    [InlineData("[a|b]", "di]", "[|]")]
    [InlineData("<a|b>", "di>", "<|>")]
    public void EitherDelimiterAndTheMnemonic_NameTheSamePair(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void ACountOnADelimiterObject_IsIgnoredRatherThanRefused()
    {
        // Real vim walks one nesting level outwards per count. That is not implemented, and a count
        // is dropped rather than turned down — 'd2i(' is 'di('.
        VimRun run = VimScript.Run("((a|b))", "d2i(");

        Assert.Equal("((|))", run.Text);
        Assert.Equal(VimScript.Run("((a|b))", "di(").Text, run.Text);
        Assert.Null(run.Status);
    }

    [Theory]
    [InlineData("\"a|b\"", "di\"", "\"|\"")]
    [InlineData("\"a|b\"", "da\"", "|")]
    [InlineData("'a|b'", "di'", "'|'")]
    [InlineData("`a|b`", "di`", "`|`")]
    // The quotes themselves count as inside the pair, which is what makes 'ci"' work from either end.
    [InlineData("|\"ab\"", "di\"", "\"|\"")]
    [InlineData("\"ab|\"", "di\"", "\"|\"")]
    public void AQuoteObject_IsThePairTheCaretFallsIn(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // Paired off from the start of the line: the first and second quote are a pair, the third and
    // fourth are the next, and the space between the second and the third is inside neither.
    [InlineData("\"a\"| \"b\"", "di\"", "\"a\"| \"b\"")]
    [InlineData("\"a\" \"b|\"", "di\"", "\"a\" \"|\"")]
    // And a pair never crosses a line break, or an apostrophe three paragraphs up would capture half
    // the document. The quote below is a different line's business.
    [InlineData("\"|a\nb\"", "di\"", "\"|a\nb\"")]
    // An odd quote on the line opens nothing.
    [InlineData("say |\"hi", "di\"", "say |\"hi")]
    public void AQuoteObject_NeitherNestsNorLeavesTheLine(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("foo |bar baz", "yiw", "bar")]
    [InlineData("foo |bar baz", "yaw", "bar ")]
    [InlineData("x(a|b)y", "yi(", "ab")]
    [InlineData("x(a|b)y", "ya(", "(ab)")]
    [InlineData("[a|b]", "yi[", "ab")]
    [InlineData("{a|b}", "ya{", "{ab}")]
    [InlineData("<a|b>", "yi<lt>", "ab")]
    [InlineData("\"a|b\"", "yi\"", "ab")]
    [InlineData("'a|b'", "ya'", "'ab'")]
    [InlineData("`a|b`", "yi`", "ab")]
    public void AYankOverATextObject_FillsTheRegisterWithWhatItCovered(string before, string keys, string text)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(VimRegister.Characters(text), run.Register);
        Assert.Equal(before.Replace("|", string.Empty, StringComparison.Ordinal), run.Buffer.Text);
    }

    [Fact]
    public void AChangeOverATextObject_TakesTheTextAndEntersInsertMode()
    {
        VimRun pending = VimScript.Run("foo |bar baz", "ciw");

        Assert.Equal(VimMode.Insert, pending.Mode);
        Assert.Equal("foo | baz", pending.Text);
        Assert.Equal(VimRegister.Characters("bar"), pending.Register);

        Assert.Equal("foo qu|x baz", VimScript.Run("foo |bar baz", "ciwqux<Esc>").Text);
        Assert.Equal("x(qu|x)y", VimScript.Run("x(a|b)y", "ci(qux<Esc>").Text);
    }

    [Fact]
    public void ChangeOnAnEmptyPair_EntersInsertModeBetweenTheDelimiters()
    {
        // An empty object is a match with nothing in it, not a miss — the same rule 'c0' at the start
        // of a line already follows.
        VimRun run = VimScript.Run("(|)", "ci(");

        Assert.Equal(VimMode.Insert, run.Mode);
        Assert.Equal("(|)", run.Text);
        Assert.Null(run.Register);

        Assert.Equal("(|x)", VimScript.Run("(|)", "ci(x<Esc>").Text);
    }

    [Fact]
    public void DeleteOnAnEmptyPair_HasNothingToDoAndTakesNothing()
    {
        VimRun run = VimScript.Run("(|)", "di(");

        Assert.Equal("(|)", run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.Null(run.Register);
        Assert.Null(run.Status);
    }

    [Theory]
    [InlineData("foo |bar baz", "viw", "foo [ba|r] baz")]
    [InlineData("foo |bar baz", "vaw", "foo [bar| ]baz")]
    [InlineData("x(a|b)y", "vi(", "x([a|b])y")]
    [InlineData("x(a|b)y", "va(", "x[(ab|)]y")]
    [InlineData("\"a|b\"", "vi\"", "\"[a|b]\"")]
    public void ATextObjectInAVisualMode_SelectsWhatItCovers(string before, string keys, string highlight)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(highlight, run.Highlight);
        Assert.Equal(VimMode.Visual, run.Mode);
    }

    [Fact]
    public void ATextObjectInAVisualMode_MovesBothEndsRatherThanExtendingTheSelection()
    {
        // 'viw' is "select the word I am on", not "extend by a word": the anchor the 'v' left at the
        // 'o' is gone, and both ends are where the object is.
        VimRun run = VimScript.Run("f|oo bar", "vlliw");

        Assert.Equal("foo[| ]bar", run.Highlight);
        Assert.Equal(VimMode.Visual, run.Mode);
    }

    [Fact]
    public void ATextObjectInLinewiseVisualMode_StaysLinewiseOverTheLinesItTouches()
    {
        VimRun run = VimScript.Run("{\nf|oo\n}", "Vi{");

        Assert.Equal(VimMode.VisualLine, run.Mode);
        Assert.Equal("{\nfoo\n", run.SelectedText);
    }

    [Fact]
    public void AnOperatorAfterAVisualTextObject_ActsOnWhatItSelected()
    {
        VimRun run = VimScript.Run("foo |bar baz", "viwd");

        Assert.Equal("foo | baz", run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.Equal(VimRegister.Characters("bar"), run.Register);
    }

    [Theory]
    [InlineData("a|bc", "di(")]
    [InlineData("a|bc", "da(")]
    [InlineData("a|bc", "di\"")]
    // An unmatched delimiter on one side is no pair at all.
    [InlineData("(a|b", "di(")]
    [InlineData("a|b)", "di(")]
    // Beside a pair rather than inside it.
    [InlineData("x(ab)|y", "di(")]
    public void ATextObjectThatMatchesNothing_SaysNothingAndChangesNothing(string before, string keys)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(before, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.Null(run.Status);
        Assert.Null(run.Register);
    }

    [Fact]
    public void AWordObjectWithNothingUnderIt_IsAMatchWithNothingInIt()
    {
        // The end of an empty buffer: there is a run under the caret, and it is empty. A delete has
        // nothing to do and a change still enters insert mode.
        VimRun deleted = VimScript.Run("|", "diw");

        Assert.Equal("|", deleted.Text);
        Assert.Equal(VimMode.Normal, deleted.Mode);
        Assert.Null(deleted.Status);
        Assert.Null(deleted.Register);

        Assert.Equal(VimMode.Insert, VimScript.Run("|", "ciw").Mode);
    }

    [Fact]
    public void AHalfTypedObject_IsPendingUntilItsKeyArrives()
    {
        VimRun behindAnOperator = VimScript.Run("a|bc", "di");

        Assert.True(behindAnOperator.Engine.HasPendingSequence);
        Assert.Equal("a|bc", behindAnOperator.Text);

        VimRun inAVisualMode = VimScript.Run("a|bc", "vi");

        Assert.True(inAVisualMode.Engine.HasPendingSequence);
        Assert.Equal(VimMode.Visual, inAVisualMode.Mode);
    }

    [Fact]
    public void Esc_CancelsAHalfTypedObject()
    {
        VimRun run = VimScript.Run("a|bc", "di<Esc>");

        Assert.False(run.Engine.HasPendingSequence);
        Assert.Equal("a|bc", run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
    }

    [Theory]
    // No operator waiting and no selection, so the two keys are still the insert commands they were.
    // The last case is the one that matters: 'iw' typed on its own inserts a 'w'.
    [InlineData("a|bc", "ix<Esc>", "a|xbc")]
    [InlineData("a|bc", "ax<Esc>", "ab|xc")]
    [InlineData("a|bc", "iw<Esc>", "a|wbc")]
    public void IAndA_StillInsertWhereNoObjectCouldBeUsed(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("a|bc", "diz", "Not a command: diz")]
    [InlineData("a|bc", "daq", "Not a command: daq")]
    public void AKeyThatNamesNoObject_IsNamedInFullAndDoesNothing(string before, string keys, string message)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(before, run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.Equal(message, run.Status);
        Assert.Null(run.Register);
    }

    [Fact]
    public void AKeyThatNamesNoObject_LeavesAVisualSelectionAlone()
    {
        VimRun run = VimScript.Run("a|bc\ndef", "vliz");

        Assert.Equal("abc\ndef", run.Buffer.Text);
        Assert.Equal(VimMode.Visual, run.Mode);
        Assert.Equal("a[b|c]\ndef", run.Highlight);
        Assert.Equal("Not a command: iz", run.Status);
    }

    [Fact]
    public void AnObjectBehindAnOperator_ParsesAsThatOperatorOverIt()
    {
        KeySequenceParser parser = new();

        parser.Feed(VimKey.Typed('2'));
        parser.Feed(VimKey.Typed('d'));
        parser.Feed(VimKey.Typed('3'));
        parser.Feed(VimKey.Typed('i'));

        VimParseResult result = parser.Feed(VimKey.Typed('w'));

        Assert.Equal(VimParseStatus.Complete, result.Status);
        Assert.Equal(VimCommand.Object(VimOperator.Delete, VimTextObject.InnerWord, 6), result.Command);
    }

    [Fact]
    public void AnObjectInAVisualMode_ParsesAsAMoveThatNamesTheText()
    {
        KeySequenceParser parser = new();

        parser.Feed(VimKey.Typed('a'), VimMode.Visual);

        VimParseResult result = parser.Feed(VimKey.Typed('('), VimMode.Visual);

        Assert.Equal(VimParseStatus.Complete, result.Status);
        Assert.Equal(
            new VimCommand(VimAction.Move, TextObject: VimTextObject.AroundParen),
            result.Command);
    }

    [Fact]
    public void AnObjectOpener_IsPendingAndSaysWhatHasBeenTyped()
    {
        KeySequenceParser parser = new();

        parser.Feed(VimKey.Typed('c'));

        VimParseResult result = parser.Feed(VimKey.Typed('a'));

        Assert.Equal(VimParseStatus.Pending, result.Status);
        Assert.True(parser.HasPendingSequence);
        Assert.Equal("ca", parser.PendingSequence);
    }
}
