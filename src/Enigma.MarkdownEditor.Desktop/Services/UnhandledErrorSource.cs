namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Where an exception nothing handled was caught, which decides what can still be done about it.
/// </summary>
public enum UnhandledErrorSource
{
    /// <summary>
    /// It escaped a thread with nothing above it. .NET ends the process once the event returns, so it
    /// can be logged and nothing more.
    /// </summary>
    AppDomain,

    /// <summary>
    /// A faulted task that nobody awaited was collected. The process carries on.
    /// </summary>
    UnobservedTask,

    /// <summary>
    /// A command, a posted callback or an <c>async void</c> handler threw on the UI thread. It is
    /// marked handled, so the editor carries on.
    /// </summary>
    Dispatcher,
}
