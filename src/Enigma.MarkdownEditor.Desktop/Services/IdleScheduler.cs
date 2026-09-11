using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// <see cref="IIdleScheduler"/> over one <see cref="Timer"/> per key, marshalled back onto the UI
/// thread through <see cref="IUiDispatcher"/>.
/// </summary>
public sealed class IdleScheduler : IIdleScheduler
{
    private readonly IUiDispatcher _dispatcher;

    // Reference equality on purpose: the key is the object that scheduled the work — a document, or
    // the preview pane — so a document whose path changes under Save As keeps the timer it already
    // had, and two features never share one by accident.
    private readonly Dictionary<object, Timer> _timers = new(ReferenceEqualityComparer.Instance);
    private readonly Lock _gate = new();

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdleScheduler"/> class.
    /// </summary>
    /// <param name="dispatcher">Moves the elapsed callback back onto the UI thread.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dispatcher"/> is <see langword="null"/>.</exception>
    public IdleScheduler(IUiDispatcher dispatcher)
        => _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Either <paramref name="key"/> or <paramref name="onElapsed"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="delay"/> is negative.</exception>
    public void Schedule(object key, TimeSpan delay, Func<Task> onElapsed)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(onElapsed);
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (_timers.TryGetValue(key, out Timer? existing))
            {
                // The idle part of "idle timer": an edit pushes the work back rather than adding a
                // second one.
                existing.Change(delay, Timeout.InfiniteTimeSpan);
                return;
            }

            // Created stopped and armed immediately after it is in the dictionary, so a delay of zero
            // cannot have the callback looking for a timer that is not there yet.
            Timer timer = new(_ => Fire(key, onElapsed), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _timers[key] = timer;
            timer.Change(delay, Timeout.InfiniteTimeSpan);
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
    public void Cancel(object key)
    {
        ArgumentNullException.ThrowIfNull(key);

        Timer? timer;

        lock (_gate)
        {
            if (!_timers.Remove(key, out timer))
            {
                return;
            }
        }

        timer.Dispose();
    }

    /// <inheritdoc />
    public void CancelAll()
    {
        Timer[] timers;

        lock (_gate)
        {
            timers = [.. _timers.Values];
            _timers.Clear();
        }

        foreach (Timer timer in timers)
        {
            timer.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }

        CancelAll();
    }

    private void Fire(object key, Func<Task> onElapsed)
    {
        // Removed before the callback runs, not after: this is a one-shot timer, and leaving it in the
        // dictionary would have the next edit restart a timer that has already fired.
        Cancel(key);

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
        }

        _dispatcher.Post(onElapsed);
    }
}
