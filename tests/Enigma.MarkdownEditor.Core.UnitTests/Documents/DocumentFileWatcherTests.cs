using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Core.Documents;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Documents;

/// <summary>
/// Tests for the per-document watcher against a real temp directory: that a content edit is reported
/// at all — which is precisely what the tree's watcher cannot see — that a neighbouring file is not,
/// and that a disposed watcher goes quiet.
/// </summary>
/// <remarks>
/// These drive the operating system's own notification machinery, so they wait for an event rather
/// than assert on the next line. The waits are generous — a slow CI agent must not turn a correct
/// watcher into a red build — and every one of them is bounded, so a broken watcher fails rather than
/// hangs.
/// </remarks>
public sealed class DocumentFileWatcherTests
{
    private static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan LongEnoughToBeSure = TimeSpan.FromMilliseconds(700);

    [Fact]
    public async Task AContentEditToTheWatchedFile_IsReported()
    {
        using TempDocumentFolder folder = new();
        string path = folder.WriteBytes("notes.md", Encoding.ASCII.GetBytes("one"));

        using Collector collector = new(path);

        File.WriteAllText(path, "one two three");

        await collector.NextAsync();

        Assert.True(collector.Count >= 1);
    }

    [Fact]
    public async Task DeletingTheWatchedFile_IsReported()
    {
        using TempDocumentFolder folder = new();
        string path = folder.WriteBytes("notes.md", Encoding.ASCII.GetBytes("one"));

        using Collector collector = new(path);

        File.Delete(path);

        await collector.NextAsync();
    }

    [Fact]
    public async Task CreatingAWatchedFileThatWasNotThereYet_IsReported()
    {
        using TempDocumentFolder folder = new();
        string path = folder.Combine("later.md");

        using Collector collector = new(path);

        File.WriteAllText(path, "here now");

        await collector.NextAsync();
    }

    [Fact]
    public async Task AChangeToANeighbouringFile_IsNotReported()
    {
        using TempDocumentFolder folder = new();
        string path = folder.WriteBytes("watched.md", Encoding.ASCII.GetBytes("one"));

        using Collector collector = new(path);

        File.WriteAllText(folder.Combine("someone-else.md"), "not your business");

        await Task.Delay(LongEnoughToBeSure, TestContext.Current.CancellationToken);

        Assert.Equal(0, collector.Count);
    }

    [Fact]
    public async Task ABurstOfWrites_IsReportedOnce()
    {
        using TempDocumentFolder folder = new();
        string path = folder.WriteBytes("notes.md", Encoding.ASCII.GetBytes(string.Empty));

        using Collector collector = new(path);

        for (int index = 0; index < 20; index++)
        {
            File.WriteAllText(path, $"revision {index}");
        }

        await collector.NextAsync();
        await Task.Delay(LongEnoughToBeSure, TestContext.Current.CancellationToken);

        // One save is several raw events; twenty of them must still not be twenty reloads.
        Assert.Equal(1, collector.Count);
    }

    [Fact]
    public async Task ADisposedWatcher_ReportsNothingMore()
    {
        using TempDocumentFolder folder = new();
        string path = folder.WriteBytes("notes.md", Encoding.ASCII.GetBytes("one"));

        Collector collector = new(path);

        File.WriteAllText(path, "two");
        await collector.NextAsync();

        collector.Dispose();
        File.WriteAllText(path, "three");

        await Task.Delay(LongEnoughToBeSure, TestContext.Current.CancellationToken);

        Assert.Equal(1, collector.Count);
    }

    [Fact]
    public void AFileWhoseFolderIsNotThere_LeavesTheWatcherStoppedRatherThanThrowing()
    {
        using TempDocumentFolder folder = new();
        using DocumentFileWatcher watcher = new(Path.Combine(folder.Root, "unplugged", "notes.md"), Debounce);

        watcher.Start();

        Assert.False(watcher.IsWatching);
    }

    [Fact]
    public void AStartedWatcher_KnowsItIsWatchingAndStartingTwiceIsHarmless()
    {
        using TempDocumentFolder folder = new();
        string path = folder.WriteBytes("notes.md", Encoding.ASCII.GetBytes("one"));

        using DocumentFileWatcher watcher = new(path, Debounce);

        watcher.Start();
        watcher.Start();

        Assert.True(watcher.IsWatching);
        Assert.Equal(Path.GetFullPath(path), watcher.FilePath);
    }

    [Fact]
    public void ItsArguments_AreChecked()
    {
        Assert.ThrowsAny<ArgumentException>(() => new DocumentFileWatcher("  "));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new DocumentFileWatcher(Path.Combine(Path.GetTempPath(), "notes.md"), TimeSpan.Zero));
    }

    /// <summary>
    /// A started watcher plus a bounded wait for its next report.
    /// </summary>
    private sealed class Collector : IDisposable
    {
        private readonly DocumentFileWatcher _watcher;
        private readonly SemaphoreSlim _arrived = new(0);

        private int _count;

        public Collector(string filePath)
        {
            _watcher = new DocumentFileWatcher(filePath, Debounce);
            _watcher.Changed += OnChanged;
            _watcher.Start();
        }

        public int Count => Volatile.Read(ref _count);

        public async Task NextAsync()
            => Assert.True(
                await _arrived.WaitAsync(Timeout, TestContext.Current.CancellationToken),
                "The watcher reported nothing.");

        public void Dispose()
        {
            _watcher.Changed -= OnChanged;
            _watcher.Dispose();
            _arrived.Dispose();
        }

        private void OnChanged(object? sender, EventArgs e)
        {
            Interlocked.Increment(ref _count);
            _arrived.Release();
        }
    }
}
