namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// Builds a real <see cref="DirectoryWatcher"/> per workspace root.
/// </summary>
public sealed class DirectoryWatcherFactory : IDirectoryWatcherFactory
{
    /// <inheritdoc />
    public IDirectoryWatcher Create(string rootDirectory) => new DirectoryWatcher(rootDirectory);
}
