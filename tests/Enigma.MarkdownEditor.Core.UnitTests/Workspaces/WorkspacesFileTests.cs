using System;
using System.IO;
using System.Threading.Tasks;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Configuration;
using Enigma.MarkdownEditor.Core.Workspaces;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Workspaces;

/// <summary>
/// The workspaces file itself: what round-trips, what is rejected, and what a broken file does to
/// start-up. The rules are enforced here as well as in the manage dialog because the file is
/// hand-editable — the dialog's checks are for the user, these are for the file.
/// </summary>
public sealed class WorkspacesFileTests : IDisposable
{
    private readonly TempWorkspacesFolder _folder = new();

    [Fact]
    public void AMissingFile_YieldsAnEmptyListRatherThanAFailure()
    {
        ConfigLoadResult<WorkspacesConfig> result = WorkspacesLoader.Load(_folder.WorkspacesFile);

        Assert.Null(result.Failure);
        Assert.Empty(result.Config.Value.Workspaces);
        Assert.Null(result.Config.Value.LastActiveWorkspaceId);
        Assert.Equal(WorkspacesConfig.CurrentVersion, result.Config.Value.Version);
    }

    [Fact]
    public async Task TheFile_RoundTripsTheWorkspacesInOrderWithTheirIdentifiers()
    {
        WorkspaceService service = _folder.CreateService();
        Workspace documents = await service.CreateAsync(
            "Documents",
            _folder.ExistingDirectory("Documents"),
            TestContext.Current.CancellationToken);
        Workspace notes = await service.CreateAsync(
            "Notes",
            _folder.ExistingDirectory("Notes"),
            TestContext.Current.CancellationToken);

        WorkspacesConfig reloaded = WorkspacesLoader.Load(_folder.WorkspacesFile).Config.Value;

        Assert.Equal([documents.Id, notes.Id], reloaded.Workspaces.ConvertAll(workspace => workspace.Id));
        Assert.Equal(notes.Id, reloaded.LastActiveWorkspaceId);
        Assert.Equal(WorkspacesConfig.CurrentVersion, reloaded.Version);
    }

