using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.Views;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Views;

/// <summary>
/// The space around the editor's line-number column.
/// </summary>
/// <remarks>
/// <para>
/// Asserted through a real pane in a shown window, because that is what makes the assertion worth
/// anything: <c>ShowLineNumbers</c> builds this control in code and puts it in
/// <c>TextArea.LeftMargins</c>, so what has to be proved is that the value assigned in the pane's
/// constructor is the one still on it after the framework has been through the tree — a style setter
/// for the same property would have lost that race, which is why the pane sets it directly.
/// </para>
/// <para>
/// The <em>value</em> is as far as this can go: the headless drawing platform never measures a margin
/// inside <c>LeftMargins</c> — the column comes out of a layout pass with a desired size of zero — so
/// how much room the numbers actually get on screen stays a manual reading.
/// </para>
/// </remarks>
public sealed class EditorLineNumbersTests
{
    [AvaloniaFact]
    public async Task TheLineNumberColumn_KeepsClearSpaceOnBothSidesThroughALayoutPass()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md");

        Assert.Equal(EditorPaneView.LineNumberSpacing, strip.LineNumbers.Margin.Left);
        Assert.Equal(EditorPaneView.LineNumberSpacing, strip.LineNumbers.Margin.Right);

        // Horizontal only: a top or bottom margin would push the numbers out of step with the lines
        // they number.
        Assert.Equal(0, strip.LineNumbers.Margin.Top);
        Assert.Equal(0, strip.LineNumbers.Margin.Bottom);
    }

    [AvaloniaFact]
    public void TheSpacing_IsEnoughToRead()
        => Assert.True(
            EditorPaneView.LineNumberSpacing >= 2,
            $"{EditorPaneView.LineNumberSpacing}px on each side is not breathing room");
}
