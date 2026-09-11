using System;
using System.Collections.Generic;
using System.IO;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// What the workspace tree shows, and what it hands to the editor rather than to the operating system.
/// </summary>
/// <remarks>
/// Every rule here is a pure decision over a name, so the tree's contents are testable without a file
/// system: <see cref="DirectoryLister"/> is the only place that touches disk, and it does nothing but
/// feed these methods.
/// </remarks>
public static class FileTreeFilter
{
    /// <summary>
    /// The extensions the tree counts as markdown. These are shown whatever the ShowAllFiles setting
    /// says, and always open in the editor.
    /// </summary>
    public static IReadOnlySet<string> MarkdownExtensions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".md", ".markdown" };

    /// <summary>
    /// The extensions that open in the editor rather than in the operating system's default
    /// application, once the tree is showing everything.
    /// </summary>
    /// <remarks>
    /// An allow-list rather than a sniff of the file's first bytes: the wrong answer here opens a
    /// binary in a text editor, and a list a user can predict beats a heuristic they cannot. Anything
    /// not named here — including an extensionless file — goes to the OS, which is the safe direction
    /// to be wrong in.
    /// </remarks>
    public static IReadOnlySet<string> TextExtensions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".text", ".log", ".csv", ".tsv",
            ".json", ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf", ".xml",
            ".html", ".htm", ".css", ".js", ".ts", ".jsx", ".tsx",
            ".cs", ".fs", ".vb", ".java", ".kt", ".go", ".rs", ".py", ".rb", ".php", ".sql",
            ".sh", ".bash", ".zsh", ".ps1", ".bat", ".cmd",
        };

    /// <summary>
    /// Decides whether an entry of a directory is listed in the tree.
    /// </summary>
    /// <param name="name">The entry's own name, without its directory.</param>
    /// <param name="isDirectory">Whether the entry is a directory.</param>
    /// <param name="isHidden">
    /// Whether the file system marks the entry hidden or system — on Windows that is how
    /// <c>.git</c> and friends are usually flagged, on top of the leading dot.
    /// </param>
    /// <param name="showAllFiles">The ShowAllFiles setting: <see langword="true"/> widens the filter
    /// from markdown to every file.</param>
    /// <returns><see langword="true"/> when the entry belongs in the tree.</returns>
    /// <remarks>
    /// A hidden or dot-prefixed entry is excluded whatever <paramref name="showAllFiles"/> says. The
    /// setting is there to reveal the user's own files, not <c>.git</c>'s object database — a tree that
    /// can be made to walk into one is a tree that hangs.
    /// </remarks>
    public static bool Includes(string? name, bool isDirectory, bool isHidden, bool showAllFiles)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (isHidden || name.StartsWith('.'))
        {
            return false;
        }

        return isDirectory || showAllFiles || IsMarkdown(name);
    }

    /// <summary>
    /// Determines whether a path names a markdown file.
    /// </summary>
    /// <param name="path">The file's path or bare name.</param>
    /// <returns><see langword="true"/> for a markdown extension.</returns>
    public static bool IsMarkdown(string? path)
        => !string.IsNullOrWhiteSpace(path) && MarkdownExtensions.Contains(Path.GetExtension(path));

    /// <summary>
    /// Determines whether activating a file should open it in the editor rather than hand it to the
    /// operating system.
    /// </summary>
    /// <param name="path">The file's path or bare name.</param>
    /// <param name="showAllFiles">The ShowAllFiles setting.</param>
    /// <returns><see langword="true"/> when the editor takes it.</returns>
    /// <remarks>
    /// Markdown always opens in the editor — that is what this application is. Other text files only
    /// do so while the tree is showing them at all; a file the tree is hiding is one the user reached
    /// some other way, and the OS default is the better guess for it.
    /// </remarks>
    public static bool OpensInEditor(string? path, bool showAllFiles)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return IsMarkdown(path) || (showAllFiles && TextExtensions.Contains(Path.GetExtension(path)));
    }
}
