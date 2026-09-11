using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Core.Files;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Files;

/// <summary>
/// Tests for the one place the tree touches the file system, over real directories.
/// </summary>
public sealed class DirectoryListerTests
{
    [Fact]
    public async Task ADirectory_IsListedFilteredAndOrdered()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("zebra.md");
        folder.AddFile("alpha.md");
        folder.AddFile("photo.png");
        folder.AddDirectory("notes");
        folder.AddDirectory("assets");

        DirectoryListing listing = await DirectoryLister.ListAsync(
            folder.Root,
            showAllFiles: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryListingStatus.Ok, listing.Status);
        Assert.Equal(["assets", "notes", "alpha.md", "zebra.md"], listing.Entries.Select(entry => entry.Name));
    }

    [Fact]
    public async Task WithShowAllFiles_EverythingButTheHiddenEntriesIsListed()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("notes.md");
        folder.AddFile("photo.png");

        DirectoryListing listing = await DirectoryLister.ListAsync(
            folder.Root,
            showAllFiles: true,
            TestContext.Current.CancellationToken);

        Assert.Equal(["notes.md", "photo.png"], listing.Entries.Select(entry => entry.Name));
    }

    [Fact]
    public async Task DotEntries_AreExcludedEvenWithShowAllFiles()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("notes.md");
        folder.AddDirectory(".git");
        folder.AddFile(".env");

        DirectoryListing listing = await DirectoryLister.ListAsync(
            folder.Root,
            showAllFiles: true,
            TestContext.Current.CancellationToken);

        Assert.Equal(["notes.md"], listing.Entries.Select(entry => entry.Name));
    }

    [Fact]
    public async Task AnEntryTheFileSystemMarksHidden_IsExcludedEvenWithShowAllFiles()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("visible.md");
        TempTreeFolder.Hide(folder.AddFile("secret.md"));

        DirectoryListing listing = await DirectoryLister.ListAsync(
            folder.Root,
            showAllFiles: true,
            TestContext.Current.CancellationToken);

        // On Windows the attribute is what actually flags a hidden entry; elsewhere Hide is a no-op
        // and the file is simply listed, which is the correct answer on that platform.
        string[] expected = System.OperatingSystem.IsWindows() ? ["visible.md"] : ["secret.md", "visible.md"];

        Assert.Equal(expected, listing.Entries.Select(entry => entry.Name));
    }

    [Fact]
    public async Task AnEmptyDirectory_IsReadRatherThanFailed()
    {
        using TempTreeFolder folder = new();

        DirectoryListing listing = await DirectoryLister.ListAsync(
            folder.Root,
            showAllFiles: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryListingStatus.Ok, listing.Status);
        Assert.True(listing.IsEmpty);
    }

    [Fact]
    public async Task ADirectoryHoldingOnlyHiddenThings_ReadsAsEmpty()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("photo.png");
        folder.AddDirectory(".git");

        DirectoryListing listing = await DirectoryLister.ListAsync(
            folder.Root,
            showAllFiles: false,
            TestContext.Current.CancellationToken);

        Assert.True(listing.IsEmpty);
    }

    [Fact]
    public async Task ADirectoryThatIsNotThere_IsReportedAsMissing()
    {
        using TempTreeFolder folder = new();

        DirectoryListing listing = await DirectoryLister.ListAsync(
            folder.Combine("gone"),
            showAllFiles: false,
            TestContext.Current.CancellationToken);

        Assert.Equal(DirectoryListingStatus.Missing, listing.Status);
        Assert.Empty(listing.Entries);
        Assert.False(listing.IsEmpty);
    }

    [Fact]
    public async Task EachEntry_CarriesItsFullPathAndWhetherItIsADirectory()
    {
        using TempTreeFolder folder = new();
        string notes = folder.AddDirectory("notes");
        string readme = folder.AddFile("readme.md");

        DirectoryListing listing = await DirectoryLister.ListAsync(
            folder.Root,
            showAllFiles: false,
            TestContext.Current.CancellationToken);

        Assert.Collection(
            listing.Entries,
            entry =>
            {
                Assert.True(entry.IsDirectory);
                Assert.Equal(notes, entry.FullPath);
            },
            entry =>
            {
                Assert.False(entry.IsDirectory);
                Assert.Equal(readme, entry.FullPath);
            });
    }

    [Fact]
    public async Task OnlyOneLevel_IsRead()
    {
        using TempTreeFolder folder = new();
        folder.AddDirectory("notes");
        folder.AddFile(Path.Combine("notes", "deep.md"));

        DirectoryListing listing = await DirectoryLister.ListAsync(
            folder.Root,
            showAllFiles: false,
            TestContext.Current.CancellationToken);

        // Lazy expansion is what keeps a workspace over a hundred thousand files affordable.
        Assert.Equal(["notes"], listing.Entries.Select(entry => entry.Name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ANamelessDirectory_IsRejected(string? directory)
        => await Assert.ThrowsAnyAsync<System.ArgumentException>(
            () => DirectoryLister.ListAsync(directory!, showAllFiles: false, TestContext.Current.CancellationToken));

    [Fact]
    public void AFailedListing_NeedsAReason()
        => Assert.Throws<System.ArgumentOutOfRangeException>(
            () => DirectoryListing.Failed(DirectoryListingStatus.Ok));
}
