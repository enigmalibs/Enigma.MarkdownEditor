using System;
using Avalonia.Controls;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Logging.Abstractions;
using Enigma.Logging.NLog;
using Enigma.MarkdownEditor.Core;
using Enigma.MarkdownEditor.Core.Configuration;
using Enigma.MarkdownEditor.Core.Documents;
using Enigma.MarkdownEditor.Core.Files;
using Enigma.MarkdownEditor.Core.Session;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Enigma.MarkdownEditor.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NLog.Targets;

namespace Enigma.MarkdownEditor.Desktop.Extensions;

/// <summary>
/// Container registrations for the application's own composition root.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// How many days of archived log files are kept before NLog deletes them.
    /// </summary>
    public const int LogRetentionDays = 14;

    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers the six Enigma.Avalonia.Desktop services. The package ships no registration
        /// extension of its own, so this is ours to own. Three of them additionally need a host
        /// control and two the window's storage provider, wired once in
        /// <see cref="App.OnFrameworkInitializationCompleted"/>.
        /// </summary>
        public void AddEnigmaServices()
        {
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IContentDialogService, ContentDialogService>();
            services.AddSingleton<IOverlayService, OverlayService>();
            services.AddSingleton<IInfoBarService, InfoBarService>();
            services.AddSingleton<IFileDialogService, FileDialogService>();
            services.AddSingleton<IFolderDialogService, FolderDialogService>();
        }

        /// <summary>
        /// Registers the already-loaded settings.
        /// </summary>
        /// <param name="settings">What <see cref="SettingsLoader.Load(string)"/> returned.</param>
        /// <remarks>
        /// The configuration is registered as the instance the loader produced rather than through
        /// Enigma.Configuration's own <c>AddEnigmaConfiguration</c>: the settings decide how the rest
        /// of the container is built — the log level, for one — so they have to be read before there
        /// is a container to read them from. One instance, read once, is also what keeps the settings
        /// window mutating the very object every other consumer injected.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
        public void AddAppSettings(ConfigLoadResult<AppSettings> settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            services.AddSingleton(settings.Config);
        }

        /// <summary>
        /// Registers the already-loaded workspaces file and the service that owns the list.
        /// </summary>
        /// <param name="workspaces">What <see cref="WorkspacesLoader.Load(string)"/> returned.</param>
        /// <remarks>
        /// Loaded eagerly for the same reason the settings are: a configuration whose load failed
        /// caches nothing, so a broken file has to be recovered while there is still somewhere for the
        /// failure to go. The service is singleton because the list is — one file, one truth, and the
        /// switcher and the sidebar both watch its events.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="workspaces"/> is <see langword="null"/>.</exception>
        public void AddWorkspaces(ConfigLoadResult<WorkspacesConfig> workspaces)
        {
            ArgumentNullException.ThrowIfNull(workspaces);

            services.AddSingleton(workspaces.Config);
            services.AddSingleton<IWorkspaceService, WorkspaceService>();
        }

        /// <summary>
        /// Registers the already-loaded session file and the service that keeps it current.
        /// </summary>
        /// <param name="session">What <see cref="SessionLoader.Load(string)"/> returned.</param>
        /// <remarks>
        /// Loaded eagerly like the other two files, and for the extra reason that this one decides
        /// where the window opens: the placement has to be applied before the window is shown, or the
        /// user watches it jump. The service is singleton because the file is — the shell, the window
        /// and the editor pane all report into one session.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="session"/> is <see langword="null"/>.</exception>
        public void AddSession(ConfigLoadResult<SessionState> session)
        {
            ArgumentNullException.ThrowIfNull(session);

            services.AddSingleton(session.Config);
            services.AddSingleton<ISessionService, SessionService>();
        }

        /// <summary>
        /// Registers what composition needs to tell the user once there is a window to say it in.
        /// </summary>
        /// <param name="notice">The notice, or <see cref="StartupNotice.None"/>.</param>
        /// <remarks>
        /// Separate from the two <c>Add…</c> methods above because it spans them: two files can fail
        /// at once, and the shell shows one message.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="notice"/> is <see langword="null"/>.</exception>
        public void AddStartupNotice(StartupNotice notice)
        {
            ArgumentNullException.ThrowIfNull(notice);

            services.AddSingleton(notice);
        }

        /// <summary>
        /// Registers whether this start is the very first one.
        /// </summary>
        /// <param name="firstRun">What <see cref="FirstRunState.Detect(string)"/> answered.</param>
        /// <remarks>
        /// Separate from <see cref="AddAppSettings(ConfigLoadResult{AppSettings})"/> although it is a
        /// fact about the same file, because it has to be established <em>before</em> that load: the
        /// loader recovers an unreadable file by moving it aside, and the question here is exactly
        /// "was the file there".
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="firstRun"/> is <see langword="null"/>.</exception>
        public void AddFirstRun(FirstRunState firstRun)
        {
            ArgumentNullException.ThrowIfNull(firstRun);

            services.AddSingleton(firstRun);
        }

        /// <summary>
        /// Registers NLog-backed <c>ILogger&lt;T&gt;</c> over a rolling file, plus the crash handlers
        /// that flush it.
        /// </summary>
        /// <param name="logFilePath">Absolute path of the current log file (see <see cref="AppPaths.LogFile"/>).</param>
        /// <param name="debugLogging">
        /// <see langword="true"/> to record <c>Debug</c> entries as well. Fixed for the process:
        /// NLog's level rules are built with the provider, so a change to the setting applies at the
        /// next start.
        /// </param>
        /// <exception cref="ArgumentException"><paramref name="logFilePath"/> is empty or white-space.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="logFilePath"/> is <see langword="null"/>.</exception>
        public void AddAppLogging(string logFilePath, bool debugLogging)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(logFilePath);

            LogLevel minimumLevel = debugLogging ? LogLevel.Debug : LogLevel.Information;

            services.AddLogging(logging =>
            {
                // The generic host installs Console, Debug and EventSource providers. A windowed app
                // has no console to write to, and every one of them is a provider the NLog file
                // target would then be competing with.
                logging.ClearProviders();

                // Both gates have to agree: this one is Microsoft.Extensions.Logging's filter, which
                // defaults to Information and would otherwise drop Debug entries before NLog ever
                // sees them.
                logging.SetMinimumLevel(minimumLevel);
            });

            services.AddNLogLoggerBuilder(
                AppInfo.AppDataFolderName,
                builder => builder.AddFileTarget(
                    "file",
                    target =>
                    {
                        target.Layout = LoggerProviderBuilder.DefaultLayout;
                        target.FileName = logFilePath;

                        // One file per day, two weeks of history. A desktop editor's log is read when
                        // something went wrong yesterday, not last quarter.
                        target.ArchiveEvery = FileArchivePeriod.Day;
                        target.MaxArchiveDays = LogRetentionDays;
                    },
                    minLevel: minimumLevel));

            services.AddSingleton(provider => new CrashReporter(
                provider.GetRequiredService<ILogger<CrashReporter>>(),
                provider.GetRequiredService<IFlushableLoggerHandle>(),
                provider.GetRequiredService<IContentDialogService>(),
                logFilePath));
        }

        /// <summary>
        /// Registers the application shell: the main window and its ViewModel, the settings ViewModel
        /// and the services the shell drives.
        /// </summary>
        /// <remarks>
        /// The shell is singleton — this app has exactly one main window for its whole lifetime, so a
        /// transient registration would only make it possible to build a second one by accident. The
        /// settings ViewModel is the opposite: it reads the settings as they are when it is built, so
        /// it is transient and built fresh on each visit. Its view is not registered at all — the
        /// dialog service news it up, the way every other dialog view is built.
        /// </remarks>
        public void AddShell()
        {
            services.AddSingleton<MainWindow>();
            services.AddSingleton<MainWindowViewModel>();

            services.AddTransient<SettingsViewModel>();

            // Singleton like the shell it lives in: the switcher and the sidebar are two views of one
            // state, and it subscribes to the workspace service for the life of the application.
            services.AddSingleton<WorkspacesViewModel>();

            // The tree and the editor pane it opens into are the sidebar and the middle pane
            // themselves — one of each, for as long as the window exists. Both own file watchers, so a
            // second instance would mean a second set of them over the same files.
            services.AddSingleton<FileTreeViewModel>();
            services.AddSingleton<DocumentManagerViewModel>();

            // The sidebar's other tab, and singleton for the same reason the tree is: it is one pane,
            // it subscribes to the workspace service for the life of the window, and a second instance
            // would be a second scan of the same folders answering nobody.
            services.AddSingleton<SearchViewModel>();

            // The right-hand pane, and singleton for the same reason: it subscribes to the editor
            // pane for the life of the window, and a second instance would be a second subscriber
            // rendering a document nobody can see.
            services.AddSingleton<MarkdownPreviewViewModel>();

            services.AddSingleton<IDirectoryWatcherFactory, DirectoryWatcherFactory>();
            services.AddSingleton<IDocumentFileWatcherFactory, DocumentFileWatcherFactory>();
            services.AddSingleton<IUiDispatcher, UiDispatcher>();
            services.AddSingleton<IDocumentOpener, DocumentOpener>();
            services.AddSingleton<IFileOperationPrompts, FileOperationPrompts>();
            services.AddSingleton<IDocumentPrompts, DocumentPrompts>();
            services.AddSingleton<IImagePasteService, ImagePasteService>();

            // Vim emulation. The session is singleton because it is one piece of state with three
            // readers — the editor pane that writes it, the status bar that shows it and the assist
            // policy that reads it — and the engine itself belongs to the editor control rather than
            // to the container.
            services.AddSingleton<IVimSession, VimSession>();

            // The seam FEATURE-3C44 left for this: with vim off it answers yes to everything, exactly
            // as the policy it replaces did, and with vim on only insert mode is typing.
            services.AddSingleton<IEditingAssistPolicy, VimEditingAssistPolicy>();

            // Singleton and disposable: it holds one live timer per changed document plus the
            // preview pane's own, and the container is what shuts them down when the application
            // exits. One instance for both because the keys are objects — an autosave and a preview
            // refresh scheduled against different keys cannot displace each other.
            services.AddSingleton<IIdleScheduler, IdleScheduler>();

            // Resolved once, from the running platform: which implementation is right cannot change
            // while the process is alive, and the Windows one holds no state worth a second instance.
            services.AddSingleton(TrashService.ForCurrentPlatform());

            // Behind a factory rather than by type, and lazily: building it enumerates every installed
            // family and measures two glyphs in each, which is several hundred typefaces on a typical
            // Windows machine. One instance for the whole process — the installed fonts do not change
            // while the editor is running, and both the editor pane and the settings picker ask the
            // same questions of them.
            services.AddSingleton<IFontCatalog>(_ => FontCatalog.ForCurrentSystem());

            // A factory, not the window: the window depends on the ViewModel that depends on the
            // opener that depends on this, and taking the instance here would close that cycle.
            services.AddSingleton<IFileLauncher>(provider => new FileLauncher(
                provider.GetRequiredService<MainWindow>));

            // The same shape, and for the same reason: the clipboard is reached through the window,
            // and the preview pane's ViewModel — which the window depends on — is what writes to it.
            services.AddSingleton<IClipboardWriter>(provider => new ClipboardWriter(
                provider.GetRequiredService<MainWindow>));

            // Transient, and resolved per showing: the dialog reads the workspace list when it is
            // built, so a reused instance would show a stale one.
            services.AddTransient<WorkspaceManagerViewModel>();

            services.AddSingleton<IFolderPicker, FolderPicker>();

            services.AddSingleton<IWorkspaceManagerDialogService>(provider => new WorkspaceManagerDialogService(
                provider.GetRequiredService<WorkspaceManagerViewModel>,
                provider.GetRequiredService<IContentDialogService>()));

            // Transient, like the manage-workspaces one, though this ViewModel is immutable and a
            // singleton would serve: building it is three property reads, and one lifetime across
            // both dialog ViewModels is worth more than the instance it saves.
            services.AddTransient<AboutViewModel>();

            services.AddSingleton<IAboutDialogService>(provider => new AboutDialogService(
                provider.GetRequiredService<AboutViewModel>,
                provider.GetRequiredService<IContentDialogService>()));

            // Transient, like the other two dialog ViewModels: it is built per showing — which is at
            // most once in the application's whole life — and it reads the settings as they are at
            // that moment.
            services.AddTransient<FirstRunViewModel>();

            services.AddSingleton<IFirstRunDialogService>(provider => new FirstRunDialogService(
                provider.GetRequiredService<FirstRunViewModel>,
                provider.GetRequiredService<IContentDialogService>(),
                provider.GetRequiredService<ILogger<FirstRunDialogService>>()));

            services.AddSingleton<IThemeService, ThemeService>();

            services.AddSingleton<ISettingsDialogService>(provider => new SettingsDialogService(
                provider.GetRequiredService<SettingsViewModel>,
                provider.GetRequiredService<IContentDialogService>()));

            // Only fallbacks: the composition root registers the real notice and the real first-run
            // answer before it gets here. They keep the shell resolvable from a container built
            // without either — which is how the composition-root tests build it.
            services.TryAddSingleton(StartupNotice.None);
            services.TryAddSingleton(FirstRunState.Returning);
        }
    }
}
