namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// What <c>gu</c>, <c>gU</c>, <c>g~</c> and <c>~</c> write in place of the text they act on.
/// </summary>
/// <remarks>
/// Character by character rather than through <see cref="string.ToUpperInvariant"/> and its pair,
/// so that the result is exactly as long as the text it replaces: a line break inside the range
/// stays a line break, and the caret arithmetic after the write stays honest. The invariant culture
/// for the same reason <c>~</c> has always used it — a document does not change case differently
/// because of the machine it is opened on.
/// </remarks>
internal static class VimCase
{
    /// <summary>
    /// Rewrites text in the case an operator asks for.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="operator">
    /// <see cref="VimOperator.Lowercase"/>, <see cref="VimOperator.Uppercase"/> or
    /// <see cref="VimOperator.ToggleCase"/>. Anything else leaves the text as it is.
    /// </param>
    /// <returns>The rewritten text, the same length as <paramref name="text"/>.</returns>
    public static string Convert(string text, VimOperator @operator)
    {
        char[] characters = text.ToCharArray();

        for (int index = 0; index < characters.Length; index++)
        {
            characters[index] = Convert(characters[index], @operator);
        }

        return new string(characters);
    }

    private static char Convert(char character, VimOperator @operator)
        => @operator switch
        {
            VimOperator.Lowercase => char.ToLowerInvariant(character),
            VimOperator.Uppercase => char.ToUpperInvariant(character),
            VimOperator.ToggleCase => char.IsUpper(character)
                ? char.ToLowerInvariant(character)
                : char.ToUpperInvariant(character),
            _ => character,
        };
}
