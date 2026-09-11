namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// What <c>*</c> and <c>#</c> found under the caret: where it starts, and the pattern that finds it
/// again.
/// </summary>
/// <param name="Start">The offset of the word's first character.</param>
/// <param name="Pattern">
/// The pattern, ready to compile: the escaped word, between word boundaries when it is a keyword.
/// </param>
/// <remarks>
/// The start is carried because the search begins there rather than at the caret — vim's rule, and
/// what makes <c>*</c> from the middle of a word skip the word itself rather than find it.
/// </remarks>
internal readonly record struct VimSearchWord(int Start, string Pattern);
