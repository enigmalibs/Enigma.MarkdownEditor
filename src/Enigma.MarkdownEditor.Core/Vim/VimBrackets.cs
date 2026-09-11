using System;

namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// Where <c>%</c> lands, and the nesting walk it shares with the bracket text objects.
/// </summary>
/// <remarks>
/// <para>
/// Kept apart from the engine for the same reason <see cref="VimWords"/> is: this answers a question
/// about the text — "which <c>)</c> closes this <c>(</c>?" — and the engine answers what to do about
/// it. The two scans that count nesting live here rather than in <see cref="VimTextObjects"/> so
/// that <c>%</c> and <c>ci(</c> cannot disagree about which bracket matches which.
/// </para>
/// <para>
/// The three bracket pairs and nothing else, which is vim's default <c>matchpairs</c>. Vim's <c>%</c>
/// also jumps between the halves of a C comment and of a preprocessor conditional; neither has a
/// place in a markdown file.
/// </para>
/// </remarks>
internal static class VimBrackets
{
    /// <summary>
    /// Works out where <c>%</c> goes.
    /// </summary>
    /// <param name="buffer">The buffer to scan.</param>
    /// <param name="caret">Where the caret is, already pulled inside the line by the engine.</param>
    /// <returns>
    /// The offset of the matching bracket, or <see langword="null"/> when there is no bracket on the
    /// line from the caret onwards, or the one found has no match.
    /// </returns>
    /// <remarks>
    /// Vim's own two-step rule: the bracket under the caret, or failing that the first one further
    /// along the same line, and then <em>its</em> match — across lines, counting nesting.
    /// </remarks>
    public static int? Match(IVimBuffer buffer, int caret)
    {
        VimLine line = buffer.GetLineByOffset(caret);

        for (int index = caret; index < line.EndOffset; index++)
        {
            char character = buffer.CharAt(index);

            switch (character)
            {
                case '(' or '[' or '{':
                    return NextUnmatched(buffer, index + 1, character, Closing(character));

                case ')' or ']' or '}':
                    return PreviousUnmatched(buffer, index - 1, Opening(character), character);

                default:
                    break;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the closing bracket that is not matched by an opening one between <paramref name="from"/>
    /// and it.
    /// </summary>
    /// <param name="buffer">The buffer to scan.</param>
    /// <param name="from">Where to start looking, inclusive.</param>
    /// <param name="open">The opening bracket, which deepens the nesting.</param>
    /// <param name="close">The closing bracket looked for.</param>
    /// <returns>Its offset, or <see langword="null"/> when the buffer runs out first.</returns>
    public static int? NextUnmatched(IVimBuffer buffer, int from, char open, char close)
    {
        int depth = 0;

        for (int index = Math.Max(from, 0); index < buffer.TextLength; index++)
        {
            char character = buffer.CharAt(index);

            if (character == open)
            {
                depth++;
            }
            else if (character == close)
            {
                if (depth == 0)
                {
                    return index;
                }

                depth--;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the opening bracket that is not matched by a closing one between it and
    /// <paramref name="from"/>.
    /// </summary>
    /// <param name="buffer">The buffer to scan.</param>
    /// <param name="from">Where to start looking, inclusive, walking backwards.</param>
    /// <param name="open">The opening bracket looked for.</param>
    /// <param name="close">The closing bracket, which deepens the nesting.</param>
    /// <returns>Its offset, or <see langword="null"/> when the buffer runs out first.</returns>
    public static int? PreviousUnmatched(IVimBuffer buffer, int from, char open, char close)
    {
        int depth = 0;

        for (int index = Math.Min(from, buffer.TextLength - 1); index >= 0; index--)
        {
            char character = buffer.CharAt(index);

            if (character == close)
            {
                depth++;
            }
            else if (character == open)
            {
                if (depth == 0)
                {
                    return index;
                }

                depth--;
            }
        }

        return null;
    }

    private static char Closing(char open)
        => open switch
        {
            '(' => ')',
            '[' => ']',
            _ => '}',
        };

    private static char Opening(char close)
        => close switch
        {
            ')' => '(',
            ']' => '[',
            _ => '{',
        };
}
