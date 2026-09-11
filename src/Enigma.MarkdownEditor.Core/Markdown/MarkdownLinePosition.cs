namespace Enigma.MarkdownEditor.Core.Markdown;

/// <summary>
/// Where a source line falls in a rendered document: which block it belongs to, and how far into
/// that block it is.
/// </summary>
/// <param name="Index">
/// The block's position in the rendered sequence, zero-based — an index into whatever list the
/// renderer produced, which is what makes this answer usable without knowing anything about visuals.
/// </param>
/// <param name="Fraction">
/// How far through the block's source lines the line sits, from <c>0</c> at its first line towards
/// <c>1</c> at the end of its last. Always <c>0</c> for a block that occupies a single line.
/// </param>
/// <remarks>
/// A fraction rather than a line number, because the caller is about to interpolate: it knows how
/// tall the block was drawn and this says where inside it to land. Scrolling to the block's top alone
/// makes a long fenced block or a long list jump the preview to a heading paragraphs above what the
/// editor is showing.
/// </remarks>
public readonly record struct MarkdownLinePosition(int Index, double Fraction);
