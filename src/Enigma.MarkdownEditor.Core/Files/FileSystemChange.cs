using System;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// One thing the file system reported, before <see cref="FileChangeCoalescer"/> turns a burst of them
/// into the directories the tree has to re-read.
/// </summary>
public sealed class FileSystemChange
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemChange"/> class.
    /// </summary>
    /// <param name="kind">What happened.</param>
    /// <param name="fullPath">
    /// The path it happened to; for <see cref="FileChangeKind.Reset"/>, the watched root.
    /// </param>
    /// <param name="previousFullPath">The path before a rename; <see langword="null"/> otherwise.</param>
    /// <exception cref="ArgumentException"><paramref name="fullPath"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="fullPath"/> is <see langword="null"/>.</exception>
    public FileSystemChange(FileChangeKind kind, string fullPath, string? previousFullPath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        Kind = kind;
        FullPath = fullPath;
        PreviousFullPath = previousFullPath;
    }

    /// <summary>
    /// Gets what happened.
    /// </summary>
    public FileChangeKind Kind { get; }

    /// <summary>
    /// Gets the path it happened to.
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    /// Gets the path before a rename, or <see langword="null"/>.
    /// </summary>
    public string? PreviousFullPath { get; }
}
