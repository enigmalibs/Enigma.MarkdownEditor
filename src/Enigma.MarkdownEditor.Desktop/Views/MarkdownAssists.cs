using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using Enigma.MarkdownEditor.Core.Markdown;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.ViewModels;

namespace Enigma.MarkdownEditor.Desktop.Views;

/// <summary>
/// The markdown the editor writes for you: list continuation, Tab indentation, the three formatting
/// shortcuts, and turning an image on the clipboard into a file and a link.
/// </summary>
/// <remarks>
/// <para>
/// Every rule these keys follow lives in <see cref="ListContinuation"/>, <see cref="InlineFormatting"/>
/// and <see cref="ImageLink"/>, where it is a decision over a string and can be tested as one. What is
/// here is the other half: reading the line the caret is on out of the document, applying the answer
/// as one undoable edit, and knowing which keystrokes to take in the first place.
/// </para>
/// <para>
/// The handler is a tunnelling one on the editor rather than a set of key bindings, for the same
/// reason the window takes Ctrl+Tab that way: Enter and Tab already belong to AvaloniaEdit's own input
/// handler, and a bubbling handler would only ever see what it left. Every keystroke this class does
/// not want is left unhandled and reaches that handler exactly as before.
/// </para>
/// <para>
/// Nothing happens at all while <see cref="DocumentManagerViewModel.AssistsActive"/> is false. That is
/// the seam vim emulation (FEATURE-11AA) speaks through: in normal and visual mode these keys are
/// commands, not typing.
/// </para>
/// </remarks>
public sealed class MarkdownAssists
{
    private readonly TextEditor _editor;
    private readonly Func<DocumentManagerViewModel?> _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownAssists"/> class and starts taking
    /// keystrokes from <paramref name="editor"/>.
    /// </summary>
    /// <param name="editor">The editor the pane shows every document in.</param>
    /// <param name="manager">
    /// Reads the pane's ViewModel as it is now. A function rather than the instance, because the pane's
    /// DataContext outlives none of this and is replaced rather than mutated.
    /// </param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public MarkdownAssists(TextEditor editor, Func<DocumentManagerViewModel?> manager)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));

        _editor.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    /// <summary>
    /// Wraps the selection, or the word under the caret, in bold.
    /// </summary>
    public void Bold() => Format(InlineFormatting.BoldDelimiter);

    /// <summary>
    /// Wraps the selection, or the word under the caret, in italic.
    /// </summary>
    public void Italic() => Format(InlineFormatting.ItalicDelimiter);

    /// <summary>
    /// Turns the selection, or the word under the caret, into a link with the caret in its address.
    /// </summary>
    public void Link()
    {
        if (Window() is not { } window)
        {
            return;
        }

        Apply(InlineFormatting.Link(window.Text, window.Start, window.Length), window.Offset);
    }

    /// <summary>
    /// Pastes an image from the clipboard as a file beside the note and a link to it, or pastes text
    /// the way the editor always does.
    /// </summary>
    /// <returns>A task that completes once whatever was on the clipboard is in the document.</returns>
    /// <remarks>
    /// The clipboard can only be asked asynchronously, so the keystroke is taken before the answer is
    /// known and the ordinary paste is performed here rather than left to AvaloniaEdit. An image that
    /// could not be filed falls through to the same ordinary paste, which is what puts the text back
    /// on screen when the clipboard held both.
    /// </remarks>
    public async Task PasteAsync()
    {
        if (_manager() is not { } manager)
        {
            return;
        }

        Bitmap? bitmap = await ClipboardBitmapAsync();

        if (bitmap is null)
        {
            _editor.Paste();
            return;
        }

        using (bitmap)
        {
            if (await manager.PasteImageAsync(new ClipboardImage(bitmap)) is not { } markdown)
            {
                _editor.Paste();
                return;
            }

            InsertAtCaret(markdown);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_editor.IsReadOnly || (_editor.Document is null) || (_manager() is not { AssistsActive: true }))
        {
            return;
        }

        e.Handled = e.KeyModifiers switch
        {
            KeyModifiers.None when e.Key == Key.Enter => TryContinueList(),
            KeyModifiers.None when e.Key == Key.Tab => TryReindent(outdent: false),
            KeyModifiers.Shift when e.Key == Key.Tab => TryReindent(outdent: true),
            KeyModifiers.Control when e.Key == Key.B => Run(Bold),
            KeyModifiers.Control when e.Key == Key.I => Run(Italic),
            KeyModifiers.Control when e.Key == Key.K => Run(Link),
            KeyModifiers.Control when e.Key == Key.V => StartPaste(),
            _ => false,
        };
    }

    private static bool Run(Action action)
    {
        action();

        return true;
    }

