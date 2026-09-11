using System;

namespace Enigma.MarkdownEditor.Core.Documents;

/// <summary>
/// Reading, writing and normalizing a text's line terminators.
/// </summary>
/// <remarks>
/// <para>
/// A document is held in memory with line feeds alone, whatever the file used, and the file's own
/// style is re-applied when it is written back. Keeping one form in the editor is what stops a file
/// from acquiring mixed terminators the moment a line is typed into it, and re-applying the detected
/// one is what keeps the diff of a saved file limited to what the user actually changed.
/// </para>
/// <para>
/// The cost of that choice is deliberate and small: a file that already mixed both styles is written
/// back entirely in its dominant one. A file that consistently used one — which is every file that
/// has ever been through a sane tool — round-trips byte for byte.
/// </para>
/// </remarks>
public static class LineEndings
{
    /// <summary>
    /// The style a file with no line break at all — and every new file — is written with.
    /// </summary>
    public const LineEndingStyle Default = LineEndingStyle.Lf;

    /// <summary>
    /// Works out which style a text uses.
    /// </summary>
    /// <param name="text">The text as it was read from disk.</param>
    /// <returns>
    /// The dominant style, or <see cref="Default"/> when the text holds no line break. A tie goes to
    /// <see cref="LineEndingStyle.Lf"/>.
    /// </returns>
    /// <remarks>
    /// Dominant rather than first: a file whose last line was appended by a tool with the other
    /// convention should still be written in the convention of its other thousand lines.
    /// </remarks>
    public static LineEndingStyle Detect(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Default;
        }

        int crlf = 0;
        int lf = 0;

        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] != '\n')
            {
                continue;
            }

            if ((index > 0) && (text[index - 1] == '\r'))
            {
                crlf++;
            }
            else
            {
                lf++;
            }
        }

        return crlf > lf ? LineEndingStyle.CrLf : LineEndingStyle.Lf;
    }

    /// <summary>
    /// Rewrites every line break as a single line feed.
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The same text with <c>\r\n</c> and lone <c>\r</c> collapsed to <c>\n</c>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // The overwhelmingly common case on Linux, and the case a file already normalized hits on
        // every autosave: worth not allocating two strings for.
        return text.Contains('\r', StringComparison.Ordinal)
            ? text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            : text;
    }

    /// <summary>
    /// Rewrites every line break in the requested style.
    /// </summary>
    /// <param name="text">The text to convert. Need not already be normalized.</param>
    /// <param name="style">The style to write.</param>
    /// <returns>The text with exactly that terminator on every line.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static string Apply(string text, LineEndingStyle style)
    {
        string normalized = Normalize(text);

        return style == LineEndingStyle.CrLf
            ? normalized.Replace("\n", "\r\n", StringComparison.Ordinal)
            : normalized;
    }
}
