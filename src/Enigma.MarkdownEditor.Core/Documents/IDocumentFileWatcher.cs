using System;

namespace Enigma.MarkdownEditor.Core.Documents;

/// <summary>
/// Watches the one file behind an open document and says when it may have changed underneath.
/// </summary>
/// <remarks>
/// <para>
/// Separate from <see cref="Files.IDirectoryWatcher"/>, and deliberately so. That one watches names
/// for the tree and is filtered to <see cref="System.IO.NotifyFilters.FileName"/> and
/// <see cref="System.IO.NotifyFilters.DirectoryName"/> precisely so it is not woken by content: the
/// tree draws the same row whatever the bytes say. An open document is the opposite case — the
/// content is the whole of what it cares about — so it gets a watcher of its own rather than
/// broadening the tree's and waking it on every keystroke the editor saves.
/// </para>
/// <para>
/// The event says "look again", never what changed. Whether anything actually did is settled by
/// comparing a fresh <see cref="FileStamp"/> against the one taken at the last read or write, which
/// is also what tells the editor's own save apart from somebody else's.
/// </para>
/// </remarks>
public interface IDocumentFileWatcher : IDisposable
{
    /// <summary>
    /// Raised, once a burst has settled, when the file may no longer be what was read. Not raised on
    /// the UI thread — the caller marshals.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    /// Gets the file being watched.
    /// </summary>
    string FilePath { get; }

    /// <summary>
    /// Gets a value indicating whether the watcher is actually running. A file whose folder is not
    /// there leaves this <see langword="false"/> after <see cref="Start"/>.
    /// </summary>
    bool IsWatching { get; }

    /// <summary>
    /// Starts watching, if the file's folder is there. Does nothing when it is not, and nothing when
    /// already started.
    /// </summary>
    void Start();
}
