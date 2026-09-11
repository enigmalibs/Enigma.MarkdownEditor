using System;
using System.Threading.Tasks;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// One restartable idle timer per key: work that should happen once the user has stopped, rather than
/// on every keystroke.
/// </summary>
/// <remarks>
/// <para>
/// Two features want exactly this shape. Autosave writes a document a couple of seconds after the
/// last edit to it, keyed by the document. The live preview re-renders a quarter of a second after
/// the last edit, keyed by the pane. Both are a single <see cref="System.Threading.Timer"/> with a
/// <c>Change</c> on each edit — trivial, and untestable in place, which is why it is behind a seam. A
/// test schedules against a fake and fires it, instead of sleeping.
/// </para>
/// <para>
/// The callback is invoked on the UI thread: what these callbacks touch is a document and a control
/// tree, and a <c>TextDocument</c> may only be read from the thread that built it.
/// </para>
/// </remarks>
public interface IIdleScheduler : IDisposable
{
    /// <summary>
    /// Starts, or restarts, the idle timer for one key.
    /// </summary>
    /// <param name="key">
    /// Whatever identifies the work. Compared by reference, so a document that is renamed keeps its
    /// timer — and so two features scheduling under two different objects never collide.
    /// </param>
    /// <param name="delay">How long the key has to stay untouched before <paramref name="onElapsed"/> runs.</param>
    /// <param name="onElapsed">
    /// What to run on the UI thread once the delay passes. It must handle its own failures — nothing
    /// observes the task it returns.
    /// </param>
    void Schedule(object key, TimeSpan delay, Func<Task> onElapsed);

    /// <summary>
    /// Stops one key's timer, if it has one. Harmless when it does not.
    /// </summary>
    /// <param name="key">The key whose timer to stop.</param>
    void Cancel(object key);

    /// <summary>
    /// Stops every timer. Used while a dialog is up, and when the editor shuts down.
    /// </summary>
    void CancelAll();
}
