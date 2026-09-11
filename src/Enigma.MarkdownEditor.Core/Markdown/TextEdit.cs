namespace Enigma.MarkdownEditor.Core.Markdown;

/// <summary>
/// One replacement in a piece of text, and where the caret and the selection are left afterwards.
/// </summary>
/// <param name="Start">Where the replaced run begins, as an offset into the text that was passed in.</param>
/// <param name="Length">How much of that text goes. Zero for a pure insertion.</param>
/// <param name="Replacement">What takes its place. Empty for a pure deletion.</param>
/// <param name="CaretOffset">Where the caret lands, as an offset into the text <em>after</em> the edit.</param>
/// <param name="SelectionLength">How much is selected from <paramref name="CaretOffset"/> on. Zero leaves a bare caret.</param>
/// <remarks>
/// Every offset is relative to the string the deciding method was given — a single line for the list
/// assists, the lines a selection covers for the formatting ones. The editor holds the whole document
/// and adds that window's own offset; keeping the rule to "offsets into what you passed me" is what
/// lets these decisions be made, and tested, over a plain string.
/// </remarks>
public sealed record TextEdit(int Start, int Length, string Replacement, int CaretOffset, int SelectionLength = 0);
