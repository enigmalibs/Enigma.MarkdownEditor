using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Workspaces;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Workspaces;

/// <summary>
/// The workspace list, driven through a real file in a temp folder. The point of the design under test
/// is that a change is only real once it is on disk, so a fake configuration would test the wrong
/// thing: every test that says "and it survives a restart" reloads the service from the file.
/// </summary>
public sealed class WorkspaceServiceTests : IDisposable
{
    private readonly TempWorkspacesFolder _folder = new();

    [Fact]
    public void AFreshInstallation_HasNoWorkspacesAndNoneOpen()
    {
        WorkspaceService service = _folder.CreateService();

        Assert.Empty(service.Workspaces);
        Assert.Null(service.ActiveWorkspace);
    }

    [Fact]
    public async Task CreateAsync_AddsTheWorkspaceOpensItAndSurvivesARestart()
    {
        string documents = _folder.ExistingDirectory("Documents");
        WorkspaceService service = _folder.CreateService();

        Workspace created = await service.CreateAsync("Documents", documents, TestContext.Current.CancellationToken);

        Assert.Equal("Documents", created.Name);
        Assert.Equal([documents], created.RootDirectories);
        Assert.Same(created, service.ActiveWorkspace);

        WorkspaceService restarted = _folder.CreateService();
        Workspace reloaded = Assert.Single(restarted.Workspaces);

        Assert.Equal(created.Id, reloaded.Id);
        Assert.Equal("Documents", reloaded.Name);
        Assert.Equal([documents], reloaded.RootDirectories);
        Assert.Equal(created.Id, restarted.ActiveWorkspace?.Id);
    }

    [Fact]
    public async Task CreateAsync_StoresTheDirectoryNormalized()
    {
        string documents = _folder.ExistingDirectory("Documents");
        WorkspaceService service = _folder.CreateService();

        Workspace created = await service.CreateAsync(
            "Documents",
            documents + System.IO.Path.DirectorySeparatorChar,
            TestContext.Current.CancellationToken);

        // Otherwise the same folder added twice, once with a trailing separator, would count as two.
        Assert.Equal([documents], created.RootDirectories);
    }

    [Theory]
    [InlineData("documents")]
    [InlineData("DOCUMENTS")]
    [InlineData("  Documents  ")]
    public async Task CreateAsync_RejectsANameAlreadyTaken(string name)
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");

        WorkspaceValidationException failure = await Assert.ThrowsAsync<WorkspaceValidationException>(
            () => service.CreateAsync(name, _folder.ExistingDirectory("Other"), TestContext.Current.CancellationToken));

        Assert.Contains("already a workspace", failure.Message);
        Assert.Single(service.Workspaces);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_RejectsANameThatIsNotOne(string name)
    {
        WorkspaceService service = _folder.CreateService();

        await Assert.ThrowsAsync<WorkspaceValidationException>(
            () => service.CreateAsync(name, _folder.ExistingDirectory("Any"), TestContext.Current.CancellationToken));

        Assert.Empty(service.Workspaces);
    }

    [Fact]
    public async Task RenameAsync_ChangesTheNameAndKeepsTheIdentity()
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");
        Guid id = service.Workspaces[0].Id;

        await service.RenameAsync(id, "  Notes  ", TestContext.Current.CancellationToken);

