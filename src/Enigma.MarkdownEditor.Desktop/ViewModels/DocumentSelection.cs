using System;
using AvaloniaEdit.Document;

namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// A request to put the caret somewhere in a document and select what is there.
/// </summary>
/// <param name="Line">The one-based line to land on.</param>
/// <param name="Column">The one-based column to land on.</param>
/// <param name="Length">How many characters to select from there. Zero places the caret and selects nothing.</param>
/// <remarks>
/// <para>
/// In lines and columns rather than in offsets, because that is what the thing asking knows: a search
/// hit is a line of a file that was read off disk, not an offset into a buffer that may since have
/// been edited. <see cref="Resolve"/> is where the two meet.
/// </para>
/// <para>
/// Requests are carried on the document rather than pushed at the editor control, because the control
/// showing that document may not exist yet — opening a file from a search result creates the tab
/// first, and the caret can only be placed once there is something to place it in.
/// </para>
/// </remarks>
public readonly record struct DocumentSelection(int Line, int Column, int Length)
{
    /// <summary>
    /// Measures this request against a document, clamping it to what the document actually holds.
    /// </summary>
    /// <param name="document">The text to measure against.</param>
    /// <returns>The stretch to select.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Clamped rather than validated, and deliberately: the file was read by the search and is being
    /// read again by the editor, and between the two it may have been shortened. Landing on the last
    /// line of a file that changed underneath is a better answer than an exception.
    /// </remarks>
    public DocumentSelectionRange Resolve(TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        int line = Math.Clamp(Line, 1, Math.Max(1, document.LineCount));
        DocumentLine documentLine = document.GetLineByNumber(line);

        // Column is one-based and may sit one past the last character, which is where a caret at the
        // end of a line lives.
        int column = Math.Clamp(Column, 1, documentLine.Length + 1);
        int offset = documentLine.Offset + (column - 1);
        int length = Math.Clamp(Length, 0, document.TextLength - offset);

        return new DocumentSelectionRange(offset, length);
    }
}
