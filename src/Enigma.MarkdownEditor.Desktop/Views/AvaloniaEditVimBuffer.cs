using System;
using System.Collections.ObjectModel;
using Avalonia;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using Enigma.MarkdownEditor.Core.Vim;

namespace Enigma.MarkdownEditor.Desktop.Views;

/// <summary>
/// The editor control, seen through the engine's eyes.
/// </summary>
/// <remarks>
/// <para>
/// A mapping rather than a translation: AvaloniaEdit numbers lines from 1 and measures everything in
/// offsets into the document, which is vim's own arithmetic, so nothing here has to convert between
/// two ideas of where a character is.
/// </para>
/// <para>
/// The document is read through the control on every call rather than held, because the pane serves
/// every tab from one control and swaps its document underneath. A buffer that had captured one would
/// be editing the file the user has left.
/// </para>
/// <para>
/// The one exception is the subscription behind <see cref="Changed"/>. An event can only be listened
/// to on the document that raises it, so that is the one place this class keeps a reference to a
/// document — and it follows the control's own document swap, moving the subscription to whichever
/// document has just arrived. What the engine hears through <see cref="Changed"/> it takes for what
/// the user typed, so a change to a file that has left the screen must not reach it at all.
/// </para>
/// <para>
/// The view is read off the text view's visual lines — what AvaloniaEdit has actually laid out on
/// screen — rather than worked out from a line height and a pixel offset, so that a wrapped line
/// counts for the rows it takes and the line <c>L</c> names is the one a user sees at the bottom. A
/// scroll is the reverse: the line's top in document pixels, from the same layout, put where the
/// engine asked for it. Neither reading needs the control to be on screen; a view that has never been
/// laid out reports its first line as the top and one line of room, which is enough for every screen
/// command to still name a line the buffer has.
/// </para>
/// </remarks>
public sealed class AvaloniaEditVimBuffer : IVimBuffer
{
    private readonly TextEditor _editor;
    private readonly Action<string?> _status;
    private readonly Action<string?> _commandLine;

    // The stack the open group was started on, not the current document's: a tab switched while a
    // session is open has to close the group where it was opened, or the undo stack of a file nobody
    // is editing keeps collecting.
    private UndoStack? _openGroup;

    // The document whose changes are being forwarded as Changed — the one reference to a document
    // this class holds, and only because there is no listening to an event on anything else. Moved
    // on every swap the control makes, so that it is always the document on screen and never the
    // file the user has left.
    private TextDocument? _listened;

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaEditVimBuffer"/> class over an editor.
    /// </summary>
    /// <param name="editor">The control the pane shows every document in.</param>
    /// <param name="status">Where a message for the status bar goes.</param>
    /// <param name="commandLine">Where the ex/search line goes as it is typed, and its removal.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public AvaloniaEditVimBuffer(TextEditor editor, Action<string?> status, Action<string?> commandLine)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _status = status ?? throw new ArgumentNullException(nameof(status));
        _commandLine = commandLine ?? throw new ArgumentNullException(nameof(commandLine));

        // Attached for as long as the control lives, like every handler the pane installs on it: the
        // buffer is built once per editor and never handed another, so there is nothing to detach
        // from before the control itself goes. The document it starts on may well be none — the pane
        // builds its editor before the first tab arrives.
        _editor.DocumentChanged += OnEditorDocumentChanged;
        Listen(_editor.Document);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Forwarded from the document's own change event, which every route a change can take goes
    /// through — the engine's <see cref="Replace"/>, a key typed in insert mode, a paste, an undo — in
    /// the same three values, so nothing here has to work out who made it.
    /// </remarks>
    public event EventHandler<VimTextChange>? Changed;

    /// <inheritdoc />
    public int TextLength => Document?.TextLength ?? 0;

    /// <inheritdoc />
    public int LineCount => Document?.LineCount ?? 1;

    /// <inheritdoc />
    public int CaretOffset
    {
        get => _editor.CaretOffset;
        set => _editor.CaretOffset = Math.Clamp(value, 0, TextLength);
    }

