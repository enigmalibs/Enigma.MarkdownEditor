using System;
using System.Collections.Generic;
using System.IO;
using Enigma.MarkdownEditor.Core.Workspaces;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// Turns a burst of file-system events into the directories the tree has to read again.
/// </summary>
/// <remarks>
/// Pure, and that is the point: a copy of twenty files into one folder arrives as twenty events and
/// has to come out as one directory, and that reduction is worth testing without a file system under
/// it. What lands here has already been debounced — see <see cref="DirectoryWatcher"/>.
/// </remarks>
public static class FileChangeCoalescer
{
    /// <summary>
    /// Reduces a batch of changes to the set of directories to re-read.
    /// </summary>
    /// <param name="changes">The raw changes, in the order they arrived.</param>
    /// <param name="rootDirectory">
    /// The watched root. A change reported outside it is dropped — it cannot be shown under this root,
    /// and re-reading a directory the tree does not hold would be wasted work.
    /// </param>
    /// <returns>The directories to read again, or an instruction to rebuild the root.</returns>
    /// <exception cref="ArgumentException"><paramref name="rootDirectory"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static DirectoryChangeSet Coalesce(IEnumerable<FileSystemChange> changes, string rootDirectory)
    {
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        string root = Path.TrimEndingDirectorySeparator(rootDirectory);

        List<string> directories = [];
        HashSet<string> seen = new(WorkspaceValidation.PathComparer);

        foreach (FileSystemChange change in changes)
        {
            ArgumentNullException.ThrowIfNull(change);

            // Nothing after a reset can be trusted either, so it wins outright over the batch.
            if (change.Kind == FileChangeKind.Reset)
            {
                return new DirectoryChangeSet([], requiresFullRefresh: true);
            }

            Add(change.FullPath);

            // A rename touches two directories whenever the file moved between them; when it did not,
            // the second Add is a no-op against the same parent.
            if (change.PreviousFullPath is { } previous)
            {
                Add(previous);
            }
        }

        return directories.Count == 0 ? DirectoryChangeSet.None : new DirectoryChangeSet(directories);

        void Add(string path)
        {
            if (DirectoryOf(path) is not { } directory)
            {
                return;
            }

            if (!IsWithin(directory, root))
            {
                return;
            }

            if (seen.Add(directory))
            {
                directories.Add(directory);
            }
        }
    }

    // The parent is what has to be re-listed: whether the path itself is a file or a directory is
    // exactly what the event does not say, and the parent's listing answers it either way.
    private static string? DirectoryOf(string path)
    {
        try
        {
            string? parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(path));

            return string.IsNullOrEmpty(parent) ? null : parent;
        }
        catch (Exception exception) when (exception is ArgumentException or PathTooLongException)
        {
            return null;
        }
    }

    private static bool IsWithin(string directory, string root)
    {
        if (WorkspaceValidation.PathComparer.Equals(directory, root))
        {
            return true;
        }

        return directory.StartsWith(root, PathComparison) &&
               (directory.Length > root.Length) &&
               ((directory[root.Length] == Path.DirectorySeparatorChar) ||
                (directory[root.Length] == Path.AltDirectorySeparatorChar));
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
