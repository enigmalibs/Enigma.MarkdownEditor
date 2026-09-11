using System;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// One entry of a listed directory: what the tree needs to draw a node, and nothing more.
/// </summary>
/// <remarks>
/// A snapshot, deliberately: it is read from the file system once and never refreshed in place, so a
/// stale entry is replaced rather than mutated. That is what lets the tree diff two listings.
/// </remarks>
public sealed class FileSystemEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemEntry"/> class.
    /// </summary>
    /// <param name="fullPath">The entry's absolute path.</param>
    /// <param name="name">The entry's own name, without its directory.</param>
    /// <param name="isDirectory">Whether the entry is a directory.</param>
    /// <exception cref="ArgumentException">Either string is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Either string is <see langword="null"/>.</exception>
    public FileSystemEntry(string fullPath, string name, bool isDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        FullPath = fullPath;
        Name = name;
        IsDirectory = isDirectory;
    }

    /// <summary>
    /// Gets the entry's absolute path.
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    /// Gets the entry's own name, without its directory.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the entry is a directory.
    /// </summary>
    public bool IsDirectory { get; }
}
