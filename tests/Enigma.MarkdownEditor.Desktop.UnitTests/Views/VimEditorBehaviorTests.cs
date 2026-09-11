using System;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using AvaloniaEdit;
using Enigma.MarkdownEditor.Core.Vim;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.Views;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Views;

/// <summary>
/// The seam between the vim engine and the editor control: the options the engine is seeded with,
/// and the keys that type nothing but reach it all the same.
/// </summary>
/// <remarks>
/// <para>
/// The engine's own behaviour is the Core suite's business, a string at a time. What has to be proved
/// here is the part a string cannot reach: that the indentation settings the editor draws with are
/// the ones <c>&gt;&gt;</c> reads, and that Home, End, Tab, the page keys and the six scroll keys
/// vim spells with Control arrive at the engine from a real key event in normal mode and are left to
/// AvaloniaEdit — or to the markdown assists, for <c>Ctrl+B</c> — in insert mode.
/// </para>
/// <para>
/// The screen cases open a file taller than the window, because a two-line file in a window that
/// shows twenty has no page to scroll, and read the view back off the text view's own visual lines —
/// which is exactly where the adapter reads it from, so what they prove is the whole path from the
/// key to the layout.
/// </para>
/// </remarks>
public sealed class VimEditorBehaviorTests
{
    // Many more lines than the harness's window shows — the headless platform's line height is small,
    // so the window shows well over a hundred — numbered so that a case can name one.
    private const int TallLines = 1000;
    [AvaloniaFact]
    public void Refresh_SeedsTheEnginesOptionsFromTheEditors()
    {
        using EditorHarness harness = new();
        TextEditor editor = new();
        editor.Options.IndentationSize = 2;
        editor.Options.ConvertTabsToSpaces = false;

        VimEditorBehavior behavior = new(editor, () => harness.Vim, _ => { });

        behavior.Refresh();

        Assert.Equal(2, behavior.Options.ShiftWidth);
        Assert.False(behavior.Options.ExpandTab);
    }

    [AvaloniaFact]
    public void Refresh_ReseedsTheOptionsEvenWhenNothingElseChanged()
    {
        using EditorHarness harness = new();
        TextEditor editor = new();
        VimEditorBehavior behavior = new(editor, () => harness.Vim, _ => { });
        behavior.Refresh();

        // The setting did not move, so the engine stays attached — and the options still follow the
        // editor's, because a Refresh is the one moment they are read.
        editor.Options.IndentationSize = 8;
        editor.Options.ConvertTabsToSpaces = true;
        behavior.Refresh();

        Assert.Equal(8, behavior.Options.ShiftWidth);
        Assert.True(behavior.Options.ExpandTab);
    }

    [AvaloniaFact]
    public async Task HomeAndEnd_AreMotionsInNormalModeAndTheEditorsInInsertMode()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md");
        Assert.True(strip.FocusEditor());

        // The file is "# one.md" and a line break. '$' puts the caret on the last character, Home
        // takes it to the first, and End back to the last — normal mode's last character, not the
        // position past it.
        strip.Type("$");
        Assert.Equal(7, strip.Editor.CaretOffset);

        strip.Press(PhysicalKey.Home);
        Assert.Equal(0, strip.Editor.CaretOffset);

        strip.Press(PhysicalKey.End);
        Assert.Equal(7, strip.Editor.CaretOffset);
        Assert.Equal(VimMode.Normal, strip.Mode);

        // In insert mode the same key is AvaloniaEdit's, and its End is the position past the last
        // character, where typing appends.
        strip.Type("i");
        Assert.Equal(VimMode.Insert, strip.Mode);

        strip.Press(PhysicalKey.End);
        Assert.Equal(8, strip.Editor.CaretOffset);

