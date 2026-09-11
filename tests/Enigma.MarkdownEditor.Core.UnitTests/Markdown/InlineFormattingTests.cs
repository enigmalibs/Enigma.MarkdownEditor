using System;
using Enigma.MarkdownEditor.Core.Markdown;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Markdown;

/// <summary>
/// Ctrl+B, Ctrl+I and Ctrl+K: what they wrap, what they take back off, and where they leave the
/// caret.
/// </summary>
public sealed class InlineFormattingTests
{
    [Theory]
    [InlineData(InlineFormatting.BoldDelimiter, "**hello** world")]
    [InlineData(InlineFormatting.ItalicDelimiter, "*hello* world")]
    public void ASelection_IsWrapped(string delimiter, string expected)
        => Assert.Equal(expected, Edits.Apply("hello world", InlineFormatting.Toggle("hello world", 0, 5, delimiter)));

    [Fact]
    public void WrappingASelection_LeavesTheSameTextSelected()
    {
        TextEdit edit = InlineFormatting.Toggle("hello world", 0, 5, InlineFormatting.BoldDelimiter);

        Assert.Equal(2, edit.CaretOffset);
        Assert.Equal(5, edit.SelectionLength);
    }

    [Theory]
    [InlineData("**hello** world", 0, 9, InlineFormatting.BoldDelimiter)]
    [InlineData("**hello** world", 2, 5, InlineFormatting.BoldDelimiter)]
    [InlineData("*hello* world", 0, 7, InlineFormatting.ItalicDelimiter)]
    [InlineData("*hello* world", 1, 5, InlineFormatting.ItalicDelimiter)]
    public void AlreadyWrappedText_IsUnwrapped(string text, int start, int length, string delimiter)
        => Assert.Equal("hello world", Edits.Apply(text, InlineFormatting.Toggle(text, start, length, delimiter)));

    [Fact]
    public void WithNothingSelected_TheWordUnderTheCaretIsTaken()
    {
        Assert.Equal("**hello** world", Edits.Apply("hello world", InlineFormatting.Toggle("hello world", 3, 0, "**")));
        Assert.Equal("hello **world**", Edits.Apply("hello world", InlineFormatting.Toggle("hello world", 6, 0, "**")));

        // At the very end of a word is still in it.
        Assert.Equal("**hello** world", Edits.Apply("hello world", InlineFormatting.Toggle("hello world", 5, 0, "**")));
    }

    [Fact]
    public void WithNothingSelectedAndNoWordToTake_ThePairIsInsertedAroundTheCaret()
    {
        TextEdit edit = InlineFormatting.Toggle("a  b", 2, 0, InlineFormatting.BoldDelimiter);

        Assert.Equal("a **** b", Edits.Apply("a  b", edit));
        Assert.Equal(4, edit.CaretOffset);
        Assert.Equal(0, edit.SelectionLength);
    }

    [Fact]
    public void ItalicOverBoldText_AddsToItRatherThanTearingOneAsteriskOff()
    {
        // "**bold**" is a pair of delimiters, not two singles, so italic has nothing of its own to
        // remove here — it wraps, which is what produces bold italic.
        Assert.Equal(
            "***bold***",
            Edits.Apply("**bold**", InlineFormatting.Toggle("**bold**", 2, 4, InlineFormatting.ItalicDelimiter)));
    }

    [Fact]
    public void BoldOverItalicText_AddsToItTheSameWay()
    {
        Assert.Equal(
            "***it***",
            Edits.Apply("*it*", InlineFormatting.Toggle("*it*", 1, 2, InlineFormatting.BoldDelimiter)));
    }

    [Fact]
    public void WrappingAndUnwrappingAgain_LeavesTheTextItStartedAs()
    {
        TextEdit wrapped = InlineFormatting.Toggle("hello world", 0, 5, InlineFormatting.BoldDelimiter);
        string bold = Edits.Apply("hello world", wrapped);

        TextEdit undone = InlineFormatting.Toggle(bold, wrapped.CaretOffset, wrapped.SelectionLength, "**");

        Assert.Equal("hello world", Edits.Apply(bold, undone));
    }

    [Fact]
    public void ASelection_BecomesTheLinksText()
    {
        TextEdit edit = InlineFormatting.Link("see docs", 4, 4);

        Assert.Equal("see [docs]()", Edits.Apply("see docs", edit));

        // Between the brackets of the address, which is the half still to be typed.
        Assert.Equal("see [docs](".Length, edit.CaretOffset);
    }

    [Fact]
    public void WithNothingSelected_TheWordUnderTheCaretBecomesTheLinksText()
        => Assert.Equal("see [docs]()", Edits.Apply("see docs", InlineFormatting.Link("see docs", 6, 0)));

    [Fact]
    public void WithNoTextAtAll_TheCaretGoesIntoTheEmptyLabelInstead()
    {
        TextEdit edit = InlineFormatting.Link("", 0, 0);

        Assert.Equal("[]()", Edits.Apply("", edit));
        Assert.Equal(1, edit.CaretOffset);
    }

    [Fact]
    public void ARangeOutsideTheText_IsRejectedRatherThanClamped()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => InlineFormatting.Toggle("abc", 2, 5, "**"));
        Assert.Throws<ArgumentOutOfRangeException>(() => InlineFormatting.Toggle("abc", -1, 1, "**"));
        Assert.Throws<ArgumentOutOfRangeException>(() => InlineFormatting.Link("abc", 4, 0));
    }

    [Fact]
    public void TheArgumentsAreGuarded()
    {
        Assert.Throws<ArgumentNullException>(() => InlineFormatting.Toggle(null!, 0, 0, "**"));
        Assert.Throws<ArgumentNullException>(() => InlineFormatting.Toggle("abc", 0, 0, null!));
        Assert.Throws<ArgumentException>(() => InlineFormatting.Toggle("abc", 0, 0, string.Empty));
        Assert.Throws<ArgumentNullException>(() => InlineFormatting.Link(null!, 0, 0));
    }
}
