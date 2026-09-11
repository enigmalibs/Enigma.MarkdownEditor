namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// A stretch of a document's text, as the editor control addresses it.
/// </summary>
/// <param name="Offset">Where the stretch starts, zero-based, from the start of the document.</param>
/// <param name="Length">How many characters it covers. Never runs past the end of the document.</param>
/// <remarks>
/// What <see cref="DocumentSelection"/> becomes once it has been measured against a real document:
/// offsets rather than a line and a column, because that is the only thing the control's
/// <c>Select</c> understands, and clamped, because the text may have moved on since the request was
/// made.
/// </remarks>
public readonly record struct DocumentSelectionRange(int Offset, int Length);
