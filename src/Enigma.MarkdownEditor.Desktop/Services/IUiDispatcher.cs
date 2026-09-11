using System;
using System.Threading.Tasks;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Moves work onto the UI thread.
/// </summary>
/// <remarks>
/// A seam over <c>Dispatcher.UIThread</c>, and the only reason it exists is testability: a file
/// watcher raises its events on a thread-pool thread, so everything it triggers has to be marshalled —
/// and a unit test has no Avalonia application, so a posted job on the real dispatcher would simply
/// never run.
/// </remarks>
public interface IUiDispatcher
{
    /// <summary>
    /// Queues an operation to run on the UI thread.
    /// </summary>
    /// <param name="operation">
    /// The work to run. It must handle its own failures — nothing observes the task it returns, so an
    /// exception escaping it would be lost.
    /// </param>
    void Post(Func<Task> operation);
}
