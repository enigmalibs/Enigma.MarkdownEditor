using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Hands every unhandled exception to the crash reporter — including the ones caught before there
/// was a crash reporter to hand them to.
/// </summary>
/// <remarks>
/// <para>
/// The process-wide events are hooked on the first line of <c>Main</c>, but the reporter needs the
/// logger and the dialog host, and those exist only once the container is built. What arrives in
/// between is written to <see cref="Trace"/> at once and kept — up to <see cref="EarlyLimit"/>
/// reports — then replayed when the reporter attaches, so a failure early in the start that the
/// application survives still reaches the log and the user.
/// </para>
/// <para>
/// Thread-safe: the AppDomain event is raised on the faulting thread and the unobserved-task event on
/// the finalizer thread. The reporter is called outside the lock, so a reporter that is slow, or that
/// reports again, cannot deadlock the router.
/// </para>
/// </remarks>
public sealed class UnhandledExceptionRouter
{
    /// <summary>
    /// How many reports are kept for the reporter before it attaches. A fault in a loop fires many
    /// times; the first few say everything the rest would.
    /// </summary>
    public const int EarlyLimit = 32;

    private readonly Lock _gate = new();
    private readonly Queue<(Exception Exception, UnhandledErrorSource Source)> _early = new();
    private ICrashReporter? _reporter;

    /// <summary>
    /// Gets a value indicating whether a reporter is attached — that is, whether a report made now
    /// reaches the log.
    /// </summary>
    public bool IsAttached
    {
        get
        {
            lock (_gate)
            {
                return _reporter is not null;
            }
        }
    }

    /// <summary>
    /// Hands over to the reporter: what was reported before it existed first, then everything from
    /// here on.
    /// </summary>
    /// <param name="reporter">The reporter.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reporter"/> is <see langword="null"/>.</exception>
    public void Attach(ICrashReporter reporter)
    {
        ArgumentNullException.ThrowIfNull(reporter);

        List<(Exception Exception, UnhandledErrorSource Source)> early;

        lock (_gate)
        {
            _reporter = reporter;
            early = [.. _early];
            _early.Clear();
        }

        foreach ((Exception exception, UnhandledErrorSource source) in early)
        {
            reporter.Report(exception, source);
        }
    }

    /// <summary>
    /// Takes the reporter away again — before the container that owns it is disposed, so a late
    /// report does not log through a disposed logger. Reports are kept and traced again from here on.
    /// </summary>
    public void Detach()
    {
        lock (_gate)
        {
            _reporter = null;
        }
    }

    /// <summary>
    /// Reports an exception nothing handled.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="source">Where it was caught.</param>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
    public void Report(Exception exception, UnhandledErrorSource source)
    {
        ArgumentNullException.ThrowIfNull(exception);

        ICrashReporter? reporter;

        lock (_gate)
        {
            reporter = _reporter;

            if (reporter is null && _early.Count < EarlyLimit)
            {
                _early.Enqueue((exception, source));
            }
        }

        if (reporter is null)
        {
            // In full, stack included: with no log file yet, this may be the only record there is.
            Trace.TraceError("Unhandled exception before the crash reporter was ready ({0}): {1}", source, exception);
            return;
        }

        reporter.Report(exception, source);
    }
}
