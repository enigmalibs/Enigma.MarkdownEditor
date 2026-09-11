using System.Collections.Generic;
using Avalonia.Headless.XUnit;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using Enigma.MarkdownEditor.Core.Vim;
using Enigma.MarkdownEditor.Desktop.Views;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Views;

/// <summary>
/// What the adapter reports as a change to the text, and which document it is listening to for it.
/// </summary>
/// <remarks>
/// <para>
/// A bare <c>TextEditor</c> rather than a pane in a window: <c>Changed</c> is wired to the document
/// and to the control's own document swap, neither of which needs a template, a layout pass or the
/// keyboard, and a window round it would only add what a failure could hide behind.
/// </para>
/// <para>
/// The swap is the case that matters. One control serves every tab, and the engine takes what this
/// event carries for what the user typed — so a change to a document that has left the screen, an
/// undo on it or a reload from disk, must not be heard at all, and the document that took its place
/// must be heard from its first keystroke.
/// </para>
/// </remarks>
public sealed class AvaloniaEditVimBufferTests
{
    [AvaloniaFact]
    public void ATypedCharacter_IsReportedWhereItLanded()
    {
        TextEditor editor = new() { Document = new TextDocument("abc") };
        List<VimTextChange> changes = Listen(editor);

        // Through the text area's own input path, which is where a key the engine let through to insert
        // mode ends up — no window and no focus needed for it headless.
        editor.CaretOffset = 1;
        editor.TextArea.PerformTextInput("x");

        Assert.Equal(new VimTextChange(1, 0, "x"), Assert.Single(changes));
        Assert.Equal("axbc", editor.Document.Text);
    }

    [AvaloniaFact]
    public void ARemoval_IsReportedWithItsLengthAndNothingInItsPlace()
    {
        TextEditor editor = new() { Document = new TextDocument("abc") };
        List<VimTextChange> changes = Listen(editor);

        editor.Document.Remove(1, 1);

        Assert.Equal(new VimTextChange(1, 1, string.Empty), Assert.Single(changes));
    }

    [AvaloniaFact]
    public void TheEnginesOwnChange_IsReportedLikeAnyOther()
    {
        TextEditor editor = new() { Document = new TextDocument("abc") };
        AvaloniaEditVimBuffer buffer = new(editor, _ => { }, _ => { });
        List<VimTextChange> changes = [];
        buffer.Changed += (_, change) => changes.Add(change);

        buffer.Replace(0, 3, "xy");

        Assert.Equal(new VimTextChange(0, 3, "xy"), Assert.Single(changes));
    }

    [AvaloniaFact]
    public void AfterASwap_TheDocumentLeftBehindIsNotHeard()
    {
        TextDocument left = new("abc");
        TextEditor editor = new() { Document = left };
        List<VimTextChange> changes = Listen(editor);

        editor.Document = new TextDocument("other");
        left.Insert(0, "!");

        Assert.Empty(changes);
    }

    [AvaloniaFact]
    public void AfterASwap_TheDocumentThatArrivedIsHeard()
    {
        TextEditor editor = new() { Document = new TextDocument("abc") };
        List<VimTextChange> changes = Listen(editor);

        TextDocument arrived = new("other");
        editor.Document = arrived;
        arrived.Insert(5, "!");

        Assert.Equal(new VimTextChange(5, 0, "!"), Assert.Single(changes));
    }

    [AvaloniaFact]
    public void ASwapToNoDocument_IsSurvived_AndTheNextDocumentIsHeard()
    {
        TextDocument left = new("abc");
        TextEditor editor = new() { Document = left };
        List<VimTextChange> changes = Listen(editor);

        // The pane's editor is in this state before the first tab arrives and after the last one closes.
        editor.Document = null;
        left.Insert(0, "!");
        Assert.Empty(changes);

        TextDocument arrived = new("other");
        editor.Document = arrived;
        arrived.Insert(0, "!");

        Assert.Equal(new VimTextChange(0, 0, "!"), Assert.Single(changes));
    }

    [AvaloniaFact]
    public void ABufferOverAnEditorWithNoDocument_HearsTheFirstOneGiven()
    {
        TextEditor editor = new() { Document = null };
        List<VimTextChange> changes = Listen(editor);

        TextDocument arrived = new("abc");
        editor.Document = arrived;
        arrived.Insert(3, "!");

        Assert.Equal(new VimTextChange(3, 0, "!"), Assert.Single(changes));
    }

    [AvaloniaFact]
    public void TheViewport_IsReportedForAnEditorThatWasNeverLaidOut_AndAScrollDoesNotThrow()
    {
        // A control with no window round it has laid out nothing; what the engine gets is still a view
        // with a first line and some room, a scroll that goes nowhere quietly, and a Changed event that
        // fires exactly as it did before the view was asked anything.
        TextEditor editor = new() { Document = new TextDocument("abc\ndef\nghi") };
        AvaloniaEditVimBuffer buffer = new(editor, _ => { }, _ => { });
        List<VimTextChange> changes = [];
        buffer.Changed += (_, change) => changes.Add(change);

        Assert.Equal(1, buffer.FirstVisibleLine);
        Assert.True(buffer.VisibleLineCount >= 1);

        buffer.ScrollToLine(3, VimScrollAlignment.Center);
        buffer.ScrollToLine(99, VimScrollAlignment.Bottom);
        buffer.ScrollToLine(0, VimScrollAlignment.Top);

        editor.TextArea.PerformTextInput("x");

        Assert.Equal(new VimTextChange(0, 0, "x"), Assert.Single(changes));
    }

    [AvaloniaFact]
    public void WithNoDocument_TheViewportIsOneLineAndAScrollIsSurvived()
    {
        TextEditor editor = new() { Document = null };
        AvaloniaEditVimBuffer buffer = new(editor, _ => { }, _ => { });

        Assert.Equal(1, buffer.FirstVisibleLine);
        Assert.Equal(1, buffer.VisibleLineCount);

        buffer.ScrollToLine(1, VimScrollAlignment.Top);
    }

    /// <summary>
    /// Puts a buffer over the editor and collects everything it reports.
    /// </summary>
    /// <remarks>
    /// The buffer itself is not returned: the control holds it through the handlers it installed, and
    /// no test here asks it anything a document cannot be asked directly.
    /// </remarks>
    private static List<VimTextChange> Listen(TextEditor editor)
    {
        List<VimTextChange> changes = [];

        AvaloniaEditVimBuffer buffer = new(editor, _ => { }, _ => { });
        buffer.Changed += (_, change) => changes.Add(change);

        return changes;
    }
}
