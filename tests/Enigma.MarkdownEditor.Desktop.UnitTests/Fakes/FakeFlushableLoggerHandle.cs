using Enigma.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// A logger handle that only counts its flushes — the crash reporter's whole use of it.
/// </summary>
internal sealed class FakeFlushableLoggerHandle : IFlushableLoggerHandle
{
    /// <summary>
    /// How many times <see cref="Flush"/> was called.
    /// </summary>
    public int Flushes { get; private set; }

    public ILoggerFactory Factory => NullLoggerFactory.Instance;

    public ILogger Logger => NullLogger.Instance;

    public ILogger<T> CreateLogger<T>() => NullLogger<T>.Instance;

    public void Flush() => Flushes++;

    public void Dispose()
    {
    }
}
