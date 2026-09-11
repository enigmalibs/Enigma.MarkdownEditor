using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Session;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Services;

/// <summary>
/// The service that keeps the session file current: that a change is scheduled rather than written,
/// that everything shares one write, that the file it lays down is the one the next start reads, and
/// that nothing it does can reach the user as an exception.
/// </summary>
public sealed class SessionServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _sessionFile;
    private readonly FakeIdleScheduler _idle = new();
    private readonly FakeWorkspaceService _workspaces = new();

    public SessionServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _sessionFile = Path.Combine(_root, AppPaths.SessionFileName);
    }

    [Fact]
    public void ARecordedChange_IsScheduledRatherThanWritten()
    {
        using SessionService service = Create();

        service.RecordShell(ViewMode.PreviewOnly, SidebarTab.Search, isSidebarVisible: false);

        Assert.True(_idle.IsScheduled(service));
        Assert.Equal(SessionService.WriteDelay, _idle.DelayFor(service));

        // A tab opened while a folder is being clicked through must not be a write per click.
        Assert.False(File.Exists(_sessionFile));
    }

    [Fact]
    public async Task TheScheduledWrite_PutsEverythingRecordedOnDisk()
    {
        using SessionService service = Create();

        service.RecordShell(ViewMode.EditorOnly, SidebarTab.Search, isSidebarVisible: false);
        service.RecordDocuments(["/tmp/one.md", "/tmp/two.md"], activeIndex: 1);
        service.RecordWindow(new WindowPlacement { X = 10, Y = 20, Width = 1280, Height = 800, IsMaximized = true });
        service.RecordPanes(new PaneLayout { SidebarWidth = 300, EditorWeight = 3, PreviewWeight = 2 });

        await _idle.FireAsync(service);

        SessionState reread = SessionLoader.Load(_sessionFile).Config.Value;

        Assert.Equal(ViewMode.EditorOnly, reread.ViewMode);
        Assert.Equal(SidebarTab.Search, reread.SidebarTab);
        Assert.False(reread.IsSidebarVisible);
        Assert.Equal(["/tmp/one.md", "/tmp/two.md"], reread.OpenDocuments);
        Assert.Equal(1, reread.ActiveDocumentIndex);
        Assert.Equal(1280, reread.Window?.Width);
        Assert.True(reread.Window?.IsMaximized);
        Assert.Equal(300, reread.Panes?.SidebarWidth);
    }

    [Fact]
    public void FourKindsOfChange_ShareOneTimer()
    {
        using SessionService service = Create();

        service.RecordShell(ViewMode.Split, SidebarTab.Explorer, isSidebarVisible: true);
        service.RecordDocuments(["/tmp/one.md"], activeIndex: 0);
        service.RecordWindow(new WindowPlacement { Width = 800, Height = 600 });
        service.RecordPanes(new PaneLayout { SidebarWidth = 280, EditorWeight = 1, PreviewWeight = 1 });

        // The file is written whole, so two pending writes of it would only be the same write twice.
        Assert.Equal(1, _idle.ScheduledCount);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(-4)]
    public void AnActiveTabTheListCannotHold_IsRecordedAsNoActiveTab(int activeIndex)
    {
        using SessionService service = Create();

        service.RecordDocuments(["/tmp/one.md"], activeIndex);

        // Or the next start would quarantine a file this build wrote itself.
        Assert.Equal(-1, service.State.ActiveDocumentIndex);
    }

    [Fact]
    public void EveryRecord_StampsTheWorkspaceThatIsOpen()
    {
        Workspace notes = _workspaces.Seed("Notes", active: true);
        using SessionService service = Create();

        service.RecordPanes(new PaneLayout { SidebarWidth = 280, EditorWeight = 1, PreviewWeight = 1 });

        Assert.Equal(notes.Id, service.State.ActiveWorkspaceId);
    }

    [Fact]
    public async Task OpeningAnotherWorkspace_IsItselfAChangeWorthWriting()
    {
        Workspace notes = _workspaces.Seed("Notes");
        using SessionService service = Create();

        // Nothing else the session tracks moved, so without this the stamp would still name the
        // workspace the editor started in.
        await _workspaces.ActivateAsync(notes.Id, TestContext.Current.CancellationToken);

        Assert.True(_idle.IsScheduled(service));
        Assert.Equal(notes.Id, service.State.ActiveWorkspaceId);
    }

    [Fact]
    public async Task Flush_WritesNowAndDropsThePendingTimer()
    {
        using SessionService service = Create();
        service.RecordShell(ViewMode.PreviewOnly, SidebarTab.Explorer, isSidebarVisible: true);

        await service.FlushAsync(TestContext.Current.CancellationToken);

        Assert.False(_idle.IsScheduled(service));
        Assert.Equal(ViewMode.PreviewOnly, SessionLoader.Load(_sessionFile).Config.Value.ViewMode);
    }

    [Fact]
    public async Task AFlushThatIsCancelled_StopsQuietlyRatherThanThrowingOnTheWayOut()
    {
        using SessionService service = Create();
        service.RecordShell(ViewMode.PreviewOnly, SidebarTab.Explorer, isSidebarVisible: true);

        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        // A cancelled token is what AppShutdown hands this when the closing sequence runs out of time.
        // An exception here would come out of the exit handler, which is a crash dialog over a session
        // file — the one thing this service promises never to do.
        await service.FlushAsync(cancelled.Token);

        Assert.False(_idle.IsScheduled(service));
    }

    [Fact]
    public async Task AWriteThatFails_IsLoggedRatherThanThrownAtWhoeverMovedTheSplitter()
    {
        using SessionService service = Create();

        // A state the validator refuses. Reached by hand here; in the application it would be a file
        // system that said no. Either way the caller is a property setter, and an exception there is
        // a crash dialog over a forgotten tab order.
        service.State.OpenDocuments = [];
        service.State.ActiveDocumentIndex = 3;

        service.RecordWindow(new WindowPlacement { Width = 800, Height = 600 });

        await _idle.FireAsync(service);
        await service.FlushAsync(TestContext.Current.CancellationToken);

        Assert.False(File.Exists(_sessionFile));
    }

    [Fact]
    public void ASessionFromAnotherWorkspace_HasItsTabsDropped()
    {
        _workspaces.Seed("Notes", active: true);
        using SessionService service = Create();

        service.State.ActiveWorkspaceId = Guid.NewGuid();
        service.State.OpenDocuments = [Note("kept.md")];
        service.State.ActiveDocumentIndex = 0;

        SessionDocuments documents = service.DocumentsToRestore();

        Assert.True(documents.IsForAnotherWorkspace);
        Assert.Empty(documents.Paths);
    }

    [Fact]
    public void ASessionForTheOpenWorkspace_KeepsTheNotesThatAreStillThere()
    {
        Workspace notes = _workspaces.Seed("Notes", active: true);
        using SessionService service = Create();

        string kept = Note("kept.md");
        service.State.ActiveWorkspaceId = notes.Id;
        service.State.OpenDocuments = [kept, Path.Combine(_root, "gone.md")];
        service.State.ActiveDocumentIndex = 0;

        SessionDocuments documents = service.DocumentsToRestore();

        Assert.False(documents.IsForAnotherWorkspace);
        Assert.Equal([kept], documents.Paths);
        Assert.Single(documents.Missing);
    }

    [Fact]
    public async Task ADisposedService_LetsGoOfTheWorkspaceServiceAndRecordsNothingMore()
    {
        Workspace notes = _workspaces.Seed("Notes");
        SessionService service = Create();

        service.Dispose();
        service.RecordShell(ViewMode.EditorOnly, SidebarTab.Search, isSidebarVisible: false);

        Assert.False(_idle.IsScheduled(service));

        // The container disposes this at shutdown; a handler left on a singleton workspace service
        // would keep it alive with it.
        await _workspaces.ActivateAsync(notes.Id, TestContext.Current.CancellationToken);

        Assert.False(_idle.IsScheduled(service));
    }

    [Fact]
    public void DisposingTwice_IsHarmless()
    {
        SessionService service = Create();

        service.Dispose();
        service.Dispose();
    }

    [Fact]
    public void ARecorderWithNothingToRecord_IsRejected()
    {
        using SessionService service = Create();

        Assert.Throws<ArgumentNullException>(() => service.RecordDocuments(null!, 0));
        Assert.Throws<ArgumentNullException>(() => service.RecordWindow(null!));
        Assert.Throws<ArgumentNullException>(() => service.RecordPanes(null!));
    }

    [Fact]
    public void EveryDependency_IsRequired()
    {
        IConfig<SessionState> config = SessionLoader.Load(_sessionFile).Config;
        NullLogger<SessionService> logger = NullLogger<SessionService>.Instance;

        Assert.Throws<ArgumentNullException>(() => new SessionService(null!, _workspaces, _idle, logger));
        Assert.Throws<ArgumentNullException>(() => new SessionService(config, null!, _idle, logger));
        Assert.Throws<ArgumentNullException>(() => new SessionService(config, _workspaces, null!, logger));
        Assert.Throws<ArgumentNullException>(() => new SessionService(config, _workspaces, _idle, null!));
    }

    public void Dispose()
    {
        _idle.Dispose();

        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A leftover temp folder is not worth failing a green test over.
        }
    }

    private SessionService Create()
        => new(
            SessionLoader.Load(_sessionFile).Config,
            _workspaces,
            _idle,
            NullLogger<SessionService>.Instance);

    private string Note(string name)
    {
        string path = Path.Combine(_root, name);
        File.WriteAllText(path, "# note");

        return path;
    }
}
