using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// The WORD motions <c>W</c>, <c>B</c> and <c>E</c> and the objects <c>iW</c> and <c>aW</c>: the
/// same shapes as their lowercase pairs, read with two character classes instead of three.
/// </summary>
/// <remarks>
/// Most of these cases sit a capital next to its lowercase on the same text, because the difference
/// between them — <c>foo.bar</c> is one WORD and three words — is the whole of what there is to test.
/// The rest hold the capitals to the rules the lowercase motions already keep: the empty-line stop,
/// <c>cW</c> reaching the end of the WORD like <c>cE</c>, and <c>dW</c> stopping at a line's end.
/// </remarks>
public sealed class VimBigWordTests
{
    [Theory]
    [InlineData("|foo.bar baz", "W", "foo.bar |baz")]
    [InlineData("|foo.bar baz", "w", "foo|.bar baz")]
    [InlineData("|foo bar", "W", "foo |bar")]
    [InlineData("|a.b c.d e", "2W", "a.b c.d |e")]
    [InlineData("|a,b;c d", "W", "a,b;c |d")]
    [InlineData("|foo   bar", "W", "foo   |bar")]
    // Across a line break, and the last WORD has nowhere further to go.
    [InlineData("a|bc\ndef", "W", "abc\n|def")]
    [InlineData("foo |b.r", "W", "foo b.|r")]
    [InlineData("|", "W", "|")]
    public void W_GoesToTheStartOfTheNextWord(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // An empty line is a WORD in its own right, exactly as it is a word.
    [InlineData("a|bc\n\ndef", "W", "abc\n|\ndef")]
    [InlineData("a|bc\n\ndef", "WW", "abc\n\n|def")]
    [InlineData("abc\n\n|def", "B", "abc\n|\ndef")]
    // And 'E' runs through it, because there is nothing there to end on.
    [InlineData("ab|c\n\nd.f", "E", "abc\n\nd.|f")]
    public void AnEmptyLine_IsAWordOfItsOwnForTheCapitalsToo(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("foo.bar b|az", "B", "foo.bar |baz")]
    [InlineData("foo.bar |baz", "B", "|foo.bar baz")]
    [InlineData("foo.b|ar", "B", "|foo.bar")]
    [InlineData("foo.b|ar", "b", "foo.|bar")]
    [InlineData("a.b c.d |e", "2B", "|a.b c.d e")]
    [InlineData("abc\n|d.f", "B", "|abc\nd.f")]
    [InlineData("|foo", "B", "|foo")]
    public void B_GoesToTheStartOfThePreviousWord(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("|foo.bar baz", "E", "foo.ba|r baz")]
    [InlineData("|foo.bar baz", "e", "fo|o.bar baz")]
    [InlineData("foo.ba|r baz", "E", "foo.bar ba|z")]
    [InlineData("|foo bar", "E", "fo|o bar")]
    [InlineData("|a.b c.d", "2E", "a.b c.|d")]
    // Nothing left to end on: E stays where it is rather than running off the document.
    [InlineData("foo ba|r", "E", "foo ba|r")]
    [InlineData("|", "E", "|")]
    public void E_GoesToTheEndOfTheWord(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("|foo.bar baz", "dW", "|baz")]
    [InlineData("|foo.bar baz", "dw", "|.bar baz")]
    [InlineData("|foo.bar baz", "dE", "| baz")]
    [InlineData("foo.bar |baz", "dB", "|baz")]
    [InlineData("foo.b|ar baz", "dB", "|ar baz")]
    [InlineData("|a.b c.d e", "d2W", "|e")]
    // The line break at the end of a line does not belong to 'dW', exactly as it does not to 'dw'.
    [InlineData("foo |a.b\nbaz", "dW", "foo| \nbaz")]
    [InlineData("|foo.bar\n\nbaz", "dW", "|\n\nbaz")]
    public void AnOperatorOverAWordMotion_TakesExactlyWhatItReaches(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // 'cW' on a WORD changes to its end, like 'cE', so the blank after it survives — vim's 'cw'
    // quirk, carried to the capital.
    [InlineData("|foo.bar baz", "cW", "| baz")]
    [InlineData("|foo.bar baz", "cE", "| baz")]
    // On a blank there is no WORD under the caret, so 'cW' changes the blanks like 'dW' would.
    [InlineData("foo|   bar", "cW", "foo|bar")]
    public void CW_ChangesToTheEndOfTheWordLikeCE(string before, string keys, string after)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(after, run.Text);
        Assert.Equal(VimMode.Insert, run.Mode);
    }

    [Theory]
    [InlineData("|foo.bar baz", "yW", "foo.bar ")]
    [InlineData("|foo.bar baz", "yE", "foo.bar")]
    [InlineData("foo.bar |baz", "yB", "foo.bar ")]
    [InlineData("foo |a.b baz", "yiW", "a.b")]
    [InlineData("foo |a.b baz", "yaW", "a.b ")]
    public void AYankOverAWordMotionOrObject_FillsTheRegisterCharacterwise(string before, string keys, string text)
        => Assert.Equal(VimRegister.Characters(text), VimScript.Run(before, keys).Register);

    [Theory]
    [InlineData("foo |a.b baz", "diW", "foo | baz")]
    [InlineData("foo a|.b baz", "diW", "foo | baz")]
    [InlineData("foo a|.b baz", "diw", "foo a|b baz")]
    [InlineData("|foo.bar", "diW", "|")]
    [InlineData("|foo.bar", "diw", "|.bar")]
    // On a run of blanks the object is the blanks, from either side, exactly as for 'iw'.
    [InlineData("a.b|   c.d", "diW", "a.b|c.d")]
    public void IW_IsTheRunOfNonBlanksUnderTheCaret(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("foo a|.b baz", "daW", "foo |baz")]
    [InlineData("|a.b c.d", "daW", "|c.d")]
    // No blanks after the WORD, so the ones before it come instead: vim's fallback, carried over.
    [InlineData("foo a|.b", "daW", "fo|o")]
    // From a run of blanks the pair is the other way round: the blanks and the WORD after them.
    [InlineData("foo| a.b baz", "daW", "foo| baz")]
    public void AW_TakesTheBlanksBesideTheWordToo(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // 'iW' counts runs, so three of them is WORD, blanks, WORD; 'aW' counts WORDs with their blanks.
    [InlineData("|a.b c.d e", "d2iW", "|c.d e")]
    [InlineData("|a.b c.d e", "d3iW", "| e")]
    [InlineData("|a.b c.d e", "d2aW", "|e")]
    [InlineData("|a.b c.d", "d9iW", "|")]
    public void ACountOnAWordObject_TakesThatManyOfThem(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("foo a|.b baz", "viW", "foo [a.|b] baz")]
    [InlineData("foo a|.b baz", "vaW", "foo [a.b| ]baz")]
    [InlineData("foo a|.b baz", "viw", "foo a[|.]b baz")]
    public void AWordObjectInAVisualMode_SelectsWhatItCovers(string before, string keys, string highlight)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(highlight, run.Highlight);
        Assert.Equal(VimMode.Visual, run.Mode);
    }

    [Theory]
    [InlineData("|foo.bar baz", "vWd", "|az")]
    [InlineData("|foo.bar baz", "vEd", "| baz")]
    [InlineData("foo.bar |baz", "vBd", "|az")]
    public void AWordMotionInAVisualMode_ExtendsTheSelection(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void ChangeOverAnInnerWord_TakesTheWordAndEntersInsertMode()
    {
        VimRun run = VimScript.Run("foo a|.b baz", "ciWx<Esc>");

        Assert.Equal("foo |x baz", run.Text);
        Assert.Equal(VimMode.Normal, run.Mode);
        Assert.Equal(VimRegister.Characters("a.b"), run.Register);
    }

    [Theory]
    [InlineData("diW", VimOperator.Delete, VimTextObject.InnerBigWord)]
    [InlineData("caW", VimOperator.Change, VimTextObject.AroundBigWord)]
    [InlineData("y2iW", VimOperator.Yank, VimTextObject.InnerBigWord)]
    public void AWordObjectBehindAnOperator_ParsesAsThatOperatorOverIt(
        string keys,
        VimOperator @operator,
        VimTextObject textObject)
    {
        KeySequenceParser parser = new();
        VimParseResult result = default;

        foreach (char character in keys)
        {
            result = parser.Feed(VimKey.Typed(character));
        }

        Assert.Equal(VimParseStatus.Complete, result.Status);
        Assert.Equal(@operator, result.Command.Operator);
        Assert.Equal(textObject, result.Command.TextObject);
        Assert.Equal(VimAction.Operate, result.Command.Action);
    }
}
