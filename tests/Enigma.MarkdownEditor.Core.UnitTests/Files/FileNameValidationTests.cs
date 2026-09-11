using System;
using System.IO;
using Enigma.MarkdownEditor.Core.Files;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Files;

/// <summary>
/// Tests for the rules a name has to satisfy. They are what stands between a user and a workspace that
/// only opens on the machine it was made on, so every rule is pinned rather than sampled.
/// </summary>
public sealed class FileNameValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ANameWithNothingInIt_IsRejected(string? name)
        => Assert.Equal("A name cannot be empty.", FileNameValidation.Validate(name, isDirectory: false));

    [Theory]
    [InlineData("notes/2024.md")]
    [InlineData(@"notes\2024.md")]
    [InlineData("/notes.md")]
    public void ANameThatIsReallyAPath_IsRejected(string name)
        => Assert.Equal(@"A name cannot contain \ or /.", FileNameValidation.Validate(name, isDirectory: false));

    [Fact]
    public void ANameHoldingACharacterThePlatformForbids_IsRejected()
    {
        // Asked of the platform's own list rather than of a hard-coded one, so the test says the same
        // thing on Windows, where it is long, and on Linux, where it is two characters.
        foreach (char forbidden in Path.GetInvalidFileNameChars())
        {
            Assert.NotNull(FileNameValidation.Validate($"note{forbidden}s.md", isDirectory: false));
        }
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    public void ADotOrTwo_IsRejected(string name)
    {
        Assert.NotNull(FileNameValidation.Validate(name, isDirectory: false));
        Assert.NotNull(FileNameValidation.Validate(name, isDirectory: true));
    }

    [Theory]
    [InlineData("notes ")]
    [InlineData("notes.md ")]
    [InlineData("notes.")]
    [InlineData("drafts.")]
    public void ANameEndingInASpaceOrADot_IsRejectedOnEveryPlatform(string name)
        => Assert.Equal(
            "A name cannot end with a space or a dot.",
            FileNameValidation.Validate(name, isDirectory: false));

    [Theory]
    [InlineData("CON")]
    [InlineData("con")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("nul.md")]
    [InlineData("COM1")]
    [InlineData("lpt9.tar.gz")]
    public void AWindowsDeviceName_IsRejectedOnEveryPlatform(string name)
        => Assert.NotNull(FileNameValidation.Validate(name, isDirectory: false));

    [Theory]
    [InlineData("CONSOLE.md")]
    [InlineData("nullify.md")]
    [InlineData("COM10")]
    [InlineData("LPT0")]
    public void ANameThatMerelyBeginsLikeADeviceName_IsAccepted(string name)
        => Assert.Null(FileNameValidation.Validate(name, isDirectory: false));

    [Fact]
    public void ANameLongerThanTheLimit_IsRejected()
    {
        string tooLong = new('a', FileNameValidation.MaxNameLength + 1);
        string theLimit = new('a', FileNameValidation.MaxNameLength);

        Assert.Equal(
            $"A name cannot be longer than {FileNameValidation.MaxNameLength} characters.",
            FileNameValidation.Validate(tooLong, isDirectory: false));
        Assert.Null(FileNameValidation.Validate(theLimit, isDirectory: false));
    }

    [Theory]
    [InlineData("notes.md")]
    [InlineData("drafts")]
    [InlineData(".gitignore")]
    [InlineData("archive.tar.gz")]
    [InlineData("notes (2).md")]
    [InlineData("Réunion du 4 mars.md")]
    public void AnOrdinaryName_IsAccepted(string name)
        => Assert.Null(FileNameValidation.Validate(name, isDirectory: false));

    [Fact]
    public void AFreeName_IsAcceptedInItsFolder()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("notes.md");

        Assert.Null(FileNameValidation.ValidateIn("todo.md", folder.Root, isDirectory: false));
    }

    [Fact]
    public void ANameAFileAlreadyHas_IsACollision()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("notes.md");

        Assert.Equal(
            "There is already a file called 'notes.md' here.",
            FileNameValidation.ValidateIn("notes.md", folder.Root, isDirectory: false));
    }

    [Fact]
    public void ANameAFolderAlreadyHas_IsACollision()
    {
        using TempTreeFolder folder = new();
        folder.AddDirectory("drafts");

        Assert.Equal(
            "There is already a folder called 'drafts' here.",
            FileNameValidation.ValidateIn("drafts", folder.Root, isDirectory: true));
    }

    [Fact]
    public void AFileAndAFolder_CollideWithEachOther()
    {
        using TempTreeFolder folder = new();
        folder.AddDirectory("drafts");

        // Nothing can be called "drafts" here, whatever kind of thing it would have been.
        Assert.NotNull(FileNameValidation.ValidateIn("drafts", folder.Root, isDirectory: false));
    }

    [Fact]
    public void AnEntryKeepingItsOwnName_IsNotACollision()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("notes.md");

        Assert.Null(FileNameValidation.ValidateIn("notes.md", folder.Root, isDirectory: false, currentName: "notes.md"));
    }

    [Fact]
    public void AnEntryChangingOnlyTheCaseOfItsOwnName_IsNotACollision()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("notes.md");

        Assert.Null(FileNameValidation.ValidateIn("Notes.md", folder.Root, isDirectory: false, currentName: "notes.md"));
    }

    [Fact]
    public void AnEntryTakingAnotherEntrysName_IsStillACollision()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("notes.md");
        folder.AddFile("todo.md");

        Assert.Equal(
            "There is already a file called 'notes.md' here.",
            FileNameValidation.ValidateIn("notes.md", folder.Root, isDirectory: false, currentName: "todo.md"));
    }

    [Fact]
    public void ABrokenName_IsReportedBeforeTheFolderIsLookedAt()
    {
        using TempTreeFolder folder = new();

        Assert.Equal("A name cannot be empty.", FileNameValidation.ValidateIn("   ", folder.Root, isDirectory: false));
    }

    [Fact]
    public void AFolderThatIsNotThere_HoldsNothingToCollideWith()
    {
        using TempTreeFolder folder = new();

        Assert.Null(FileNameValidation.ValidateIn("notes.md", folder.Combine("gone"), isDirectory: false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ANamelessFolder_IsRejected(string? parentDirectory)
        => Assert.ThrowsAny<ArgumentException>(
            () => FileNameValidation.ValidateIn("notes.md", parentDirectory!, isDirectory: false));
}