    /// <inheritdoc />
    public string NewLine
    {
        get
        {
            if (Document is not { } document)
            {
                return Environment.NewLine;
            }

            // The delimiter of the line the caret is on, falling back to the platform's only for a
            // document that has no line break in it yet. A file the editor read has been normalized
            // on the way in and is written back in its own style on the way out, so this keeps a
            // buffer from growing a second kind of line ending while it is being edited.
            return TextUtilities.GetNewLineFromDocument(document, document.GetLineByOffset(CaretOffset).LineNumber);
        }
    }

    private TextDocument? Document => _editor.Document;

    /// <inheritdoc />
    public char CharAt(int offset) => Document?.GetCharAt(offset) ?? '\0';

    /// <inheritdoc />
    public string GetText(int offset, int length)
    {
        if (Document is not { } document)
        {
            return string.Empty;
        }

        int start = Math.Clamp(offset, 0, document.TextLength);

        return document.GetText(start, Math.Clamp(length, 0, document.TextLength - start));
    }

    /// <inheritdoc />
    public VimLine GetLine(int lineNumber)
    {
        if (Document is not { } document)
        {
            return default;
        }

        return Read(document, document.GetLineByNumber(Math.Clamp(lineNumber, 1, document.LineCount)));
    }

    /// <inheritdoc />
    public VimLine GetLineByOffset(int offset)
    {
        if (Document is not { } document)
        {
            return default;
        }

        return Read(document, document.GetLineByOffset(Math.Clamp(offset, 0, document.TextLength)));
    }

    /// <inheritdoc />
    public void Replace(int offset, int length, string text) => Document?.Replace(offset, length, text);

    /// <inheritdoc />
    public void BeginUndoGroup()
    {
        if ((_openGroup is not null) || (Document is not { } document))
        {
            return;
        }

        _openGroup = document.UndoStack;
        _openGroup.StartUndoGroup();
    }

    /// <inheritdoc />
    public void EndUndoGroup()
    {
        UndoStack? stack = _openGroup;
        _openGroup = null;

        stack?.EndUndoGroup();
    }

    /// <inheritdoc />
    /// <remarks>
    /// The editor's own stack, not one the engine keeps: <c>u</c> and <c>Ctrl+Z</c> have to take back
    /// the same step, or a file edited half in normal mode and half with the menu would have two
    /// histories that disagree.
    /// </remarks>
    public void Undo() => _editor.Undo();

    /// <inheritdoc />
    public void Redo() => _editor.Redo();

    /// <inheritdoc />
    /// <remarks>
    /// The control's own selection, which is the whole point of the visual modes going through it: the
    /// highlight a user sees while selecting with <c>v</c> is the one they see while selecting with the
    /// mouse, in the same colours, and it survives a document change the same way.
    /// </remarks>
    public void SetSelection(int start, int end)
    {
        if (Document is not { } document)
        {
            return;
        }

        int from = Math.Clamp(start, 0, document.TextLength);
        int to = Math.Clamp(end, 0, document.TextLength);

        // Assigned rather than set through TextEditor.Select, which drags the caret to the end of the
        // range. In a visual mode the caret is the moving end of the selection and has already been
        // put where it belongs — for a selection made backwards, at its start.
        _editor.TextArea.Selection = Selection.Create(_editor.TextArea, from, to);
    }

    /// <inheritdoc />
    public void ClearSelection() => _editor.TextArea.ClearSelection();

    /// <inheritdoc />
    public void ScrollToCaret() => _editor.TextArea.Caret.BringCaretToView();

    /// <inheritdoc />
    public int FirstVisibleLine => Viewport() is { } view ? view.First : 1;

