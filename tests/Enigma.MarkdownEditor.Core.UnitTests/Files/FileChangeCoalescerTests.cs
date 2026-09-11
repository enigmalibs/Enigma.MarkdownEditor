using System;
using System.IO;
using Enigma.MarkdownEditor.Core.Files;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Files;

/// <summary>
/// Tests for the reduction from a burst of file-system events to the directories the tree re-reads.
/// Pure, so a copy of twenty files can be asserted without copying twenty files.
/// </summary>
public sealed class FileChangeCoalescerTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "workspace"));

    [Fact]
    public void ManyChangesInOneDirectory_ComeOutAsThatDirectoryOnce()
    {
        FileSystemChange[] changes =
        [
            Created("a.md"),
            Created("b.md"),
            Created("c.md"),
            Deleted("a.md"),
        ];

        DirectoryChangeSet result = FileChangeCoalescer.Coalesce(changes, Root);

        Assert.False(result.RequiresFullRefresh);
        Assert.Equal([Root], result.Directories);
    }

    [Fact]
    public void AChangeDeepInTheTree_NamesItsOwnParentRatherThanTheRoot()
    {
        string nested = Path.Combine(Root, "notes", "2026");

        DirectoryChangeSet result = FileChangeCoalescer.Coalesce([Created(Path.Combine("notes", "2026", "a.md"))], Root);

        // Re-listing an ancestor would only read its immediate children, so it would not see this.
        Assert.Equal([nested], result.Directories);
    }

    [Fact]
    public void ARenameAcrossDirectories_NamesBothOfThem()
    {
        FileSystemChange moved = new(
            FileChangeKind.Renamed,
            Path.Combine(Root, "archive", "a.md"),
            Path.Combine(Root, "inbox", "a.md"));

        DirectoryChangeSet result = FileChangeCoalescer.Coalesce([moved], Root);

        Assert.Equal([Path.Combine(Root, "archive"), Path.Combine(Root, "inbox")], result.Directories);
    }

    [Fact]
    public void ARenameInPlace_NamesItsDirectoryOnce()
    {
        FileSystemChange renamed = new(
            FileChangeKind.Renamed,
            Path.Combine(Root, "b.md"),
            Path.Combine(Root, "a.md"));

        DirectoryChangeSet result = FileChangeCoalescer.Coalesce([renamed], Root);

        Assert.Equal([Root], result.Directories);
    }

    [Fact]
    public void ADirectoriesOrder_IsTheOrderItsFirstEventArrivedIn()
    {
        FileSystemChange[] changes =
        [
            Created(Path.Combine("beta", "x.md")),
            Created("y.md"),
            Created(Path.Combine("beta", "z.md")),
        ];

        DirectoryChangeSet result = FileChangeCoalescer.Coalesce(changes, Root);

        Assert.Equal([Path.Combine(Root, "beta"), Root], result.Directories);
    }

    [Fact]
    public void AResetAnywhereInTheBatch_WinsOverEverythingElse()
    {
        FileSystemChange[] changes =
        [
            Created("a.md"),
            new(FileChangeKind.Reset, Root),
            Created("b.md"),
        ];

        DirectoryChangeSet result = FileChangeCoalescer.Coalesce(changes, Root);

        // The watcher lost events, so nothing about the batch — before or after — can be trusted.
        Assert.True(result.RequiresFullRefresh);
        Assert.Empty(result.Directories);
        Assert.False(result.IsEmpty);
    }

    [Fact]
    public void AChangeOutsideTheRoot_IsDropped()
    {
        string elsewhere = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "workspace-other", "a.md"));

        DirectoryChangeSet result = FileChangeCoalescer.Coalesce(
            [new FileSystemChange(FileChangeKind.Created, elsewhere)],
            Root);

        // "workspace-other" starts with "workspace" without being under it — a plain prefix test
        // would have let it through.
        Assert.True(result.IsEmpty);
    }

    [Fact]
    public void NoChanges_MeanNothingToDo()
    {
        DirectoryChangeSet result = FileChangeCoalescer.Coalesce([], Root);

        Assert.True(result.IsEmpty);
        Assert.False(result.RequiresFullRefresh);
    }

    [Fact]
    public void ATrailingSeparatorOnTheRoot_DoesNotChangeTheAnswer()
    {
        DirectoryChangeSet result = FileChangeCoalescer.Coalesce([Created("a.md")], Root + Path.DirectorySeparatorChar);

        Assert.Equal([Root], result.Directories);
    }

    [Fact]
    public void ItsArguments_AreRequired()
    {
        Assert.Throws<ArgumentNullException>(() => FileChangeCoalescer.Coalesce(null!, Root));
        Assert.ThrowsAny<ArgumentException>(() => FileChangeCoalescer.Coalesce([], "  "));
        Assert.Throws<ArgumentNullException>(() => new FileSystemChange(FileChangeKind.Created, null!));
    }

    private static FileSystemChange Created(string relativePath)
        => new(FileChangeKind.Created, Path.Combine(Root, relativePath));

    private static FileSystemChange Deleted(string relativePath)
        => new(FileChangeKind.Deleted, Path.Combine(Root, relativePath));
}
