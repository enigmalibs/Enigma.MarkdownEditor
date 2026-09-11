using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// <c>:s</c> — what its argument says, and what the buffer looks like after it.
/// </summary>
/// <remarks>
/// <para>
/// Vim's shape, .NET's engine. The argument is <c>/pattern/replacement/flags</c>, with any
/// non-alphanumeric character in place of the <c>/</c> — <c>:s#a/b#c#</c> is how a path is
/// substituted without escaping every slash — and a <c>\</c> before the delimiter puts it in the
/// text. The pattern is left as typed for <see cref="VimPattern"/> to compile. The replacement is
/// .NET's: <c>$1</c> is the first group, <c>$0</c> the match, <c>$$</c> a dollar, and <c>&amp;</c>
/// is an ampersand, not the match. Vim's three escapes are honoured because .NET has no way to say
/// them — <c>\r</c> breaks the line, <c>\t</c> is a tab and <c>\\</c> a backslash — and any other
/// backslash is left as it is, which is also what .NET does with one.
/// </para>
/// <para>
/// Three flags: <c>g</c> for every match on the line rather than the first, and <c>i</c> or <c>I</c>
/// to settle the case for this command alone. The rest of vim's — <c>c</c> to confirm each one,
/// <c>n</c> to count, <c>&amp;</c> to keep the last flags — are not read, and a character that is not
/// a flag is named as trailing, which is vim's answer too.
/// </para>
/// <para>
/// The substitution runs line by line, as vim's does: the pattern is matched against one line's text
/// at a time, so <c>^</c> and <c>$</c> are that line's ends and a match cannot cross a line break.
/// Everything that changed is handed back as one replacement, from the first changed line to the
/// last, so that <c>:%s</c> over a long file is a single write and a single <c>u</c>.
/// </para>
/// </remarks>
internal static class VimSubstitute
{
    /// <summary>
    /// Reads the argument of a <c>:s</c>.
    /// </summary>
    /// <param name="argument">What followed the <c>s</c>, trimmed.</param>
    /// <param name="request">What it asked for, when it could be read.</param>
    /// <param name="error">
    /// Why it could not: the characters after the flags that are not flags. <see langword="null"/>
    /// when the argument did not begin with a delimiter at all, which is not a substitute — the caller
    /// names it as it would any other line it cannot read.
    /// </param>
    /// <returns>Whether the argument is a substitute.</returns>
    public static bool TryParse(string argument, out VimSubstituteRequest request, out string? error)
    {
        request = default;
        error = null;

        if ((argument.Length == 0) || !IsDelimiter(argument[0]))
        {
            return false;
        }

        char delimiter = argument[0];
        int at = 1;

        string pattern = ReadField(argument, ref at, delimiter, unescape: false);
        string replacement = ReadField(argument, ref at, delimiter, unescape: true);

        bool global = false;
        bool? ignoreCase = null;

        for (; at < argument.Length; at++)
        {
            switch (argument[at])
            {
                case 'g':
                    global = true;
                    break;

                case 'i':
                    ignoreCase = true;
                    break;

                case 'I':
                    ignoreCase = false;
                    break;

                case ' ' or '\t':
                    break;

                default:
                    error = $"Trailing characters: {argument[at..]}";

                    return false;
            }
        }

        request = new VimSubstituteRequest(pattern, replacement, global, ignoreCase);

        return true;
    }

