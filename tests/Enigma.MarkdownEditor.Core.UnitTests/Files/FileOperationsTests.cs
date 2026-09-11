using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Core.Files;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Files;

/// <summary>
/// Tests for the operations the tree's context menu carries out, over real folders. What matters here
/// is not only that the right thing happens, but that the wrong thing is refused with a sentence
/// rather than by overwriting something the user still wanted.
/// </summary>
public sealed class FileOperationsTests
{
    [Fact]
    public async Task ANewFile_IsCreatedAndIsEmpty()
    {
        using TempTreeFolder folder = new();

        string created = await FileOperations.CreateFileAsync(
            folder.Root,
            "notes.md",
            TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(folder.Root, "notes.md"), created);
        Assert.True(File.Exists(created));
        Assert.Equal(0, new FileInfo(created).Length);
    }

    [Fact]
    public async Task ANewFolder_IsCreated()
    {
        using TempTreeFolder folder = new();

        string created = await FileOperations.CreateDirectoryAsync(
            folder.Root,
            "drafts",
            TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(folder.Root, "drafts"), created);
        Assert.True(Directory.Exists(created));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("notes/2024.md")]
    [InlineData("CON.md")]
    [InlineData("notes.md ")]
    public async Task ANameTheRulesRefuse_NeverReachesTheFileSystem(string name)
    {
        using TempTreeFolder folder = new();

        await Assert.ThrowsAsync<FileOperationException>(
            () => FileOperations.CreateFileAsync(folder.Root, name, TestContext.Current.CancellationToken));
        Assert.Empty(Directory.GetFileSystemEntries(folder.Root));
    }

    [Fact]
    public async Task ANameAFileAlreadyHas_LeavesThatFileAlone()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("notes.md", "mine");

