namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// What the file system reported about a path.
/// </summary>
public enum FileChangeKind
{
    /// <summary>
    /// The path appeared.
    /// </summary>
    Created,

    /// <summary>
    /// The path went away.
    /// </summary>
    Deleted,

    /// <summary>
    /// The path was renamed. Both names matter — the old one has to leave the tree.
    /// </summary>
    Renamed,

    /// <summary>
    /// The watcher lost events: its buffer overflowed, or it failed outright. Nothing is known about
    /// what changed, so the only honest answer is to read the whole root again.
    /// </summary>
    Reset,
}
