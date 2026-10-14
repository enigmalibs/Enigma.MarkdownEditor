using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Files;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.ViewModels;

/// <summary>
/// Tests for the file tree, over real directories and a fake watcher.
/// </summary>
/// <remarks>
/// The directories are real because listing them is the point; the watcher is fake because waiting for
/// the operating system to notice a file is not — the real one is covered in <c>DirectoryWatcherTests</c>.
/// This split is what lets these assert the tree's reaction to a change without a single sleep.
/// </remarks>
public sealed class FileTreeViewModelTests : IDisposable
{
    private readonly string _root;
    private readonly string _settingsFile;
    private readonly FakeUiDispatcher _dispatcher = new();
    private readonly FakeDirectoryWatcherFactory _watchers = new();
    private readonly FakeWorkspaceService _workspaces = new();
    private readonly FakeInfoBarService _infoBar = new();
    private readonly FakeDocumentOpener _opener = new();
    private readonly FakeFileOperationPrompts _prompts = new();
    private readonly FakeTrashService _trash = new();

    public FileTreeViewModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _settingsFile = Path.Combine(_root, AppPaths.SettingsFileName);
    }

    [Fact]
    public async Task WithNoWorkspace_ThereIsNoTree()
    {
        using FileTreeViewModel tree = Create();
        await _dispatcher.DrainAsync();

        Assert.Empty(tree.Roots);
        Assert.False(tree.HasRoots);
    }

    [Fact]
    public async Task TheOnlyRoot_OpensItselfAndListsWhatTheFilterAllows()
    {
        string workspace = SeedWorkspace("notes");
        WriteFile(workspace, "zebra.md");
        WriteFile(workspace, "alpha.md");
        WriteFile(workspace, "photo.png");
        Directory.CreateDirectory(Path.Combine(workspace, "drafts"));

        using FileTreeViewModel tree = await CreateAndDrainAsync();

        FileTreeNodeViewModel root = Assert.Single(tree.Roots);
        Assert.True(root.IsRoot);
        Assert.Equal("notes", root.Name);
        Assert.True(root.IsExpanded);
        Assert.Equal(["drafts", "alpha.md", "zebra.md"], root.Children.Select(child => child.Name));
    }

    [Fact]
    public async Task TwoRoots_StayClosedAndEachCarriesAnExpander()
    {
        string first = SeedWorkspace("first", "second");
        WriteFile(first, "a.md");

        using FileTreeViewModel tree = await CreateAndDrainAsync();

        Assert.Equal(2, tree.Roots.Count);
        Assert.All(tree.Roots, root => Assert.False(root.IsExpanded));

        // The single Loading child is what gives an unread folder an expander to click.
        Assert.All(
            tree.Roots,
            root => Assert.Equal(FileTreeNodeKind.Loading, Assert.Single(root.Children).Kind));
    }

    [Fact]
    public async Task ARootThatIsNotThere_IsDrawnAsMissingRatherThanDropped()
    {
        _workspaces.Seed("Work", active: true, Path.Combine(_root, "unplugged"));

        using FileTreeViewModel tree = await CreateAndDrainAsync();

        FileTreeNodeViewModel root = Assert.Single(tree.Roots);
        Assert.Equal(FileTreeNodeKind.Missing, root.Kind);
        Assert.True(root.IsMissing);
        Assert.Empty(root.Children);
    }

    [Fact]
    public async Task AnEmptyFolder_SaysSoRatherThanOpeningOntoNothing()
    {
        string workspace = SeedWorkspace("notes");
        Directory.CreateDirectory(Path.Combine(workspace, "drafts"));

        using FileTreeViewModel tree = await CreateAndDrainAsync();

        FileTreeNodeViewModel drafts = Assert.Single(tree.Roots[0].Children);
        drafts.IsExpanded = true;
        await _dispatcher.DrainAsync();

        Assert.Equal(FileTreeNodeKind.Empty, Assert.Single(drafts.Children).Kind);
    }

    [Fact]
    public async Task ExpandingAFolder_ReadsItOnceAndNotAgain()
    {
        string workspace = SeedWorkspace("notes");
        Directory.CreateDirectory(Path.Combine(workspace, "drafts"));
        WriteFile(Path.Combine(workspace, "drafts"), "idea.md");

        using FileTreeViewModel tree = await CreateAndDrainAsync();
        FileTreeNodeViewModel drafts = Assert.Single(tree.Roots[0].Children);

        drafts.IsExpanded = true;
        await _dispatcher.DrainAsync();

        Assert.True(drafts.IsLoaded);
        Assert.Equal(["idea.md"], drafts.Children.Select(child => child.Name));

        int postsAfterFirstExpansion = _dispatcher.PostCount;
        drafts.IsExpanded = false;
        drafts.IsExpanded = true;

        Assert.Equal(postsAfterFirstExpansion, _dispatcher.PostCount);
    }

    [Fact]
    public async Task AFileCreatedOutside_AppearsWhenTheWatcherReportsIt()
    {
        string workspace = SeedWorkspace("notes");
        WriteFile(workspace, "a.md");

        using FileTreeViewModel tree = await CreateAndDrainAsync();
        WriteFile(workspace, "b.md");

        _watchers.For(workspace).Raise(workspace);
        await _dispatcher.DrainAsync();

        Assert.Equal(["a.md", "b.md"], tree.Roots[0].Children.Select(child => child.Name));
    }

    [Fact]
    public async Task AFileDeletedOutside_Leaves()
    {
        string workspace = SeedWorkspace("notes");
        WriteFile(workspace, "a.md");
        WriteFile(workspace, "b.md");

        using FileTreeViewModel tree = await CreateAndDrainAsync();
        File.Delete(Path.Combine(workspace, "a.md"));

        _watchers.For(workspace).Raise(workspace);
        await _dispatcher.DrainAsync();

        Assert.Equal(["b.md"], tree.Roots[0].Children.Select(child => child.Name));
    }

    [Fact]
    public async Task AChangeAroundAnOpenFolder_LeavesItOpenAndKeepsTheVeryNode()
    {
        string workspace = SeedWorkspace("notes");
        string drafts = Path.Combine(workspace, "drafts");
        Directory.CreateDirectory(drafts);
        WriteFile(drafts, "idea.md");

        using FileTreeViewModel tree = await CreateAndDrainAsync();
        FileTreeNodeViewModel folder = Assert.Single(tree.Roots[0].Children);
        folder.IsExpanded = true;
        await _dispatcher.DrainAsync();

        WriteFile(workspace, "new.md");
        _watchers.For(workspace).Raise(workspace);
        await _dispatcher.DrainAsync();

        // Same object, not a rebuilt one: that identity is what carries the expansion, and with it
        // everything the user had opened underneath.
        Assert.Same(folder, tree.Roots[0].Children[0]);
        Assert.True(folder.IsExpanded);
        Assert.Equal(["idea.md"], folder.Children.Select(child => child.Name));
        Assert.Equal(["drafts", "new.md"], tree.Roots[0].Children.Select(child => child.Name));
    }

    [Fact]
    public async Task AChangeInsideAnOpenSubfolder_IsAppliedThere()
    {
        string workspace = SeedWorkspace("notes");
        string drafts = Path.Combine(workspace, "drafts");
        Directory.CreateDirectory(drafts);
        WriteFile(drafts, "idea.md");

        using FileTreeViewModel tree = await CreateAndDrainAsync();
        FileTreeNodeViewModel folder = Assert.Single(tree.Roots[0].Children);
        folder.IsExpanded = true;
        await _dispatcher.DrainAsync();

        WriteFile(drafts, "another.md");
        _watchers.For(workspace).Raise(drafts);
        await _dispatcher.DrainAsync();

        Assert.Equal(["another.md", "idea.md"], folder.Children.Select(child => child.Name));
    }

    [Fact]
    public async Task AChangeInAFolderNobodyOpened_IsIgnored()
    {
        string workspace = SeedWorkspace("notes");
        string drafts = Path.Combine(workspace, "drafts");
        Directory.CreateDirectory(drafts);

        using FileTreeViewModel tree = await CreateAndDrainAsync();
        FileTreeNodeViewModel folder = Assert.Single(tree.Roots[0].Children);

        WriteFile(drafts, "idea.md");
        _watchers.For(workspace).Raise(drafts);
        await _dispatcher.DrainAsync();

        // Nothing to update: it reads itself, current, the first time it is opened.
        Assert.False(folder.IsLoaded);
        Assert.Equal(FileTreeNodeKind.Loading, Assert.Single(folder.Children).Kind);
    }

    [Fact]
    public async Task AWatcherThatLostEvents_ReadsEverythingThatWasOpenAgain()
    {
        string workspace = SeedWorkspace("notes");
        string drafts = Path.Combine(workspace, "drafts");
        Directory.CreateDirectory(drafts);

        using FileTreeViewModel tree = await CreateAndDrainAsync();
        FileTreeNodeViewModel folder = Assert.Single(tree.Roots[0].Children);
        folder.IsExpanded = true;
        await _dispatcher.DrainAsync();

        WriteFile(workspace, "root.md");
        WriteFile(drafts, "deep.md");

        _watchers.For(workspace).RaiseReset();
        await _dispatcher.DrainAsync();

        Assert.Equal(["drafts", "root.md"], tree.Roots[0].Children.Select(child => child.Name));
        Assert.Equal(["deep.md"], folder.Children.Select(child => child.Name));
    }

    [Fact]
    public async Task ShowAllFiles_WidensTheTreeAndIsWrittenToTheSettingsFile()
    {
        string workspace = SeedWorkspace("notes");
        WriteFile(workspace, "a.md");
        WriteFile(workspace, "photo.png");

        IConfig<AppSettings> settings = LoadSettings();
        using FileTreeViewModel tree = await CreateAndDrainAsync(settings);

        Assert.Equal(["a.md"], tree.Roots[0].Children.Select(child => child.Name));

        tree.ShowAllFiles = true;
        await _dispatcher.DrainAsync();

        Assert.Equal(["a.md", "photo.png"], tree.Roots[0].Children.Select(child => child.Name));
        Assert.True(settings.Value.ShowAllFiles);

        // Re-read from disk: the toggle has to survive a restart, not just this run.
        Assert.True(SettingsLoader.Load(_settingsFile).Config.Value.ShowAllFiles);
    }

    [Fact]
    public async Task ShowAllFiles_ReadsAndWritesTheLiveSettingsObject()
    {
        SeedWorkspace("notes");
        IConfig<AppSettings> settings = LoadSettings();
        settings.Value.ShowAllFiles = true;

        using FileTreeViewModel tree = await CreateAndDrainAsync(settings);

        // Backed by the very object the settings window edits, so the two controls cannot disagree.
        Assert.True(tree.ShowAllFiles);
    }

    [Fact]
    public async Task TheSettingChangedInTheSettingsDialog_ReachesTheTreeWhenItCloses()
    {
        string workspace = SeedWorkspace("notes");
        WriteFile(workspace, "photo.png");

        IConfig<AppSettings> settings = LoadSettings();
        using FileTreeViewModel tree = await CreateAndDrainAsync(settings);

        Assert.Equal(FileTreeNodeKind.Empty, Assert.Single(tree.Roots[0].Children).Kind);

        settings.Value.ShowAllFiles = true;
        await tree.OnSettingsChangedAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["photo.png"], tree.Roots[0].Children.Select(child => child.Name));
    }

    [Fact]
    public async Task OpeningAFile_HandsItToTheDocumentOpener()
    {
        string workspace = SeedWorkspace("notes");
        string note = WriteFile(workspace, "a.md");

        using FileTreeViewModel tree = await CreateAndDrainAsync();
        FileTreeNodeViewModel file = Assert.Single(tree.Roots[0].Children);

        await tree.OpenCommand.ExecuteAsync(file);

        Assert.Equal([note], _opener.Opened);
    }

    [Fact]
    public async Task OpeningAFolder_TogglesItInsteadOfOpeningAnything()
    {
        string workspace = SeedWorkspace("notes");
        Directory.CreateDirectory(Path.Combine(workspace, "drafts"));

        using FileTreeViewModel tree = await CreateAndDrainAsync();
        FileTreeNodeViewModel folder = Assert.Single(tree.Roots[0].Children);

        await tree.OpenCommand.ExecuteAsync(folder);
        await _dispatcher.DrainAsync();

        Assert.True(folder.IsExpanded);
        Assert.Empty(_opener.Opened);
    }

    [Fact]
    public async Task OpeningAnInformationalRow_DoesNothing()
    {
        string workspace = SeedWorkspace("notes");
        Directory.CreateDirectory(Path.Combine(workspace, "drafts"));

        using FileTreeViewModel tree = await CreateAndDrainAsync();
        FileTreeNodeViewModel folder = Assert.Single(tree.Roots[0].Children);
        folder.IsExpanded = true;
        await _dispatcher.DrainAsync();

        await tree.OpenCommand.ExecuteAsync(Assert.Single(folder.Children));
        await tree.OpenCommand.ExecuteAsync(null);

        Assert.Empty(_opener.Opened);
    }

    [Fact]
    public async Task AFailureOpeningAFile_IsSurfacedRatherThanSwallowed()
    {
        string workspace = SeedWorkspace("notes");
        WriteFile(workspace, "a.md");

        FakeDocumentOpener opener = new() { Failure = new IOException("gone") };
        using FileTreeViewModel tree = await CreateAndDrainAsync(opener: opener);

        // An AsyncRelayCommand parks a faulted task rather than rethrowing, so a double-click would
        // otherwise simply appear to do nothing.
        await tree.OpenCommand.ExecuteAsync(Assert.Single(tree.Roots[0].Children));

        Assert.Single(_infoBar.Shown);
    }

    [Fact]
    public async Task ChangingTheWorkspace_RebuildsTheTreeAndItsWatchers()
    {
        string first = SeedWorkspace("first");
        string second = Path.Combine(_root, "second");
        Directory.CreateDirectory(second);
        Workspace theSecond = _workspaces.Seed("Second", active: false, second);

        using FileTreeViewModel tree = await CreateAndDrainAsync();
        FakeDirectoryWatcher firstWatcher = _watchers.For(first);

        await _workspaces.ActivateAsync(theSecond.Id, TestContext.Current.CancellationToken);
        await _dispatcher.DrainAsync();

        Assert.Equal(["second"], tree.Roots.Select(root => root.Name));
        Assert.True(firstWatcher.IsDisposed);
        Assert.True(_watchers.For(second).IsWatching);
    }

    [Fact]
    public async Task Refreshing_ReadsTheFoldersAgain()
    {
        string workspace = SeedWorkspace("notes");

        using FileTreeViewModel tree = await CreateAndDrainAsync();
        WriteFile(workspace, "late.md");

        await tree.RefreshCommand.ExecuteAsync(null);
        await _dispatcher.DrainAsync();

        Assert.Equal(["late.md"], tree.Roots[0].Children.Select(child => child.Name));
    }

    [Fact]
    public async Task Disposing_StopsTheWatchers()
    {
        string workspace = SeedWorkspace("notes");

        FileTreeViewModel tree = await CreateAndDrainAsync();
        tree.Dispose();

        Assert.True(_watchers.For(workspace).IsDisposed);

        // Disposing twice is harmless.
        tree.Dispose();
    }

    [Fact]
    public async Task WithNoWorkspace_ThereIsNothingAnyOperationCanActOn()
    {
        using FileTreeViewModel tree = await CreateAndDrainAsync();

        Assert.False(tree.NewFileCommand.CanExecute(null));
        Assert.False(tree.NewFolderCommand.CanExecute(null));
        Assert.False(tree.RenameCommand.CanExecute(null));
        Assert.False(tree.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public async Task CreatingAFile_PutsItInTheOnlyRootAndOpensIt()
    {
        string workspace = SeedWorkspace("notes");
        FakeFileOperationPrompts prompts = new() { NewFileName = "idea.md" };

        using FileTreeViewModel tree = await CreateAndDrainAsync(prompts: prompts);

        // Nothing is selected: a workspace over one folder has exactly one answer to "where?".
        Assert.True(tree.NewFileCommand.CanExecute(null));
        await tree.NewFileCommand.ExecuteAsync(null);
        await _dispatcher.DrainAsync();

        string created = Path.Combine(workspace, "idea.md");

        Assert.Equal([workspace], prompts.AskedForNewFileIn);
        Assert.True(File.Exists(created));
        Assert.Equal(["idea.md"], tree.Roots[0].Children.Select(child => child.Name));
        Assert.Equal([created], _opener.Opened);

        // Revealed, not merely created: an entry the user cannot see is one they have to go looking for.
        Assert.Equal(created, tree.SelectedNode?.FullPath);
    }

    [Fact]
    public async Task ColouringAFolder_WritesItThroughTheServiceAndTintsThatRowAlone()
    {
        string workspace = SeedWorkspace("notes");
        string drafts = Directory.CreateDirectory(Path.Combine(workspace, "drafts")).FullName;
        Directory.CreateDirectory(Path.Combine(workspace, "archive"));

        using FileTreeViewModel tree = await CreateAndDrainAsync();

        tree.SelectedNode = tree.Roots[0].Children.First(child => child.Name == "drafts");

        Assert.True(tree.SetColorCommand.CanExecute(FolderColor.Blue));
        await tree.SetColorCommand.ExecuteAsync(FolderColor.Blue);
        await _dispatcher.DrainAsync();

        Assert.Equal(
            FolderColor.Blue,
            FolderColorMap.ColorOf(_workspaces.ActiveWorkspace?.FolderColors, drafts));

        // The row that carries it, and nothing else — not its parent, not its sibling, not a file.
        Assert.Equal(FolderColor.Blue, tree.SelectedNode?.Color);
        Assert.Equal(FolderColor.None, tree.Roots[0].Color);
        Assert.Equal(
            FolderColor.None,
            tree.Roots[0].Children.First(child => child.Name == "archive").Color);
    }

    [Fact]
    public async Task ColouringAFolder_LeavesTheTreeAsTheUserOpenedIt()
    {
        string workspace = SeedWorkspace("notes");
        string drafts = Directory.CreateDirectory(Path.Combine(workspace, "drafts")).FullName;
        WriteFile(drafts, "old.md");

        using FileTreeViewModel tree = await CreateAndDrainAsync();

        FileTreeNodeViewModel folder = Assert.Single(tree.Roots[0].Children);
        folder.IsExpanded = true;
        await _dispatcher.DrainAsync();

        FileTreeNodeViewModel note = Assert.Single(folder.Children);

        tree.SelectedNode = folder;
        await tree.SetColorCommand.ExecuteAsync(FolderColor.Teal);
        await _dispatcher.DrainAsync();

        // The reason a colour has an event of its own: routed through WorkspacesChanged it would
        // re-list every root, and the folder the user opened would shut under their pointer.
        Assert.True(folder.IsExpanded);
        Assert.Same(note, Assert.Single(folder.Children));
        Assert.Equal(FolderColor.Teal, folder.Color);
    }

    [Fact]
    public async Task ClearingAFoldersColour_TakesTheTintOff()
    {
        string workspace = SeedWorkspace("notes");
        Directory.CreateDirectory(Path.Combine(workspace, "drafts"));

        using FileTreeViewModel tree = await CreateAndDrainAsync();

        FileTreeNodeViewModel folder = Assert.Single(tree.Roots[0].Children);
        tree.SelectedNode = folder;

        await tree.SetColorCommand.ExecuteAsync(FolderColor.Pink);
        await _dispatcher.DrainAsync();
        Assert.Equal(FolderColor.Pink, folder.Color);

        await tree.SetColorCommand.ExecuteAsync(FolderColor.None);
        await _dispatcher.DrainAsync();

        Assert.Equal(FolderColor.None, folder.Color);
        Assert.Empty(_workspaces.ActiveWorkspace?.FolderColors ?? []);
    }

    [Fact]
    public async Task AColourSetBeforeAFolderWasOpened_IsOnItsRowTheMomentItIs()
    {
        string workspace = SeedWorkspace("notes");
        string drafts = Directory.CreateDirectory(Path.Combine(workspace, "drafts")).FullName;
        Directory.CreateDirectory(Path.Combine(drafts, "q3"));

        _workspaces.ActiveWorkspace!.FolderColors.Add(
            new FolderColorEntry { Path = Path.Combine(drafts, "q3"), Color = FolderColor.Green });

        using FileTreeViewModel tree = await CreateAndDrainAsync();

        FileTreeNodeViewModel folder = Assert.Single(tree.Roots[0].Children);
        folder.IsExpanded = true;
        await _dispatcher.DrainAsync();

        // Listed for the first time, so the node was built after the colour was set: the listing has
        // to pick it up, not only the colour event.
        Assert.Equal(FolderColor.Green, Assert.Single(folder.Children).Color);
    }

    [Fact]
    public async Task AWorkspaceRoot_CanBeColouredEvenThoughItCannotBeRenamed()
    {
        string workspace = SeedWorkspace("notes");

        using FileTreeViewModel tree = await CreateAndDrainAsync();

        tree.SelectedNode = tree.Roots[0];

        // A colour goes into the workspaces file rather than onto the file system, so it cannot leave
        // the two out of step the way a rename or a delete would.
        Assert.False(tree.CanActOnSelection);
        Assert.True(tree.CanColorSelection);

        await tree.SetColorCommand.ExecuteAsync(FolderColor.Red);
        await _dispatcher.DrainAsync();

        Assert.Equal(
            FolderColor.Red,
            FolderColorMap.ColorOf(_workspaces.ActiveWorkspace?.FolderColors, workspace));
    }

    [Fact]
    public async Task ColouringIsOfferedForAFolderAndNothingElse()
    {
        string workspace = SeedWorkspace("notes");
        Directory.CreateDirectory(Path.Combine(workspace, "empty"));
        WriteFile(workspace, "a.md");

        using FileTreeViewModel tree = await CreateAndDrainAsync();

        tree.SelectedNode = null;
        Assert.False(tree.CanColorSelection);

        tree.SelectedNode = tree.Roots[0].Children.First(child => child.IsFile);
        Assert.False(tree.CanColorSelection);
        Assert.False(tree.SetColorCommand.CanExecute(FolderColor.Blue));

        FileTreeNodeViewModel folder = tree.Roots[0].Children.First(child => child.IsDirectory);
        folder.IsExpanded = true;
        await _dispatcher.DrainAsync();

        tree.SelectedNode = Assert.Single(folder.Children);
        Assert.True(tree.SelectedNode.IsInformational);
        Assert.False(tree.CanColorSelection);
    }

    [Fact]
    public async Task WithNoWorkspace_ThereIsNothingToColour()
    {
        using FileTreeViewModel tree = await CreateAndDrainAsync();

        Assert.False(tree.CanColorSelection);
        Assert.False(tree.SetColorCommand.CanExecute(FolderColor.Blue));
    }

    [Fact]
    public async Task ANewNote_ArrivesWithItsOwnTitleInIt()
    {
        string workspace = SeedWorkspace("notes");
        FakeFileOperationPrompts prompts = new() { NewFileName = "idea.md" };

        using FileTreeViewModel tree = await CreateAndDrainAsync(prompts: prompts);

        await tree.NewFileCommand.ExecuteAsync(null);
        await _dispatcher.DrainAsync();

        // The bytes, not the text: the heading has to arrive as UTF-8 with no byte-order mark and
        // line feeds, which is what every other file this editor writes looks like.
        byte[] bytes = await File.ReadAllBytesAsync(
            Path.Combine(workspace, "idea.md"),
            TestContext.Current.CancellationToken);

        Assert.Equal("# idea\n\n"u8.ToArray(), bytes);
    }

    [Fact]
    public async Task ANewFileThatIsNotMarkdown_ArrivesEmpty()
    {
        string workspace = SeedWorkspace("notes");
        FakeFileOperationPrompts prompts = new() { NewFileName = "data.json" };

        using FileTreeViewModel tree = await CreateAndDrainAsync(prompts: prompts);

        await tree.NewFileCommand.ExecuteAsync(null);
        await _dispatcher.DrainAsync();

        // A '# heading' written into a file the tree can also create, but the editor does not own, is
        // corruption rather than help.
        Assert.Empty(await File.ReadAllBytesAsync(
            Path.Combine(workspace, "data.json"),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreatingAFile_WhenTheUserBacksOut_WritesNothing()
    {
        string workspace = SeedWorkspace("notes");

        using FileTreeViewModel tree = await CreateAndDrainAsync();

        await tree.NewFileCommand.ExecuteAsync(null);
        await _dispatcher.DrainAsync();

        Assert.Empty(Directory.GetFileSystemEntries(workspace));
        Assert.Empty(_opener.Opened);
        Assert.Empty(_infoBar.Shown);
    }

    [Fact]
    public async Task ANewFileTheEditorWouldNotTake_IsCreatedButNotOpened()
    {
        string workspace = SeedWorkspace("notes");
        FakeFileOperationPrompts prompts = new() { NewFileName = "photo.png" };

        using FileTreeViewModel tree = await CreateAndDrainAsync(prompts: prompts);

        await tree.NewFileCommand.ExecuteAsync(null);
        await _dispatcher.DrainAsync();

        // Creating an empty file and having the operating system launch something for it is not what
        // "open the new file" means here.
        Assert.True(File.Exists(Path.Combine(workspace, "photo.png")));
        Assert.Empty(_opener.Opened);
    }

    [Fact]
    public async Task CreatingAFile_WhileAFileIsSelected_UsesThatFilesOwnFolder()
    {
        string workspace = SeedWorkspace("notes");
        string drafts = Directory.CreateDirectory(Path.Combine(workspace, "drafts")).FullName;
        WriteFile(drafts, "old.md");

        FakeFileOperationPrompts prompts = new() { NewFileName = "new.md" };
        using FileTreeViewModel tree = await CreateAndDrainAsync(prompts: prompts);

        FileTreeNodeViewModel folder = Assert.Single(tree.Roots[0].Children);
        folder.IsExpanded = true;
        await _dispatcher.DrainAsync();

        tree.SelectedNode = Assert.Single(folder.Children);

        await tree.NewFileCommand.ExecuteAsync(null);
        await _dispatcher.DrainAsync();

        Assert.Equal([drafts], prompts.AskedForNewFileIn);
        Assert.True(File.Exists(Path.Combine(drafts, "new.md")));
    }

    [Fact]
    public async Task CreatingAFile_FromAnEmptyFoldersOwnRow_UsesThatFolder()
    {
        string workspace = SeedWorkspace("notes");
        string drafts = Directory.CreateDirectory(Path.Combine(workspace, "drafts")).FullName;

        FakeFileOperationPrompts prompts = new() { NewFileName = "first.md" };
        using FileTreeViewModel tree = await CreateAndDrainAsync(prompts: prompts);

        FileTreeNodeViewModel folder = Assert.Single(tree.Roots[0].Children);
        folder.IsExpanded = true;
        await _dispatcher.DrainAsync();

        // The "Nothing here" row describes the folder rather than sitting in it — and selecting it is
        // exactly when a user reaches for "New file".
        FileTreeNodeViewModel nothingHere = Assert.Single(folder.Children);
        Assert.Equal(FileTreeNodeKind.Empty, nothingHere.Kind);
        tree.SelectedNode = nothingHere;

        await tree.NewFileCommand.ExecuteAsync(null);
        await _dispatcher.DrainAsync();

        Assert.Equal([drafts], prompts.AskedForNewFileIn);
        Assert.Equal(["first.md"], folder.Children.Select(child => child.Name));
    }

    [Fact]
    public async Task CreatingAFolder_ShowsItStraightAway()
    {
        string workspace = SeedWorkspace("notes");
        FakeFileOperationPrompts prompts = new() { NewFolderName = "drafts" };

        using FileTreeViewModel tree = await CreateAndDrainAsync(prompts: prompts);

        await tree.NewFolderCommand.ExecuteAsync(null);
        await _dispatcher.DrainAsync();

        Assert.True(Directory.Exists(Path.Combine(workspace, "drafts")));
        Assert.Equal(["drafts"], tree.Roots[0].Children.Select(child => child.Name));
    }

    [Fact]
    public async Task ANameTheOperationRefuses_IsReportedWithoutTakingTheTreeDown()
    {
        string workspace = SeedWorkspace("notes");
        WriteFile(workspace, "taken.md");

        FakeFileOperationPrompts prompts = new() { NewFileName = "taken.md" };
        using FileTreeViewModel tree = await CreateAndDrainAsync(prompts: prompts);

        await tree.NewFileCommand.ExecuteAsync(null);
        await _dispatcher.DrainAsync();

        // The dialog checks as the name is typed, so reaching this is a race — but an AsyncRelayCommand
        // parks a faulted task, and without the report the menu entry would appear to do nothing.
        Assert.Single(_infoBar.Shown);
        Assert.Equal(["taken.md"], tree.Roots[0].Children.Select(child => child.Name));
    }

    [Fact]
    public async Task Renaming_ReplacesTheRowAndPutsItBackInOrder()
    {
        string workspace = SeedWorkspace("notes");
        WriteFile(workspace, "b.md");
        WriteFile(workspace, "c.md");

        FakeFileOperationPrompts prompts = new() { NewName = "a.md" };
        using FileTreeViewModel tree = await CreateAndDrainAsync(prompts: prompts);

        tree.SelectedNode = tree.Roots[0].Children.Single(child => child.Name == "c.md");

        await tree.RenameCommand.ExecuteAsync(null);
        await _dispatcher.DrainAsync();

        Assert.Equal([Path.Combine(workspace, "c.md")], prompts.AskedToRename);
        Assert.False(File.Exists(Path.Combine(workspace, "c.md")));
        Assert.Equal(["a.md", "b.md"], tree.Roots[0].Children.Select(child => child.Name));
    }

    [Fact]
    public async Task RenamingToTheNameItAlreadyHas_ChangesNothing()
    {
        string workspace = SeedWorkspace("notes");
        WriteFile(workspace, "a.md");

        FakeFileOperationPrompts prompts = new() { NewName = "a.md" };
        using FileTreeViewModel tree = await CreateAndDrainAsync(prompts: prompts);

        FileTreeNodeViewModel before = Assert.Single(tree.Roots[0].Children);
        tree.SelectedNode = before;

        await tree.RenameCommand.ExecuteAsync(null);
        await _dispatcher.DrainAsync();

        // Keeping the current name is an answer, not a failure — so nothing is re-read and nothing is
        // reported.
        Assert.Same(before, Assert.Single(tree.Roots[0].Children));
        Assert.Empty(_infoBar.Shown);
    }

    [Fact]
    public async Task AWorkspaceRoot_IsNeitherRenamedNorDeleted()
    {
        string workspace = SeedWorkspace("notes");
        WriteFile(workspace, "a.md");

        FakeFileOperationPrompts prompts = new() { NewName = "renamed", ConfirmsPermanentDelete = true };
        using FileTreeViewModel tree = await CreateAndDrainAsync(prompts: prompts);

        tree.SelectedNode = tree.Roots[0];

        Assert.False(tree.CanActOnSelection);
        Assert.False(tree.RenameCommand.CanExecute(null));
        Assert.False(tree.DeleteCommand.CanExecute(null));

        // Executed anyway: a disabled command is a courtesy, and the guard has to hold on its own.
        await tree.RenameCommand.ExecuteAsync(null);
        await tree.DeleteCommand.ExecuteAsync(null);
        await _dispatcher.DrainAsync();

        Assert.True(Directory.Exists(workspace));
        Assert.Empty(prompts.AskedToRename);
        Assert.Empty(_trash.Trashed);
    }

    [Fact]
    public async Task Deleting_SendsItToTheTrashAndTakesTheRowWithIt()
    {
        string workspace = SeedWorkspace("notes");
        string note = WriteFile(workspace, "a.md");
        WriteFile(workspace, "b.md");

        using FileTreeViewModel tree = await CreateAndDrainAsync();
        tree.SelectedNode = tree.Roots[0].Children.Single(child => child.Name == "a.md");

        await tree.DeleteCommand.ExecuteAsync(null);
        await _dispatcher.DrainAsync();

        Assert.Equal([note], _trash.Trashed);
        Assert.False(File.Exists(note));
        Assert.Equal(["b.md"], tree.Roots[0].Children.Select(child => child.Name));

        // No confirmation: it went somewhere it can be fetched back from, and a prompt on every delete
        // is the one users learn to click through.
        Assert.Equal(0, _prompts.PermanentDeletesAsked);

        // And no InfoBar either: that is for what went wrong, and the row leaving the tree is the
        // whole report of a delete that happened.
        Assert.Empty(_infoBar.Shown);
    }

    [Fact]
    public async Task DeletingAFolder_TakesWhatIsInsideItToo()
    {
        string workspace = SeedWorkspace("notes");
        string drafts = Directory.CreateDirectory(Path.Combine(workspace, "drafts")).FullName;
        WriteFile(drafts, "idea.md");

        using FileTreeViewModel tree = await CreateAndDrainAsync();
        tree.SelectedNode = Assert.Single(tree.Roots[0].Children);

        await tree.DeleteCommand.ExecuteAsync(null);
        await _dispatcher.DrainAsync();

        Assert.False(Directory.Exists(drafts));
        Assert.Equal(FileTreeNodeKind.Empty, Assert.Single(tree.Roots[0].Children).Kind);
    }

    [Fact]
    public async Task DeletingWithNoTrash_AsksFirstAndThenDeletesOutright()
    {
        string workspace = SeedWorkspace("notes");
        string note = WriteFile(workspace, "a.md");

        FakeFileOperationPrompts prompts = new() { ConfirmsPermanentDelete = true };
        FakeTrashService trash = new() { IsAvailable = false };

        using FileTreeViewModel tree = await CreateAndDrainAsync(prompts: prompts, trash: trash);
        tree.SelectedNode = Assert.Single(tree.Roots[0].Children);

        await tree.DeleteCommand.ExecuteAsync(null);
        await _dispatcher.DrainAsync();

        Assert.Equal(1, prompts.PermanentDeletesAsked);
        Assert.False(File.Exists(note));
        Assert.Equal(FileTreeNodeKind.Empty, Assert.Single(tree.Roots[0].Children).Kind);
    }

    [Fact]
    public async Task DeletingWithNoTrash_WhenTheUserRefuses_LeavesTheFileExactlyWhereItWas()
    {
        string workspace = SeedWorkspace("notes");
        string note = WriteFile(workspace, "a.md");

        FakeTrashService trash = new() { IsAvailable = false };

        using FileTreeViewModel tree = await CreateAndDrainAsync(trash: trash);
        tree.SelectedNode = Assert.Single(tree.Roots[0].Children);

        await tree.DeleteCommand.ExecuteAsync(null);
        await _dispatcher.DrainAsync();

        Assert.Equal(1, _prompts.PermanentDeletesAsked);
        Assert.True(File.Exists(note));
        Assert.Equal(["a.md"], tree.Roots[0].Children.Select(child => child.Name));
    }

    [Fact]
    public async Task ATrashThatDeclines_FallsBackToTheSameConfirmation()
    {
        string workspace = SeedWorkspace("notes");
        string note = WriteFile(workspace, "a.md");

        // The trash is there; it just would not take this one — a cross-device move, typically.
        FakeFileOperationPrompts prompts = new() { ConfirmsPermanentDelete = true };
        FakeTrashService trash = new() { Accepts = false };

        using FileTreeViewModel tree = await CreateAndDrainAsync(prompts: prompts, trash: trash);
        tree.SelectedNode = Assert.Single(tree.Roots[0].Children);

        await tree.DeleteCommand.ExecuteAsync(null);
        await _dispatcher.DrainAsync();

        Assert.Equal(1, prompts.PermanentDeletesAsked);
        Assert.False(File.Exists(note));
    }

    [Fact]
    public async Task ATrashThatBreaks_IsSurfacedRatherThanSwallowed()
    {
        string workspace = SeedWorkspace("notes");
        string note = WriteFile(workspace, "a.md");

        FakeTrashService trash = new() { Failure = new IOException("the shell said no") };

        using FileTreeViewModel tree = await CreateAndDrainAsync(trash: trash);
        tree.SelectedNode = Assert.Single(tree.Roots[0].Children);

        await tree.DeleteCommand.ExecuteAsync(null);
        await _dispatcher.DrainAsync();

        Assert.Single(_infoBar.Shown);
        Assert.Equal(0, _prompts.PermanentDeletesAsked);
        Assert.True(File.Exists(note));
    }

    // xUnit1051 asks these five calls to pass TestContext.Current.CancellationToken, and there is
    // no token to pass: the public FileTreeViewModel.MoveAsync(string, string) takes none by
    // design. Its only real caller is the drop handler, fire-and-forget from a pointer event, so a
    // move runs under the ViewModel's own _work CTS like every other tree operation and is
    // superseded by a later refresh or by disposal rather than by a caller's token. The analyzer is
    // keying on the private three-argument overload, which the test assembly cannot call. The rule
    // is not raised at build time by the pinned xunit.v3 3.2.2 analyzer either — only the IDE
    // reports it — and the suppression is scoped to exactly the five move tests so the rule stays
    // live for every test added around them.
#pragma warning disable xUnit1051
    [Fact]
    public async Task MovingAFileIntoAFolder_ReReadsBothEnds()
    {
        string workspace = SeedWorkspace("notes");
        string drafts = Directory.CreateDirectory(Path.Combine(workspace, "drafts")).FullName;
        string note = WriteFile(workspace, "a.md");

        using FileTreeViewModel tree = await CreateAndDrainAsync();

        FileTreeNodeViewModel folder = tree.Roots[0].Children.Single(child => child.Name == "drafts");
        folder.IsExpanded = true;
        await _dispatcher.DrainAsync();

        await tree.MoveAsync(note, drafts);
        await _dispatcher.DrainAsync();

        Assert.True(File.Exists(Path.Combine(drafts, "a.md")));
        Assert.Equal(["drafts"], tree.Roots[0].Children.Select(child => child.Name));
        Assert.Equal(["a.md"], folder.Children.Select(child => child.Name));
        Assert.Equal(0, _prompts.ConflictsAsked);
    }

    [Fact]
    public async Task MovingOntoATakenName_KeepsBothWhenAsked()
    {
        string workspace = SeedWorkspace("notes");
        string drafts = Directory.CreateDirectory(Path.Combine(workspace, "drafts")).FullName;
        string note = WriteFile(workspace, "a.md");
        WriteFile(drafts, "a.md");

        FakeFileOperationPrompts prompts = new() { Resolution = MoveResolution.KeepBoth };
        using FileTreeViewModel tree = await CreateAndDrainAsync(prompts: prompts);

        await tree.MoveAsync(note, drafts);
        await _dispatcher.DrainAsync();

        Assert.Equal(1, prompts.ConflictsAsked);
        Assert.False(File.Exists(note));
        Assert.True(File.Exists(Path.Combine(drafts, "a.md")));
        Assert.True(File.Exists(Path.Combine(drafts, "a (2).md")));
    }

    [Fact]
    public async Task MovingOntoATakenName_OverwritesWhenAsked()
    {
        string workspace = SeedWorkspace("notes");
        string drafts = Directory.CreateDirectory(Path.Combine(workspace, "drafts")).FullName;

        string note = Path.Combine(workspace, "a.md");
        File.WriteAllText(note, "the one being moved");
        File.WriteAllText(Path.Combine(drafts, "a.md"), "the one already there");

        FakeFileOperationPrompts prompts = new() { Resolution = MoveResolution.Overwrite };
        using FileTreeViewModel tree = await CreateAndDrainAsync(prompts: prompts);

        await tree.MoveAsync(note, drafts);
        await _dispatcher.DrainAsync();

        Assert.Equal(1, prompts.ConflictsAsked);
        Assert.False(File.Exists(note));
        Assert.Equal("the one being moved", File.ReadAllText(Path.Combine(drafts, "a.md")));
    }

    [Fact]
    public async Task MovingOntoATakenName_DoesNothingWhenCancelled()
    {
        string workspace = SeedWorkspace("notes");
        string drafts = Directory.CreateDirectory(Path.Combine(workspace, "drafts")).FullName;
        string note = WriteFile(workspace, "a.md");
        WriteFile(drafts, "a.md");

        // Cancel is the fake's default, exactly as a dismissed dialog is the real one's.
        using FileTreeViewModel tree = await CreateAndDrainAsync();

        await tree.MoveAsync(note, drafts);
        await _dispatcher.DrainAsync();

        Assert.Equal(1, _prompts.ConflictsAsked);
        Assert.True(File.Exists(note));
        Assert.Single(Directory.GetFiles(drafts));
    }

    [Fact]
    public async Task AMoveThePlannerRejects_IsReportedRatherThanAttempted()
    {
        string workspace = SeedWorkspace("notes");
        string drafts = Directory.CreateDirectory(Path.Combine(workspace, "drafts")).FullName;

        using FileTreeViewModel tree = await CreateAndDrainAsync();

        await tree.MoveAsync(drafts, drafts);
        await _dispatcher.DrainAsync();

        Assert.True(Directory.Exists(drafts));
        Assert.Single(_infoBar.Shown);
        Assert.Equal(0, _prompts.ConflictsAsked);
    }
#pragma warning restore xUnit1051

    [Fact]
    public void ADropIsRefusedUpFront_WhenItCouldOnlyMeanNothing()
    {
        string notes = Path.Combine(_root, "notes");
        string drafts = Path.Combine(notes, "drafts");
        string deeper = Path.Combine(drafts, "deeper");
        string note = Path.Combine(notes, "a.md");

        Assert.False(FileTreeViewModel.CanAcceptDrop(drafts, drafts));
        Assert.False(FileTreeViewModel.CanAcceptDrop(note, notes));
        Assert.False(FileTreeViewModel.CanAcceptDrop(drafts, deeper));
        Assert.False(FileTreeViewModel.CanAcceptDrop(null, notes));
        Assert.False(FileTreeViewModel.CanAcceptDrop(note, "  "));

        // A sibling whose name merely starts with the source's is not underneath it.
        Assert.True(FileTreeViewModel.CanAcceptDrop(drafts, Path.Combine(notes, "drafts-old")));
        Assert.True(FileTreeViewModel.CanAcceptDrop(note, drafts));
    }

    [Fact]
    public void EveryDependency_IsRequired()
    {
        IConfig<AppSettings> settings = LoadSettings();

        Assert.Throws<ArgumentNullException>(() => new FileTreeViewModel(
            null!, settings, _watchers, _opener, _prompts, _trash, _dispatcher, _infoBar, NullLogger<FileTreeViewModel>.Instance));
        Assert.Throws<ArgumentNullException>(() => new FileTreeViewModel(
            _workspaces, null!, _watchers, _opener, _prompts, _trash, _dispatcher, _infoBar, NullLogger<FileTreeViewModel>.Instance));
        Assert.Throws<ArgumentNullException>(() => new FileTreeViewModel(
            _workspaces, settings, null!, _opener, _prompts, _trash, _dispatcher, _infoBar, NullLogger<FileTreeViewModel>.Instance));
        Assert.Throws<ArgumentNullException>(() => new FileTreeViewModel(
            _workspaces, settings, _watchers, null!, _prompts, _trash, _dispatcher, _infoBar, NullLogger<FileTreeViewModel>.Instance));
        Assert.Throws<ArgumentNullException>(() => new FileTreeViewModel(
            _workspaces, settings, _watchers, _opener, null!, _trash, _dispatcher, _infoBar, NullLogger<FileTreeViewModel>.Instance));
        Assert.Throws<ArgumentNullException>(() => new FileTreeViewModel(
            _workspaces, settings, _watchers, _opener, _prompts, null!, _dispatcher, _infoBar, NullLogger<FileTreeViewModel>.Instance));
        Assert.Throws<ArgumentNullException>(() => new FileTreeViewModel(
            _workspaces, settings, _watchers, _opener, _prompts, _trash, null!, _infoBar, NullLogger<FileTreeViewModel>.Instance));
        Assert.Throws<ArgumentNullException>(() => new FileTreeViewModel(
            _workspaces, settings, _watchers, _opener, _prompts, _trash, _dispatcher, null!, NullLogger<FileTreeViewModel>.Instance));
        Assert.Throws<ArgumentNullException>(() => new FileTreeViewModel(
            _workspaces, settings, _watchers, _opener, _prompts, _trash, _dispatcher, _infoBar, null!));
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

    private string SeedWorkspace(params string[] folderNames)
    {
        string[] directories = [.. folderNames.Select(name => Path.Combine(_root, name))];

        foreach (string directory in directories)
        {
            Directory.CreateDirectory(directory);
        }

        _workspaces.Seed("Work", active: true, directories);

        return directories[0];
    }

    private static string WriteFile(string directory, string name)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, "# note");

        return path;
    }

    private IConfig<AppSettings> LoadSettings() => SettingsLoader.Load(_settingsFile).Config;

    private FileTreeViewModel Create(
        IConfig<AppSettings>? settings = null,
        IDocumentOpener? opener = null,
        IFileOperationPrompts? prompts = null,
        ITrashService? trash = null)
        => new(
            _workspaces,
            settings ?? LoadSettings(),
            _watchers,
            opener ?? _opener,
            prompts ?? _prompts,
            trash ?? _trash,
            _dispatcher,
            _infoBar,
            NullLogger<FileTreeViewModel>.Instance);

    private async Task<FileTreeViewModel> CreateAndDrainAsync(
        IConfig<AppSettings>? settings = null,
        IDocumentOpener? opener = null,
        IFileOperationPrompts? prompts = null,
        ITrashService? trash = null)
    {
        FileTreeViewModel tree = Create(settings, opener, prompts, trash);
        await _dispatcher.DrainAsync();

        return tree;
    }
}
