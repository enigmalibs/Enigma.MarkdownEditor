using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// Runs posted work straight away and keeps the tasks, so a test can wait for it.
/// </summary>
/// <remarks>
/// The real dispatcher queues onto Avalonia's UI thread, which a unit test does not have — posted work
/// there would simply never run. This one starts each operation inline and hands back a way to await
/// everything it started, including the work that work went on to post.
/// </remarks>
internal sealed class FakeUiDispatcher : IUiDispatcher
{
    private readonly List<Task> _posted = [];
    private readonly List<Func<Task>> _deferred = [];

    /// <summary>
    /// Set to hold posted work back until <see cref="DrainAsync"/> instead of starting it inline.
    /// </summary>
    /// <remarks>
    /// What the real dispatcher does — the caller carries on and the posted work runs later — and the
    /// only way to write the race down: a search's hits are marshalled through here, and dropping the
    /// ones belonging to a superseded query is only observable if they can be made to arrive late.
    /// </remarks>
    public bool Defer { get; set; }

    public int PostCount => _posted.Count + _deferred.Count;

    public void Post(Func<Task> operation)
    {
        if (Defer)
        {
            _deferred.Add(operation);
            return;
        }

        _posted.Add(operation());
    }

    /// <summary>
    /// Waits for everything posted so far, and for anything it posts in turn — expanding a root loads
    /// it, and loading it can post again.
    /// </summary>
    public async Task DrainAsync()
    {
        // Held-back work first, and in a loop, because starting it can post more of it.
        while (_deferred.Count > 0)
        {
            Func<Task>[] pending = [.. _deferred];
            _deferred.Clear();

            foreach (Func<Task> operation in pending)
            {
                _posted.Add(operation());
            }
        }

        for (int index = 0; index < _posted.Count; index++)
        {
            await _posted[index];
        }
    }
}
