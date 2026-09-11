using System;
using System.IO;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Configuration;
using Enigma.Logging.Abstractions;
using Enigma.MarkdownEditor.Core.Configuration;
using Enigma.MarkdownEditor.Core.Documents;
using Enigma.MarkdownEditor.Core.Files;
using Enigma.MarkdownEditor.Core.Session;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.Extensions;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Enigma.MarkdownEditor.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Extensions;

/// <summary>
/// Composition-root tests. They assert the registrations rather than resolving everything: the six
/// Enigma services and <see cref="MainWindow"/> are Avalonia controls/services that need an
/// initialized Avalonia application, which a plain unit test does not have. A missing or wrongly
/// scoped registration is still caught here rather than at the app's first click.
/// </summary>
public sealed class ServiceCollectionExtensionsTests
{
    [Theory]
    [InlineData(typeof(INavigationService))]
    [InlineData(typeof(IContentDialogService))]
    [InlineData(typeof(IOverlayService))]
    [InlineData(typeof(IInfoBarService))]
    [InlineData(typeof(IFileDialogService))]
    [InlineData(typeof(IFolderDialogService))]
    public void AddEnigmaServices_RegistersTheServiceAsASingleton(Type serviceType)
    {
        ServiceCollection services = [];
        services.AddEnigmaServices();

        ServiceDescriptor descriptor = Assert.Single(services, s => s.ServiceType == serviceType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Theory]
    [InlineData(typeof(MainWindow), ServiceLifetime.Singleton)]
    [InlineData(typeof(MainWindowViewModel), ServiceLifetime.Singleton)]
    [InlineData(typeof(IThemeService), ServiceLifetime.Singleton)]
    [InlineData(typeof(ISettingsDialogService), ServiceLifetime.Singleton)]
    [InlineData(typeof(StartupNotice), ServiceLifetime.Singleton)]
    // It reads the settings as they are when it is built, so each visit to settings needs a fresh one.
    [InlineData(typeof(SettingsViewModel), ServiceLifetime.Transient)]
    [InlineData(typeof(WorkspacesViewModel), ServiceLifetime.Singleton)]
    [InlineData(typeof(IWorkspaceManagerDialogService), ServiceLifetime.Singleton)]
    // One tree, because it owns the file watchers: a second instance would mean a second set of them
    // over the same folders.
    [InlineData(typeof(FileTreeViewModel), ServiceLifetime.Singleton)]
    // One editor pane, for the same reason: it owns a watcher per open document.
    [InlineData(typeof(DocumentManagerViewModel), ServiceLifetime.Singleton)]
    // One preview pane: it subscribes to the editor pane for the life of the window.
    [InlineData(typeof(MarkdownPreviewViewModel), ServiceLifetime.Singleton)]
    // One search pane, for the same reason: it follows the workspace for the life of the window, and
    // a second would be a second scan of the same folders answering nobody.
    [InlineData(typeof(SearchViewModel), ServiceLifetime.Singleton)]
    [InlineData(typeof(IDirectoryWatcherFactory), ServiceLifetime.Singleton)]
    [InlineData(typeof(IDocumentFileWatcherFactory), ServiceLifetime.Singleton)]
    [InlineData(typeof(IUiDispatcher), ServiceLifetime.Singleton)]
    [InlineData(typeof(IDocumentOpener), ServiceLifetime.Singleton)]
    [InlineData(typeof(IFileLauncher), ServiceLifetime.Singleton)]
    [InlineData(typeof(IFileOperationPrompts), ServiceLifetime.Singleton)]
    [InlineData(typeof(IDocumentPrompts), ServiceLifetime.Singleton)]
    [InlineData(typeof(IImagePasteService), ServiceLifetime.Singleton)]
    // One font catalog: building it enumerates and measures every installed family, and the installed
    // fonts do not change while the editor runs.
    [InlineData(typeof(IFontCatalog), ServiceLifetime.Singleton)]
    // One piece of vim state for the whole window: the pane writes the mode, the status bar and the
    // assist policy read it.
    [InlineData(typeof(IVimSession), ServiceLifetime.Singleton)]
    // The seam vim emulation speaks through: with vim off it answers yes to everything, exactly as
    // the policy it replaced did.
    [InlineData(typeof(IEditingAssistPolicy), ServiceLifetime.Singleton)]
    // It holds one live timer per changed document, and the container is what shuts them down.
    [InlineData(typeof(IIdleScheduler), ServiceLifetime.Singleton)]
    // Which trash implementation is right cannot change while the process is alive.
    [InlineData(typeof(ITrashService), ServiceLifetime.Singleton)]
    // The manage dialog reads the workspace list when it is built, so it is built per showing.
    [InlineData(typeof(WorkspaceManagerViewModel), ServiceLifetime.Transient)]
    // The welcome dialog, built per showing like the other two dialog ViewModels — at most once in
    // the application's whole life.
    [InlineData(typeof(FirstRunViewModel), ServiceLifetime.Transient)]
    [InlineData(typeof(IFirstRunDialogService), ServiceLifetime.Singleton)]
    [InlineData(typeof(FirstRunState), ServiceLifetime.Singleton)]
    public void AddShell_RegistersTheShellTypeWithTheRightLifetime(Type serviceType, ServiceLifetime lifetime)
    {
        ServiceCollection services = [];
        services.AddShell();

        ServiceDescriptor descriptor = Assert.Single(services, s => s.ServiceType == serviceType);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    [Fact]
    public void AddShell_FallsBackToTheEmptyStartupNotice()
    {
        ServiceCollection services = [];
        services.AddShell();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(StartupNotice.None, provider.GetRequiredService<StartupNotice>());
    }

    [Fact]
    public void AddShell_FallsBackToNotBeingAFirstRun()
    {
        ServiceCollection services = [];
        services.AddShell();

        using ServiceProvider provider = services.BuildServiceProvider();

        // The safe fallback: a container built without the real answer must not onboard anybody.
        Assert.Same(FirstRunState.Returning, provider.GetRequiredService<FirstRunState>());
    }

    [Fact]
    public void AddFirstRun_RegistersTheAnswerTheCompositionRootDecided()
    {
        ServiceCollection services = [];
        services.AddFirstRun(FirstRunState.First);
        services.AddShell();

        using ServiceProvider provider = services.BuildServiceProvider();

        // Registered before AddShell's TryAdd fallback, and therefore the one that wins: the gate can
        // only be read before the settings loader runs, so composition is the only place that knows.
        Assert.Same(FirstRunState.First, provider.GetRequiredService<FirstRunState>());
    }

    [Fact]
    public void AddFirstRun_RequiresAnAnswer()
    {
        ServiceCollection services = [];

        Assert.Throws<ArgumentNullException>(() => services.AddFirstRun(null!));
    }

    [Fact]
    public void AddAppSettings_RegistersTheLoadedConfigurationItselfRatherThanRereadingTheFile()
    {
        using TempFolder folder = new();
        ConfigLoadResult<AppSettings> settings = SettingsLoader.Load(folder.SettingsFile);

        ServiceCollection services = [];
        services.AddAppSettings(settings);
        services.AddShell();

        using ServiceProvider provider = services.BuildServiceProvider();

        // The very object the settings window mutates has to be the one every other consumer holds.
        Assert.Same(settings.Config, provider.GetRequiredService<IConfig<AppSettings>>());
        Assert.Same(StartupNotice.None, provider.GetRequiredService<StartupNotice>());
    }

    [Fact]
    public void AddWorkspaces_RegistersTheLoadedConfigurationAndTheServiceOverIt()
    {
        using TempFolder folder = new();
        ConfigLoadResult<WorkspacesConfig> workspaces = WorkspacesLoader.Load(folder.WorkspacesFile);

        ServiceCollection services = [];
        services.AddWorkspaces(workspaces);

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(workspaces.Config, provider.GetRequiredService<IConfig<WorkspacesConfig>>());
        Assert.NotNull(provider.GetRequiredService<IWorkspaceService>());
        Assert.Same(
            provider.GetRequiredService<IWorkspaceService>(),
            provider.GetRequiredService<IWorkspaceService>());
    }

    [Fact]
    public void AddSession_RegistersTheLoadedConfigurationAndTheServiceOverIt()
    {
        using TempFolder folder = new();
        ConfigLoadResult<SessionState> session = SessionLoader.Load(folder.SessionFile);

        ServiceCollection services = [];
        services.AddLogging();
        services.AddWorkspaces(WorkspacesLoader.Load(folder.WorkspacesFile));
        services.AddSession(session);
        services.AddShell();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Same(session.Config, provider.GetRequiredService<IConfig<SessionState>>());
        Assert.Same(
            provider.GetRequiredService<ISessionService>(),
            provider.GetRequiredService<ISessionService>());
    }

    [Fact]
    public void AddStartupNotice_KeepsANoticeThatAddShellWouldOtherwiseDefault()
    {
        using TempFolder folder = new();
        File.WriteAllText(folder.SettingsFile, "{ broken");

        ServiceCollection services = [];
        services.AddStartupNotice(SettingsStartupNotice.For(SettingsLoader.Load(folder.SettingsFile)));
        services.AddShell();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.True(provider.GetRequiredService<StartupNotice>().HasNotice);
    }

    [Fact]
    public void EveryRegistrationRejectsANullArgument()
    {
        ServiceCollection services = [];

        Assert.Throws<ArgumentNullException>(() => services.AddAppSettings(null!));
        Assert.Throws<ArgumentNullException>(() => services.AddWorkspaces(null!));
        Assert.Throws<ArgumentNullException>(() => services.AddSession(null!));
        Assert.Throws<ArgumentNullException>(() => services.AddStartupNotice(null!));
    }

    [Fact]
    public void AddAppLogging_RoutesILoggerThroughTheFlushableNLogHandle()
    {
        using TempFolder folder = new();

        ServiceCollection services = [];
        services.AddEnigmaServices();
        services.AddAppLogging(folder.LogFile, debugLogging: false);

        using ServiceProvider provider = services.BuildServiceProvider();

        // The crash handlers depend on the handle being flushable — that is why NLog was picked over
        // Serilog, so it is worth an assertion rather than a comment.
        Assert.NotNull(provider.GetRequiredService<IFlushableLoggerHandle>());
        Assert.Same(
            provider.GetRequiredService<ILoggerHandle>(),
            provider.GetRequiredService<IFlushableLoggerHandle>());
        Assert.NotNull(provider.GetRequiredService<ILogger<ServiceCollectionExtensionsTests>>());
        Assert.NotNull(provider.GetRequiredService<CrashReporter>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddAppLogging_DropsTheHostsDefaultProvidersForTheFileTarget(bool debugLogging)
    {
        using TempFolder folder = new();

        ServiceCollection services = [];
        services.AddSingleton<ILoggerProvider>(NullLoggerProvider.Instance);
        services.AddEnigmaServices();
        services.AddAppLogging(folder.LogFile, debugLogging);

        // A windowed app has no console, and every stock provider is one the file target would be
        // competing with — so exactly one provider survives: NLog's.
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(ILoggerProvider));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddAppLogging_RejectsALogPathItCannotUse(string? logFilePath)
    {
        ServiceCollection services = [];

        Assert.ThrowsAny<ArgumentException>(() => services.AddAppLogging(logFilePath!, debugLogging: false));
    }

    [Fact]
    public void TheComposedContainer_ResolvesTheShellViewModel()
    {
        using TempFolder folder = new();

        ServiceCollection services = [];
        services.AddAppSettings(SettingsLoader.Load(folder.SettingsFile));
        services.AddWorkspaces(WorkspacesLoader.Load(folder.WorkspacesFile));
        services.AddSession(SessionLoader.Load(folder.SessionFile));
        services.AddAppLogging(folder.LogFile, debugLogging: false);
        services.AddEnigmaServices();
        services.AddShell();

        // The real ThemeService takes the running Avalonia Application, which the composition root
        // registers and a unit test has no way to supply. Overriding it after AddShell is the
        // container's own last-registration-wins rule, and leaves the rest of the graph as shipped.
        services.AddSingleton<IThemeService, FakeThemeService>();

        // Same reason: the real dispatcher queues onto Avalonia's UI thread, and the file tree posts
        // its first read the moment it is built.
        services.AddSingleton<IUiDispatcher, FakeUiDispatcher>();

        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<MainWindowViewModel>());
        Assert.NotNull(provider.GetRequiredService<SettingsViewModel>());

        // The launcher takes a factory rather than the window itself, so the graph resolves without
        // an Avalonia application — which is exactly the cycle that factory exists to break.
        Assert.NotNull(provider.GetRequiredService<IDocumentOpener>());
    }

    private sealed class TempFolder : IDisposable
    {
        private readonly string _root;

        public TempFolder()
        {
            _root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        public string SettingsFile => Path.Combine(_root, AppPaths.SettingsFileName);

        public string WorkspacesFile => Path.Combine(_root, AppPaths.WorkspacesFileName);

        public string SessionFile => Path.Combine(_root, AppPaths.SessionFileName);

        public string LogFile => Path.Combine(_root, AppPaths.LogsFolderName, AppPaths.LogFileName);

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // A leftover temp folder is not worth failing a green test over.
            }
        }
    }
}
