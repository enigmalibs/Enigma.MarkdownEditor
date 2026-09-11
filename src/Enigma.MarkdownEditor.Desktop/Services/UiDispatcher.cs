using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// <see cref="IUiDispatcher"/> over Avalonia's own dispatcher.
/// </summary>
public sealed class UiDispatcher : IUiDispatcher
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="operation"/> is <see langword="null"/>.</exception>
    public void Post(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // The returned task is deliberately discarded: the contract is that the operation handles its
        // own failures, and awaiting it here would only park them somewhere nobody looks.
        Dispatcher.UIThread.Post(() => _ = operation());
    }
}
