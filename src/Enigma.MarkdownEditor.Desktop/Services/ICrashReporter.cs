using System;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// The safety net's far end: what <see cref="UnhandledExceptionRouter"/> hands every unhandled
/// exception to once it exists.
/// </summary>
public interface ICrashReporter
{
    /// <summary>
    /// Records an exception nothing else handled and, where the process survives it, tells the user.
    /// Callable from any thread.
    /// </summary>
    /// <param name="exception">The exception.</param>
    /// <param name="source">Where it was caught.</param>
    void Report(Exception exception, UnhandledErrorSource source);
}
