using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// Holds the scheduled callbacks instead of timing them, so a test fires an autosave or a preview
/// refresh rather than sleeping through its delay.
/// </summary>
internal sealed class FakeIdleScheduler : IIdleScheduler
{
    private readonly Dictionary<object, (TimeSpan Delay, Func<Task> OnElapsed)> _scheduled =
        new(ReferenceEqualityComparer.Instance);

    public int ScheduledCount => _scheduled.Count;

    public int CancelAllCount { get; private set; }

    public bool IsScheduled(object key) => _scheduled.ContainsKey(key);

    public TimeSpan DelayFor(object key) => _scheduled[key].Delay;

    /// <summary>
    /// Runs one key's pending work, as the real timer would once the delay passed.
    /// </summary>
    public Task FireAsync(object key)
    {
        if (!_scheduled.Remove(key, out (TimeSpan Delay, Func<Task> OnElapsed) entry))
        {
            throw new InvalidOperationException("Nothing was scheduled for that key.");
        }

        return entry.OnElapsed();
    }

    public void Schedule(object key, TimeSpan delay, Func<Task> onElapsed) => _scheduled[key] = (delay, onElapsed);

    public void Cancel(object key) => _scheduled.Remove(key);

    public void CancelAll()
    {
        CancelAllCount++;
        _scheduled.Clear();
    }

    public void Dispose() => _scheduled.Clear();
}
