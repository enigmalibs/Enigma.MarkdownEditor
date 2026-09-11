using System;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// Watches one workspace root and reports, in debounced batches, which of its directories the tree
/// has to read again.
/// </summary>
public interface IDirectoryWatcher : IDisposable
{
    /// <summary>
    /// Raised once a burst of changes has settled. Not raised on the UI thread — the caller marshals.
    /// </summary>
    event EventHandler<DirectoryChangeSet>? Changed;

    /// <summary>
    /// Gets the root directory being watched.
    /// </summary>
    string RootDirectory { get; }

    /// <summary>
    /// Gets a value indicating whether the watcher is actually running. A root that is not on disk
    /// leaves this <see langword="false"/> after <see cref="Start"/>.
    /// </summary>
    bool IsWatching { get; }

    /// <summary>
    /// Starts watching, if the root is there. Does nothing when it is not, and nothing when already
    /// started.
    /// </summary>
    void Start();
}
