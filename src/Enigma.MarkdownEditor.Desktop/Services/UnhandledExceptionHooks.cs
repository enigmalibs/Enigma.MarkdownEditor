using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Hooks the process-wide unhandled-exception events and hands what they catch to
/// <see cref="Router"/>.
/// </summary>
/// <remarks>
/// <para>
/// Static because what it adapts is: <see cref="AppDomain.UnhandledException"/>,
/// <see cref="TaskScheduler.UnobservedTaskException"/> and the UI dispatcher's
/// <see cref="Dispatcher.UnhandledException"/> belong to the process, and they fire long before the
/// container that builds the crash reporter exists. Everything with state lives in the router, which
/// is an ordinary instance a test can build on its own.
/// </para>
/// <para>
/// The three are deliberately not equivalent. A UI-thread exception is marked handled and the editor
/// keeps running — losing every open document's unsaved text to one bad event handler is a worse
/// outcome than carrying on in a slightly unknown state. An unobserved task is marked observed, so
/// what happens to it does not depend on a runtime switch. An AppDomain exception escaped a thread
/// with nothing above it, and .NET ends the process once the event returns whatever a handler does.
/// </para>
/// </remarks>
public static class UnhandledExceptionHooks
{
    private static readonly Lock Gate = new();
    private static bool _installed;
    private static bool _dispatcherInstalled;

    /// <summary>
    /// Gets where every hooked exception goes, and where the crash reporter attaches.
    /// </summary>
    public static UnhandledExceptionRouter Router { get; } = new();

    /// <summary>
    /// Hooks <see cref="AppDomain.UnhandledException"/> and
    /// <see cref="TaskScheduler.UnobservedTaskException"/>. The first thing <c>Main</c> does; calling it
    /// again does nothing.
    /// </summary>
    public static void Install()
    {
        lock (Gate)
        {
            if (_installed)
            {
                return;
            }

            _installed = true;
        }

        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    /// <summary>
    /// Hooks the UI dispatcher's <see cref="Dispatcher.UnhandledException"/>. Called once the platform
    /// is set up — touching the dispatcher before that is not safe — and before anything else the
    /// application does; calling it again does nothing.
    /// </summary>
    public static void InstallDispatcher()
    {
        lock (Gate)
        {
            if (_dispatcherInstalled)
            {
                return;
            }

            _dispatcherInstalled = true;
        }

        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
    }

    private static void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        // Anything can be thrown from IL, not only an Exception; the report still needs one.
        Exception exception = e.ExceptionObject as Exception
            ?? new InvalidOperationException($"An object of type {e.ExceptionObject?.GetType().FullName ?? "null"} was thrown.");

        Router.Report(exception, UnhandledErrorSource.AppDomain);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        Router.Report(e.Exception, UnhandledErrorSource.UnobservedTask);
    }

    private static void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        Router.Report(e.Exception, UnhandledErrorSource.Dispatcher);
    }
}
