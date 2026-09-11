using System;
using Enigma.MarkdownEditor.Core.Documents;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// A per-document watcher the test raises itself, so the editor's reaction to a file changing
/// underneath can be asserted without waiting for the operating system to notice one.
/// </summary>
internal sealed class FakeDocumentFileWatcher : IDocumentFileWatcher
{
    public FakeDocumentFileWatcher(string filePath) => FilePath = filePath;

    public event EventHandler? Changed;

    public string FilePath { get; }

    public bool IsWatching { get; private set; }

    public bool IsDisposed { get; private set; }

    public void Start() => IsWatching = true;

    /// <summary>
    /// Reports that the file may no longer be what was read, as the real watcher does once a burst
    /// has settled.
    /// </summary>
    public void Raise() => Changed?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        IsDisposed = true;
        IsWatching = false;
    }
}
