using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// One entry a <see cref="RecordingLogger{T}"/> kept.
/// </summary>
/// <param name="Level">How severe it was recorded as.</param>
/// <param name="Message">The formatted message.</param>
/// <param name="Failure">The exception it carried, if any.</param>
internal sealed record LogEntry(LogLevel Level, string Message, Exception? Failure);

/// <summary>
/// An <see cref="ILogger{TCategoryName}"/> that keeps what it was told, for the few assertions where
/// the log entry IS the behaviour — a failure the user is deliberately not shown.
/// </summary>
/// <remarks>
/// The rest of the suite passes <c>NullLogger</c>, because a log line is usually a side effect of
/// something else a test can see. It is not always: "the write failed, nothing was written, and
/// nobody was interrupted about it" leaves the log as the only evidence the failure was noticed at
/// all, and a test that skipped it would pass over a silently swallowed exception.
/// </remarks>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _entries = [];

    /// <summary>
    /// Everything recorded, oldest first.
    /// </summary>
    public IReadOnlyList<LogEntry> Entries => _entries;

    /// <summary>
    /// Everything recorded at one level.
    /// </summary>
    public IReadOnlyList<LogEntry> At(LogLevel level) => _entries.FindAll(entry => entry.Level == level);

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
        => Scope.Instance;

    // Everything, so a test never fails because a level happened to be filtered out.
    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        _entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    private sealed class Scope : IDisposable
    {
        public static Scope Instance { get; } = new();

        public void Dispose()
        {
            // Nothing is scoped: what a test reads is the flat list of entries.
        }
    }
}
