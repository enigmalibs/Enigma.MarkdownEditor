using System;
using System.Linq;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.ViewModels;

/// <summary>
/// The toolbar switcher and the sidebar. Both are views of the workspace service, so the tests are
/// mostly about one thing: whatever the service says changed, these two show without being told twice.
/// </summary>
public sealed class WorkspacesViewModelTests
{
    [Fact]
    public void WithNoWorkspaces_TheSidebarShowsItsEmptyState()
    {
        WorkspacesViewModel viewModel = Create(new FakeWorkspaceService());

        Assert.False(viewModel.HasWorkspaces);
        Assert.False(viewModel.HasActiveWorkspace);
        Assert.Equal("No workspace", viewModel.ActiveWorkspaceName);
        Assert.Equal("WORKSPACE", viewModel.SidebarHeader);
    }

    [Fact]
    public void WithAWorkspaceOpen_TheSwitcherAndSidebarShowIt()
    {
        FakeWorkspaceService service = new();
        service.Seed("Notes");
        service.Seed("Documents", active: true, "/tmp/a", "/tmp/b");

        WorkspacesViewModel viewModel = Create(service);

        Assert.Equal(2, viewModel.Items.Count);
        Assert.Equal("Documents", viewModel.ActiveWorkspaceName);
        Assert.Equal("DOCUMENTS", viewModel.SidebarHeader);
        Assert.True(viewModel.Items.Single(item => item.Name == "Documents").IsActive);
        Assert.False(viewModel.Items.Single(item => item.Name == "Notes").IsActive);
    }

    [Fact]
    public void AWorkspaceWhoseFolderIsGone_IsFlaggedRatherThanHidden()
    {
        FakeWorkspaceService service = new();
        service.Seed("Documents", active: true, "/tmp/definitely-not-there");

        WorkspacesViewModel viewModel = Create(service);

        Assert.True(viewModel.HasMissingRoots);
        Assert.Contains("not there right now", viewModel.MissingRootsWarning);
        Assert.True(viewModel.Items[0].HasMissingRoots);

        // The workspace is still open: a folder on a share is missing only while it is unplugged.
        Assert.True(viewModel.HasActiveWorkspace);
    }

    [Fact]
    public async Task TheActivateCommand_OpensTheWorkspaceAndTheListFollows()
    {
        FakeWorkspaceService service = new();
        service.Seed("Documents", active: true);
        Workspace notes = service.Seed("Notes");
        WorkspacesViewModel viewModel = Create(service);

        await viewModel.ActivateCommand.ExecuteAsync(viewModel.Items.Single(item => item.Id == notes.Id));

        Assert.Equal("Notes", viewModel.ActiveWorkspaceName);
        Assert.True(viewModel.Items.Single(item => item.Id == notes.Id).IsActive);
    }

    [Fact]
    public async Task AFailureOpeningAWorkspace_IsSurfacedRatherThanSwallowed()
    {
        FakeWorkspaceService service = new() { Failure = new UnauthorizedAccessException("read-only") };
        Workspace notes = service.Seed("Notes");
        FakeInfoBarService infoBar = new();
        WorkspacesViewModel viewModel = Create(service, infoBar: infoBar);

        // An AsyncRelayCommand parks a faulted task instead of rethrowing, so without the ViewModel's
        // own catch the switcher would just appear to do nothing.
        await viewModel.ActivateCommand.ExecuteAsync(viewModel.Items.Single(item => item.Id == notes.Id));

        Assert.Single(infoBar.Shown);
    }

    [Fact]
    public async Task TheManageCommand_OpensTheDialogOnce()
    {
        FakeWorkspaceManagerDialogService dialog = new();
        WorkspacesViewModel viewModel = Create(new FakeWorkspaceService(), dialog);

        await viewModel.ManageCommand.ExecuteAsync(null);

        Assert.Equal(1, dialog.ShowCount);
    }

    [Fact]
    public async Task AFailureOpeningTheDialog_IsSurfacedRatherThanSwallowed()
    {
        FakeInfoBarService infoBar = new();
        WorkspacesViewModel viewModel = Create(
            new FakeWorkspaceService(),
            new FakeWorkspaceManagerDialogService { Failure = new InvalidOperationException("no host") },
            infoBar);

        await viewModel.ManageCommand.ExecuteAsync(null);

        Assert.Single(infoBar.Shown);
    }

    [Fact]
    public async Task AWorkspaceCreatedElsewhere_AppearsWithoutBeingToldTwice()
    {
        FakeWorkspaceService service = new();
        WorkspacesViewModel viewModel = Create(service);

        // The manage dialog edits through the service, not through this ViewModel — the events are
        // what keep the switcher and the sidebar current while that dialog is open.
        await service.CreateAsync("Documents", "/tmp/documents", TestContext.Current.CancellationToken);

        Assert.True(viewModel.HasWorkspaces);
        Assert.True(viewModel.HasActiveWorkspace);
        Assert.Equal("Documents", viewModel.ActiveWorkspaceName);
    }

    [Fact]
    public void EveryDependency_IsRequired()
    {
        FakeWorkspaceService service = new();
        FakeWorkspaceManagerDialogService dialog = new();
        FakeInfoBarService infoBar = new();
        NullLogger<WorkspacesViewModel> logger = NullLogger<WorkspacesViewModel>.Instance;

        Assert.Throws<ArgumentNullException>(() => new WorkspacesViewModel(null!, dialog, infoBar, logger));
        Assert.Throws<ArgumentNullException>(() => new WorkspacesViewModel(service, null!, infoBar, logger));
        Assert.Throws<ArgumentNullException>(() => new WorkspacesViewModel(service, dialog, null!, logger));
        Assert.Throws<ArgumentNullException>(() => new WorkspacesViewModel(service, dialog, infoBar, null!));
    }

    private static WorkspacesViewModel Create(
        FakeWorkspaceService service,
        FakeWorkspaceManagerDialogService? dialog = null,
        FakeInfoBarService? infoBar = null)
        => new(
            service,
            dialog ?? new FakeWorkspaceManagerDialogService(),
            infoBar ?? new FakeInfoBarService(),
            NullLogger<WorkspacesViewModel>.Instance);
}
