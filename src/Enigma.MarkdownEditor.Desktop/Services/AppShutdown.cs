using System;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Runs the application's closing sequence from a thread that is about to block on it, without
/// deadlocking and without waiting for ever.
/// </summary>
/// <remarks>
/// <para>
/// The exit handler is synchronous — the lifetime does not await it — and runs on the UI thread, while
/// everything it has to do is asynchronous. Awaiting that work inline and blocking on the result is
/// the shape that hangs: the continuation after each <c>await</c> captures Avalonia's synchronization
/// context and is posted back to the very thread that is blocked waiting for it, so neither side ever
/// moves again. That is what left the process running after the window had gone.
/// </para>
/// <para>
/// Two things make it safe here. The work is started with <see cref="Task.Run(Func{Task})"/>, so it
/// begins on a thread-pool thread with no synchronization context to capture and its continuations
/// resume on the pool rather than on the blocked caller; and the wait is bounded, so a step that never
/// finishes costs a few seconds and a log line instead of a process that has to be killed. Work
/// abandoned on a timeout is left where it is, on a pool thread — that is, a background thread, which
/// cannot hold the process open.
/// </para>
/// </remarks>
public static class AppShutdown
{
    /// <summary>
    /// How long the closing sequence gets before it is abandoned.
    /// </summary>
    /// <remarks>
    /// Long enough for a session file and an NLog flush on a machine that is busy or paging, short
    /// enough that someone who closed the window is not left watching a taskbar entry and wondering
    /// whether to reach for the task manager.
    /// </remarks>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Runs <paramref name="work"/> on the thread pool, blocking the caller until it finishes or
    /// <paramref name="timeout"/> elapses.
    /// </summary>
    /// <param name="work">
    /// The closing sequence. Handed a token that is signalled if time runs out, so a cooperative step
    /// can stop tidily rather than being cut off.
    /// </param>
    /// <param name="timeout">How long to wait before giving up on it.</param>
    /// <returns>
    /// <see langword="true"/> if the work finished within <paramref name="timeout"/>; otherwise
    /// <see langword="false"/>, meaning it was abandoned and whatever it had left to do did not happen.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="work"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// An exception from <paramref name="work"/> is deliberately not swallowed. A closing step that
    /// throws is a defect, the crash handlers are still installed when this runs, and a shutdown that
    /// quietly ate its own failure is a bug report nobody can write.
    /// </remarks>
    public static bool Run(Func<CancellationToken, Task> work, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(work);

        CancellationTokenSource cancellation = new();

        // The whole point of the method. Started here the work has no synchronization context to
        // capture, so its continuations resume on a pool thread that is free to run them instead of
        // being posted back to the thread blocked on the line below.
        Task shutdown = Task.Run(() => work(cancellation.Token));

        // The wait handle rather than Task.Wait, for two reasons. Wait is allowed to run the task
        // inline on the calling thread, which would put the work straight back onto the context this
        // method exists to keep it off; and a step that faulted then reaches the crash handler as the
        // exception it actually threw rather than wrapped in an AggregateException.
        if (((IAsyncResult)shutdown).AsyncWaitHandle.WaitOne(timeout))
        {
            cancellation.Dispose();
            shutdown.GetAwaiter().GetResult();

            return true;
        }

        // Out of time. Asking is not a formality: a cooperative write that checks its token can still
        // stop tidily instead of being cut off half-way through the file it is replacing.
        cancellation.Cancel();

        // And the source is then left undisposed on purpose. The abandoned work still holds its token,
        // and there is no timer and no wait handle behind this one, so disposing it would buy nothing
        // and risk faulting a thread nobody is watching any more.
        return false;
    }
}
