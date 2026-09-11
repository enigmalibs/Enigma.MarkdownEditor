using System;
using Enigma.MarkdownEditor.Core.Files;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Files;

/// <summary>
/// Tests for the numbering a "keep both" produces. The exact shape matters: it is what the user reads
/// in the tree afterwards to tell the two entries apart.
/// </summary>
public sealed class UniqueNameTests
{
    [Theory]
    [InlineData("notes.md", false)]
    [InlineData("drafts", true)]
    public void TheFirstAttempt_IsTheNameItself(string name, bool isDirectory)
        => Assert.Equal(name, UniqueName.Candidate(name, 1, isDirectory));

    [Fact]
    public void AFile_TakesItsNumberBeforeTheExtension()
        => Assert.Equal("notes (2).md", UniqueName.Candidate("notes.md", 2, isDirectory: false));

    [Fact]
    public void AMultiPartExtension_CountsAsItsLastPartOnly()
    {
        // Path.GetExtension's rule, kept rather than second-guessed: the number lands inside what a
        // person would call the extension, and the file still opens.
        Assert.Equal("archive.tar (2).gz", UniqueName.Candidate("archive.tar.gz", 2, isDirectory: false));
    }

    [Fact]
    public void AFileWithNoExtension_TakesItsNumberAtTheEnd()
        => Assert.Equal("Makefile (3)", UniqueName.Candidate("Makefile", 3, isDirectory: false));

    [Fact]
    public void ADotfile_TakesItsNumberAtTheEnd()
        => Assert.Equal(".gitignore (2)", UniqueName.Candidate(".gitignore", 2, isDirectory: false));

    [Theory]
    [InlineData("drafts", 2, "drafts (2)")]
    [InlineData("v1.2", 3, "v1.2 (3)")]
    public void AFolder_KeepsItsWholeNameAndTakesTheNumberAtTheEnd(string name, int attempt, string expected)
        => Assert.Equal(expected, UniqueName.Candidate(name, attempt, isDirectory: true));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AnAttemptBeforeTheFirst_IsRejected(int attempt)
        => Assert.Throws<ArgumentOutOfRangeException>(() => UniqueName.Candidate("notes.md", attempt, isDirectory: false));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ANamelessCandidate_IsRejected(string? name)
        => Assert.ThrowsAny<ArgumentException>(() => UniqueName.Candidate(name!, 2, isDirectory: false));

    [Fact]
    public void ANameNothingIsUsing_IsTakenAsItIs()
    {
        using TempTreeFolder folder = new();

        Assert.Equal("notes.md", UniqueName.In(folder.Root, "notes.md", isDirectory: false));
    }

    [Fact]
    public void EveryNameAlreadyThere_IsSkippedOver()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("notes.md");
        folder.AddFile("notes (2).md");

        Assert.Equal("notes (3).md", UniqueName.In(folder.Root, "notes.md", isDirectory: false));
    }

    [Fact]
    public void AFolderOfThatName_IsSkippedOverToo()
    {
        using TempTreeFolder folder = new();

        // A file and a folder cannot share a name, so a folder in the way has to be numbered past
        // exactly as a file would be.
        folder.AddDirectory("notes.md");

        Assert.Equal("notes (2).md", UniqueName.In(folder.Root, "notes.md", isDirectory: false));
    }

    [Fact]
    public void AFolderName_IsNumberedInItsOwnShape()
    {
        using TempTreeFolder folder = new();
        folder.AddDirectory("drafts");

        Assert.Equal("drafts (2)", UniqueName.In(folder.Root, "drafts", isDirectory: true));
    }

    [Fact]
    public void AFolderWithEveryNumberedNameTakenUp_GivesUpRatherThanLoopsForEver()
    {
        using TempTreeFolder folder = new();

        // The documented bound is a thousand attempts, so a thousand names have to be in the way
        // before the refusal is the one under test rather than a slow success.
        for (int attempt = 1; attempt <= 1000; attempt++)
        {
            folder.AddFile(UniqueName.Candidate("n.md", attempt, isDirectory: false));
        }

        Assert.Throws<FileOperationException>(() => UniqueName.In(folder.Root, "n.md", isDirectory: false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ANamelessFolder_IsRejected(string? directory)
        => Assert.ThrowsAny<ArgumentException>(() => UniqueName.In(directory!, "notes.md", isDirectory: false));
}
