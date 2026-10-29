using System;
using Avalonia.Controls;
using Enigma.MarkdownEditor.Core;
using Enigma.MarkdownEditor.Desktop.Views;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Runs the start-up, and turns a start-up that throws into a window that says so.
/// </summary>
/// <remarks>
/// The start-up runs inside <c>OnFrameworkInitializationCompleted</c>, not as a dispatcher job, so what
/// it throws reaches no hook on its way out — it would end the process from <c>Main</c> with nothing on
/// screen. Here it is reported like any other unhandled exception (logged when logging got that far,
/// traced otherwise), and the lifetime is handed a <see cref="StartupErrorWindow"/> as its main window
/// instead. A failure inside that window itself propagates: there is no host left to try.
/// </remarks>
public static class StartupGuard
{
    /// <summary>
    /// Runs <paramref name="start"/>; if it throws, reports the exception and hands
    /// <paramref name="setMainWindow"/> a window showing it.
    /// </summary>
    /// <param name="start">The start-up.</param>
    /// <param name="setMainWindow">Gives the lifetime its main window.</param>
    /// <param name="router">Where the failure is reported.</param>
    /// <param name="logFilePath">Named in the message when the failure reached the log.</param>
    /// <exception cref="ArgumentNullException">Any reference argument is <see langword="null"/>.</exception>
    public static void Run(Action start, Action<Window> setMainWindow, UnhandledExceptionRouter router, string logFilePath)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(setMainWindow);
        ArgumentNullException.ThrowIfNull(router);
        ArgumentNullException.ThrowIfNull(logFilePath);

        try
        {
            start();
        }
        catch (Exception failure)
        {
            router.Report(failure, UnhandledErrorSource.Startup);

            // Asked after the report: attached means the crash reporter has just written it to the log.
            setMainWindow(new StartupErrorWindow(Describe(failure, router.IsAttached ? logFilePath : null)));
        }
    }

    /// <summary>
    /// What the start-up error dialog says.
    /// </summary>
    /// <param name="failure">What the start-up threw.</param>
    /// <param name="logFilePath">The log the details went to, or <see langword="null"/> if logging had not started.</param>
    /// <returns>The message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is <see langword="null"/>.</exception>
    public static string Describe(Exception failure, string? logFilePath)
    {
        ArgumentNullException.ThrowIfNull(failure);

        string details = logFilePath is null
            ? "It failed before logging had started, so the details could not be written to the log."
            : $"The full details were written to {logFilePath}.";

        return $"{ErrorDialog.Describe(failure)}{Environment.NewLine}{Environment.NewLine}" +
            $"{AppInfo.DisplayName} could not start. {details}";
    }
}
