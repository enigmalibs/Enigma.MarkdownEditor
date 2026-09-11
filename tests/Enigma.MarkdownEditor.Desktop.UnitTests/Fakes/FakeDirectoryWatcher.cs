using System;
using Enigma.MarkdownEditor.Core.Files;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// A watcher whose batches the test raises itself, so the tree's reaction to a change can be asserted
/// without producing one and waiting for the operating system to notice.
/// </summary>
internal sealed class FakeDirectoryWatcher : IDirectoryWatcher
{
    public FakeDirectoryWatcher(string rootDirectory) => RootDirectory = rootDirectory;

    public event EventHandler<DirectoryChangeSet>? Changed;

    public string RootDirectory { get; }

    public bool IsWatching { get; private set; }

    public bool IsDisposed { get; private set; }

    public void Start() => IsWatching = true;

    /// <summary>
    /// Reports a batch, as the real watcher does once a burst has settled.
    /// </summary>
    public void Raise(params string[] directories) => Changed?.Invoke(this, new DirectoryChangeSet(directories));

    /// <summary>
    /// Reports that events were lost, as the real watcher does when its buffer overflows.
    /// </summary>
    public void RaiseReset() => Changed?.Invoke(this, new DirectoryChangeSet([], requiresFullRefresh: true));

    public void Dispose()
    {
        IsDisposed = true;
        IsWatching = false;
    }
}
