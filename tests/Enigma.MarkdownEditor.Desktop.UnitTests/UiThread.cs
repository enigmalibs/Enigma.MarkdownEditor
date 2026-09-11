using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.MarkdownEditor.Desktop.UnitTests;

/// <summary>
/// Runs a test body on one dedicated thread, under a synchronization context that keeps every
/// continuation on it.
/// </summary>
/// <remarks>
/// <para>
/// The editor's ViewModels are thread-affine, and not by choice: an AvaloniaEdit
/// <c>TextDocument</c> remembers the thread it was built on and verifies access on every use. In the
/// application that thread is the UI thread, and Avalonia's own synchronization context is what keeps
/// every <c>await</c> coming back to it.
/// </para>
/// <para>
/// A test has neither. Without a context, the continuation after <c>await File.ReadAllBytesAsync</c>
/// lands on an arbitrary thread-pool thread, the document is built there, and the next assertion —
/// on a different pool thread again — throws. This supplies the missing half: one thread, one queue,
/// and every continuation posted back to it, which is exactly the shape the code runs in for real.
/// </para>
/// </remarks>
internal static class UiThread
{
    /// <summary>
    /// Runs <paramref name="body"/> to completion on a single thread and rethrows whatever it threw.
    /// </summary>
    public static Task RunAsync(Func<Task> body)
    {
        ArgumentNullException.ThrowIfNull(body);

        TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Thread thread = new(() =>
        {
            using PumpingContext context = new();
            SynchronizationContext.SetSynchronizationContext(context);

            context.Post(
                async _ =>
                {
                    try
                    {
                        await body();
                        completion.SetResult();
                    }
                    catch (Exception failure)
                    {
                        completion.SetException(failure);
                    }
                    finally
                    {
                        // Lets the pump below drain whatever is left and return, ending the thread.
                        context.Complete();
                    }
                },
                null);

            context.Pump();
        })
        {
            IsBackground = true,
            Name = "UiThread",
        };

        thread.Start();

        return completion.Task;
    }

    private sealed class PumpingContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = [];

        public override void Post(SendOrPostCallback d, object? state)
        {
            try
            {
                _queue.Add((d, state));
            }
            catch (InvalidOperationException)
            {
                // Posted after the body finished. Nothing is waiting on it, and the alternative is a
                // green test failing on the way out.
            }
        }

        public override void Send(SendOrPostCallback d, object? state)
            => throw new NotSupportedException("A blocking Send would deadlock the one thread there is.");

        public void Pump()
        {
            foreach ((SendOrPostCallback callback, object? state) in _queue.GetConsumingEnumerable())
            {
                callback(state);
            }
        }

        public void Complete() => _queue.CompleteAdding();

        public void Dispose() => _queue.Dispose();
    }
}
