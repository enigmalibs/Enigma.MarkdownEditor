using System;
using System.Threading.Tasks;
using AvaloniaEdit.Document;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.ViewModels;

/// <summary>
/// Tests for the arithmetic behind "open this hit and select the match": a line and a column measured
/// against the text the editor is actually holding.
/// </summary>
/// <remarks>
/// On the pumped thread throughout, because a <see cref="TextDocument"/> verifies that every access
/// comes from the thread that built it. This is where the plan's "lands selection exactly on the
/// match" is asserted — what the control then does with the offset is one call, and one this suite has
/// no editor to make.
/// </remarks>
public sealed class DocumentSelectionTests
{
    [Fact]
    public Task ALineAndAColumn_BecomeAnOffsetIntoTheText()
        => UiThread.RunAsync(() =>
        {
            TextDocument document = new("first\nthe needle here\n");

            DocumentSelectionRange range = new DocumentSelection(2, 5, 6).Resolve(document);

            // "first\n" is six characters, and the match starts four into the line after it.
            Assert.Equal(10, range.Offset);
            Assert.Equal(6, range.Length);
            Assert.Equal("needle", document.GetText(range.Offset, range.Length));

            return Task.CompletedTask;
        });

    [Fact]
    public Task TheFirstCharacter_IsOffsetZero()
        => UiThread.RunAsync(() =>
        {
            TextDocument document = new("needle\n");

            DocumentSelectionRange range = new DocumentSelection(1, 1, 6).Resolve(document);

            Assert.Equal(0, range.Offset);
            Assert.Equal("needle", document.GetText(range.Offset, range.Length));

            return Task.CompletedTask;
        });

    [Fact]
    public Task ALinePastTheEnd_LandsOnTheLastOne()
        => UiThread.RunAsync(() =>
        {
            TextDocument document = new("one\ntwo\n");

            // The file was read by the search and read again by the editor, and shortened in between.
            // Landing on the last line beats throwing at someone who only clicked a result.
            DocumentSelectionRange range = new DocumentSelection(99, 1, 3).Resolve(document);

            Assert.Equal(document.GetLineByNumber(document.LineCount).Offset, range.Offset);

            return Task.CompletedTask;
        });

    [Fact]
    public Task AColumnPastTheEndOfItsLine_LandsAtTheLinesEnd()
        => UiThread.RunAsync(() =>
        {
            TextDocument document = new("one\ntwo\n");

            DocumentSelectionRange range = new DocumentSelection(1, 40, 1).Resolve(document);

            // One past the last character is where a caret at the end of a line lives.
            Assert.Equal(3, range.Offset);

            return Task.CompletedTask;
        });

    [Fact]
    public Task ALengthRunningPastTheEnd_IsCutToWhatIsThere()
        => UiThread.RunAsync(() =>
        {
            TextDocument document = new("needle");

            DocumentSelectionRange range = new DocumentSelection(1, 4, 400).Resolve(document);

            Assert.Equal(3, range.Offset);
            Assert.Equal(3, range.Length);
            Assert.Equal("dle", document.GetText(range.Offset, range.Length));

            return Task.CompletedTask;
        });

    [Fact]
    public Task ALineOrColumnBelowOne_IsClampedUpToIt()
        => UiThread.RunAsync(() =>
        {
            TextDocument document = new("needle\n");

            DocumentSelectionRange range = new DocumentSelection(0, 0, 6).Resolve(document);

            Assert.Equal(0, range.Offset);
            Assert.Equal(6, range.Length);

            return Task.CompletedTask;
        });

    [Fact]
    public Task AZeroLength_PlacesTheCaretAndSelectsNothing()
        => UiThread.RunAsync(() =>
        {
            TextDocument document = new("needle\n");

            DocumentSelectionRange range = new DocumentSelection(1, 3, 0).Resolve(document);

            Assert.Equal(2, range.Offset);
            Assert.Equal(0, range.Length);

            return Task.CompletedTask;
        });

    [Fact]
    public Task AnEmptyDocument_IsAnsweredRatherThanThrownAt()
        => UiThread.RunAsync(() =>
        {
            TextDocument document = new(string.Empty);

            DocumentSelectionRange range = new DocumentSelection(3, 7, 5).Resolve(document);

            Assert.Equal(0, range.Offset);
            Assert.Equal(0, range.Length);

            return Task.CompletedTask;
        });

    [Fact]
    public void ANullDocument_IsRejected()
        => Assert.Throws<ArgumentNullException>(() => new DocumentSelection(1, 1, 1).Resolve(null!));
}
