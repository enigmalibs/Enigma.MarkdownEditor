using System.Collections.Generic;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// The order a directory's entries are listed in: directories first, then files, each group by
/// <see cref="NaturalNameComparer"/>.
/// </summary>
public sealed class FileSystemEntryComparer : IComparer<FileSystemEntry>
{
    /// <summary>
    /// Gets the shared instance. The comparer holds no state.
    /// </summary>
    public static FileSystemEntryComparer Instance { get; } = new();

    /// <summary>
    /// Compares two entries.
    /// </summary>
    /// <param name="x">The first entry.</param>
    /// <param name="y">The second entry.</param>
    /// <returns>A negative value, zero, or a positive value, as the ordering demands.</returns>
    public int Compare(FileSystemEntry? x, FileSystemEntry? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        return x.IsDirectory != y.IsDirectory
            ? (x.IsDirectory ? -1 : 1)
            : NaturalNameComparer.Instance.Compare(x.Name, y.Name);
    }
}
