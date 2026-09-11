using Avalonia.Controls;

namespace Enigma.MarkdownEditor.Desktop.Controls;

/// <summary>
/// One rendered block: the visual, and the source line it was written on.
/// </summary>
/// <param name="Visual">The control the reader sees.</param>
/// <param name="StartLine">
/// The first source line of the markdown block behind it, one-based — the editor's own numbering,
/// which Markdig counts from zero.
/// </param>
/// <remarks>
/// The line travels with the visual rather than being looked up afterwards because only the walk
/// knows the pairing: the renderer skips block kinds it does not draw, so the <em>n</em>th visual is
/// not the <em>n</em>th block of the document. Reconstructing the mapping from the AST alone would be
/// a second copy of that decision, and the two would drift.
/// </remarks>
public readonly record struct MarkdownBlockVisual(Control Visual, int StartLine);
