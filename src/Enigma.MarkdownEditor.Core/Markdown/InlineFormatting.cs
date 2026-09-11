using System;

namespace Enigma.MarkdownEditor.Core.Markdown;

/// <summary>
/// The three inline shortcuts — bold, italic and link — as decisions over plain strings.
/// </summary>
/// <remarks>
/// <para>
/// Each method is given a window of text, an offset into it and a length, and answers with a
/// <see cref="TextEdit"/>. The window is whatever the editor can cheaply hand over — the lines a
/// selection covers — rather than the whole document, so pressing Ctrl+B in a large file does not
/// copy it.
/// </para>
/// <para>
/// With nothing selected the shortcut takes the word the caret is in, which is what makes it usable
/// without reaching for the mouse; with the caret between two words there is no word to take, and the
/// pair of delimiters is inserted with the caret left between them.
/// </para>
/// </remarks>
public static class InlineFormatting
{
    /// <summary>
    /// What wraps bold text.
    /// </summary>
    public const string BoldDelimiter = "**";

    /// <summary>
    /// What wraps italic text.
    /// </summary>
    public const string ItalicDelimiter = "*";

    /// <summary>
    /// Wraps a range in a delimiter, or takes the delimiter back off when it is already there.
    /// </summary>
    /// <param name="text">The window the offsets are into.</param>
    /// <param name="start">Where the selection begins, or the caret when nothing is selected.</param>
    /// <param name="length">How much is selected. Zero takes the word under the caret instead.</param>
    /// <param name="delimiter">
    /// <see cref="BoldDelimiter"/> or <see cref="ItalicDelimiter"/> — or any other run of one repeated
    /// character that markdown wraps with.
    /// </param>
    /// <returns>The edit, which leaves the affected text selected.</returns>
    /// <remarks>
    /// The delimiter is recognised whether it is inside the selection (<c>**word**</c> selected whole)
    /// or just outside it (<c>word</c> selected inside an existing pair). A run longer than the
    /// delimiter is not a match, which is what keeps italic from tearing one asterisk off
    /// <c>**bold**</c> — asked for italic there, it adds a pair and produces <c>***bold***</c>.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="delimiter"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The range falls outside <paramref name="text"/>.</exception>
    public static TextEdit Toggle(string text, int start, int length, string delimiter)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentException.ThrowIfNullOrEmpty(delimiter);

        (int rangeStart, int rangeLength) = RangeFor(text, start, length);
        int width = delimiter.Length;

        // Selected whole, delimiters and all.
        if ((rangeLength >= (2 * width))
            && Delimits(text, rangeStart, delimiter)
            && Delimits(text, (rangeStart + rangeLength) - width, delimiter))
        {
            string bare = text.Substring(rangeStart + width, rangeLength - (2 * width));

            return new TextEdit(rangeStart, rangeLength, bare, rangeStart, bare.Length);
        }

        // Selected inside a pair that is already there.
        if (Delimits(text, rangeStart - width, delimiter) && Delimits(text, rangeStart + rangeLength, delimiter))
        {
            string bare = text.Substring(rangeStart, rangeLength);

            return new TextEdit(rangeStart - width, rangeLength + (2 * width), bare, rangeStart - width, bare.Length);
        }

        string wrapped = string.Concat(delimiter, text.AsSpan(rangeStart, rangeLength), delimiter);

        return new TextEdit(rangeStart, rangeLength, wrapped, rangeStart + width, rangeLength);
    }

    /// <summary>
    /// Turns a range into an inline link, ready for the address to be typed.
    /// </summary>
    /// <param name="text">The window the offsets are into.</param>
    /// <param name="start">Where the selection begins, or the caret when nothing is selected.</param>
    /// <param name="length">How much is selected. Zero takes the word under the caret instead.</param>
    /// <returns>The edit, with the caret in whichever half of the link is still empty.</returns>
    /// <remarks>
    /// The caret goes into the address, because the text half is the half that was just filled in. When
    /// there was nothing to fill it with — no selection and no word under the caret — it goes into the
    /// text half instead, which is then the one thing that has to be typed first.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The range falls outside <paramref name="text"/>.</exception>
    public static TextEdit Link(string text, int start, int length)
    {
        ArgumentNullException.ThrowIfNull(text);

        (int rangeStart, int rangeLength) = RangeFor(text, start, length);

        string replacement = $"[{text.Substring(rangeStart, rangeLength)}]()";

        int caret = rangeLength == 0
            ? rangeStart + 1
            : (rangeStart + replacement.Length) - 1;

        return new TextEdit(rangeStart, rangeLength, replacement, caret);
    }

    private static (int Start, int Length) RangeFor(string text, int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start + length, text.Length);

        if (length > 0)
        {
            return (start, length);
        }

        int wordStart = start;

        while ((wordStart > 0) && IsWordCharacter(text[wordStart - 1]))
        {
            wordStart--;
        }

        int wordEnd = start;

        while ((wordEnd < text.Length) && IsWordCharacter(text[wordEnd]))
        {
            wordEnd++;
        }

        return (wordStart, wordEnd - wordStart);
    }

    // Letters, digits and the underscore, which is as much of a word as markdown ever wraps: a
    // trailing comma or full stop inside the emphasis would be visible in the rendered output.
    private static bool IsWordCharacter(char character) => char.IsLetterOrDigit(character) || (character == '_');

    // True only when the delimiter sits at that position AND the run of that character is exactly the
    // delimiter's length, so "**" is never read as a single italic asterisk with something beside it.
    private static bool Delimits(string text, int position, string delimiter)
    {
        if ((position < 0) || ((position + delimiter.Length) > text.Length))
        {
            return false;
        }

        if (string.CompareOrdinal(text, position, delimiter, 0, delimiter.Length) != 0)
        {
            return false;
        }

        char marker = delimiter[0];

        if ((position > 0) && (text[position - 1] == marker))
        {
            return false;
        }

        int after = position + delimiter.Length;

        return (after >= text.Length) || (text[after] != marker);
    }
}