        await Assert.ThrowsAsync<FileOperationException>(
            () => FileOperations.CreateFileAsync(folder.Root, "notes.md", TestContext.Current.CancellationToken));
        Assert.Equal("mine", File.ReadAllText(folder.Combine("notes.md")));
    }

    [Fact]
    public async Task ANameAFolderAlreadyHas_LeavesThatFolderAlone()
    {
        using TempTreeFolder folder = new();
        folder.AddFile(Path.Combine("drafts", "notes.md"), "mine");

        await Assert.ThrowsAsync<FileOperationException>(
            () => FileOperations.CreateDirectoryAsync(folder.Root, "drafts", TestContext.Current.CancellationToken));
        Assert.True(File.Exists(folder.Combine(Path.Combine("drafts", "notes.md"))));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ANamelessFolderToCreateIn_IsRejected(string? directory)
        => await Assert.ThrowsAnyAsync<ArgumentException>(
            () => FileOperations.CreateFileAsync(directory!, "notes.md", TestContext.Current.CancellationToken));

    [Fact]
    public async Task ARename_GivesTheEntryItsNewName()
    {
        using TempTreeFolder folder = new();
        string path = folder.AddFile("notes.md", "text");

        string renamed = await FileOperations.RenameAsync(path, "todo.md", TestContext.Current.CancellationToken);

        Assert.Equal(folder.Combine("todo.md"), renamed);
        Assert.False(File.Exists(path));
        Assert.Equal("text", File.ReadAllText(renamed));
    }

    [Fact]
    public async Task ARenameToTheNameItAlreadyHas_ChangesNothing()
    {
        using TempTreeFolder folder = new();
        string path = folder.AddFile("notes.md", "text");

        string renamed = await FileOperations.RenameAsync(path, "notes.md", TestContext.Current.CancellationToken);

        Assert.Equal(path, renamed);
        Assert.Equal("text", File.ReadAllText(renamed));
    }

    [Fact]
    public async Task ARenameThatOnlyChangesTheCase_IsCarriedOut()
    {
        using TempTreeFolder folder = new();
        string path = folder.AddFile("notes.md", "text");

        string renamed = await FileOperations.RenameAsync(path, "Notes.md", TestContext.Current.CancellationToken);

        // Read back from the folder itself: File.Exists would answer yes to either spelling on
        // Windows, so only the entry the file system lists proves the rename happened.
        string listed = Path.GetFileName(Directory.GetFiles(folder.Root).Single());

        Assert.Equal(folder.Combine("Notes.md"), renamed);
        Assert.Equal("Notes.md", listed);
        Assert.Equal("text", File.ReadAllText(renamed));
    }

    [Fact]
    public async Task AFolderRenameThatOnlyChangesTheCase_IsCarriedOut()
    {
        using TempTreeFolder folder = new();
        string path = folder.AddDirectory("drafts");
        folder.AddFile(Path.Combine("drafts", "notes.md"), "text");

        string renamed = await FileOperations.RenameAsync(path, "Drafts", TestContext.Current.CancellationToken);

        string listed = Path.GetFileName(Directory.GetDirectories(folder.Root).Single());

        Assert.Equal(folder.Combine("Drafts"), renamed);
        Assert.Equal("Drafts", listed);
        Assert.Equal("text", File.ReadAllText(Path.Combine(renamed, "notes.md")));
    }

    [Fact]
    public async Task ARenameOntoANameSomethingElseHas_IsRefused()
    {
        using TempTreeFolder folder = new();
        string path = folder.AddFile("notes.md", "mine");
        folder.AddFile("todo.md", "yours");

        FileOperationException failure = await Assert.ThrowsAsync<FileOperationException>(
            () => FileOperations.RenameAsync(path, "todo.md", TestContext.Current.CancellationToken));

        Assert.Equal("There is already a file called 'todo.md' here.", failure.Message);
        Assert.Equal("yours", File.ReadAllText(folder.Combine("todo.md")));
    }

    [Fact]
    public async Task ARenameOfSomethingThatIsGone_IsRefused()
    {
        using TempTreeFolder folder = new();

        await Assert.ThrowsAsync<FileOperationException>(
            () => FileOperations.RenameAsync(folder.Combine("gone.md"), "todo.md", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AMoveWithNothingInItsWay_PutsTheEntryInTheTargetFolder()
    {
        using TempTreeFolder folder = new();
        string source = folder.AddFile("notes.md", "text");
        string target = folder.AddDirectory("drafts");
        FileMovePlan plan = FileMovePlanner.Plan(source, target);

        string moved = await FileOperations.MoveAsync(
            plan,
            MoveResolution.KeepBoth,
            TestContext.Current.CancellationToken);

        // A plan with nothing in its way ignores the answer: there was no question to answer.
        Assert.Equal(Path.Combine(target, "notes.md"), moved);
        Assert.False(File.Exists(source));
        Assert.Equal("text", File.ReadAllText(moved));
    }

    [Fact]
    public async Task AConflictAnsweredWithOverwrite_ReplacesTheFileThatWasThere()
    {
        using TempTreeFolder folder = new();
        string source = folder.AddFile(Path.Combine("inbox", "notes.md"), "new");
        string target = folder.AddDirectory("drafts");
        folder.AddFile(Path.Combine("drafts", "notes.md"), "old");
        FileMovePlan plan = FileMovePlanner.Plan(source, target);

        string moved = await FileOperations.MoveAsync(
            plan,
            MoveResolution.Overwrite,
            TestContext.Current.CancellationToken);

        Assert.Equal(FileMovePlanStatus.Conflict, plan.Status);
        Assert.Equal(Path.Combine(target, "notes.md"), moved);
        Assert.Equal("new", File.ReadAllText(moved));
        Assert.False(File.Exists(source));
    }

    [Fact]
    public async Task AConflictAnsweredWithOverwrite_ReplacesTheFolderThatWasThereWithEverythingInIt()
    {
        using TempTreeFolder folder = new();
        string source = folder.AddDirectory(Path.Combine("inbox", "drafts"));
        folder.AddFile(Path.Combine("inbox", "drafts", "new.md"));
        string target = folder.AddDirectory("archive");
        folder.AddFile(Path.Combine("archive", "drafts", "old.md"));
        FileMovePlan plan = FileMovePlanner.Plan(source, target);

        string moved = await FileOperations.MoveAsync(
            plan,
            MoveResolution.Overwrite,
            TestContext.Current.CancellationToken);

        Assert.Equal(FileMovePlanStatus.Conflict, plan.Status);
        Assert.Equal(Path.Combine(target, "drafts"), moved);
        Assert.Equal(["new.md"], Directory.GetFiles(moved).Select(file => Path.GetFileName(file)));
        Assert.False(Directory.Exists(source));
    }

    [Fact]
    public async Task AConflictAnsweredWithKeepBoth_NumbersTheEntryBeingMoved()
    {
        using TempTreeFolder folder = new();
        string source = folder.AddFile(Path.Combine("inbox", "notes.md"), "new");
        string target = folder.AddDirectory("drafts");
        folder.AddFile(Path.Combine("drafts", "notes.md"), "old");
        FileMovePlan plan = FileMovePlanner.Plan(source, target);

        string moved = await FileOperations.MoveAsync(
            plan,
            MoveResolution.KeepBoth,
            TestContext.Current.CancellationToken);

        Assert.Equal(Path.Combine(target, "notes (2).md"), moved);
        Assert.Equal("new", File.ReadAllText(moved));
        Assert.Equal("old", File.ReadAllText(Path.Combine(target, "notes.md")));
    }

    [Fact]
    public async Task AMoveTheUserCancelled_IsNeverCarriedOut()
    {
        using TempTreeFolder folder = new();
        string source = folder.AddFile("notes.md");
        string target = folder.AddDirectory("drafts");
        FileMovePlan plan = FileMovePlanner.Plan(source, target);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => FileOperations.MoveAsync(plan, MoveResolution.Cancel, TestContext.Current.CancellationToken));
        Assert.True(File.Exists(source));
    }

    [Fact]
    public async Task AMoveThePlannerRefused_IsRefusedAgainWithItsOwnSentence()
    {
        using TempTreeFolder folder = new();
        string source = folder.AddFile("notes.md");
        FileMovePlan plan = FileMovePlanner.Plan(source, folder.Root);

        FileOperationException failure = await Assert.ThrowsAsync<FileOperationException>(
            () => FileOperations.MoveAsync(plan, MoveResolution.Overwrite, TestContext.Current.CancellationToken));

        Assert.Equal(plan.Rejection, failure.Message);
        Assert.True(File.Exists(source));
    }

    [Fact]
    public async Task ADeletedFile_IsGone()
    {
        using TempTreeFolder folder = new();
        string path = folder.AddFile("notes.md", "text");

        await FileOperations.DeleteAsync(path, TestContext.Current.CancellationToken);

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task ADeletedFolder_TakesEverythingUnderItWithIt()
    {
        using TempTreeFolder folder = new();
        string path = folder.AddDirectory("drafts");
        folder.AddFile(Path.Combine("drafts", "2024", "notes.md"), "text");

        await FileOperations.DeleteAsync(path, TestContext.Current.CancellationToken);

        Assert.False(Directory.Exists(path));
        Assert.Empty(Directory.GetFileSystemEntries(folder.Root));
    }

    [Fact]
    public async Task DeletingSomethingThatIsAlreadyGone_IsNotAFailure()
    {
        using TempTreeFolder folder = new();

        // The caller asked for it not to be there, and it is not there.
        await FileOperations.DeleteAsync(folder.Combine("gone.md"), TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ANamelessPathToDelete_IsRejected(string? path)
        => await Assert.ThrowsAnyAsync<ArgumentException>(
            () => FileOperations.DeleteAsync(path!, TestContext.Current.CancellationToken));
}
