using System;
using System.IO;
using Enigma.MarkdownEditor.Core.Files;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Files;

/// <summary>
/// Tests for what a drop in the tree would do, over real folders. Every branch is a different thing
/// the user sees — a move, a dialog, or a sentence — so all of them are pinned.
/// </summary>
public sealed class FileMovePlannerTests
{
    [Fact]
    public void AMoveWithNothingInItsWay_IsReady()
    {
        using TempTreeFolder folder = new();
        string source = folder.AddFile("notes.md");
        string target = folder.AddDirectory("drafts");

        FileMovePlan plan = FileMovePlanner.Plan(source, target);

        Assert.Equal(FileMovePlanStatus.Ready, plan.Status);
        Assert.Null(plan.Rejection);
        Assert.Equal(source, plan.SourcePath);
        Assert.Equal(folder.Root, plan.SourceDirectory);
        Assert.Equal("notes.md", plan.Name);
        Assert.Equal(target, plan.TargetDirectory);
        Assert.Equal(Path.Combine(target, "notes.md"), plan.DestinationPath);
        Assert.False(plan.IsDirectory);
    }

    [Fact]
    public void AFolderMovedIntoASibling_IsReadyAndKnowsItIsAFolder()
    {
        using TempTreeFolder folder = new();
        string source = folder.AddDirectory("drafts");
        string target = folder.AddDirectory("archive");

        FileMovePlan plan = FileMovePlanner.Plan(source, target);

        Assert.Equal(FileMovePlanStatus.Ready, plan.Status);
        Assert.True(plan.IsDirectory);
    }

    [Fact]
    public void ASourceThatIsNotThere_IsRejected()
    {
        using TempTreeFolder folder = new();
        string target = folder.AddDirectory("drafts");

        FileMovePlan plan = FileMovePlanner.Plan(folder.Combine("gone.md"), target);

        Assert.Equal(FileMovePlanStatus.Rejected, plan.Status);
        Assert.Equal("'gone.md' is no longer there.", plan.Rejection);
    }

    [Fact]
    public void ATargetThatIsNotThere_IsRejected()
    {
        using TempTreeFolder folder = new();
        string source = folder.AddFile("notes.md");

        FileMovePlan plan = FileMovePlanner.Plan(source, folder.Combine("gone"));

        Assert.Equal(FileMovePlanStatus.Rejected, plan.Status);
        Assert.Equal("The folder 'gone' is no longer there.", plan.Rejection);
    }

    [Fact]
    public void AFolderDroppedOnItself_IsRejected()
    {
        using TempTreeFolder folder = new();
        string source = folder.AddDirectory("drafts");

        FileMovePlan plan = FileMovePlanner.Plan(source, source);

        Assert.Equal(FileMovePlanStatus.Rejected, plan.Status);
        Assert.Equal("A folder cannot be moved into itself.", plan.Rejection);
    }

    [Fact]
    public void AFolderDroppedOnSomethingUnderneathIt_IsRejected()
    {
        using TempTreeFolder folder = new();
        string source = folder.AddDirectory("drafts");
        string target = folder.AddDirectory(Path.Combine("drafts", "2024", "march"));

        FileMovePlan plan = FileMovePlanner.Plan(source, target);

        Assert.Equal(FileMovePlanStatus.Rejected, plan.Status);
        Assert.Equal("A folder cannot be moved into itself.", plan.Rejection);
    }

    [Fact]
    public void AFolderDroppedOnOneThatMerelyStartsWithItsName_IsNotMistakenForItsOwnChild()
    {
        using TempTreeFolder folder = new();
        string source = folder.AddDirectory("notes");
        string target = folder.AddDirectory("notes-archive");

        FileMovePlan plan = FileMovePlanner.Plan(source, target);

        Assert.Equal(FileMovePlanStatus.Ready, plan.Status);
    }

    [Fact]
    public void AnEntryDroppedOnTheFolderItIsAlreadyIn_IsRejected()
    {
        using TempTreeFolder folder = new();
        string source = folder.AddFile("notes.md");

        FileMovePlan plan = FileMovePlanner.Plan(source, folder.Root);

        Assert.Equal(FileMovePlanStatus.Rejected, plan.Status);
        Assert.Equal("'notes.md' is already in this folder.", plan.Rejection);
    }

    [Fact]
    public void ADestinationNameThatIsTaken_IsAConflict()
    {
        using TempTreeFolder folder = new();
        string source = folder.AddFile("notes.md");
        string target = folder.AddDirectory("drafts");
        folder.AddFile(Path.Combine("drafts", "notes.md"));

        FileMovePlan plan = FileMovePlanner.Plan(source, target);

        Assert.Equal(FileMovePlanStatus.Conflict, plan.Status);
        Assert.Null(plan.Rejection);
        Assert.Equal(Path.Combine(target, "notes.md"), plan.DestinationPath);
    }

    [Fact]
    public void ADestinationNameTakenByAFolder_IsAConflictJustTheSame()
    {
        using TempTreeFolder folder = new();
        string source = folder.AddFile("notes.md");
        string target = folder.AddDirectory("drafts");
        folder.AddDirectory(Path.Combine("drafts", "notes.md"));

        FileMovePlan plan = FileMovePlanner.Plan(source, target);

        Assert.Equal(FileMovePlanStatus.Conflict, plan.Status);
    }

    [Fact]
    public void APathTheRuntimeCannotEvenResolve_IsRejectedRatherThanThrown()
    {
        using TempTreeFolder folder = new();
        string target = folder.AddDirectory("drafts");

        // A drag can carry text that was never a path. That is a refused drop, not a crash.
        FileMovePlan plan = FileMovePlanner.Plan(folder.Combine("bad\0name.md"), target);

        Assert.Equal(FileMovePlanStatus.Rejected, plan.Status);
        Assert.NotNull(plan.Rejection);
    }

    [Fact]
    public void ATargetPathTheRuntimeCannotEvenResolve_IsRejectedRatherThanThrown()
    {
        using TempTreeFolder folder = new();
        string source = folder.AddFile("notes.md");

        FileMovePlan plan = FileMovePlanner.Plan(source, folder.Combine("bad\0folder"));

        Assert.Equal(FileMovePlanStatus.Rejected, plan.Status);
        Assert.NotNull(plan.Rejection);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ANamelessSource_IsRejected(string? sourcePath)
    {
        using TempTreeFolder folder = new();

        Assert.ThrowsAny<ArgumentException>(() => FileMovePlanner.Plan(sourcePath!, folder.Root));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ANamelessTarget_IsRejected(string? targetDirectory)
    {
        using TempTreeFolder folder = new();
        string source = folder.AddFile("notes.md");

        Assert.ThrowsAny<ArgumentException>(() => FileMovePlanner.Plan(source, targetDirectory!));
    }
}