    /// <inheritdoc />
    /// <remarks>
    /// The document lines the laid-out visual lines cover, a partly hidden last line excluded — or,
    /// when the document is too short to fill the view, the rows the view has room for, so that half
    /// a page is half the window and not half a five-line file.
    /// </remarks>
    public int VisibleLineCount => Viewport() is { } view ? view.Count : 1;

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The line's own top and height, from the layout, against the view's height — so a wrapped line
    /// put at the bottom is wholly at the bottom. The scroll viewer clamps what it is given, so a line
    /// asked to the top when the document ends too soon for that goes as far down as the view allows,
    /// and the caret's line is on screen either way.
    /// </para>
    /// <para>
    /// Through the text view's own <see cref="TextView.MakeVisible"/> rather than
    /// <see cref="TextEditor.ScrollToVerticalOffset"/>, which in AvaloniaEdit 12.0 moves nothing over
    /// this control: a rectangle the height of the view, starting at the offset wanted, is what makes
    /// <c>MakeVisible</c> put exactly that offset at the top, whichever way the view has to move. The
    /// layout is run at once rather than left to the next render, so that a reading of the view in
    /// the same command — the next key of a macro — finds the lines where they are now.
    /// </para>
    /// </remarks>
    public void ScrollToLine(int lineNumber, VimScrollAlignment alignment)
    {
        if (Document is not { } document)
        {
            return;
        }

        TextView view = _editor.TextArea.TextView;
        DocumentLine line = document.GetLineByNumber(Math.Clamp(lineNumber, 1, document.LineCount));

        view.EnsureVisualLines();

        double top = view.GetVisualTopByDocumentLine(line.LineNumber);
        double height = view.GetOrConstructVisualLine(line).Height;
        double room = view.Bounds.Height;

        double offset = alignment switch
        {
            VimScrollAlignment.Center => top - ((room - height) / 2),
            VimScrollAlignment.Bottom => (top + height) - room,
            _ => top,
        };

        view.MakeVisible(new Rect(view.ScrollOffset.X, Math.Max(offset, 0), 1, room));
        view.UpdateLayout();
    }

    /// <inheritdoc />
    public void ShowStatus(string? message) => _status(message);

    /// <inheritdoc />
    public void ShowCommandLine(string? line) => _commandLine(line);

    private static VimLine Read(TextDocument document, DocumentLine line)
        => new(line.LineNumber, line.Offset, document.GetText(line), line.DelimiterLength);

    /// <summary>
    /// Where the view stands: the first document line on screen, and how many lines there is room for.
    /// </summary>
    /// <returns>The two numbers, or <see langword="null"/> when there is no document to show.</returns>
    private (int First, int Count)? Viewport()
    {
        if (Document is null)
        {
            return null;
        }

        TextView view = _editor.TextArea.TextView;

        view.EnsureVisualLines();

        ReadOnlyCollection<VisualLine> lines = view.VisualLines;

        if (lines.Count == 0)
        {
            return null;
        }

        VisualLine first = lines[0];
        VisualLine last = lines[^1];
        double scrolled = view.ScrollOffset.Y;
        double room = view.Bounds.Height;

        // A line that is only partly on screen is not one vim would call visible, at either edge: 'H'
        // goes to the first line a user can read whole and 'L' to the last — and a first line cut off
        // at the top is what a view scrolled to a line's exact top reports for the line above it, a
        // sliver of nothing that would otherwise make Ctrl+E stand still. Each is kept when it is the
        // only line there is.
        if ((lines.Count > 1) && (first.VisualTop < (scrolled - 0.5)))
        {
            first = lines[1];
        }

        if ((lines.Count > 1) && ((last.VisualTop + last.Height) > (scrolled + room + 0.5)))
        {
            last = lines[^2];
        }

        int shown = (last.LastDocumentLine.LineNumber - first.FirstDocumentLine.LineNumber) + 1;

        // A document too short to fill the view still has a window of a certain height round it, and
        // that height is what a page and a half-page are measured against.
        bool fills = view.DocumentHeight > room;
        int rows = view.DefaultLineHeight > 0 ? (int)Math.Floor(room / view.DefaultLineHeight) : 0;

        return (first.FirstDocumentLine.LineNumber, Math.Max(fills ? shown : Math.Max(shown, rows), 1));
    }

    // Read through the control rather than from the event's own NewDocument, like everything else
    // here: the control is the one authority on what is on screen, and the answer cannot be wrong.
    private void OnEditorDocumentChanged(object? sender, DocumentChangedEventArgs e) => Listen(_editor.Document);

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
        => Changed?.Invoke(this, new VimTextChange(e.Offset, e.RemovalLength, e.InsertedText.Text));

    private void Listen(TextDocument? document)
    {
        if (ReferenceEquals(_listened, document))
        {
            return;
        }

        if (_listened is not null)
        {
            _listened.Changed -= OnDocumentChanged;
        }

        _listened = document;

        if (_listened is not null)
        {
            _listened.Changed += OnDocumentChanged;
        }
    }
}
