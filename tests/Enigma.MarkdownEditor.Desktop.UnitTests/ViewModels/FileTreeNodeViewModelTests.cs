using System;
using System.Collections.Generic;
using System.IO;
using Enigma.Icons.Phosphor;
using Enigma.MarkdownEditor.Core.Files;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.Controls;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.ViewModels;

/// <summary>
/// Tests for one row of the tree: what it is drawn as, what it says on hover, and the one thing it
/// does — ask for its children the first time it is opened.
/// </summary>
public sealed class FileTreeNodeViewModelTests
{
    [Fact]
    public void AFileNode_CarriesItsPathAndOpens()
    {
        FileTreeNodeViewModel node = FileTreeNodeViewModel.For(
            new FileSystemEntry(Path.Combine("/w", "a.md"), "a.md", isDirectory: false));

        Assert.True(node.IsFile);
        Assert.False(node.IsDirectory);
        Assert.False(node.IsInformational);
        Assert.Empty(node.Children);
        Assert.Equal(Path.Combine("/w", "a.md"), node.Tooltip);
    }

    [Fact]
    public void ADirectoryNode_StartsWithThePlaceholderThatGivesItAnExpander()
    {
        FileTreeNodeViewModel node = FileTreeNodeViewModel.For(
            new FileSystemEntry(Path.Combine("/w", "notes"), "notes", isDirectory: true));

        Assert.True(node.IsDirectory);
        Assert.Equal(FileTreeNodeKind.Loading, Assert.Single(node.Children).Kind);
    }

    [Fact]
    public void ExpandingADirectory_AsksForItsChildrenExactlyOnce()
    {
        List<FileTreeNodeViewModel> asked = [];
        FileTreeNodeViewModel node = FileTreeNodeViewModel.For(
            new FileSystemEntry(Path.Combine("/w", "notes"), "notes", isDirectory: true),
            asked.Add);

        node.IsExpanded = true;
        node.IsExpanded = true;

        Assert.Equal([node], asked);
    }

    [Fact]
    public void ExpandingAFile_AsksForNothing()
    {
        List<FileTreeNodeViewModel> asked = [];
        FileTreeNodeViewModel node = FileTreeNodeViewModel.For(
            new FileSystemEntry(Path.Combine("/w", "a.md"), "a.md", isDirectory: false),
            asked.Add);

        node.IsExpanded = true;

        Assert.Empty(asked);
    }

