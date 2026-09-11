using System.IO;
using Enigma.MarkdownEditor.Core.Files;

namespace Enigma.MarkdownEditor.Core.Markdown;

/// <summary>
/// What a file the editor has just created starts with.
/// </summary>
/// <remarks>
/// <para>
/// One decision over a name, which is why it is here and not in the tree that calls it: a note is
/// worth opening with its own title already in it, and a <c>.json</c> or a <c>.ps1</c> the tree can
/// also create is not — a <c>#</c> heading written into either of those is corruption rather than
/// help. So the extension is asked, through the same <see cref="FileTreeFilter"/> the tree filters
/// with, and everything else answers with nothing.
/// </para>
/// <para>
/// Line feeds alone, as everywhere in this assembly: text is held normalized and the file's own
/// terminator is put back on the way to disk (<c>LineEndings.Apply</c>).
/// </para>
/// </remarks>
public static class MarkdownScaffold
{
    /// <summary>
    /// The text a newly created file should start with.
    /// </summary>
    /// <param name="fileName">The file's name, as the user typed it, with or without a directory.</param>
    /// <returns>
    /// The title heading and the blank line after it, or the empty string when the file should start
    /// empty — which is every file that is not markdown, and a markdown file with no name in front of
    /// its extension.
    /// </returns>
    /// <remarks>
    /// The title is the name with its extension taken off and nothing else done to it: no case
    /// change, no <c>-</c> or <c>_</c> turned into a space. Someone who called the file
    /// <c>2026-09-10.stand-up.md</c> named it that on purpose, and a heading that reads differently
    /// from the file it is in is a heading they have to correct.
    /// </remarks>
    public static string For(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !FileTreeFilter.IsMarkdown(fileName))
        {
            return string.Empty;
        }

        string title = Path.GetFileNameWithoutExtension(fileName).Trim();

        // A file called '.md' has an extension and no name. '# ' on its own is not a title, so such a
        // file starts empty like any other the scaffold has nothing to say about.
        return title.Length == 0 ? string.Empty : $"# {title}\n\n";
    }
}
