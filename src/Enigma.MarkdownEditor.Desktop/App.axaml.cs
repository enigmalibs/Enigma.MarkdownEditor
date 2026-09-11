using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Logging.Abstractions;
using Enigma.MarkdownEditor.Core;
using Enigma.MarkdownEditor.Core.Configuration;
using Enigma.MarkdownEditor.Core.Session;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.Extensions;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Enigma.MarkdownEditor.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Enigma.MarkdownEditor.Desktop;

/// <summary>
/// The Avalonia application object, and the app's composition root.
/// </summary>
/// <remarks>
/// <para>
/// Start-up is in two parts, and the split is what makes the splash screen possible. Everything the
/// splash itself needs — the persisted theme variant, and the window — happens inline; everything
/// else is posted back to the dispatcher, because the message loop has not started yet and a window
/// shown by a method that has not returned is a window that never paints.
/// </para>
/// <para>
/// The order inside the posted half is not free arrangement. The crash handlers cover the window
/// construction that follows them, every dialog host is registered before anything can ask for a
/// dialog, and the main window is shown before the splash is closed — under
/// <see cref="ShutdownMode.OnLastWindowClose"/>, closing the splash while it is the only window on
/// screen ends the process.
/// </para>
/// </remarks>
public partial class App : Application
{
    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        // Before the load, and it has to be: the loader recovers an unreadable settings file by moving
        // it aside, which leaves the path absent — so the same question asked one line later would
        // read a corruption as a first run and re-onboard someone who has been using the editor for
        // months. See FirstRunState.
        FirstRunState firstRun = FirstRunState.Detect(AppPaths.SettingsFile);

        // Read before the container is built, not from it: the log level and the theme are decided by
        // the settings, so composition depends on them. A file that cannot be read is recovered here
        // rather than crashing the start-up — see SettingsLoader.
        ConfigLoadResult<AppSettings> settings = SettingsLoader.Load(AppPaths.SettingsFile);

        // Same treatment, one file later: an unreadable workspaces file is quarantined and the editor
        // starts with an empty list rather than throwing on the first read of it.
        ConfigLoadResult<WorkspacesConfig> workspaces = WorkspacesLoader.Load(AppPaths.WorkspacesFile);

        // And the third: where the editor was left. Read here as well because the window's placement
        // has to be applied before it is shown, and the window is built a few lines below.
        ConfigLoadResult<SessionState> session = SessionLoader.Load(AppPaths.SessionFile);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The theme, before the first window of any kind exists — including the splash, which is
            // shown on the next line and would otherwise be painted in the OS default and repainted
            // a moment later. ThemeService owns the same mapping and applies it whenever the setting
            // changes; it cannot be used here because the container that holds it is built below.
            RequestedThemeVariant = ThemeService.ToVariant(settings.Config.Value.Theme);

            SplashWindow splash = new();
            splash.Show();

            // Started at the moment the splash was asked for, which is what its minimum display is
            // measured from.
            Stopwatch splashShown = Stopwatch.StartNew();

            // Posted, so this method can return and let the message loop paint what was just shown.
            // Background priority is the point of the exercise: layout and rendering run at higher
            // priorities than Normal, so a continuation posted at the default would take its turn
            // *before* the splash had drawn a single frame, which is the white rectangle this whole
            // arrangement exists to avoid.
            Dispatcher.UIThread.Post(
                () => ComposeAndHandOver(desktop, splash, splashShown, firstRun, settings, workspaces, session),
                DispatcherPriority.Background);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Builds the application behind the splash screen, then hands the screen over to the main window.
    /// </summary>
    /// <param name="desktop">The lifetime the main window is given to.</param>
    /// <param name="splash">The window currently on screen, closed once the main window is up.</param>
    /// <param name="splashShown">Running since the splash was shown.</param>
    /// <param name="firstRun">Whether the settings file was there, decided before it could be moved.</param>
    /// <param name="settings">The settings load, reported here now that there is a logger.</param>
    /// <param name="workspaces">The workspaces load, reported for the same reason.</param>
    /// <param name="session">The session load, reported for the same reason.</param>
    /// <remarks>
    /// Runs as one dispatcher job rather than as an <c>async</c> continuation, deliberately: a failure
    /// in a job propagates to <see cref="Dispatcher.UnhandledException"/> — which is
    /// <see cref="CrashReporter"/>'s own handler, logging and flushing synchronously — whereas a
    /// failure inside an unawaited task would sit unobserved until a garbage collection noticed it.
    /// A start-up that failed is exactly the crash that must reach the log.
    /// </remarks>
    private void ComposeAndHandOver(
        IClassicDesktopStyleApplicationLifetime desktop,
        SplashWindow splash,
        Stopwatch splashShown,
        FirstRunState firstRun,
        ConfigLoadResult<AppSettings> settings,
        ConfigLoadResult<WorkspacesConfig> workspaces,
        ConfigLoadResult<SessionState> session)
    {
        bool handOverScheduled = false;

        try
        {
            // A desktop app must not depend on the working directory, so configuration is read beside
            // the executable rather than from wherever the process happened to be launched.
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                ContentRootPath = AppContext.BaseDirectory,
            });

