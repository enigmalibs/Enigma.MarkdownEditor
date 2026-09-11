namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// Builds the watcher for one workspace root.
/// </summary>
/// <remarks>
/// A seam, so the tree's reaction to a change can be tested by raising one rather than by producing
/// one and waiting for the operating system to notice.
/// </remarks>
public interface IDirectoryWatcherFactory
{
    /// <summary>
    /// Builds a watcher for a root directory. The watcher is not started.
    /// </summary>
    /// <param name="rootDirectory">The directory to watch.</param>
    /// <returns>The watcher, which the caller owns and disposes.</returns>
    IDirectoryWatcher Create(string rootDirectory);
}
