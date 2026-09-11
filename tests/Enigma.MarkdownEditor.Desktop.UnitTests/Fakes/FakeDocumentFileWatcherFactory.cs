using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enigma.MarkdownEditor.Core.Documents;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// Hands out <see cref="FakeDocumentFileWatcher"/> instances and remembers every one it made, so a
/// test can raise a change on a given file — and check that a superseded watcher was disposed.
/// </summary>
internal sealed class FakeDocumentFileWatcherFactory : IDocumentFileWatcherFactory
{
    public List<FakeDocumentFileWatcher> Created { get; } = [];

    public IDocumentFileWatcher Create(string filePath)
    {
        FakeDocumentFileWatcher watcher = new(Path.GetFullPath(filePath));
        Created.Add(watcher);

        return watcher;
    }

    /// <summary>
    /// The live watcher over a file — the most recent one, since Save As rebuilds it.
    /// </summary>
    public FakeDocumentFileWatcher For(string filePath)
    {
        string full = Path.GetFullPath(filePath);

        return Created.LastOrDefault(watcher => string.Equals(watcher.FilePath, full, Comparison))
               ?? throw new InvalidOperationException($"No watcher was made for '{full}'.");
    }

    private static StringComparison Comparison
        => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
