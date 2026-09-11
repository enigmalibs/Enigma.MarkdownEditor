using System;
using System.Collections.Generic;
using System.Text;
using Enigma.MarkdownEditor.Core.Vim;

namespace Enigma.MarkdownEditor.Core.UnitTests.Vim;

/// <summary>
/// A buffer that is a string, a caret and a counter or two.
/// </summary>
/// <remarks>
/// <para>
/// The whole reason <see cref="IVimBuffer"/> exists. Every rule the engine follows is reachable from
/// here without a text control, a window or a UI thread, which is what makes a command matrix of
/// several hundred cases cost a line each.
/// </para>
/// <para>
/// Lines are split on <c>\n</c> alone, which is what the editor holds: a document is normalized on
/// read and written back in the file's own style, so a buffer with carriage returns in it is not a
/// state the engine can be in.
/// </para>
/// <para>
/// Undo is a stack of whole-text snapshots with the same grouping rule the real one has — a change
/// inside an open group joins the step already there rather than starting a new one. Crude next to
/// AvaloniaEdit's, and exactly as crude as it needs to be: what the tests ask is "how many steps did
/// that command cost", which is a question about the grouping and not about the diffing.
/// </para>
/// <para>
/// Every write raises <see cref="Changed"/>, the engine's included, exactly as a real document
/// would: what the engine does with the report — record it as typing or not — is the engine's to get
/// right, and a fake that reported only the editor's typing would hide the cases where it does not.
/// </para>
/// <para>
/// The view is two numbers: which line is at the top, and how many lines the window has room for.
/// With no height set the window is as tall as the buffer, so that the hundreds of cases that never
/// scroll see every line; a case about scrolling sets <see cref="ViewportLines"/> and reads back
/// <see cref="FirstVisibleLine"/> and <see cref="Scrolls"/> — every instruction the engine gave, in
/// order, because whether the engine asked for the right line at the right place is the whole of what
/// the real editor is then trusted with.
/// </para>
/// </remarks>
internal sealed class FakeVimBuffer : IVimBuffer
{
    private readonly StringBuilder _text;
    private readonly List<Snapshot> _undo = [];
    private readonly List<Snapshot> _redo = [];
    private readonly List<(int Line, VimScrollAlignment Alignment)> _scrolls = [];

    private bool _groupHasStep;
    private int _firstVisibleLine = 1;

    public FakeVimBuffer(string text)
    {
        _text = new StringBuilder(text);
        CaretOffset = 0;
    }

    /// <inheritdoc />
    public event EventHandler<VimTextChange>? Changed;

    /// <summary>
    /// The buffer's whole text.
    /// </summary>
    public string Text => _text.ToString();

    /// <inheritdoc />
    public int CaretOffset { get; set; }

    /// <inheritdoc />
    public int TextLength => _text.Length;

    /// <inheritdoc />
    public int LineCount => Lines().Count;

    /// <inheritdoc />
    public string NewLine { get; init; } = "\n";

    /// <summary>
    /// How many undo groups have been opened, and how many of those are still open.
    /// </summary>
    public int UndoGroupsOpened { get; private set; }

    /// <summary>
    /// How many times the text has been written, whoever wrote it.
    /// </summary>
    /// <remarks>
    /// The engine's stated rule is that one command is one call to <see cref="Replace"/> — which is
    /// what makes one command one undo step without a group having to say so — and this is what lets
    /// a case assert it.
    /// </remarks>
    public int Replacements { get; private set; }

    /// <summary>
    /// How many undo groups are open right now. An insert session leaves exactly one.
    /// </summary>
    public int OpenUndoGroups { get; private set; }

    /// <summary>
    /// How many steps are on the undo stack — how many times <c>u</c> would change something.
    /// </summary>
    public int UndoSteps => _undo.Count;

    /// <summary>
    /// What the engine has asked to be shown as selected, or <see langword="null"/> when nothing is.
    /// </summary>
    /// <remarks>
    /// Two offsets, exactly as the real one gets them: what a visual mode is <em>for</em> is the
    /// highlight, so "did the engine ask for the right one" is a question the tests have to be able to
    /// put, and the real control's answer to it is not reachable from here.
    /// </remarks>
    public (int Start, int End)? Selection { get; private set; }

    /// <summary>
    /// The text the selection covers, or the empty string when there is none.
    /// </summary>
    public string SelectedText
        => Selection is { } selection ? _text.ToString(selection.Start, selection.End - selection.Start) : string.Empty;

    /// <summary>
    /// The last message the engine put on the status bar, or <see langword="null"/> if it cleared it.
    /// </summary>
    public string? Status { get; private set; }

    /// <summary>
    /// The command line as the engine last showed it, or <see langword="null"/> when it took it away.
    /// </summary>
    public string? CommandLine { get; private set; }

    /// <summary>
    /// How many times the engine asked for the caret to be brought into view.
    /// </summary>
    public int ScrollRequests { get; private set; }

    /// <summary>
    /// How many lines the window has room for, or zero for a window as tall as the buffer.
    /// </summary>
    public int ViewportLines { get; set; }

    /// <inheritdoc />
    /// <remarks>
    /// Settable, so that a case can start with the view part way down the buffer, and clamped on the
    /// way out so that a view left below a buffer that has since shrunk still names a line it has.
    /// </remarks>
    public int FirstVisibleLine
    {
        get => Math.Clamp(_firstVisibleLine, 1, LineCount);
        set => _firstVisibleLine = value;
    }

    /// <inheritdoc />
    public int VisibleLineCount => Math.Max(ViewportLines > 0 ? ViewportLines : LineCount, 1);

