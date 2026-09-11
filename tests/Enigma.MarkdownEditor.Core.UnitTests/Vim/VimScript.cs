using System;
using System.Collections.Generic;
using System.Text;
using Enigma.MarkdownEditor.Core.Vim;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// Runs a string of keys against a buffer written as text with a <c>|</c> where the caret is.
/// </summary>
/// <remarks>
/// <para>
/// What turns a state-machine test into one readable line: <c>Run("a|bc", "x")</c> is "with the caret
/// on the b, press x", and the answer comes back in the same notation — <c>"a|c"</c> — so a case
/// asserts on what the editor looks like rather than on an offset nobody can picture.
/// </para>
/// <para>
/// Keys are written in <see cref="VimKeyNotation"/> — the engine's own, the one a macro register
/// holds: the characters of the string, with the named ones in angle brackets (<c>&lt;Esc&gt;</c>,
/// <c>&lt;CR&gt;</c>, <c>&lt;BS&gt;</c>, <c>&lt;Tab&gt;</c>, <c>&lt;Home&gt;</c>, <c>&lt;End&gt;</c>,
/// the arrows, <c>&lt;Other&gt;</c> for a key the engine has no meaning for, <c>&lt;C-x&gt;</c> for
/// Control plus a character) and <c>&lt;lt&gt;</c> for a literal <c>&lt;</c>. A space in the string
/// is the space bar.
/// </para>
/// <para>
/// A key the engine passes through is typed into the buffer, exactly as the text editor would type
/// it. That is what makes insert mode testable at all: the engine never sees the text it lets past,
/// so a harness that dropped those keys would prove the opposite of what the tests are for.
/// </para>
/// <para>
/// A visual selection reads back in the same notation with two more markers: <c>a[b|c]d</c> is "b and
/// c are selected, the caret is on c". Which end the caret is at is the point — a selection made
/// leftwards is <c>a[|bc]d</c> — and a picture is the only way to assert on that and still be able to
/// read the case afterwards.
/// </para>
/// </remarks>
internal static class VimScript
{
    /// <summary>
    /// The caret, in the notation a case is written and read in.
    /// </summary>
    public const char CaretMarker = '|';

    /// <summary>
    /// Where the selection begins, when a case is reading one back.
    /// </summary>
    public const char SelectionStartMarker = '[';

    /// <summary>
    /// Where it ends, one past the last character it covers.
    /// </summary>
    public const char SelectionEndMarker = ']';

    /// <summary>
    /// Runs <paramref name="keys"/> against <paramref name="textWithCaret"/>.
    /// </summary>
    /// <param name="textWithCaret">The buffer, with a <c>|</c> where the caret is.</param>
    /// <param name="keys">The keys to type.</param>
    /// <param name="options">The engine's options, or <see langword="null"/> for the defaults.</param>
    public static VimRun Run(string textWithCaret, string keys, VimOptions? options = null)
        => Run(NewBuffer(textWithCaret), keys, options);

    /// <summary>
    /// Runs <paramref name="keys"/> against <paramref name="textWithCaret"/> seen through a window
    /// <paramref name="viewportLines"/> lines tall, with the buffer's first line at the top.
    /// </summary>
    /// <param name="textWithCaret">The buffer, with a <c>|</c> where the caret is.</param>
    /// <param name="keys">The keys to type.</param>
    /// <param name="viewportLines">How many lines the window has room for.</param>
    public static VimRun Run(string textWithCaret, string keys, int viewportLines)
        => Run(textWithCaret, keys, viewportLines, firstVisibleLine: 1);

    /// <summary>
    /// Runs <paramref name="keys"/> against <paramref name="textWithCaret"/> seen through a window
    /// <paramref name="viewportLines"/> lines tall, scrolled so that <paramref name="firstVisibleLine"/>
    /// is at the top.
    /// </summary>
    /// <param name="textWithCaret">The buffer, with a <c>|</c> where the caret is.</param>
    /// <param name="keys">The keys to type.</param>
    /// <param name="viewportLines">How many lines the window has room for.</param>
    /// <param name="firstVisibleLine">The line at the top of the window when the keys start.</param>
    /// <remarks>
    /// The caret is where the <c>|</c> puts it whether or not that is on screen: a case about
    /// <c>Ctrl+E</c> pulling the caret back into view needs it off screen to begin with.
    /// </remarks>
    public static VimRun Run(string textWithCaret, string keys, int viewportLines, int firstVisibleLine)
    {
        FakeVimBuffer buffer = NewBuffer(textWithCaret);
        buffer.ViewportLines = viewportLines;
        buffer.FirstVisibleLine = firstVisibleLine;

        return Run(buffer, keys, options: null);
    }

    private static VimRun Run(FakeVimBuffer buffer, string keys, VimOptions? options)
    {
        VimEngine engine = new(buffer, options);

        // Collected rather than acted on: what ':wq' amounts to is the application's business, and
        // what a case here asks is only whether the engine asked for it, once, and for the right one.
        List<VimExCommand> exCommands = [];
        engine.ExCommandRequested += (_, command) => exCommands.Add(command);

        foreach (VimKey key in VimKeyNotation.Parse(keys))
        {
            if (!engine.HandleKey(key))
            {
                Type(buffer, key);
            }
        }

        return new VimRun(Render(buffer), engine.Mode, buffer, engine, exCommands);
    }

