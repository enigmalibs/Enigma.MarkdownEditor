using Enigma.MarkdownEditor.Core.Vim;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// The three motions that read the structure of the text rather than its words: <c>%</c> to the
/// matching bracket, <c>}</c> and <c>{</c> between paragraphs, and <c>)</c> and <c>(</c> between
/// sentences.
/// </summary>
/// <remarks>
/// <para>
/// The paragraph cases are mostly about one rule, vim's exclusive-linewise one, because it is the
/// rule that decides whether <c>d}</c> takes whole lines or characters: from the first column of a
/// paragraph it takes the lines and stops short of the blank line after them, and from the middle of
/// a line it takes to the end of the paragraph and leaves the blank line alone.
/// </para>
/// <para>
/// The sentence cases are a transcription of vim's own definition, edge by edge — the closing quote
/// after a full stop, the abbreviation, the sentence that runs over a line break, the paragraph
/// boundary — because the definition has no shortcut that survives the second example.
/// </para>
/// </remarks>
public sealed class VimBracketParagraphTests
{
    [Theory]
    [InlineData("|(abc)", "%", "(abc|)")]
    [InlineData("(abc|)", "%", "|(abc)")]
    [InlineData("|[a]", "%", "[a|]")]
    [InlineData("{a|}", "%", "|{a}")]
    // Not on a bracket: the first one along the line, and then its match.
    [InlineData("a|b(c)d", "%", "ab(c|)d")]
    [InlineData("a|b)c", "%", "a|b)c")]
    // Nesting is counted, in both directions, and a different kind of bracket does not count.
    [InlineData("|((a))", "%", "((a)|)")]
    [InlineData("(|(a))", "%", "((a|))")]
    [InlineData("((a)|)", "%", "|((a))")]
    [InlineData("|(a[)]", "%", "(a[|)]")]
    // And the match is not bound to a line.
    [InlineData("|{\nfoo\n}", "%", "{\nfoo\n|}")]
    [InlineData("{\nfoo\n|}", "%", "|{\nfoo\n}")]
    public void Percent_GoesToTheMatchingBracket(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("a|bc", "%")]
    [InlineData("|(abc", "%")]
    [InlineData("abc|)", "%")]
    [InlineData("|", "%")]
    public void PercentWithNoMatch_DoesNothingAndSaysNothing(string before, string keys)
    {
        VimRun run = VimScript.Run(before, keys);

        Assert.Equal(before, run.Text);
        Assert.Null(run.Status);
    }