    /// <summary>
    /// The last line on screen: the top line plus the room, or the buffer's last line when the buffer
    /// ends first.
    /// </summary>
    public int LastVisibleLine => Math.Min((FirstVisibleLine + VisibleLineCount) - 1, LineCount);

    /// <summary>
    /// Every scroll the engine asked for, in order — which line, and where on the screen.
    /// </summary>
    public IReadOnlyList<(int Line, VimScrollAlignment Alignment)> Scrolls => _scrolls;

    /// <inheritdoc />
    public char CharAt(int offset) => _text[offset];

    /// <inheritdoc />
    public string GetText(int offset, int length) => _text.ToString(offset, length);

    /// <inheritdoc />
    public VimLine GetLine(int lineNumber)
    {
        IReadOnlyList<VimLine> lines = Lines();

        return lines[Math.Clamp(lineNumber, 1, lines.Count) - 1];
    }

    /// <inheritdoc />
    public VimLine GetLineByOffset(int offset)
    {
        int clamped = Math.Clamp(offset, 0, _text.Length);

        foreach (VimLine line in Lines())
        {
            if ((clamped >= line.Offset) && (clamped <= line.EndOffset))
            {
                return line;
            }
        }

        return GetLine(LineCount);
    }

    /// <inheritdoc />
    public void Replace(int offset, int length, string text)
    {
        Replacements++;
        Capture();

        _text.Remove(offset, length);
        _text.Insert(offset, text);

        Changed?.Invoke(this, new VimTextChange(offset, length, text));
    }

    /// <inheritdoc />
    public void BeginUndoGroup()
    {
        UndoGroupsOpened++;
        OpenUndoGroups++;
        _groupHasStep = false;
    }

    /// <inheritdoc />
    public void EndUndoGroup()
    {
        // The real stack throws when a group is closed that was never opened. Counting down to a
        // negative number here is what would catch the engine doing it.
        OpenUndoGroups--;
        _groupHasStep = false;
    }

    /// <inheritdoc />
    public void Undo()
    {
        if (_undo.Count == 0)
        {
            return;
        }

        Snapshot restored = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        _redo.Add(Current());

        Restore(restored);
    }

    /// <inheritdoc />
    public void Redo()
    {
        if (_redo.Count == 0)
        {
            return;
        }

        Snapshot restored = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        _undo.Add(Current());

        Restore(restored);
    }

    /// <inheritdoc />
    public void SetSelection(int start, int end) => Selection = (start, end);

    /// <inheritdoc />
    public void ClearSelection() => Selection = null;

    /// <inheritdoc />
    public void ScrollToCaret() => ScrollRequests++;

    /// <inheritdoc />
    /// <remarks>
    /// The view goes exactly where it is asked, vim's way: a line put at the top may be the buffer's
    /// last, with nothing under it, and a line put in the middle or at the bottom is worked back to
    /// the top line that shows it there — never above the first line, because there is nothing above
    /// the first line to show.
    /// </remarks>
    public void ScrollToLine(int lineNumber, VimScrollAlignment alignment)
    {
        int line = Math.Clamp(lineNumber, 1, LineCount);
        int rows = VisibleLineCount;

        _scrolls.Add((line, alignment));

        _firstVisibleLine = alignment switch
        {
            VimScrollAlignment.Center => Math.Max(line - ((rows - 1) / 2), 1),
            VimScrollAlignment.Bottom => Math.Max((line - rows) + 1, 1),
            _ => line,
        };
    }

    /// <inheritdoc />
    public void ShowStatus(string? message) => Status = message;

    /// <inheritdoc />
    public void ShowCommandLine(string? line) => CommandLine = line;

    /// <summary>
    /// Types a character the way the editor does when the engine passes a key through, so that insert
    /// mode can be driven end to end.
    /// </summary>
    public void Type(char character)
    {
        string text = character == '\n' ? NewLine : character.ToString();

        Replace(CaretOffset, 0, text);
        CaretOffset += text.Length;
    }

    /// <summary>
    /// Rubs out the character before the caret, the way the editor does when the engine passes
    /// Backspace through in insert mode.
    /// </summary>
    public void Rub()
    {
        if (CaretOffset <= 0)
        {
            return;
        }

        Replace(CaretOffset - 1, 1, string.Empty);
        CaretOffset--;
    }

    private Snapshot Current() => new(Text, CaretOffset);

    private void Restore(Snapshot snapshot)
    {
        int removed = _text.Length;

        _text.Clear();
        _text.Append(snapshot.Text);
        CaretOffset = snapshot.Caret;

        // Reported as one change over the whole text, which is honest and is also what the engine
        // needs: an undo in the middle of an insert session is not typing, and the record ends there.
        Changed?.Invoke(this, new VimTextChange(0, removed, snapshot.Text));
    }

    private void Capture()
    {
        if (_groupHasStep)
        {
            return;
        }

        _undo.Add(Current());
        _redo.Clear();

        // Everything else that happens before the group closes joins this step. That is the rule the
        // acceptance criterion is about: 'cc', the text typed after it and 'Esc' cost one 'u'.
        _groupHasStep = OpenUndoGroups > 0;
    }

    private List<VimLine> Lines()
    {
        List<VimLine> lines = [];
        string text = _text.ToString();

        int start = 0;
        int number = 1;

        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] != '\n')
            {
                continue;
            }

            lines.Add(new VimLine(number++, start, text[start..index], 1));
            start = index + 1;
        }

        lines.Add(new VimLine(number, start, text[start..], 0));

        return lines;
    }

    private readonly record struct Snapshot(string Text, int Caret);
}