    private bool StartPaste()
    {
        // Not awaited, and deliberately: a key handler is void. The task inserts into the document
        // itself, and any failure inside it reaches the Dispatcher's unhandled-exception handler,
        // which is installed before the window is ever shown.
        _ = PasteAsync();

        return true;
    }

    private bool TryContinueList()
    {
        if ((_editor.Document is not { } document) || !_editor.TextArea.Selection.IsEmpty)
        {
            // Enter over a selection replaces it. Continuing the marker of a line that is about to be
            // half deleted would be answering a different question.
            return false;
        }

        DocumentLine line = document.GetLineByOffset(_editor.CaretOffset);
        string text = document.GetText(line);

        if (ListContinuation.OnEnter(text, _editor.CaretOffset - line.Offset) is not { } edit)
        {
            return false;
        }

        Apply(edit, line.Offset);

        return true;
    }

    private bool TryReindent(bool outdent)
    {
        if ((_editor.Document is not { } document) || !_editor.TextArea.Selection.IsEmpty)
        {
            // A selection spanning several lines is AvaloniaEdit's block indent, which is the better
            // answer for it than anything a single item's rules could say.
            return false;
        }

        DocumentLine line = document.GetLineByOffset(_editor.CaretOffset);
        string text = document.GetText(line);

        if (ListContinuation.Parse(text) is null)
        {
            return false;
        }

        int column = _editor.CaretOffset - line.Offset;

        TextEdit? edit = outdent
            ? ListContinuation.OnOutdent(text, column, LinesAbove(document, line))
            : ListContinuation.OnIndent(text, column, LinesAbove(document, line));

        if (edit is { } reindent)
        {
            Apply(reindent, line.Offset);
        }

        // Handled either way. Inside a list item there is no useful second meaning for Tab, and a
        // literal tab character dropped into the middle of a bullet is not one.
        return true;
    }

    private void Format(string delimiter)
    {
        if (Window() is not { } window)
        {
            return;
        }

        Apply(InlineFormatting.Toggle(window.Text, window.Start, window.Length, delimiter), window.Offset);
    }

    // The lines a selection covers, which is as much of the document as the formatting rules need to
    // see. Reading the whole of it would copy the file on every Ctrl+B.
    private FormattingWindow? Window()
    {
        if (_editor.IsReadOnly || (_editor.Document is not { } document))
        {
            return null;
        }

        int start = _editor.SelectionStart;
        int length = _editor.SelectionLength;

        DocumentLine first = document.GetLineByOffset(start);
        DocumentLine last = length == 0 ? first : document.GetLineByOffset(start + length);

        return new FormattingWindow(
            document.GetText(first.Offset, last.EndOffset - first.Offset),
            first.Offset,
            start - first.Offset,
            length);
    }

    private static IEnumerable<string> LinesAbove(TextDocument document, DocumentLine line)
    {
        for (DocumentLine? above = line.PreviousLine; above is not null; above = above.PreviousLine)
        {
            yield return document.GetText(above);
        }
    }

    private async Task<Bitmap?> ClipboardBitmapAsync()
    {
        if (TopLevel.GetTopLevel(_editor)?.Clipboard is not { } clipboard)
        {
            return null;
        }

        try
        {
            return await clipboard.TryGetBitmapAsync();
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // A clipboard another application is holding open, or one whose image cannot be decoded.
            // Nothing is said about it: the ordinary paste that follows is the answer, and it is the
            // one the user asked for.
            return null;
        }
    }

    private void InsertAtCaret(string text)
    {
        if (_editor.Document is not { } document)
        {
            return;
        }

        using (document.RunUpdate())
        {
            int offset = _editor.SelectionLength > 0 ? _editor.SelectionStart : _editor.CaretOffset;

            document.Replace(offset, _editor.SelectionLength, text);
            _editor.TextArea.ClearSelection();
            _editor.CaretOffset = offset + text.Length;
        }
    }

    // One undo group, so a continued bullet or a pair of asterisks comes back off in a single Ctrl+Z
    // rather than in the two or three edits it was made of.
    private void Apply(TextEdit edit, int windowOffset)
    {
        if (_editor.Document is not { } document)
        {
            return;
        }

        using (document.RunUpdate())
        {
            document.Replace(windowOffset + edit.Start, edit.Length, edit.Replacement);

            if (edit.SelectionLength > 0)
            {
                _editor.Select(windowOffset + edit.CaretOffset, edit.SelectionLength);
            }
            else
            {
                _editor.TextArea.ClearSelection();
                _editor.CaretOffset = windowOffset + edit.CaretOffset;
            }
        }
    }

    private readonly record struct FormattingWindow(string Text, int Offset, int Start, int Length);
}
