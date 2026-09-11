using System;
using System.Collections.Generic;
using System.Linq;
using Enigma.MarkdownEditor.Core.Files;
using Enigma.MarkdownEditor.Core.Workspaces;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// Hands out <see cref="FakeDirectoryWatcher"/> instances and remembers every one it made, so a test
/// can raise a change on a given root — and check that the previous watchers were disposed.
/// </summary>
internal sealed class FakeDirectoryWatcherFactory : IDirectoryWatcherFactory
{
    public List<FakeDirectoryWatcher> Created { get; } = [];

    public IDirectoryWatcher Create(string rootDirectory)
    {
        FakeDirectoryWatcher watcher = new(rootDirectory);
        Created.Add(watcher);

        return watcher;
    }

    /// <summary>
    /// The live watcher over a root — the most recent one, since a refresh rebuilds them.
    /// </summary>
    public FakeDirectoryWatcher For(string rootDirectory)
        => Created.LastOrDefault(watcher =>
               WorkspaceValidation.PathComparer.Equals(watcher.RootDirectory, rootDirectory))
           ?? throw new InvalidOperationException($"No watcher was made for '{rootDirectory}'.");
}
