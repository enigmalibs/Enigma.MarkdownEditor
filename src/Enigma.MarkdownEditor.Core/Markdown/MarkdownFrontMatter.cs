using System;
using System.Collections.Generic;
using System.Linq;

namespace Enigma.MarkdownEditor.Core.Markdown;

/// <summary>
/// What a document's YAML front matter holds, to the depth the preview announces it: the names of its
/// top-level entries.
/// </summary>
/// <remarks>
/// Deliberately not a YAML parser. The front matter never renders as content, so nothing here decides
/// what the reader sees — only what the chip above the note lists. Reading the keys off the left
/// margin is enough for that, and a real parser would be a dependency and a class of failures bought
/// for one line of text.
/// </remarks>
public static class MarkdownFrontMatter
{
    /// <summary>
    /// The names of the top-level entries in a block of YAML front matter, in the order they were
    /// written.
    /// </summary>
    /// <param name="text">
    /// The front matter, with or without its <c>---</c> fences. <see langword="null"/> or empty gives
    /// an empty list.
    /// </param>
    /// <returns>The key names. Empty when the block holds none — a sequence, a comment, or nothing.</returns>
    /// <remarks>
    /// Indented lines are the values of the key above them and are skipped, as are comments, sequence
    /// items and the fences. A duplicated key is listed once: the chip is a summary, and YAML would
    /// only keep one of them anyway.
    /// </remarks>
    public static IReadOnlyList<string> Keys(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        List<string> keys = [];

        foreach (string line in text.Split('\n'))
        {
            if (Key(line.TrimEnd('\r')) is { } key && !keys.Contains(key, StringComparer.Ordinal))
            {
                keys.Add(key);
            }
        }

        return keys;
    }

    private static string? Key(string line)
    {
        // Anything indented belongs to the entry above it; anything starting with one of these is not
        // an entry at all — the fences, a comment, a sequence item, a document separator.
        if ((line.Length == 0)
            || char.IsWhiteSpace(line[0])
            || line.StartsWith("---", StringComparison.Ordinal)
            || line.StartsWith("...", StringComparison.Ordinal)
            || (line[0] is '#' or '-'))
        {
            return null;
        }

        int colon = line.IndexOf(':', StringComparison.Ordinal);

        // `key:` or `key: value`. A colon with something other than a space after it is inside the
        // value — a bare URL on the line, most often — and the line is not a mapping entry.
        if ((colon <= 0) || ((colon + 1 < line.Length) && !char.IsWhiteSpace(line[colon + 1])))
        {
            return null;
        }

        string key = line[..colon].Trim().Trim('"', '\'');

        return key.Length > 0 ? key : null;
    }
}
