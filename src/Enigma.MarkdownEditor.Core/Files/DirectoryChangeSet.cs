using System;
using System.Collections.Generic;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// What a burst of file-system events amounts to for the tree: the directories whose contents are no
/// longer trustworthy.
/// </summary>
/// <remarks>
/// Never "what changed", always "what to read again". A tree that tried to apply individual events
/// would have to trust their order and their completeness, and a <see cref="System.IO.FileSystemWatcher"/>
/// guarantees neither; re-listing the handful of affected directories is both simpler and correct.
/// </remarks>
public sealed class DirectoryChangeSet
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DirectoryChangeSet"/> class.
    /// </summary>
    /// <param name="directories">The directories to read again.</param>
    /// <param name="requiresFullRefresh">Whether the whole root has to be rebuilt instead.</param>
    /// <exception cref="ArgumentNullException"><paramref name="directories"/> is <see langword="null"/>.</exception>
    public DirectoryChangeSet(IReadOnlyList<string> directories, bool requiresFullRefresh = false)
    {
        ArgumentNullException.ThrowIfNull(directories);

        Directories = directories;
        RequiresFullRefresh = requiresFullRefresh;
    }

    /// <summary>
    /// Gets an empty set — nothing to do.
    /// </summary>
    public static DirectoryChangeSet None { get; } = new([]);

    /// <summary>
    /// Gets the directories to read again, in the order their first event arrived.
    /// </summary>
    public IReadOnlyList<string> Directories { get; }

    /// <summary>
    /// Gets a value indicating whether events were lost and the whole root has to be rebuilt.
    /// </summary>
    public bool RequiresFullRefresh { get; }

    /// <summary>
    /// Gets a value indicating whether there is nothing to do.
    /// </summary>
    public bool IsEmpty => !RequiresFullRefresh && (Directories.Count == 0);
}