    [Fact]
    public async Task TheFile_StaysHandEditable()
    {
        WorkspaceService service = _folder.CreateService();
        await service.CreateAsync(
            "Documents",
            _folder.ExistingDirectory("Documents"),
            TestContext.Current.CancellationToken);

        string json = _folder.ReadWorkspacesFile();

        // Indented, with the property names as they are written in the class: the file is meant to be
        // opened and edited, which is also why a broken one is recovered rather than fatal.
        Assert.Contains("\n", json, StringComparison.Ordinal);
        Assert.Contains("\"Workspaces\"", json, StringComparison.Ordinal);
        Assert.Contains("\"RootDirectories\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheFile_RoundTripsFolderColoursWithTheColourWrittenByName()
    {
        WorkspaceService service = _folder.CreateService();
        Workspace documents = await service.CreateAsync(
            "Documents",
            _folder.ExistingDirectory("Documents"),
            TestContext.Current.CancellationToken);
        string drafts = _folder.ExistingDirectory(Path.Combine("Documents", "drafts"));

        await service.SetFolderColorAsync(
            documents.Id,
            drafts,
            FolderColor.Teal,
            TestContext.Current.CancellationToken);

        // By name, not as a number: someone opening this file has to be able to tell what it says and
        // to change Teal to Blue without consulting the source.
        Assert.Contains("\"Color\": \"Teal\"", _folder.ReadWorkspacesFile(), StringComparison.Ordinal);

        WorkspacesConfig reloaded = WorkspacesLoader.Load(_folder.WorkspacesFile).Config.Value;
        FolderColorEntry entry = Assert.Single(reloaded.Workspaces[0].FolderColors);

        Assert.Equal(drafts, entry.Path);
        Assert.Equal(FolderColor.Teal, entry.Color);
    }

    [Fact]
    public void AFileWrittenBeforeFolderColoursExisted_LoadsWithNoneOfThem()
    {
        // Every key the file was written with up to 1.5.0, and not the one this build added: a
        // workspaces file the user already has must keep working and simply pick the default up,
        // rather than be quarantined for what it does not say. That is why Version stays 1.
        _folder.WriteWorkspacesFile($$"""
            {
              "Version": 1,
              "Workspaces": [
                { "Id": "{{Guid.NewGuid()}}", "Name": "Documents", "RootDirectories": ["/tmp/a"] }
              ],
              "LastActiveWorkspaceId": null
            }
            """);

        ConfigLoadResult<WorkspacesConfig> result = WorkspacesLoader.Load(_folder.WorkspacesFile);

        Assert.Null(result.Failure);
        Assert.Null(result.QuarantinedFilePath);
        Assert.Empty(Assert.Single(result.Config.Value.Workspaces).FolderColors);
    }

    [Fact]
    public void AColouredFolderWithNoPath_IsRejected()
    {
        _folder.WriteWorkspacesFile(FileWith($$"""
            {
              "Id": "{{Guid.NewGuid()}}",
              "Name": "Documents",
              "RootDirectories": ["/tmp/a"],
              "FolderColors": [{ "Path": "", "Color": "Blue" }]
            }
            """));

        AssertRejected("Workspaces[0].FolderColors[0].Path");
    }

    [Fact]
    public void OneFolderColouredTwice_IsRejected()
    {
        _folder.WriteWorkspacesFile(FileWith($$"""
            {
              "Id": "{{Guid.NewGuid()}}",
              "Name": "Documents",
              "RootDirectories": ["/tmp/a"],
              "FolderColors": [
                { "Path": "/tmp/a", "Color": "Blue" },
                { "Path": "/tmp/a", "Color": "Red" }
              ]
            }
            """));

        // Which of the two a file inherited from would otherwise be list order, which is not a rule
        // anybody could predict.
        AssertRejected(nameof(WorkspacesConfig.Workspaces), "one folder twice");
    }

    [Fact]
    public void TwoWorkspacesWithTheSameName_AreRejected()
    {
        _folder.WriteWorkspacesFile(FileWith(
            WorkspaceEntry(Guid.NewGuid(), "Documents", "/tmp/a"),
            WorkspaceEntry(Guid.NewGuid(), "documents", "/tmp/b")));

        AssertRejected(nameof(WorkspacesConfig.Workspaces), "name two workspaces the same");
    }

    [Fact]
    public void TwoWorkspacesWithTheSameIdentifier_AreRejected()
    {
        Guid shared = Guid.NewGuid();
        _folder.WriteWorkspacesFile(FileWith(WorkspaceEntry(shared, "Documents", "/tmp/a"), WorkspaceEntry(shared, "Notes", "/tmp/b")));

        AssertRejected(nameof(WorkspacesConfig.Workspaces), "same identifier");
    }

    [Fact]
    public void AWorkspaceWithNoFolder_IsRejected()
    {
        _folder.WriteWorkspacesFile(FileWith($$"""
            { "Id": "{{Guid.NewGuid()}}", "Name": "Documents", "RootDirectories": [] }
            """));

        AssertRejected(nameof(WorkspacesConfig.Workspaces), "at least one folder");
    }

    [Fact]
    public void AWorkspaceWithTheSameFolderTwice_IsRejected()
    {
        _folder.WriteWorkspacesFile(FileWith($$"""
            { "Id": "{{Guid.NewGuid()}}", "Name": "Documents", "RootDirectories": ["/tmp/a", "/tmp/a"] }
            """));

        AssertRejected(nameof(WorkspacesConfig.Workspaces), "repeat or blank out");
    }

    [Fact]
    public void AWorkspaceWithNoName_IsRejectedByTheAttributeAlone()
    {
        _folder.WriteWorkspacesFile(FileWith(WorkspaceEntry(Guid.NewGuid(), string.Empty, "/tmp/a")));

        ConfigurationValidationException failure = AssertRejected("Workspaces[0].Name");

        // One broken value, one message: the fluent validator deliberately leaves the per-property
        // rules to the attributes rather than reporting them a second time in its own words.
        Assert.Single(failure.Errors);
    }

    [Fact]
    public void AFileFromANewerVersion_IsRejectedRatherThanReadAsBestItCan()
    {
        _folder.WriteWorkspacesFile($$"""
            {
              "Version": {{WorkspacesConfig.CurrentVersion + 1}},
              "Workspaces": [],
              "LastActiveWorkspaceId": null
            }
            """);

        // Quarantining keeps the newer file intact; reading it would mean rewriting it in this
        // version's shape and dropping whatever this build does not know about.
        AssertRejected(nameof(WorkspacesConfig.Version));
    }

    [Fact]
    public void ABrokenFile_IsQuarantinedAndTheEditorStartsWithNoWorkspaces()
    {
        _folder.WriteWorkspacesFile("{ not json at all");

        ConfigLoadResult<WorkspacesConfig> result = WorkspacesLoader.Load(_folder.WorkspacesFile);

        Assert.NotNull(result.Failure);
        Assert.Equal(_folder.QuarantinedWorkspacesFile, result.QuarantinedFilePath);
        Assert.False(result.PersistenceDisabled);
        Assert.Empty(result.Config.Value.Workspaces);
        Assert.Equal("{ not json at all", _folder.ReadQuarantinedFile());
    }

    [Fact]
    public async Task AfterQuarantine_TheNextWriteLaysDownACleanFileAtTheRealPath()
    {
        _folder.WriteWorkspacesFile("{ broken");

        ConfigLoadResult<WorkspacesConfig> result = WorkspacesLoader.Load(_folder.WorkspacesFile);
        WorkspaceService service = new(result.Config);
        await service.CreateAsync(
            "Documents",
            _folder.ExistingDirectory("Documents"),
            TestContext.Current.CancellationToken);

        Assert.Equal(_folder.WorkspacesFile, result.Config.FilePath);
        Assert.Equal("Documents", _folder.CreateService().Workspaces[0].Name);
    }

    public void Dispose() => _folder.Dispose();

    private ConfigurationValidationException AssertRejected(string memberPath, string? messagePart = null)
    {
        ConfigLoadResult<WorkspacesConfig> result = WorkspacesLoader.Load(_folder.WorkspacesFile);

        ConfigurationValidationException failure =
            Assert.IsType<ConfigurationValidationException>(result.Failure);

        Assert.Contains(failure.Errors, error => error.MemberPath == memberPath);

        if (messagePart is not null)
        {
            Assert.Contains(failure.Errors, error => error.Message.Contains(messagePart, StringComparison.Ordinal));
        }

        return failure;
    }

    private static string WorkspaceEntry(Guid id, string name, string root)
        => $$"""{ "Id": "{{id}}", "Name": "{{name}}", "RootDirectories": ["{{root}}"] }""";

    private static string FileWith(params string[] workspaces)
        => $$"""
            {
              "Version": 1,
              "Workspaces": [{{string.Join(",", workspaces)}}]
            }
            """;
}
