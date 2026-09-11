using System;

namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// The text the vim engine acts on, reduced to what it actually asks for.
/// </summary>
/// <remarks>
/// <para>
/// The seam that keeps the engine free of Avalonia. Everything vim does is expressed as reads and
/// writes through this interface, so the whole command surface is reachable from a test with a string
/// in it — which matters here more than anywhere else in the application, because a modal editor is
/// a state machine whose bugs are combinations rather than lines.
/// </para>
/// <para>
/// Offsets are into the whole buffer and lines are numbered from 1, which is both vim's convention
/// and AvaloniaEdit's, so the adapter is a mapping rather than a translation.
/// </para>
/// <para>
/// Two of its members are not about text at all — <see cref="ShowStatus"/> and
/// <see cref="ShowCommandLine"/>. They are here because everything the engine says goes to the same
/// strip beside the mode indicator, and the editor is what knows where that is. What the engine asks
/// the <em>application</em> for is a different matter and leaves by a different door:
/// <see cref="VimEngine.ExCommandRequested"/>, because saving and closing a file are not something a
/// buffer can be asked to do.
/// </para>
/// <para>
/// One member goes the other way — <see cref="Changed"/>, the buffer telling the engine. It exists
/// because insert mode is the one place the engine does not see the keys: what is typed there goes
/// to the editor, and the change it makes is the only trace. <c>.</c> and a macro both need that
/// trace, so the buffer reports every change and the engine listens while an insert session is open.
/// </para>
/// <para>
/// Three members are about the view rather than the text — <see cref="FirstVisibleLine"/>,
/// <see cref="VisibleLineCount"/> and <see cref="ScrollToLine"/>. They exist because a handful of
/// vim's commands are about the screen: <c>H</c> is the top line <em>on screen</em>, <c>Ctrl+D</c>
/// is half a <em>window</em>, <c>zz</c> puts the caret's line in the middle of the <em>view</em>.
/// The engine cannot know any of that from the text, so the editor reports where the view stands and
/// takes an instruction to move it; everything about what those commands mean stays on this side.
/// </para>
/// </remarks>
public interface IVimBuffer
{
    /// <summary>
    /// Raised after every change to the text, whoever made it — the engine, the editor's own typing,
    /// an undo.
    /// </summary>
    /// <remarks>
    /// Raised for the document the buffer shows <em>now</em>: an implementation over a control that
    /// swaps documents must follow the swap, so that a change to a document no longer on screen is
    /// not reported as one the user typed.
    /// </remarks>
    event EventHandler<VimTextChange>? Changed;

    /// <summary>
    /// Gets the number of characters in the buffer.
    /// </summary>
    int TextLength { get; }

    /// <summary>
    /// Gets the number of lines in the buffer, never less than one.
    /// </summary>
    int LineCount { get; }

    /// <summary>
    /// Gets or sets where the caret is, as an offset into the buffer.
    /// </summary>
    /// <remarks>
    /// The engine clamps before it writes; an implementation is still expected to survive an offset
    /// outside the buffer rather than throwing, because the document can change underneath it.
    /// </remarks>
    int CaretOffset { get; set; }

    /// <summary>
    /// Gets the line terminator to write when a command adds a line.
    /// </summary>
    /// <remarks>
    /// The buffer's own, not the platform's: a file is read, edited and written back with the endings
    /// it arrived with, and <c>o</c> must not be the one command that mixes a second kind in.
    /// </remarks>
    string NewLine { get; }

    /// <summary>
    /// Reads one character.
    /// </summary>
    /// <param name="offset">Where to read, inside the buffer.</param>
    /// <returns>The character at <paramref name="offset"/>.</returns>
    /// <remarks>
    /// A character at a time, because that is how a word motion walks: it stops at the first change
    /// of character class, which is normally a handful of characters away and never a whole line.
    /// </remarks>
    char CharAt(int offset);

    /// <summary>
    /// Reads a run of text.
    /// </summary>
    /// <param name="offset">Where it starts.</param>
    /// <param name="length">How long it is.</param>
    /// <returns>The text.</returns>
    /// <remarks>
    /// What the register is filled from. A run rather than a character at a time, because a yank
    /// knows exactly how much it wants before it asks — unlike a word motion, which is looking for
    /// where to stop.
    /// </remarks>
    string GetText(int offset, int length);

    /// <summary>
    /// Reads a line by its number.
    /// </summary>
    /// <param name="lineNumber">The 1-based line number, clamped by the implementation.</param>
    /// <returns>The line.</returns>
    VimLine GetLine(int lineNumber);

    /// <summary>
    /// Reads the line an offset falls on.
    /// </summary>
    /// <param name="offset">An offset into the buffer, clamped by the implementation.</param>
    /// <returns>The line.</returns>
    VimLine GetLineByOffset(int offset);

    /// <summary>
    /// Replaces a run of text.
    /// </summary>
    /// <param name="offset">Where the run starts.</param>
    /// <param name="length">How long it is. Zero for a pure insertion.</param>
    /// <param name="text">What takes its place. Empty for a pure deletion.</param>
    void Replace(int offset, int length, string text);

