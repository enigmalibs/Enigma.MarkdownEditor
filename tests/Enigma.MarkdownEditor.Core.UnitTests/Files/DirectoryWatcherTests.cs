using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Core.Files;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Files;

/// <summary>
/// Tests for the watcher against a real temp directory: that a change is reported, that a burst of
/// them is reported once, and that a disposed watcher goes quiet.
/// </summary>
/// <remarks>
/// These drive the operating system's own notification machinery, so they wait for an event rather
/// than assert on the next line. The waits are generous — a slow CI agent must not turn a correct
/// watcher into a red build — and every one of them is bounded, so a broken watcher fails rather than
/// hangs.
/// </remarks>
public sealed class DirectoryWatcherTests
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task ACreatedFile_IsReportedAsItsDirectory()
    {
        using TempTreeFolder folder = new();
        using Collector collector = new(folder.Root);

        folder.AddFile("notes.md");

        DirectoryChangeSet changes = await collector.NextAsync();

        Assert.False(changes.RequiresFullRefresh);
        Assert.Contains(folder.Root, changes.Directories);
    }

    [Fact]
    public async Task AChangeInASubdirectory_IsReportedBecauseTheWatchRecurses()
    {
        using TempTreeFolder folder = new();
        string nested = folder.AddDirectory("notes");

        using Collector collector = new(folder.Root);

        folder.AddFile(Path.Combine("notes", "deep.md"));

        DirectoryChangeSet changes = await collector.NextAsync();

        Assert.Contains(nested, changes.Directories);
    }

    [Fact]
    public async Task ABurstOfChanges_IsReportedAsOneDirectory()
    {
        using TempTreeFolder folder = new();
        using Collector collector = new(folder.Root);

        for (int index = 0; index < 20; index++)
        {
            folder.AddFile($"note-{index}.md");
        }

        DirectoryChangeSet changes = await collector.NextAsync();

        // Twenty files in, one directory out — the whole point of coalescing.
        Assert.Equal([folder.Root], changes.Directories);
    }

    [Fact]
    public async Task ADeletedFile_IsReportedTooAndTheDirectoryAppearsOnce()
    {
        using TempTreeFolder folder = new();
        string doomed = folder.AddFile("doomed.md");

        using Collector collector = new(folder.Root);

        File.Delete(doomed);

        DirectoryChangeSet changes = await collector.NextAsync();

        Assert.Equal([folder.Root], changes.Directories);
    }

    [Fact]
    public void ARootThatIsNotThere_LeavesTheWatcherStoppedRatherThanThrowing()
    {
        using TempTreeFolder folder = new();
        using DirectoryWatcher watcher = new(folder.Combine("unplugged"), Debounce);

        watcher.Start();

        // An unplugged drive is not an error; the tree already draws that root as missing.
        Assert.False(watcher.IsWatching);
    }

    [Fact]
    public async Task ADisposedWatcher_ReportsNothingMore()
    {
        using TempTreeFolder folder = new();
        Collector collector = new(folder.Root);

        folder.AddFile("first.md");
        await collector.NextAsync();

        collector.Dispose();
        folder.AddFile("second.md");

        // Long enough that a watcher still running would have reported by now.
        await Task.Delay(TimeSpan.FromMilliseconds(600), TestContext.Current.CancellationToken);

        Assert.Equal(1, collector.BatchCount);
    }

    [Fact]
    public void ItsArguments_AreChecked()
    {
        Assert.ThrowsAny<ArgumentException>(() => new DirectoryWatcher("  "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new DirectoryWatcher("/tmp", TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DirectoryWatcher("/tmp", Debounce, TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void AStartedWatcher_KnowsItIsWatchingAndStartingTwiceIsHarmless()
    {
        using TempTreeFolder folder = new();
        using DirectoryWatcher watcher = new(folder.Root, Debounce);

        watcher.Start();
        watcher.Start();

        Assert.True(watcher.IsWatching);
        Assert.Equal(Path.TrimEndingDirectorySeparator(folder.Root), watcher.RootDirectory);
    }

    /// <summary>
    /// A started watcher plus the batches it has raised, and a bounded wait for the next one.
    /// </summary>
    private sealed class Collector : IDisposable
    {
        private readonly DirectoryWatcher _watcher;
        private readonly List<DirectoryChangeSet> _batches = [];
        private readonly SemaphoreSlim _arrived = new(0);

        public Collector(string rootDirectory)
        {
            _watcher = new DirectoryWatcher(rootDirectory, Debounce);
            _watcher.Changed += OnChanged;
            _watcher.Start();
        }

        public int BatchCount
        {
            get
            {
                lock (_batches)
                {
                    return _batches.Count;
                }
            }
        }

        public async Task<DirectoryChangeSet> NextAsync()
        {
            Assert.True(await _arrived.WaitAsync(Timeout, TestContext.Current.CancellationToken), "No batch arrived.");

            lock (_batches)
            {
                return _batches[^1];
            }
        }

        public void Dispose()
        {
            _watcher.Changed -= OnChanged;
            _watcher.Dispose();
            _arrived.Dispose();
        }

        private void OnChanged(object? sender, DirectoryChangeSet changes)
        {
            lock (_batches)
            {
                _batches.Add(changes);
            }

            _arrived.Release();
        }
    }
}
