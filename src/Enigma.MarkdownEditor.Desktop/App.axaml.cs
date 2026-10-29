using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
/// The order of start-up is not free arrangement. The settings are read before the container is
/// built because composition depends on them; the crash handlers cover the window construction that
/// follows them; and every dialog host is registered before anything can ask for a dialog. The main
/// window is assigned to the lifetime rather than shown: the lifetime shows it on its way into the
/// message loop, which it enters once <see cref="OnFrameworkInitializationCompleted"/> has returned —
/// so the main window is the first thing on screen.
/// </remarks>
public partial class App : Application
{
    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        // The platform is up by now — touching the dispatcher before it is not safe — and nothing has
        // been dispatched yet, so every UI-thread failure from here on is caught.
        UnhandledExceptionHooks.InstallDispatcher();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // All of the start-up, guarded: it runs here rather than as a dispatcher job, so what it
            // throws would otherwise end the process from Main with nothing on screen.
            StartupGuard.Run(
                () => Start(desktop),
                window => desktop.MainWindow = window,
                UnhandledExceptionHooks.Router,
                AppPaths.LogFile);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Reads what the start-up depends on, applies the theme, and composes the application.
    /// </summary>
    /// <param name="desktop">The lifetime the main window is given to.</param>
    private void Start(IClassicDesktopStyleApplicationLifetime desktop)
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

        // The theme, before the window exists, so its first frame is painted in the right variant
        // rather than in the OS default and repainted a moment later. ThemeService owns the same
        // mapping and applies it whenever the setting changes; it cannot be used here because the
        // container that holds it is built below.
        RequestedThemeVariant = ThemeService.ToVariant(settings.Config.Value.Theme);

        Compose(desktop, firstRun, settings, workspaces, session);
    }

    /// <summary>
    /// Builds the application and hands the main window to the lifetime.
    /// </summary>
    /// <param name="desktop">The lifetime the main window is given to.</param>
    /// <param name="firstRun">Whether the settings file was there, decided before it could be moved.</param>
    /// <param name="settings">The settings load, reported here now that there is a logger.</param>
    /// <param name="workspaces">The workspaces load, reported for the same reason.</param>
    /// <param name="session">The session load, reported for the same reason.</param>
    private void Compose(
        IClassicDesktopStyleApplicationLifetime desktop,
        FirstRunState firstRun,
        ConfigLoadResult<AppSettings> settings,
        ConfigLoadResult<WorkspacesConfig> workspaces,
        ConfigLoadResult<SessionState> session)
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

        // First, so that everything below reaches the log — including the window construction — and
        // whatever was caught before the container existed reaches it now.
        CrashReporter crashReporter = services.GetRequiredService<CrashReporter>();
        UnhandledExceptionHooks.Router.Attach(crashReporter);

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

        // The error dialog goes on the same host, and has to wait for whatever question is open on it.
        crashReporter.AttachHost(mainWindow.HostDialog);

        // Assigned, not shown: the lifetime shows its MainWindow on its way into the message loop.
        desktop.MainWindow = mainWindow;
        logger.LogInformation("{DisplayName} started.", AppInfo.DisplayName);

        desktop.Exit += (_, _) =>
        {
            logger.LogInformation("{DisplayName} is shutting down.", AppInfo.DisplayName);

            // Before the sequence below disposes the container that owns the reporter: a late report
            // goes to Trace rather than through a disposed logger.
            UnhandledExceptionHooks.Router.Detach();

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
    }
}