            builder.Services.AddSingleton<Application>(this);
            builder.Services.AddFirstRun(firstRun);
            builder.Services.AddAppSettings(settings);
            builder.Services.AddWorkspaces(workspaces);
            builder.Services.AddSession(session);
            builder.Services.AddStartupNotice(StartupNotice.Combine(
                SettingsStartupNotice.For(settings),
                StartupNotice.Combine(
                    WorkspacesStartupNotice.For(workspaces),
                    SessionStartupNotice.For(session))));
            builder.Services.AddAppLogging(AppPaths.LogFile, settings.Config.Value.DebugLogging);
            builder.Services.AddEnigmaServices();
            builder.Services.AddShell();

            // Started, never run: the Avalonia lifetime owns the message loop; the host only supplies
            // configuration, logging and services.
            IHost host = builder.Build();
            host.Start();

            IServiceProvider services = host.Services;
            ILogger<App> logger = services.GetRequiredService<ILogger<App>>();

            // First, so that everything below is already covered — including the window construction.
            services.GetRequiredService<CrashReporter>().Install();

            if (settings.Failure is not null)
            {
                logger.LogWarning(
                    settings.Failure,
                    "The settings file could not be read; running on defaults. Quarantined to: {QuarantinedFile}. Changes will be discarded: {PersistenceDisabled}.",
                    settings.QuarantinedFilePath ?? "(not moved)",
                    settings.PersistenceDisabled);
            }

            if (workspaces.Failure is not null)
            {
                logger.LogWarning(
                    workspaces.Failure,
                    "The workspaces file could not be read; running with no workspaces. Quarantined to: {QuarantinedFile}. Changes will be discarded: {PersistenceDisabled}.",
                    workspaces.QuarantinedFilePath ?? "(not moved)",
                    workspaces.PersistenceDisabled);
            }

            if (session.Failure is not null)
            {
                logger.LogWarning(
                    session.Failure,
                    "The session file could not be read; starting as a first run would. Quarantined to: {QuarantinedFile}. Changes will be discarded: {PersistenceDisabled}.",
                    session.QuarantinedFilePath ?? "(not moved)",
                    session.PersistenceDisabled);
            }

            MainWindow mainWindow = services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = services.GetRequiredService<MainWindowViewModel>();

            // All five run before the window is shown: an Enigma service whose host is still
            // unregistered throws InvalidOperationException the moment anything asks it for a
            // dialog, an overlay, a notification or a file picker.
            services.GetRequiredService<IContentDialogService>().RegisterHost(mainWindow.HostDialog);
            services.GetRequiredService<IOverlayService>().RegisterHost(mainWindow.HostOverlay);
            services.GetRequiredService<IInfoBarService>().RegisterHost(mainWindow.HostInfoBar);
            services.GetRequiredService<IFileDialogService>().SetStorageProvider(mainWindow.StorageProvider);
            services.GetRequiredService<IFolderDialogService>().SetStorageProvider(mainWindow.StorageProvider);

            // Assigned, but not shown yet: the lifetime shows its MainWindow on its way into the
            // message loop, which it has already entered by the time this job runs. Showing it is
            // the hand-over below.
            desktop.MainWindow = mainWindow;
            logger.LogInformation("{DisplayName} started.", AppInfo.DisplayName);