    [Theory]
    // Inclusive: both brackets go with everything between them, from either end and from before.
    [InlineData("x|(ab)y", "d%", "x|y")]
    [InlineData("x(ab|)y", "d%", "x|y")]
    [InlineData("a|b(c)d", "d%", "a|d")]
    [InlineData("|{\nfoo\n}x", "d%", "|x")]
    [InlineData("|(abc", "d%", "|(abc")]
    public void AnOperatorOverPercent_TakesBothBrackets(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void AYankOverPercent_FillsTheRegisterAndMovesToTheOpeningBracket()
    {
        VimRun run = VimScript.Run("x(ab|)y", "y%");

        Assert.Equal("x|(ab)y", run.Text);
        Assert.Equal(VimRegister.Characters("(ab)"), run.Register);
    }

    [Theory]
    // Vim's formula, rounding up: line (N × lines + 99) / 100.
    [InlineData("|a\nb\nc\nd", "50%", "a\n|b\nc\nd")]
    [InlineData("|a\nb\nc\nd", "100%", "a\nb\nc\n|d")]
    [InlineData("|a\nb\nc\nd", "1%", "|a\nb\nc\nd")]
    [InlineData("a\nb\nc\n|d", "25%", "|a\nb\nc\nd")]
    [InlineData("|a\nb\nc\nd", "75%", "a\nb\n|c\nd")]
    // Past a hundred it clamps, as every other count that overshoots does.
    [InlineData("|a\nb\nc\nd", "150%", "a\nb\nc\n|d")]
    [InlineData("|a\n  b\nc\nd", "50%", "a\n  |b\nc\nd")]
    public void PercentWithACount_GoesToThatPercentageOfTheBuffer(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void AnOperatorOverACountedPercent_IsLinewise()
    {
        VimRun run = VimScript.Run("|a\nb\nc\nd", "d50%");

        Assert.Equal("|c\nd", run.Text);
        Assert.Equal(VimRegister.Lines("a\nb"), run.Register);
    }

    [Theory]
    [InlineData("|abc\ndef\n\nghi", "}", "abc\ndef\n|\nghi")]
    [InlineData("abc\nd|ef\n\nghi", "}", "abc\ndef\n|\nghi")]
    // From a blank line, over the paragraph after it to the next blank line.
    [InlineData("abc\n|\ndef\n\nghi", "}", "abc\n\ndef\n|\nghi")]
    [InlineData("abc\n|\n\ndef\n\nghi", "}", "abc\n\n\ndef\n|\nghi")]
    // No blank line left: the end of the buffer, on its last character.
    [InlineData("a|bc\ndef", "}", "abc\nde|f")]
    [InlineData("abc\n|\ndef", "}", "abc\n\nde|f")]
    // A line of blanks is not a paragraph boundary — only a line with nothing on it is.
    [InlineData("|abc\n  \ndef", "}", "abc\n  \nde|f")]
    [InlineData("|a\n\nb\n\nc", "2}", "a\n\nb\n|\nc")]
    public void CloseBrace_GoesToTheNextBlankLine(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("abc\n\nd|ef", "{", "abc\n|\ndef")]
    [InlineData("abc\n\ndef\n|\nghi", "{", "abc\n|\ndef\n\nghi")]
    // No blank line above: the start of the buffer.
    [InlineData("abc\nd|ef", "{", "|abc\ndef")]
    [InlineData("|\nabc", "{", "|\nabc")]
    [InlineData("a\n\nb\n\n|c", "2{", "a\n|\nb\n\nc")]
    public void OpenBrace_GoesToThePreviousBlankLine(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // A count that runs out of paragraphs moves nowhere at all, which is vim's rule and what makes
    // a count safe to type blind.
    [InlineData("|a\n\nb", "3}")]
    [InlineData("a\n\n|b", "3{")]
    public void ACountThatOvershoots_MovesNowhere(string before, string keys)
        => Assert.Equal(before, VimScript.Run(before, keys).Text);

    [Theory]
    // From the first column — or the first non-blank — the range is linewise and stops short of
    // the blank line, so the paragraph goes and the gap stays.
    [InlineData("|abc\ndef\n\nghi", "d}", "|\nghi")]
    [InlineData("  |abc\ndef\n\nghi", "d}", "|\nghi")]
    [InlineData("|abc\ndef\n\nghi", "c}", "|\n\nghi")]
    // From mid-line the range is characterwise and stops at the end of the paragraph's last line,
    // so the line break before the blank line stays too.
    [InlineData("a|bc\ndef\n\nghi", "d}", "|a\n\nghi")]
    // On the last paragraph the motion reaches the end of the buffer, last character included.
    [InlineData("a|bc\ndef", "d}", "|a")]
    [InlineData("|abc\ndef", "d}", "|")]
    // Backwards: to the blank line above, whole lines from the first column and characters from
    // mid-line.
    [InlineData("abc\n\ndef\n|ghi", "d{", "abc\n|ghi")]
    [InlineData("abc\n\ndef\ng|hi", "d{", "abc\n|hi")]
    public void AnOperatorOverAParagraphMotion_FollowsTheExclusiveLinewiseRule(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("|abc\ndef\n\nghi", "d}", "abc\ndef", VimRegisterKind.Linewise)]
    [InlineData("a|bc\ndef\n\nghi", "d}", "bc\ndef", VimRegisterKind.Characterwise)]
    [InlineData("a|bc\ndef\n\nghi", "y}", "bc\ndef", VimRegisterKind.Characterwise)]
    public void WhatAParagraphMotionTakes_GoesInTheRegisterWithItsKind(
        string before,
        string keys,
        string text,
        VimRegisterKind kind)
        => Assert.Equal(new VimRegister(text, kind), VimScript.Run(before, keys).Register);

    [Fact]
    public void AYankOverACloseBrace_LeavesTheCaretWhereItWas()
        => Assert.Equal("a|bc\ndef\n\nghi", VimScript.Run("a|bc\ndef\n\nghi", "y}").Text);

    [Theory]
    [InlineData("|One. Two.", ")", "One. |Two.")]
    [InlineData("|One.  Two.", ")", "One.  |Two.")]
    [InlineData("|Hi! There? Yes.", ")", "Hi! |There? Yes.")]
    [InlineData("Hi! T|here? Yes.", ")", "Hi! There? |Yes.")]
    // A full stop with no blank after it does not end a sentence — so an abbreviation's first dot
    // is passed over and its second, followed by a space, is the end.
    [InlineData("|e.g. foo", ")", "e.g. |foo")]
    // Any run of closing quotes and brackets may sit between the full stop and the blank.
    [InlineData("|\"Hi.\" Bye.", ")", "\"Hi.\" |Bye.")]
    [InlineData("|(One.) Two.", ")", "(One.) |Two.")]
    // A full stop at the end of a line ends the sentence, and the next starts on the next line.
    [InlineData("|One.\nTwo.", ")", "One.\n|Two.")]
    [InlineData("|One.\n  Two.", ")", "One.\n  |Two.")]
    // A paragraph boundary is a sentence boundary too, from either side of it.
    [InlineData("|One\n\nTwo", ")", "One\n|\nTwo")]
    [InlineData("One\n|\nTwo", ")", "One\n\n|Two")]
    // No sentence end left: the end of the buffer, on its last character.
    [InlineData("|abc def", ")", "abc de|f")]
    [InlineData("|One. Two.", "))", "One. Two|.")]
    [InlineData("|A. B. C.", "2)", "A. B. |C.")]
    public void CloseParen_GoesToTheStartOfTheNextSentence(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    [InlineData("One. |Two.", "(", "|One. Two.")]
    [InlineData("One. Tw|o.", "(", "One. |Two.")]
    [InlineData("One. Two|.", "(", "One. |Two.")]
    [InlineData("One. Two. |Three.", "((", "|One. Two. Three.")]
    [InlineData("One. Two. |Three.", "2(", "|One. Two. Three.")]
    [InlineData("One.\n|Two.", "(", "|One.\nTwo.")]
    [InlineData("One\n\n|Two", "(", "One\n|\nTwo")]
    [InlineData("|One. Two.", "(", "|One. Two.")]
    public void OpenParen_GoesToTheStartOfThisOrThePreviousSentence(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Theory]
    // Exclusive, in both directions: the sentence goes and the one after it stays in place.
    [InlineData("|One. Two.", "d)", "|Two.")]
    [InlineData("One. |Two.", "d(", "|Two.")]
    [InlineData("On|e. Two.", "d)", "On|Two.")]
    [InlineData("|One. Two.", "2d)", "|")]
    public void AnOperatorOverASentenceMotion_TakesToTheSentenceBoundary(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);

    [Fact]
    public void AChangeOverASentence_TakesItAndEntersInsertMode()
    {
        VimRun run = VimScript.Run("|One. Two.", "c)");

        Assert.Equal("|Two.", run.Text);
        Assert.Equal(VimMode.Insert, run.Mode);
        Assert.Equal(VimRegister.Characters("One. "), run.Register);
    }

    [Theory]
    // A visual selection is inclusive of the character under the caret, and on the blank line that
    // character is its line break — so the blank line goes with the paragraph here.
    [InlineData("|abc\ndef\n\nghi", "v}d", "|ghi")]
    [InlineData("|One. Two.", "v)d", "|wo.")]
    [InlineData("x|(ab)y", "v%d", "x|y")]
    public void TheStructureMotions_ExtendAVisualSelection(string before, string keys, string after)
        => Assert.Equal(after, VimScript.Run(before, keys).Text);
}
