using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Desktop.Services;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Services;

/// <summary>
/// The closing sequence: that it finishes when the thread that started it is blocked waiting for it,
/// that it gives up rather than hanging, and that what it gives up on is asked to stop first.
/// </summary>
/// <remarks>
/// The first test is the regression test for the bug itself. It runs under <see cref="UiThread"/>,
/// whose synchronization context posts every continuation back to the one thread it owns — the same
/// arrangement Avalonia's UI thread has — and then blocks that thread inside
/// <see cref="AppShutdown.Run"/>. Awaited inline, the work's continuation would be queued behind the
/// block that is waiting for it and neither would ever move.
/// </remarks>
public sealed class AppShutdownTests : IDisposable
{
    private readonly string _root;
    private readonly string _file;

    public AppShutdownTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _file = Path.Combine(_root, "written-on-the-way-out.txt");
    }

    [Fact]
    public async Task WorkThatAwaits_FinishesEvenThoughTheThreadThatStartedItIsBlocked()
    {
        bool finished = false;

        await UiThread.RunAsync(() =>
        {
            finished = AppShutdown.Run(
                async cancellationToken =>
                {
                    // A real asynchronous file write, which is what flushing the session ultimately
                    // does. Awaited on the caller's context this never resumes: the thread that would
                    // have to run the continuation is the one blocked inside Run.
                    await File.WriteAllTextAsync(_file, "written", cancellationToken);
                },
                TimeSpan.FromSeconds(30));

            return Task.CompletedTask;
        });

        Assert.True(finished);
        Assert.Equal("written", await File.ReadAllTextAsync(_file, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void WorkThatFinishesImmediately_IsReportedDoneAndThrowsNothing()
    {
        bool ran = false;

        bool finished = AppShutdown.Run(
            _ =>
            {
                ran = true;

                return Task.CompletedTask;
            },
            AppShutdown.DefaultTimeout);

        Assert.True(finished);
        Assert.True(ran);
    }

    [Fact]
    public void WorkThatNeverFinishes_IsAbandonedRatherThanWaitedFor()
    {
        TimeSpan timeout = TimeSpan.FromMilliseconds(250);
        Stopwatch waited = Stopwatch.StartNew();

        bool finished = AppShutdown.Run(_ => new TaskCompletionSource().Task, timeout);

        waited.Stop();

        Assert.False(finished);

        // Generous, because a busy build agent is not a hung shutdown. The point is that it returned
        // at all: the whole defect was a wait with no end to it.
        Assert.True(waited.Elapsed < TimeSpan.FromSeconds(10), $"Gave up after {waited.Elapsed}.");
    }

    [Fact]
    public void WorkThatOverrunsTheTimeout_IsAskedToStop()
    {
        CancellationToken observed = CancellationToken.None;
        using ManualResetEventSlim started = new();

        bool finished = AppShutdown.Run(
            cancellationToken =>
            {
                observed = cancellationToken;
                started.Set();

                return new TaskCompletionSource().Task;
            },
            TimeSpan.FromMilliseconds(250));

        Assert.False(finished);

        // Waited for rather than assumed: the work runs on the thread pool, and on a loaded machine it
        // can start after the wait has already given up. Either way the token it is handed is the one
        // cancellation was requested on.
        Assert.True(started.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        Assert.True(observed.IsCancellationRequested);
    }

    [Fact]
    public void WorkThatThrows_SurfacesTheExceptionItActuallyThrew()
    {
        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(
            () => _ = AppShutdown.Run(
                _ => Task.FromException(new InvalidOperationException("the host would not stop")),
                AppShutdown.DefaultTimeout));

        // Not an AggregateException wrapping it: on this path the crash handlers are still installed,
        // and what they log should be the fault rather than the wrapper.
        Assert.Equal("the host would not stop", failure.Message);
    }

    [Fact]
    public void NoSequenceAtAll_IsRefused()
        => Assert.Throws<ArgumentNullException>(() => _ = AppShutdown.Run(null!, AppShutdown.DefaultTimeout));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A leftover temp folder is not worth failing a green test over.
        }
    }
}
