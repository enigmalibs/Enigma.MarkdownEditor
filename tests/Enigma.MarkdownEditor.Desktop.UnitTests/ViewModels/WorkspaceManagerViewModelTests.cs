using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.ViewModels;

/// <summary>
/// The manage-workspaces dialog, over a real workspaces file in a temp folder and a folder picker that
/// answers without a window. The persistence is deliberately real: the dialog's whole contract is that
/// every edit is written as it is made, so a fake service would test the wrong half.
/// </summary>
public sealed class WorkspaceManagerViewModelTests : IDisposable
{
    private readonly string _root;
    private readonly string _workspacesFile;
    private readonly FakeFolderPicker _folderPicker = new();

    public WorkspaceManagerViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _workspacesFile = Path.Combine(_root, AppPaths.WorkspacesFileName);
    }

    [Fact]
    public void WithNoWorkspaces_ThereIsNothingToSelectAndNothingToEdit()
    {
        WorkspaceManagerViewModel viewModel = Create(Service());

        Assert.Empty(viewModel.Workspaces);
        Assert.False(viewModel.HasWorkspaces);
        Assert.False(viewModel.HasSelection);
        Assert.Empty(viewModel.Roots);
        Assert.False(viewModel.CanRename);
    }

    [Fact]
    public async Task AddingAWorkspace_NamesItAfterTheFolderAndSelectsIt()
    {
        WorkspaceManagerViewModel viewModel = Create(Service());
        _folderPicker.Picked = ExistingDirectory("Documents");

        await viewModel.AddWorkspaceCommand.ExecuteAsync(null);

        Assert.Equal("Documents", Assert.Single(viewModel.Workspaces).Name);
        Assert.Equal("Documents", viewModel.SelectedWorkspace?.Name);
        Assert.Equal("Documents", viewModel.EditedName);
        Assert.Equal(_folderPicker.Picked, Assert.Single(viewModel.Roots).Path);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task AddingAWorkspaceWithAFolderNameAlreadyUsed_NumbersItInsteadOfFailing()
    {
        WorkspaceManagerViewModel viewModel = Create(Service());
        _folderPicker.Picked = ExistingDirectory("Documents");
        await viewModel.AddWorkspaceCommand.ExecuteAsync(null);

        _folderPicker.Picked = ExistingDirectory("Other/Documents");
        await viewModel.AddWorkspaceCommand.ExecuteAsync(null);

        Assert.Equal(["Documents", "Documents (2)"], viewModel.Workspaces.Select(item => item.Name));
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task CancellingTheFolderPicker_CreatesNothing()
    {
        WorkspaceManagerViewModel viewModel = Create(Service());
        _folderPicker.Picked = null;

        await viewModel.AddWorkspaceCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.Workspaces);
        Assert.Equal(1, _folderPicker.PickCount);
    }

    [Fact]
    public async Task AFolderPickerThatFails_IsReportedRatherThanSwallowed()
    {
        FakeFolderPicker broken = new() { Failure = new InvalidOperationException("Storage provider is not set") };
        WorkspaceManagerViewModel viewModel = Create(Service(), broken);

        await viewModel.AddWorkspaceCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Contains("folder picker", viewModel.ErrorMessage);
    }

    [Fact]
    public async Task RenamingAWorkspace_AppliesAndPersistsIt()
    {
        WorkspaceManagerViewModel viewModel = await WithOneWorkspaceAsync();

        viewModel.EditedName = "Notes";
        Assert.True(viewModel.CanRename);

        await viewModel.RenameWorkspaceCommand.ExecuteAsync(null);

        Assert.Equal("Notes", viewModel.SelectedWorkspace?.Name);
        Assert.Equal("Notes", Service().Workspaces[0].Name);
        Assert.False(viewModel.CanRename);
    }

    [Fact]
    public async Task ANameAlreadyTaken_IsReportedAsItIsTypedAndTheButtonStaysOff()
    {
        WorkspaceManagerViewModel viewModel = await WithTwoWorkspacesAsync();
        viewModel.SelectedWorkspace = viewModel.Workspaces[1];

        viewModel.EditedName = "Documents";

        Assert.True(viewModel.HasNameError);
        Assert.Contains("already a workspace", viewModel.NameError);
        Assert.False(viewModel.CanRename);
    }

    [Fact]
    public async Task ANameThatIsTheWorkspacesOwn_IsNotAnError()
    {
        WorkspaceManagerViewModel viewModel = await WithOneWorkspaceAsync();

        viewModel.EditedName = "Documents";

        Assert.False(viewModel.HasNameError);

        // Nothing to apply either: the rename button is for changes, not for re-typing.
        Assert.False(viewModel.CanRename);
    }

    [Fact]
    public async Task RemovingAWorkspace_AsksFirstAndOnlyThenRemoves()
    {
        WorkspaceManagerViewModel viewModel = await WithTwoWorkspacesAsync();
        viewModel.SelectedWorkspace = viewModel.Workspaces[1];

        viewModel.BeginRemoveWorkspaceCommand.Execute(null);

        Assert.True(viewModel.IsRemoveConfirmationVisible);
        Assert.Contains("Notes", viewModel.RemoveConfirmationText);
        Assert.Equal(2, viewModel.Workspaces.Count);

        await viewModel.RemoveWorkspaceCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsRemoveConfirmationVisible);
        Assert.Equal("Documents", Assert.Single(viewModel.Workspaces).Name);
        Assert.Single(Service().Workspaces);
    }

    [Fact]
    public async Task CancellingTheRemoval_KeepsTheWorkspace()
    {
        WorkspaceManagerViewModel viewModel = await WithOneWorkspaceAsync();
        viewModel.BeginRemoveWorkspaceCommand.Execute(null);

        viewModel.CancelRemoveWorkspaceCommand.Execute(null);

        Assert.False(viewModel.IsRemoveConfirmationVisible);
        Assert.Single(viewModel.Workspaces);
    }

    [Fact]
    public async Task ChangingTheSelection_DropsAPendingConfirmation()
    {
        WorkspaceManagerViewModel viewModel = await WithTwoWorkspacesAsync();
        viewModel.SelectedWorkspace = viewModel.Workspaces[0];
        viewModel.BeginRemoveWorkspaceCommand.Execute(null);

        viewModel.SelectedWorkspace = viewModel.Workspaces[1];

        // Otherwise the bar would still be asking about the workspace that is no longer selected.
        Assert.False(viewModel.IsRemoveConfirmationVisible);
    }

    [Fact]
    public async Task AddingAFolder_AppendsItToTheSelectedWorkspace()
    {
        WorkspaceManagerViewModel viewModel = await WithOneWorkspaceAsync();
        _folderPicker.Picked = ExistingDirectory("Extra");

        await viewModel.AddRootCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.Roots.Count);
        Assert.Equal(_folderPicker.Picked, viewModel.Roots[1].Path);
        Assert.Equal(2, Service().Workspaces[0].RootDirectories.Count);
    }

    [Fact]
    public async Task AddingAFolderTheWorkspaceAlreadyHas_IsReportedInline()
    {
        WorkspaceManagerViewModel viewModel = await WithOneWorkspaceAsync();
        _folderPicker.Picked = viewModel.Roots[0].Path;

        await viewModel.AddRootCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasError);
        Assert.Contains("already a folder", viewModel.ErrorMessage);
        Assert.Single(viewModel.Roots);
    }

    [Fact]
    public async Task TheLastFolderCannotBeRemoved()
    {
        WorkspaceManagerViewModel viewModel = await WithOneWorkspaceAsync();
        viewModel.SelectedRoot = viewModel.Roots[0];

        Assert.False(viewModel.CanRemoveRoot);
    }

    [Fact]
    public async Task RemovingAFolder_LeavesTheOthers()
    {
        WorkspaceManagerViewModel viewModel = await WithOneWorkspaceAsync();
        _folderPicker.Picked = ExistingDirectory("Extra");
        await viewModel.AddRootCommand.ExecuteAsync(null);
        viewModel.SelectedRoot = viewModel.Roots[1];

        Assert.True(viewModel.CanRemoveRoot);
        await viewModel.RemoveRootCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Roots);
        Assert.Single(Service().Workspaces[0].RootDirectories);
    }

    [Fact]
    public async Task ReorderingFolders_MovesTheSelectedOneAndKeepsItSelected()
    {
        WorkspaceManagerViewModel viewModel = await WithOneWorkspaceAsync();
        _folderPicker.Picked = ExistingDirectory("Extra");
        await viewModel.AddRootCommand.ExecuteAsync(null);

        viewModel.SelectedRoot = viewModel.Roots[1];
        Assert.True(viewModel.CanMoveRootUp);
        Assert.False(viewModel.CanMoveRootDown);

        await viewModel.MoveRootUpCommand.ExecuteAsync(null);

        Assert.Equal(_folderPicker.Picked, viewModel.Roots[0].Path);
        Assert.Equal(_folderPicker.Picked, viewModel.SelectedRoot?.Path);
        Assert.Equal(_folderPicker.Picked, Service().Workspaces[0].RootDirectories[0]);
    }

    [Fact]
    public async Task AFolderThatIsNotThere_IsShownAsMissingRatherThanDropped()
    {
        WorkspaceManagerViewModel viewModel = Create(Service());
        _folderPicker.Picked = ExistingDirectory("Documents");
        await viewModel.AddWorkspaceCommand.ExecuteAsync(null);

        Directory.Delete(_folderPicker.Picked);
        viewModel = Create(Service());

        Assert.True(viewModel.Roots[0].IsMissing);
        Assert.True(viewModel.Workspaces[0].HasMissingRoots);
        Assert.Contains("1 missing", viewModel.Workspaces[0].Summary);
    }

    [Fact]
    public void EveryDependency_IsRequired()
    {
        WorkspaceService service = Service();
        NullLogger<WorkspaceManagerViewModel> logger = NullLogger<WorkspaceManagerViewModel>.Instance;

        Assert.Throws<ArgumentNullException>(() => new WorkspaceManagerViewModel(null!, _folderPicker, logger));
        Assert.Throws<ArgumentNullException>(() => new WorkspaceManagerViewModel(service, null!, logger));
        Assert.Throws<ArgumentNullException>(() => new WorkspaceManagerViewModel(service, _folderPicker, null!));
    }

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

    private WorkspaceService Service() => new(WorkspacesLoader.Load(_workspacesFile).Config);

    private WorkspaceManagerViewModel Create(WorkspaceService service, FakeFolderPicker? folderPicker = null)
        => new(service, folderPicker ?? _folderPicker, NullLogger<WorkspaceManagerViewModel>.Instance);

    private string ExistingDirectory(string name)
    {
        string path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);

        return path;
    }

    private async Task<WorkspaceManagerViewModel> WithOneWorkspaceAsync()
    {
        WorkspaceManagerViewModel viewModel = Create(Service());
        _folderPicker.Picked = ExistingDirectory("Documents");
        await viewModel.AddWorkspaceCommand.ExecuteAsync(null);

        return viewModel;
    }

    private async Task<WorkspaceManagerViewModel> WithTwoWorkspacesAsync()
    {
        WorkspaceManagerViewModel viewModel = await WithOneWorkspaceAsync();
        _folderPicker.Picked = ExistingDirectory("Notes");
        await viewModel.AddWorkspaceCommand.ExecuteAsync(null);

        return viewModel;
    }
}