    /// <summary>
    /// Works out what a substitute changes.
    /// </summary>
    /// <param name="buffer">The buffer.</param>
    /// <param name="pattern">The compiled pattern.</param>
    /// <param name="replacement">The replacement, as <see cref="TryParse"/> left it.</param>
    /// <param name="global">Every match on a line, or the first.</param>
    /// <param name="firstLine">The first line of the range, counted from one.</param>
    /// <param name="lastLine">The last, never before the first.</param>
    /// <returns>The one write to make, or <see langword="null"/> when nothing in the range matched.</returns>
    /// <exception cref="RegexMatchTimeoutException">A match ran past <see cref="VimPattern.Timeout"/>.</exception>
    /// <remarks>
    /// The caret goes where vim leaves it: on the last line a substitution touched — after a <c>\r</c>
    /// has split that line, the last of the pieces — and the engine finds its first non-blank once the
    /// text is in.
    /// </remarks>
    public static VimSubstitution? Apply(
        IVimBuffer buffer,
        Regex pattern,
        string replacement,
        bool global,
        int firstLine,
        int lastLine)
    {
        string newLine = buffer.NewLine;

        // A line break in the replacement is the buffer's own, whatever the file's endings are.
        string text = replacement.Replace("\n", newLine, StringComparison.Ordinal);
        int perLine = global ? -1 : 1;

        // From the first changed line on, every line — the unchanged ones between two changes have
        // to be written back as they are — and the index of the last one that changed, so that the
        // unchanged tail can be dropped again.
        List<string> pieces = [];
        int lastChanged = -1;
        int substitutions = 0;
        int lines = 0;
        VimLine first = default;
        VimLine last = default;

        for (int number = firstLine; number <= lastLine; number++)
        {
            VimLine line = buffer.GetLine(number);
            int before = substitutions;

            string replaced = pattern.Replace(
                line.Text,
                match =>
                {
                    substitutions++;

                    return match.Result(text);
                },
                perLine);

            if (substitutions == before)
            {
                if (pieces.Count > 0)
                {
                    pieces.Add(line.Text);
                }

                continue;
            }

            if (pieces.Count == 0)
            {
                first = line;
            }

            pieces.Add(replaced);
            lastChanged = pieces.Count - 1;
            last = line;
            lines++;
        }

        if (lastChanged < 0)
        {
            return null;
        }

        pieces.RemoveRange(lastChanged + 1, pieces.Count - lastChanged - 1);

        string joined = string.Join(newLine, pieces);
        string tail = pieces[^1];
        int caret = joined.Length - tail.Length;
        int split = tail.LastIndexOf(newLine, StringComparison.Ordinal);

        if (split >= 0)
        {
            caret += split + newLine.Length;
        }

        return new VimSubstitution(first.Offset, last.EndOffset - first.Offset, joined, caret, substitutions, lines);
    }

    /// <summary>
    /// Whether a character may stand round the pattern: anything but a letter, a digit, a blank,
    /// <c>\</c>, <c>"</c> or <c>|</c>, which is vim's list.
    /// </summary>
    private static bool IsDelimiter(char character)
        => !char.IsLetterOrDigit(character)
            && !char.IsWhiteSpace(character)
            && (character is not ('\\' or '"' or '|'));

    /// <summary>
    /// Reads up to the next delimiter that is not escaped, and steps over it.
    /// </summary>
    /// <param name="unescape">
    /// Whether this is the replacement, whose escapes are resolved here. The pattern's are left for
    /// the regular expression, which already reads a backslash before a delimiter as the delimiter
    /// itself.
    /// </param>
    private static string ReadField(string argument, ref int at, char delimiter, bool unescape)
    {
        StringBuilder field = new();

        while (at < argument.Length)
        {
            char character = argument[at];

            if (character == delimiter)
            {
                at++;

                break;
            }

            if ((character != '\\') || ((at + 1) >= argument.Length))
            {
                field.Append(character);
                at++;

                continue;
            }

            char next = argument[at + 1];
            at += 2;

            if (!unescape)
            {
                field.Append(character).Append(next);
            }
            else if (next == delimiter)
            {
                field.Append(next);
            }
            else
            {
                switch (next)
                {
                    case 'r':
                        field.Append('\n');
                        break;

                    case 't':
                        field.Append('\t');
                        break;

                    case '\\':
                        field.Append('\\');
                        break;

                    default:
                        field.Append(character).Append(next);
                        break;
                }
            }
        }

        return field.ToString();
    }
}