    /// <summary>
    /// Builds a buffer from text with a <c>|</c> in it, with the caret where the marker was.
    /// </summary>
    public static FakeVimBuffer NewBuffer(string textWithCaret)
    {
        int caret = textWithCaret.IndexOf(CaretMarker, StringComparison.Ordinal);

        if (caret < 0)
        {
            throw new ArgumentException(
                $"A case has to say where the caret is, with a '{CaretMarker}'.",
                nameof(textWithCaret));
        }

        return new FakeVimBuffer(textWithCaret.Remove(caret, 1)) { CaretOffset = caret };
    }

    /// <summary>
    /// Writes a buffer back out with a <c>|</c> where its caret is.
    /// </summary>
    public static string Render(FakeVimBuffer buffer)
        => buffer.Text.Insert(Math.Clamp(buffer.CaretOffset, 0, buffer.TextLength), CaretMarker.ToString());

    /// <summary>
    /// Writes a buffer back out with a <c>|</c> where its caret is and the selection in brackets.
    /// </summary>
    public static string RenderSelection(FakeVimBuffer buffer)
    {
        string text = buffer.Text;
        int caret = Math.Clamp(buffer.CaretOffset, 0, text.Length);
        (int start, int end) = buffer.Selection ?? (-1, -1);

        StringBuilder builder = new();

        // One pass over the positions rather than three insertions into a string: the three markers
        // can land on the same offset — a selection made leftwards puts the caret on its start — and
        // this is what fixes which order they come out in.
        for (int offset = 0; offset <= text.Length; offset++)
        {
            if (offset == start)
            {
                builder.Append(SelectionStartMarker);
            }

            if (offset == caret)
            {
                builder.Append(CaretMarker);
            }

            if (offset == end)
            {
                builder.Append(SelectionEndMarker);
            }

            if (offset < text.Length)
            {
                builder.Append(text[offset]);
            }
        }

        return builder.ToString();
    }

    private static void Type(FakeVimBuffer buffer, VimKey key)
    {
        // Only what a text editor would actually put in the document, or do to the caret. The arrow
        // keys do nothing; they are here so that a case can prove insert mode leaves them to the
        // editor. Enter, Backspace and Tab are the named keys that do something in insert mode,
        // because AvaloniaEdit splits the line, rubs a character out and indents with them — and Home
        // and End move the caret — and the engine, which turns them all down in insert mode, has to be
        // shown letting that happen.
        switch (key.Special)
        {
            case VimSpecialKey.Enter:
                buffer.Type('\n');
                break;

            case VimSpecialKey.Backspace:
                buffer.Rub();
                break;

            case VimSpecialKey.Tab:
                buffer.Type('\t');
                break;

            case VimSpecialKey.Home:
                buffer.CaretOffset = buffer.GetLineByOffset(buffer.CaretOffset).Offset;
                break;

            case VimSpecialKey.End:
                buffer.CaretOffset = buffer.GetLineByOffset(buffer.CaretOffset).EndOffset;
                break;

            case VimSpecialKey.None when !char.IsControl(key.Character) && !key.HasCommandModifier:
                buffer.Type(key.Character);
                break;

            default:
                break;
        }
    }
}

/// <summary>
/// What a run left behind.
/// </summary>
/// <param name="Text">The buffer, with a <c>|</c> where the caret is.</param>
/// <param name="Mode">The mode the engine ended in.</param>
/// <param name="Buffer">The buffer itself, for the cases that ask about undo groups or the status.</param>
/// <param name="Engine">The engine itself, for the cases that ask what is still pending.</param>
/// <param name="ExCommands">The ex commands the run asked the application for, in order.</param>
internal sealed record VimRun(
    string Text,
    VimMode Mode,
    FakeVimBuffer Buffer,
    VimEngine Engine,
    IReadOnlyList<VimExCommand> ExCommands)
{
    /// <summary>
    /// What the run left in the unnamed register, or <see langword="null"/> if it took nothing.
    /// </summary>
    public VimRegister? Register => Engine.Register;

    /// <summary>
    /// What the run left in a named register, or <see langword="null"/> if it holds nothing.
    /// </summary>
    /// <param name="name">The register's name, as it would be typed after a <c>"</c>.</param>
    public VimRegister? RegisterNamed(char name) => Engine.Registers.Get(name);

    /// <summary>
    /// The buffer with a <c>|</c> where the caret is and the selection between brackets.
    /// </summary>
    public string Highlight => VimScript.RenderSelection(Buffer);

    /// <summary>
    /// The text the selection covers, or the empty string when nothing is selected.
    /// </summary>
    public string SelectedText => Buffer.SelectedText;

    /// <summary>
    /// What the run left on the status bar, or <see langword="null"/> when it said nothing.
    /// </summary>
    public string? Status => Buffer.Status;

    /// <summary>
    /// The command line the run left open, or <see langword="null"/> when there is none.
    /// </summary>
    public string? CommandLine => Buffer.CommandLine;

    /// <summary>
    /// The line at the top of the window when the run ended.
    /// </summary>
    public int FirstVisibleLine => Buffer.FirstVisibleLine;

    /// <summary>
    /// The line at the bottom of the window when the run ended.
    /// </summary>
    public int LastVisibleLine => Buffer.LastVisibleLine;

    /// <summary>
    /// The line the caret ended on, counted from one.
    /// </summary>
    public int CaretLine => Buffer.GetLineByOffset(Buffer.CaretOffset).Number;

    /// <summary>
    /// The column the caret ended in, counted from zero.
    /// </summary>
    public int CaretColumn => Buffer.CaretOffset - Buffer.GetLineByOffset(Buffer.CaretOffset).Offset;
}