    /// <summary>
    /// Starts collecting every change that follows into one undo step.
    /// </summary>
    /// <remarks>
    /// Paired with <see cref="EndUndoGroup"/> rather than scoped by a <c>using</c>, because the group
    /// an insert session opens stays open across every keystroke until <c>Esc</c> — which is exactly
    /// what makes <c>i</c>, a sentence and <c>Esc</c> come back off in one <c>u</c>.
    /// </remarks>
    void BeginUndoGroup();

    /// <summary>
    /// Closes the group <see cref="BeginUndoGroup"/> opened.
    /// </summary>
    /// <remarks>
    /// Must tolerate being called without an open group: the engine closes one on every route out of
    /// insert mode, including the ones a document swap or a disabled toggle takes.
    /// </remarks>
    void EndUndoGroup();

    /// <summary>
    /// Takes back the last change, or does nothing when there is none.
    /// </summary>
    /// <remarks>
    /// <c>u</c>. The engine keeps no history of its own: what a change costs to undo is decided by
    /// where the undo groups were opened, which is the whole reason
    /// <see cref="BeginUndoGroup"/> exists.
    /// </remarks>
    void Undo();

    /// <summary>
    /// Puts back what <see cref="Undo"/> took, or does nothing when there is nothing to put back.
    /// </summary>
    void Redo();

    /// <summary>
    /// Shows a stretch of the buffer as selected.
    /// </summary>
    /// <param name="start">Where the selection begins.</param>
    /// <param name="end">Where it ends, one past the last character it covers.</param>
    /// <remarks>
    /// <para>
    /// Two offsets and no kind, because a linewise selection is a characterwise one that happens to
    /// start and end on line boundaries: the engine works out which offsets a visual mode covers, and
    /// what is left here is the editor's own selection, which is the whole point — the highlight a
    /// user sees while selecting with vim keys is the one they see when selecting with the mouse.
    /// </para>
    /// <para>
    /// Must not move the caret. In a visual mode the caret is the moving end of the selection, and
    /// for a selection made backwards it sits at <paramref name="start"/> rather than at
    /// <paramref name="end"/>, so an implementation that dragged it to one end would break every
    /// selection made leftwards or upwards.
    /// </para>
    /// </remarks>
    void SetSelection(int start, int end);

    /// <summary>
    /// Takes the selection away, leaving the caret where it is.
    /// </summary>
    /// <remarks>
    /// Must tolerate being called when there is no selection: the engine clears one on every route out
    /// of a visual mode, including the ones a document swap or a disabled toggle takes.
    /// </remarks>
    void ClearSelection();

    /// <summary>
    /// Brings the caret back into view after a motion that may have left it off screen.
    /// </summary>
    void ScrollToCaret();

    /// <summary>
    /// Gets the number of the first line on screen, counted from one.
    /// </summary>
    /// <remarks>
    /// The top of the view as the user sees it. An implementation that shows nothing yet — no
    /// document, no layout — answers one, so that every screen motion still has a line to name.
    /// </remarks>
    int FirstVisibleLine { get; }

    /// <summary>
    /// Gets how many lines the view has room for at once, never less than one.
    /// </summary>
    /// <remarks>
    /// The window's height in lines, which is what vim measures a page and a half-page against — so
    /// with a buffer shorter than the view it is still the room the view has, and <c>Ctrl+D</c> in a
    /// five-line file goes to the last line as it does in vim. When lines wrap, the number of
    /// document lines actually on screen: a wrapped line takes rows a whole line would have had.
    /// </remarks>
    int VisibleLineCount { get; }

    /// <summary>
    /// Scrolls the view so that a line sits at the top, the middle or the bottom of it.
    /// </summary>
    /// <param name="lineNumber">The 1-based line number, clamped by the implementation.</param>
    /// <param name="alignment">Where in the view the line goes.</param>
    /// <remarks>
    /// Must not move the caret: the engine decides what happens to the caret after a scroll —
    /// nothing for <c>zz</c>, a pull onto the nearest visible line for <c>Ctrl+E</c> — and does it
    /// itself. The line goes exactly where it is asked, with no margin kept round it: the engine
    /// aligns the view on the line the caret ends up on, and relies on that line being on the edge it
    /// named. An implementation whose view cannot go as far as asked — an editor that will not show
    /// empty space under the last line — goes as far as it can.
    /// </remarks>
    void ScrollToLine(int lineNumber, VimScrollAlignment alignment);

    /// <summary>
    /// Shows a message beside the mode indicator, or clears the one that is there.
    /// </summary>
    /// <param name="message">What to say, or <see langword="null"/> to clear.</param>
    void ShowStatus(string? message);

    /// <summary>
    /// Shows the command line as it is being typed, or takes it away.
    /// </summary>
    /// <param name="line">
    /// The whole line including the <c>:</c>, <c>/</c> or <c>?</c> that opened it, or
    /// <see langword="null"/> when it has been run or cancelled.
    /// </param>
    /// <remarks>
    /// The engine keeps the text and re-sends the whole line on every keystroke, rather than the
    /// editor hosting a text box of its own. That is what makes the command line one more thing the
    /// state machine can be tested for — and it keeps the keyboard focus in the document, which a
    /// real text box would have to take away and give back.
    /// </remarks>
    void ShowCommandLine(string? line);
}