            desktop.Exit += (_, _) =>
            {
                logger.LogInformation("{DisplayName} is shutting down.", AppInfo.DisplayName);

                // Both resolved before the sequence starts. If it overruns, the work walked away from
                // is the work that disposes the container, so asking that container for a service
                // afterwards would be a race against its own disposal.
                ISessionService sessionService = services.GetRequiredService<ISessionService>();
                IFlushableLoggerHandle loggerHandle = services.GetRequiredService<IFlushableLoggerHandle>();

                // Through AppShutdown rather than inline, and that is the whole of BUG-7AB2 PHASE03:
                // this handler cannot await, and blocking on an await that captured this thread's
                // context is what used to leave the process running for ever after the window had
                // gone. See AppShutdown for the mechanism.
                bool finished = AppShutdown.Run(
                    async cancellationToken =>
                    {
                        // The window recorded its own last placement on the way out, half a second
                        // short of the debounce it would otherwise have waited for, so the session is
                        // written now rather than left to a timer that will never fire.
                        await sessionService.FlushAsync(cancellationToken).ConfigureAwait(false);

                        // Disposing the container disposes the NLog handle it owns, which flushes the
                        // buffered entries and shuts the log factory down. Nothing else has to flush
                        // on this path — unless this line is never reached, which is what the branch
                        // below is for.
                        await host.StopAsync(cancellationToken).ConfigureAwait(false);
                        host.Dispose();
                    },
                    AppShutdown.DefaultTimeout);

                if (!finished)
                {
                    logger.LogWarning(
                        "Shutting down did not finish within {Timeout}; the rest of it was abandoned and the process is exiting anyway.",
                        AppShutdown.DefaultTimeout);

                    // The host.Dispose() that normally flushes NLog is part of the work just
                    // abandoned, so without this the warning above would never reach the file — and a
                    // hang that leaves no record is the one nobody can diagnose.
                    loggerHandle.Flush();
                }
            };

            ScheduleHandOver(mainWindow, splash, splashShown);
            handOverScheduled = true;
        }
        finally
        {
            // Only when the hand-over never got scheduled — that is, when composition threw. The
            // splash is the only window on screen at that point, so closing it ends the process, which
            // is the right outcome for an application that failed to start: the exception is on its
            // way to the crash handler, and the alternative is a splash screen nothing will ever
            // replace.
            if (!handOverScheduled)
            {
                splash.Close();
            }
        }
    }

    /// <summary>
    /// Holds the splash for the remainder of its minimum display, then swaps the windows.
    /// </summary>
    /// <param name="mainWindow">The window that replaces the splash.</param>
    /// <param name="splash">The splash to close once it has.</param>
    /// <param name="splashShown">Running since the splash was shown.</param>
    /// <remarks>
    /// A floor, not a delay: composition on a cold machine takes longer than the minimum on its own,
    /// and that start hands over immediately. Only a start fast enough to make the splash flash waits
    /// at all.
    /// </remarks>
    private static void ScheduleHandOver(Window mainWindow, Window splash, Stopwatch splashShown)
    {
        TimeSpan remaining = SplashTiming.RemainingDelay(splashShown.Elapsed, SplashTiming.MinimumDisplay);

        if (remaining <= TimeSpan.Zero)
        {
            HandOver(mainWindow, splash);
            return;
        }

        // A one-shot dispatcher timer rather than an awaited delay, for the reason given on
        // ComposeAndHandOver: the callback runs as a dispatcher job, so what it throws reaches the
        // crash handler instead of a task nobody is holding.
        _ = DispatcherTimer.RunOnce(() => HandOver(mainWindow, splash), remaining);
    }

    /// <summary>
    /// Shows the main window and closes the splash, in that order.
    /// </summary>
    /// <param name="mainWindow">The window to show.</param>
    /// <param name="splash">The window to close.</param>
    /// <remarks>
    /// The order is not cosmetic. <see cref="ShutdownMode.OnLastWindowClose"/> is the default, so a
    /// splash closed while it is the only window on screen shuts the application down — and the
    /// <c>finally</c> keeps that true of the failure path as well: if showing the main window throws,
    /// the splash still goes away rather than outliving the start-up it was covering.
    /// </remarks>
    private static void HandOver(Window mainWindow, Window splash)
    {
        try
        {
            mainWindow.Show();
        }
        finally
        {
            splash.Close();
        }
    }
}
