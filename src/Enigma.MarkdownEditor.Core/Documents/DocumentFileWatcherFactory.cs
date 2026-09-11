namespace Enigma.MarkdownEditor.Core.Documents;

/// <summary>
/// Builds a real <see cref="DocumentFileWatcher"/> per open document.
/// </summary>
public sealed class DocumentFileWatcherFactory : IDocumentFileWatcherFactory
{
    /// <inheritdoc />
    public IDocumentFileWatcher Create(string filePath) => new DocumentFileWatcher(filePath);
}