        // Trimmed on the way in: a name with edge whitespace looks identical to one without.
        Assert.Equal("Notes", service.Workspaces[0].Name);
        Assert.Equal(id, service.Workspaces[0].Id);
        Assert.Equal("Notes", _folder.CreateService().Workspaces[0].Name);
    }

    [Fact]
    public async Task RenameAsync_AcceptsTheWorkspacesOwnName()
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");

        // The uniqueness rule must not fire against the workspace being renamed, or re-typing the same
        // name would be an error.
        await service.RenameAsync(service.Workspaces[0].Id, "Documents", TestContext.Current.CancellationToken);

        Assert.Equal("Documents", service.Workspaces[0].Name);
    }

    [Fact]
    public async Task RenameAsync_RejectsANameAnotherWorkspaceHas()
    {
        WorkspaceService service = await WithTwoWorkspacesAsync();

        await Assert.ThrowsAsync<WorkspaceValidationException>(
            () => service.RenameAsync(service.Workspaces[1].Id, "Documents", TestContext.Current.CancellationToken));

        Assert.Equal("Notes", service.Workspaces[1].Name);
    }

    [Fact]
    public async Task RenameAsync_RejectsAnUnknownWorkspace()
    {
        WorkspaceService service = _folder.CreateService();

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.RenameAsync(Guid.NewGuid(), "Anything", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_RemovesItAndOpensWhatIsLeft()
    {
        WorkspaceService service = await WithTwoWorkspacesAsync();
        Guid open = service.ActiveWorkspace!.Id;

        await service.DeleteAsync(open, TestContext.Current.CancellationToken);

        Workspace remaining = Assert.Single(service.Workspaces);
        Assert.Equal(remaining.Id, service.ActiveWorkspace?.Id);
        Assert.Equal(remaining.Id, _folder.CreateService().ActiveWorkspace?.Id);
    }

    [Fact]
    public async Task DeleteAsync_OfTheLastWorkspace_LeavesNothingOpen()
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");

        await service.DeleteAsync(service.Workspaces[0].Id, TestContext.Current.CancellationToken);

        Assert.Empty(service.Workspaces);
        Assert.Null(service.ActiveWorkspace);
        Assert.Null(_folder.CreateService().ActiveWorkspace);
    }

    [Fact]
    public async Task DeleteAsync_OfAWorkspaceThatIsNotOpen_LeavesTheOpenOneAlone()
    {
        WorkspaceService service = await WithTwoWorkspacesAsync();
        Guid open = service.ActiveWorkspace!.Id;
        Guid other = service.Workspaces.First(workspace => workspace.Id != open).Id;

        await service.DeleteAsync(other, TestContext.Current.CancellationToken);

        Assert.Equal(open, service.ActiveWorkspace?.Id);
    }

    [Fact]
    public async Task AddRootDirectoryAsync_AppendsItAndPersistsTheOrder()
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");
        string extra = _folder.ExistingDirectory("Extra");

        await service.AddRootDirectoryAsync(service.Workspaces[0].Id, extra, TestContext.Current.CancellationToken);

        Assert.Equal(2, service.Workspaces[0].RootDirectories.Count);
        Assert.Equal(extra, service.Workspaces[0].RootDirectories[1]);
        Assert.Equal(extra, _folder.CreateService().Workspaces[0].RootDirectories[1]);
    }

    [Fact]
    public async Task AddRootDirectoryAsync_RejectsAFolderTheWorkspaceAlreadyHas()
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");
        string existing = service.Workspaces[0].RootDirectories[0];

        WorkspaceValidationException failure = await Assert.ThrowsAsync<WorkspaceValidationException>(
            () => service.AddRootDirectoryAsync(service.Workspaces[0].Id, existing, TestContext.Current.CancellationToken));

        Assert.Contains("already a folder", failure.Message);
        Assert.Single(service.Workspaces[0].RootDirectories);
    }

    [Fact]
    public async Task AddRootDirectoryAsync_AcceptsAFolderThatIsNotThere()
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");
        string unplugged = _folder.MissingDirectory("OnAShare");

        // A root on a share or a removable drive is missing for as long as it is unplugged; dropping it
        // from the workspace would be worse than showing it greyed out.
        await service.AddRootDirectoryAsync(service.Workspaces[0].Id, unplugged, TestContext.Current.CancellationToken);

        Assert.Contains(unplugged, service.Workspaces[0].RootDirectories);
        Assert.Equal([unplugged], WorkspaceValidation.MissingRootDirectories(service.Workspaces[0]));
    }

    [Fact]
    public async Task RemoveRootDirectoryAsync_RefusesToEmptyAWorkspace()
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");
        string only = service.Workspaces[0].RootDirectories[0];

        WorkspaceValidationException failure = await Assert.ThrowsAsync<WorkspaceValidationException>(
            () => service.RemoveRootDirectoryAsync(service.Workspaces[0].Id, only, TestContext.Current.CancellationToken));

        Assert.Contains("without a folder", failure.Message);
        Assert.Single(service.Workspaces[0].RootDirectories);
    }

    [Fact]
    public async Task RemoveRootDirectoryAsync_RemovesOneOfSeveral()
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");
        string extra = _folder.ExistingDirectory("Extra");
        Guid id = service.Workspaces[0].Id;
        await service.AddRootDirectoryAsync(id, extra, TestContext.Current.CancellationToken);

        await service.RemoveRootDirectoryAsync(id, extra, TestContext.Current.CancellationToken);

        Assert.DoesNotContain(extra, service.Workspaces[0].RootDirectories);
        Assert.Single(_folder.CreateService().Workspaces[0].RootDirectories);
    }

    [Fact]
    public async Task RemoveRootDirectoryAsync_AcceptsThePathInAnEquivalentForm()
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");
        string extra = _folder.ExistingDirectory("Extra");
        Guid id = service.Workspaces[0].Id;
        await service.AddRootDirectoryAsync(id, extra, TestContext.Current.CancellationToken);

        await service.RemoveRootDirectoryAsync(
            id,
            extra + System.IO.Path.DirectorySeparatorChar,
            TestContext.Current.CancellationToken);

        Assert.Single(service.Workspaces[0].RootDirectories);
    }

    [Fact]
    public async Task MoveRootDirectoryAsync_ReordersAndPersists()
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");
        Guid id = service.Workspaces[0].Id;
        string first = service.Workspaces[0].RootDirectories[0];
        string second = _folder.ExistingDirectory("Second");
        string third = _folder.ExistingDirectory("Third");
        await service.AddRootDirectoryAsync(id, second, TestContext.Current.CancellationToken);
        await service.AddRootDirectoryAsync(id, third, TestContext.Current.CancellationToken);

        await service.MoveRootDirectoryAsync(id, 2, 0, TestContext.Current.CancellationToken);

        Assert.Equal([third, first, second], service.Workspaces[0].RootDirectories);
        Assert.Equal([third, first, second], _folder.CreateService().Workspaces[0].RootDirectories);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 5)]
    public async Task MoveRootDirectoryAsync_RejectsAnIndexOutsideTheList(int from, int to)
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.MoveRootDirectoryAsync(service.Workspaces[0].Id, from, to, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ActivateAsync_OpensTheWorkspaceAndItReopensAfterARestart()
    {
        WorkspaceService service = await WithTwoWorkspacesAsync();
        Guid documents = service.Workspaces[0].Id;

        await service.ActivateAsync(documents, TestContext.Current.CancellationToken);

        Assert.Equal(documents, service.ActiveWorkspace?.Id);
        Assert.Equal(documents, _folder.CreateService().ActiveWorkspace?.Id);
    }

    [Fact]
    public async Task ALastActiveIdentifierThatMatchesNothing_FallsBackToTheFirstWorkspace()
    {
        WorkspaceService service = await WithTwoWorkspacesAsync();
        Guid first = service.Workspaces[0].Id;

        // Only the reference is repointed — the workspaces themselves keep their identifiers, which is
        // exactly the state a hand-deleted workspace leaves behind.
        string json = Regex.Replace(
            _folder.ReadWorkspacesFile(),
            "\"LastActiveWorkspaceId\"\\s*:\\s*\"[^\"]+\"",
            $"\"LastActiveWorkspaceId\": \"{Guid.NewGuid()}\"");
        _folder.WriteWorkspacesFile(json);

        // Hand-editing the file must not leave the editor with an empty sidebar and no way back.
        Assert.Equal(first, _folder.CreateService().ActiveWorkspace?.Id);
    }

    [Fact]
    public async Task AWorkspaceWhoseFolderIsGone_StillOpens()
    {
        WorkspaceService service = _folder.CreateService();
        string documents = _folder.ExistingDirectory("Documents");
        await service.CreateAsync("Documents", documents, TestContext.Current.CancellationToken);

        System.IO.Directory.Delete(documents);

        WorkspaceService restarted = _folder.CreateService();

        Assert.Equal("Documents", restarted.ActiveWorkspace?.Name);
        Assert.Equal([documents], WorkspaceValidation.MissingRootDirectories(restarted.ActiveWorkspace!));
    }

    [Fact]
    public async Task AFailedWrite_PutsTheListBackToWhatTheFileHolds()
    {
        IConfig<WorkspacesConfig> config = _folder.LoadConfig();
        WorkspaceService service = new(config);
        Workspace created = await service.CreateAsync(
            "Documents",
            _folder.ExistingDirectory("Documents"),
            TestContext.Current.CancellationToken);

        // Break the object behind the service's back, so the write that follows fails validation. The
        // rename must not survive in memory a write the file never took.
        config.Value.Workspaces[0].RootDirectories.Add(config.Value.Workspaces[0].RootDirectories[0]);

        await Assert.ThrowsAsync<ConfigurationValidationException>(
            () => service.RenameAsync(created.Id, "Notes", TestContext.Current.CancellationToken));

        Assert.Equal("Documents", service.Workspaces[0].Name);
        Assert.Equal("Documents", _folder.CreateService().Workspaces[0].Name);
    }

    [Fact]
    public async Task Creating_RaisesBothTheListAndTheActiveWorkspaceEvents()
    {
        WorkspaceService service = _folder.CreateService();
        List<string> raised = [];
        service.WorkspacesChanged += (_, _) => raised.Add("list");
        service.ActiveWorkspaceChanged += (_, _) => raised.Add("active");

        await service.CreateAsync(
            "Documents",
            _folder.ExistingDirectory("Documents"),
            TestContext.Current.CancellationToken);

        Assert.Equal(["active", "list"], raised);
    }

    [Fact]
    public async Task EditingTheOpenWorkspacesFolders_TellsTheSidebarAsWellAsTheSwitcher()
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");
        int activeChanges = 0;
        service.ActiveWorkspaceChanged += (_, _) => activeChanges++;

        await service.AddRootDirectoryAsync(
            service.Workspaces[0].Id,
            _folder.ExistingDirectory("Extra"),
            TestContext.Current.CancellationToken);

        // The sidebar renders the open workspace's folders, so it has to hear about this too.
        Assert.Equal(1, activeChanges);
    }

    [Fact]
    public async Task ActivatingTheWorkspaceThatIsAlreadyOpen_ChangesNothing()
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");
        int activeChanges = 0;
        service.ActiveWorkspaceChanged += (_, _) => activeChanges++;

        await service.ActivateAsync(service.Workspaces[0].Id, TestContext.Current.CancellationToken);

        Assert.Equal(0, activeChanges);
    }

    [Fact]
    public async Task SetFolderColorAsync_ColoursAFolderAndSurvivesARestart()
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");
        Workspace workspace = service.Workspaces[0];
        string drafts = _folder.ExistingDirectory(System.IO.Path.Combine("Documents", "drafts"));

        await service.SetFolderColorAsync(
            workspace.Id,
            drafts,
            FolderColor.Blue,
            TestContext.Current.CancellationToken);

        FolderColorEntry entry = Assert.Single(workspace.FolderColors);
        Assert.Equal(drafts, entry.Path);
        Assert.Equal(FolderColor.Blue, entry.Color);

        WorkspaceService restarted = _folder.CreateService();

        Assert.Equal(
            FolderColor.Blue,
            FolderColorMap.ColorOf(restarted.Workspaces[0].FolderColors, drafts));
    }

    [Fact]
    public async Task SetFolderColorAsync_StoresTheFolderNormalized()
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");
        string drafts = _folder.ExistingDirectory(System.IO.Path.Combine("Documents", "drafts"));

        await service.SetFolderColorAsync(
            service.Workspaces[0].Id,
            drafts + System.IO.Path.DirectorySeparatorChar,
            FolderColor.Red,
            TestContext.Current.CancellationToken);

        // Otherwise the same folder coloured twice, once with a trailing separator, would be two
        // entries — and which one a file inherited from would be list order.
        Assert.Equal(drafts, Assert.Single(service.Workspaces[0].FolderColors).Path);
    }

    [Fact]
    public async Task SetFolderColorAsync_ReplacesTheColourRatherThanAddingASecondEntry()
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");
        Workspace workspace = service.Workspaces[0];
        string drafts = _folder.ExistingDirectory(System.IO.Path.Combine("Documents", "drafts"));

        await service.SetFolderColorAsync(workspace.Id, drafts, FolderColor.Blue, TestContext.Current.CancellationToken);
        await service.SetFolderColorAsync(workspace.Id, drafts, FolderColor.Pink, TestContext.Current.CancellationToken);

        Assert.Equal(FolderColor.Pink, Assert.Single(workspace.FolderColors).Color);
    }

    [Fact]
    public async Task SetFolderColorAsync_WithNone_TakesTheEntryOutAltogether()
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");
        Workspace workspace = service.Workspaces[0];
        string drafts = _folder.ExistingDirectory(System.IO.Path.Combine("Documents", "drafts"));

        await service.SetFolderColorAsync(workspace.Id, drafts, FolderColor.Blue, TestContext.Current.CancellationToken);
        await service.SetFolderColorAsync(workspace.Id, drafts, FolderColor.None, TestContext.Current.CancellationToken);

        // Removed rather than kept as None: an entry that says "no colour" is a row the file does not
        // need, and the resolution rule already answers None for a folder it has never heard of.
        Assert.Empty(workspace.FolderColors);
        Assert.Empty(_folder.CreateService().Workspaces[0].FolderColors);
    }

    [Fact]
    public async Task SetFolderColorAsync_RaisesTheColourEventAndNeitherOfTheOthers()
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");
        int colourChanges = 0;
        int listChanges = 0;
        int activeChanges = 0;

        service.FolderColorsChanged += (_, _) => colourChanges++;
        service.WorkspacesChanged += (_, _) => listChanges++;
        service.ActiveWorkspaceChanged += (_, _) => activeChanges++;

        await service.SetFolderColorAsync(
            service.Workspaces[0].Id,
            _folder.ExistingDirectory(System.IO.Path.Combine("Documents", "drafts")),
            FolderColor.Teal,
            TestContext.Current.CancellationToken);

        // The other two mean "the tree is showing the wrong entries", and the tree answers them by
        // listing every root again — which would collapse the user's folders on every colour click.
        Assert.Equal(1, colourChanges);
        Assert.Equal(0, listChanges);
        Assert.Equal(0, activeChanges);
    }

    [Fact]
    public async Task SetFolderColorAsync_WithTheColourItAlreadyHas_ChangesNothing()
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");
        Workspace workspace = service.Workspaces[0];
        string drafts = _folder.ExistingDirectory(System.IO.Path.Combine("Documents", "drafts"));

        await service.SetFolderColorAsync(workspace.Id, drafts, FolderColor.Green, TestContext.Current.CancellationToken);

        int colourChanges = 0;
        service.FolderColorsChanged += (_, _) => colourChanges++;

        await service.SetFolderColorAsync(workspace.Id, drafts, FolderColor.Green, TestContext.Current.CancellationToken);
        await service.SetFolderColorAsync(
            workspace.Id,
            _folder.MissingDirectory("never-coloured"),
            FolderColor.None,
            TestContext.Current.CancellationToken);

        Assert.Equal(0, colourChanges);
    }

    [Fact]
    public async Task SetFolderColorAsync_WhenTheWriteFails_PutsTheColoursBack()
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");
        Guid workspaceId = service.Workspaces[0].Id;
        string drafts = _folder.ExistingDirectory(System.IO.Path.Combine("Documents", "drafts"));

        await service.SetFolderColorAsync(workspaceId, drafts, FolderColor.Blue, TestContext.Current.CancellationToken);
        _folder.BlockWrites();

        await Assert.ThrowsAnyAsync<Exception>(
            () => service.SetFolderColorAsync(workspaceId, drafts, FolderColor.Red, TestContext.Current.CancellationToken));

        // What the list says and what the file holds cannot drift apart — and this is also what proves
        // Workspace.Clone copies the entries rather than the list that points at them.
        Assert.Equal(
            FolderColor.Blue,
            FolderColorMap.ColorOf(service.Workspaces[0].FolderColors, drafts));
    }

    [Fact]
    public async Task SetFolderColorAsync_RejectsAnUnknownWorkspace()
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SetFolderColorAsync(
                Guid.NewGuid(),
                _folder.ExistingDirectory("Elsewhere"),
                FolderColor.Blue,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public void TheConfigurationIsRequired()
        => Assert.Throws<ArgumentNullException>(() => new WorkspaceService(null!));

    public void Dispose() => _folder.Dispose();

    private async Task<WorkspaceService> WithOneWorkspaceAsync(string name)
    {
        WorkspaceService service = _folder.CreateService();
        await service.CreateAsync(name, _folder.ExistingDirectory(name), TestContext.Current.CancellationToken);

        return service;
    }

    private async Task<WorkspaceService> WithTwoWorkspacesAsync()
    {
        WorkspaceService service = await WithOneWorkspaceAsync("Documents");
        await service.CreateAsync("Notes", _folder.ExistingDirectory("Notes"), TestContext.Current.CancellationToken);

        return service;
    }
}
