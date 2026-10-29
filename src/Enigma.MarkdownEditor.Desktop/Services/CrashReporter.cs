using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Logging.Abstractions;
using Enigma.MarkdownEditor.Core;
using Enigma.MarkdownEditor.Desktop.Views;
using Microsoft.Extensions.Logging;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Catches what nothing else caught: logs it, flushes the log to disk, and tells the user.
/// </summary>
/// <remarks>
/// <para>
/// The flush is the point. Buffered log entries live in memory until the target writes them, and an
/// AppDomain report arrives while the process is already on its way down — an entry that is only
/// buffered when the process dies is an entry that never existed. Every report is therefore logged and
/// immediately flushed through <see cref="IFlushableLoggerHandle"/>, on whatever thread made it.
/// </para>
/// <para>
/// What is shown is decided on the UI thread. The dialog goes on the main window's host, which every
/// other question shares, and <see cref="IContentDialogService.ShowAsync"/> takes the host over
/// whatever is on it — so the error waits until that question is answered, and before the host is
/// attached until it is. One error dialog at a time: what is reported while one is open or waiting is
/// logged and goes no further, because a fault in a render or input loop can fire many times a second
/// and a queue of identical dialogs would be worse than the fault. If the dialog itself cannot be
/// shown, that is logged and no dialog is tried again: the net must not loop on itself.
/// </para>
/// </remarks>
public sealed class CrashReporter : ICrashReporter
{
    private readonly ILogger<CrashReporter> _logger;
    private readonly IFlushableLoggerHandle _loggerHandle;
    private readonly IContentDialogService _dialogService;
    private readonly string _logFilePath;

    // UI-thread state from here down: read and written only by work running on the dispatcher.
    private ContentDialog? _host;
    private Exception? _pending;
    private bool _showing;
    private bool _waitingForHost;
    private bool _broken;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrashReporter"/> class.
    /// </summary>
    /// <param name="logger">Where the crash is recorded.</param>
    /// <param name="loggerHandle">The handle whose <c>Flush</c> gets that record onto the disk.</param>
    /// <param name="dialogService">Shows the error dialog on the main window's host.</param>
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
    /// Tells the reporter which control the dialog service shows its dialogs on, so it can wait for a
    /// question already open there. Call on the UI thread, once that host is registered with the
    /// service; an error reported before then is shown now.
    /// </summary>
    /// <param name="host">The host registered with the dialog service.</param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is <see langword="null"/>.</exception>
    public void AttachHost(ContentDialog host)
    {
        ArgumentNullException.ThrowIfNull(host);

        _host = host;
        TryShowPending();
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> is <see langword="null"/>.</exception>
    public void Report(Exception exception, UnhandledErrorSource source)
    {
        ArgumentNullException.ThrowIfNull(exception);

        switch (source)
        {
            case UnhandledErrorSource.AppDomain:
                _logger.LogCritical(exception, "Unhandled exception on a background thread; the process is terminating.");
                break;
            case UnhandledErrorSource.UnobservedTask:
                _logger.LogError(exception, "A faulted task was never observed.");
                break;
            default:
                _logger.LogError(exception, "Unhandled exception on the UI thread.");
                break;
        }

        _loggerHandle.Flush();

        if (source == UnhandledErrorSource.AppDomain)
        {
            // No dialog here on purpose: by the time the CLR raises this the process is already being
            // torn down, so a dialog would race the shutdown and usually lose. The flushed entry is
            // what survives, and it is what a bug report needs.
            return;
        }

        Dispatcher.UIThread.Post(() => Present(exception));
    }

    private void Present(Exception exception)
    {
        if (_broken || _showing || _pending is not null)
        {
            return;
        }

        _pending = exception;
        TryShowPending();
    }

    private void TryShowPending()
    {
        if (_broken || _showing || _pending is null || _host is null)
        {
            return;
        }

        if (_host.IsOpen)
        {
            if (!_waitingForHost)
            {
                _waitingForHost = true;
                _host.Closed += OnHostClosed;
            }

            return;
        }

        Exception exception = _pending;
        _pending = null;
        _showing = true;

        _ = ShowAsync(exception);
    }

    private void OnHostClosed(object? sender, DialogResult result)
    {
        if (_host is not null)
        {
            _host.Closed -= OnHostClosed;
        }

        _waitingForHost = false;

        // Posted rather than shown from here: the dialog that just closed is still raising its event,
        // and the code that awaited it has yet to run.
        Dispatcher.UIThread.Post(TryShowPending);
    }

    private async Task ShowAsync(Exception exception)
    {
        string message =
            $"{ErrorDialog.Describe(exception)}{Environment.NewLine}{Environment.NewLine}" +
            $"{AppInfo.DisplayName} is still running. The full details were written to {_logFilePath}.";

        try
        {
            await _dialogService.ShowAsync(dialog => ErrorDialog.Configure(dialog, message)).ConfigureAwait(true);
        }
        catch (Exception failure)
        {
            // Logged, never reported: a dialog that cannot be shown would otherwise report itself again
            // and again. The original error is in the log already.
            _broken = true;
            _logger.LogError(failure, "The error dialog could not be shown; later errors are only logged.");
            _loggerHandle.Flush();
        }
        finally
        {
            _showing = false;
        }
    }
}