    [Fact]
    public void ARootThatIsThere_IsLabelledWithItsFolderNameAndHoversItsPath()
    {
        // As the workspaces file stores it: absolute, with no trailing separator.
        string directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));

        FileTreeNodeViewModel node = FileTreeNodeViewModel.Root(directory);

        Assert.True(node.IsRoot);
        Assert.Equal(FileTreeNodeKind.Directory, node.Kind);
        Assert.Equal(Path.GetFileName(directory), node.Name);

        // Two workspaces routinely hold a root called "docs"; the path is what tells them apart.
        Assert.Equal(directory, node.Tooltip);
    }

    [Fact]
    public void ARootThatIsNotThere_IsMissingAndHasNothingToExpand()
    {
        FileTreeNodeViewModel node = FileTreeNodeViewModel.Root(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.Equal(FileTreeNodeKind.Missing, node.Kind);
        Assert.True(node.IsMissing);
        Assert.Empty(node.Children);
        Assert.Contains("not there", node.Tooltip);
    }

    [Theory]
    [InlineData(FileTreeNodeKind.Loading, PhosphorIcon.CircleNotch)]
    [InlineData(FileTreeNodeKind.Empty, PhosphorIcon.FolderDashed)]
    [InlineData(FileTreeNodeKind.AccessDenied, PhosphorIcon.Lock)]
    [InlineData(FileTreeNodeKind.Unavailable, PhosphorIcon.Warning)]
    public void AnInformationalRow_HasItsOwnIconAndIsNotAnEntry(FileTreeNodeKind kind, PhosphorIcon icon)
    {
        FileTreeNodeViewModel node = FileTreeNodeViewModel.Informational("/w", kind);

        Assert.True(node.IsInformational);
        Assert.False(node.IsFile);
        Assert.False(node.IsDirectory);
        Assert.Equal(icon, node.IconKind);
    }

    [Fact]
    public void OnlyTheInformationalKinds_HaveAnInformationalRow()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => FileTreeNodeViewModel.Informational("/w", FileTreeNodeKind.File));

    [Theory]
    [InlineData(DirectoryListingStatus.Ok, FileTreeNodeKind.Empty)]
    [InlineData(DirectoryListingStatus.AccessDenied, FileTreeNodeKind.AccessDenied)]
    [InlineData(DirectoryListingStatus.Unavailable, FileTreeNodeKind.Unavailable)]
    [InlineData(DirectoryListingStatus.Missing, FileTreeNodeKind.Unavailable)]
    public void AListingsOutcome_MapsToTheRowThatExplainsIt(
        DirectoryListingStatus status,
        FileTreeNodeKind expected)
        => Assert.Equal(expected, FileTreeNodeViewModel.For("/w", status).Kind);

    [Fact]
    public void ADirectorysIcon_FollowsItsExpansion()
    {
        FileTreeNodeViewModel node = FileTreeNodeViewModel.For(
            new FileSystemEntry(Path.Combine("/w", "notes"), "notes", isDirectory: true));

        Assert.Equal(PhosphorIcon.Folder, node.IconKind);

        node.IsExpanded = true;

        Assert.Equal(PhosphorIcon.FolderOpen, node.IconKind);
    }

    [Theory]
    [InlineData("a.md", PhosphorIcon.FileMd)]
    [InlineData("a.markdown", PhosphorIcon.FileMd)]
    [InlineData("a.png", PhosphorIcon.File)]
    public void AFilesIcon_SaysWhetherItIsMarkdown(string name, PhosphorIcon expected)
    {
        FileTreeNodeViewModel node = FileTreeNodeViewModel.For(
            new FileSystemEntry(Path.Combine("/w", name), name, isDirectory: false));

        Assert.Equal(expected, node.IconKind);
    }

    [Fact]
    public void ChangingTheExpansion_NotifiesTheIconToo()
    {
        FileTreeNodeViewModel node = FileTreeNodeViewModel.For(
            new FileSystemEntry(Path.Combine("/w", "notes"), "notes", isDirectory: true));

        List<string?> raised = [];
        node.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        node.IsExpanded = true;
        node.IsExpanded = true;

        Assert.Equal([nameof(FileTreeNodeViewModel.IsExpanded), nameof(FileTreeNodeViewModel.IconKind)], raised);
    }

    [Fact]
    public void ANodeWithNoColour_HasNoBrushToPaintItsBarWith()
    {
        FileTreeNodeViewModel node = FileTreeNodeViewModel.For(
            new FileSystemEntry(Path.Combine("/w", "notes"), "notes", isDirectory: true));

        // Null rather than a transparent brush: the Border reading it paints nothing and still takes
        // its width, which is what keeps a coloured row and an uncoloured one the same shape.
        Assert.Equal(FolderColor.None, node.Color);
        Assert.Null(node.ColorBrush);
    }

    [Fact]
    public void ChangingTheColour_NotifiesItsBrushToo()
    {
        FileTreeNodeViewModel node = FileTreeNodeViewModel.For(
            new FileSystemEntry(Path.Combine("/w", "notes"), "notes", isDirectory: true));

        List<string?> raised = [];
        node.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        node.Color = FolderColor.Blue;
        node.Color = FolderColor.Blue;

        Assert.Equal(
            [nameof(FileTreeNodeViewModel.Color), nameof(FileTreeNodeViewModel.ColorBrush)],
            raised);
        Assert.Same(FolderColorPalette.Blue, node.ColorBrush);
    }

    [Fact]
    public void ItsArguments_AreChecked()
    {
        Assert.ThrowsAny<ArgumentException>(() => new FileTreeNodeViewModel("  ", "a", FileTreeNodeKind.File));
        Assert.ThrowsAny<ArgumentException>(() => new FileTreeNodeViewModel("/w/a", " ", FileTreeNodeKind.File));
        Assert.Throws<ArgumentNullException>(() => FileTreeNodeViewModel.For(null!));
        Assert.ThrowsAny<ArgumentException>(() => FileTreeNodeViewModel.Root("  "));
    }
}
