using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Logging.Abstractions;
using Microsoft.Extensions.Logging;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Catches what nothing else caught: logs it, flushes the log to disk, and tells the user.
/// </summary>
/// <remarks>
/// <para>
/// The flush is the point. Buffered log entries live in memory until the target writes them, and the
/// two fatal paths below are running while the process is already on its way down — an entry that is
/// only buffered when the process dies is an entry that never existed. Every handler therefore logs
/// and immediately flushes through <see cref="IFlushableLoggerHandle"/>.
/// </para>
/// <para>
/// The three handlers are deliberately not equivalent. A UI-thread exception is *handled* and the
/// editor keeps running — losing a document's unsaved text to one bad event handler is a worse
/// outcome than carrying on in a slightly unknown state. The other two are reports, not rescues.
/// </para>
/// </remarks>
public sealed class CrashReporter
{
    private readonly ILogger<CrashReporter> _logger;
    private readonly IFlushableLoggerHandle _loggerHandle;
    private readonly IContentDialogService _dialogService;
    private readonly string _logFilePath;

    // 0 = no error dialog on screen. A fault inside a render or input loop can fire many times a
    // second, and a queue of identical dialogs would be worse than the fault.
    private int _dialogShown;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrashReporter"/> class.
    /// </summary>
    /// <param name="logger">Where the crash is recorded.</param>
    /// <param name="loggerHandle">The handle whose <c>Flush</c> gets that record onto the disk.</param>
    /// <param name="dialogService">Used for the best-effort error dialog.</param>
    /// <param name="logFilePath">Quoted in the dialog so the user can find the details.</param>
    /// <exception cref="ArgumentException"><paramref name="logFilePath"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Any reference argument is <see langword="null"/>.</exception>
    public CrashReporter(
        ILogger<CrashReporter> logger,
        IFlushableLoggerHandle loggerHandle,
        IContentDialogService dialogService,
        string logFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logFilePath);

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerHandle = loggerHandle ?? throw new ArgumentNullException(nameof(loggerHandle));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logFilePath = logFilePath;
    }

    /// <summary>
    /// Subscribes the three handlers. Call once, after the container is built and before the window
    /// is shown.
    /// </summary>
    public void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
    }

    private void OnAppDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        _logger.LogCritical(
            e.ExceptionObject as Exception,
            "Unhandled exception on a background thread. Terminating: {IsTerminating}.",
            e.IsTerminating);
        _loggerHandle.Flush();

        // No dialog here on purpose: by the time the CLR raises this the process is already being
        // torn down, so a dialog would race the shutdown and usually lose. The flushed entry is what
        // survives, and it is what a bug report needs.
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "A faulted task was never observed.");
        _loggerHandle.Flush();

        // Marks the fault as seen. It no longer terminates the process by default, but leaving it
        // unobserved would make the behaviour depend on a runtime configuration switch.
        e.SetObserved();

        Report("A background operation failed", e.Exception);
    }

    private void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Unhandled exception on the UI thread.");
        _loggerHandle.Flush();

        // Keeps the editor alive. The alternative is that a bug in one command takes every open
        // document's unsaved text down with it.
        e.Handled = true;

        Report("Something went wrong", e.Exception);
    }

    private void Report(string title, Exception exception)
        => Dispatcher.UIThread.Post(() => _ = ShowAsync(title, exception));

    private async Task ShowAsync(string title, Exception exception)
    {
        if (Interlocked.CompareExchange(ref _dialogShown, 1, 0) != 0)
        {
            return;
        }

        try
        {
            await _dialogService.ShowMessageAsync(
                title,
                $"{exception.GetType().Name}: {exception.Message}{Environment.NewLine}{Environment.NewLine}" +
                $"The full details were written to {_logFilePath}.");
        }
        catch (Exception failure)
        {
            // The dialog host may not be registered yet — a crash during start-up is exactly when
            // that happens — or the dialog itself may fail. Either way the log entry is already
            // written, so this must not escalate into a second crash.
            _logger.LogError(failure, "The crash dialog could not be shown.");
            _loggerHandle.Flush();
        }
        finally
        {
            Interlocked.Exchange(ref _dialogShown, 0);
        }
    }
}