        strip.Press(PhysicalKey.Home);
        Assert.Equal(0, strip.Editor.CaretOffset);
        Assert.Equal("# one.md\n", strip.Editor.Document.Text);
    }

    [AvaloniaFact]
    public async Task Tab_IsSwallowedInNormalModeAndIndentsInInsertMode()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md");
        Assert.True(strip.FocusEditor());

        strip.Press(PhysicalKey.Tab);

        Assert.Equal("# one.md\n", strip.Editor.Document.Text);
        Assert.Equal(VimMode.Normal, strip.Mode);

        strip.Type("i");
        strip.Press(PhysicalKey.Tab);

        // Whatever AvaloniaEdit indents with, the document is no longer what it was.
        Assert.NotEqual("# one.md\n", strip.Editor.Document.Text);
        Assert.Equal(VimMode.Insert, strip.Mode);
    }

    [AvaloniaFact]
    public async Task Enter_IsAMotionInNormalModeAndSplitsTheLineInInsertMode()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md");
        Assert.True(strip.FocusEditor());

        // Two lines: "# one.md" and the empty one after it. Enter moves down onto the empty line.
        strip.Press(PhysicalKey.Enter);

        Assert.Equal(9, strip.Editor.CaretOffset);
        Assert.Equal("# one.md\n", strip.Editor.Document.Text);

        strip.Type("i");
        strip.Press(PhysicalKey.Enter);

        Assert.Equal("# one.md\n\n", strip.Editor.Document.Text);
    }

    [AvaloniaFact]
    public async Task CtrlD_IsAScrollInNormalModeAndTheEditorsInInsertMode()
    {
        using TabStrip strip = await TabStrip.ShowFileAsync("tall.md", Tall());
        Assert.True(strip.FocusEditor());
        string text = strip.Editor.Document.Text;

        // Half a window down: the caret leaves line 1, the document is untouched, and the key never
        // reaches AvaloniaEdit, whose own Ctrl+D would have deleted the line.
        strip.Press(PhysicalKey.D, RawInputModifiers.Control);

        Assert.True(strip.LastKeyHandled);
        Assert.True(strip.Editor.TextArea.Caret.Line > 1);
        Assert.Equal(text, strip.Editor.Document.Text);
        Assert.Equal(VimMode.Normal, strip.Mode);

        // In insert mode the engine turns it down and it goes on to the editor.
        strip.Type("i");
        strip.Press(PhysicalKey.D, RawInputModifiers.Control);

        Assert.False(strip.LastKeyHandled);
        Assert.Equal(VimMode.Insert, strip.Mode);
    }

    [AvaloniaFact]
    public async Task CtrlB_IsAPageUpInNormalModeAndStillBoldsInInsertMode()
    {
        using TabStrip strip = await TabStrip.ShowFileAsync("tall.md", Tall());
        Assert.True(strip.FocusEditor());
        string text = strip.Editor.Document.Text;

        // The assists stand down in the command modes, as the real policy has them do; down a page
        // and back up one, the view is back at the top with the caret on its last line, and nothing
        // was made bold.
        strip.AssistsActive = false;
        strip.Press(PhysicalKey.PageDown);
        int pagedTop = TopLine(strip);
        Assert.True(pagedTop > 1);

        strip.Press(PhysicalKey.B, RawInputModifiers.Control);

        Assert.True(strip.LastKeyHandled);
        Assert.True(TopLine(strip) < pagedTop);
        Assert.True(strip.Editor.TextArea.Caret.Line > TopLine(strip));
        Assert.Equal(text, strip.Editor.Document.Text);

        // In insert mode the assists are active again and Ctrl+B is the bold shortcut, exactly as it
        // was before vim knew the key.
        strip.Type("i");
        strip.AssistsActive = true;
        strip.Press(PhysicalKey.B, RawInputModifiers.Control);

        Assert.True(strip.LastKeyHandled);
        Assert.Equal(VimMode.Insert, strip.Mode);
        Assert.Contains("**", strip.Editor.Document.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task PageDown_IsAPageInNormalModeAndTheEditorsInInsertMode()
    {
        using TabStrip strip = await TabStrip.ShowFileAsync("tall.md", Tall());
        Assert.True(strip.FocusEditor());
        string text = strip.Editor.Document.Text;

        strip.Press(PhysicalKey.PageDown);

        Assert.True(strip.LastKeyHandled);
        Assert.True(strip.Editor.TextArea.Caret.Line > 1);
        Assert.Equal(text, strip.Editor.Document.Text);
        Assert.Equal(VimMode.Normal, strip.Mode);

        strip.Type("i");
        strip.Press(PhysicalKey.PageUp);

        Assert.False(strip.LastKeyHandled);
        Assert.Equal(VimMode.Insert, strip.Mode);
        Assert.Equal(text, strip.Editor.Document.Text);
    }

    [AvaloniaFact]
    public async Task TheScreenMotions_ReadTheRealViewAndZtScrollsIt()
    {
        using TabStrip strip = await TabStrip.ShowFileAsync("tall.md", Tall());
        Assert.True(strip.FocusEditor());

        // 'L' lands on the last line the window shows: past the first, short of the last.
        strip.Type("L");
        int bottom = strip.Editor.TextArea.Caret.Line;

        Assert.InRange(bottom, 2, TallLines - 1);

        // 'zt' from line 50 puts line 50 exactly at the top of the view, which the text view's own
        // first visual line then reports — exactly, because the engine does not follow a 'zt' with the
        // editor's own "show the caret", whose margin would have pulled the view back up.
        strip.Type("50Gzt");

        Assert.Equal(50, strip.Editor.TextArea.Caret.Line);
        Assert.Equal(50, TopLine(strip));

        // 'H' from there is line 50, and 'L' is further down the same screen.
        strip.Type("H");
        Assert.Equal(50, strip.Editor.TextArea.Caret.Line);

        strip.Type("L");
        Assert.True(strip.Editor.TextArea.Caret.Line > 50);
    }

    // The document line at the top of the text view, read the way the engine reads it — through the
    // adapter, off the layout — so that a line cut off at the top edge is not counted.
    private static int TopLine(TabStrip strip)
        => new AvaloniaEditVimBuffer(strip.Editor, _ => { }, _ => { }).FirstVisibleLine;

    private static string Tall()
    {
        StringBuilder text = new();

        for (int number = 1; number <= TallLines; number++)
        {
            text.Append("line ").Append(number).Append('\n');
        }

        return text.ToString();
    }
}
