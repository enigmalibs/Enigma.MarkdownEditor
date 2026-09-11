namespace Enigma.MarkdownEditor.Core.Documents;

/// <summary>
/// Builds the watcher for one open document.
/// </summary>
/// <remarks>
/// A seam, for the same reason <see cref="Files.IDirectoryWatcherFactory"/> is one: the editor's
/// reaction to a file changing underneath is worth testing by raising the event rather than by
/// writing a file and waiting for the operating system to notice.
/// </remarks>
public interface IDocumentFileWatcherFactory
{
    /// <summary>
    /// Builds a watcher for a document's file. The watcher is not started.
    /// </summary>
    /// <param name="filePath">The file to watch.</param>
    /// <returns>The watcher, which the caller owns and disposes.</returns>
    IDocumentFileWatcher Create(string filePath);
}
