using System;
using System.Collections.Generic;
using System.IO;
using Enigma.MarkdownEditor.Core.Workspaces;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Workspaces;

/// <summary>
/// The rules as the manage dialog asks them: each answer is the sentence the user reads, so these
/// tests are as much about what is said as about whether the value is accepted.
/// </summary>
public sealed class WorkspaceValidationTests : IDisposable
{
    private readonly TempWorkspacesFolder _folder = new();

    [Fact]
    public void AFreeName_IsAccepted()
        => Assert.Null(WorkspaceValidation.ValidateName("Notes", [Named("Documents")]));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ANameThatIsNotOne_IsRejected(string? name)
        => Assert.Equal("A workspace needs a name.", WorkspaceValidation.ValidateName(name, []));

    [Fact]
    public void ANameLongerThanTheLimit_IsRejected()
    {
        string tooLong = new('x', Workspace.MaxNameLength + 1);

        Assert.Contains($"{Workspace.MaxNameLength} characters", WorkspaceValidation.ValidateName(tooLong, []));
    }

    [Fact]
    public void ANameAtTheLimit_IsAccepted()
        => Assert.Null(WorkspaceValidation.ValidateName(new string('x', Workspace.MaxNameLength), []));

    [Theory]
    [InlineData("Documents")]
    [InlineData("documents")]
    [InlineData("  DOCUMENTS ")]
    public void ANameAlreadyTaken_IsRejectedWhateverItsCase(string name)
        => Assert.Contains("already a workspace", WorkspaceValidation.ValidateName(name, [Named("Documents")]));

    [Fact]
    public void AWorkspaceKeepingItsOwnName_IsNotADuplicateOfItself()
    {
        Workspace documents = Named("Documents");

        Assert.Null(WorkspaceValidation.ValidateName("Documents", [documents], documents.Id));
    }

    [Fact]
    public void AFolderTheWorkspaceDoesNotHave_IsAccepted()
        => Assert.Null(WorkspaceValidation.ValidateRootDirectory(
            _folder.ExistingDirectory("Other"),
            WithRoots(_folder.ExistingDirectory("Documents"))));

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void ANoFolderAtAll_IsRejected(string? directory)
        => Assert.Equal("Pick a folder to add.", WorkspaceValidation.ValidateRootDirectory(directory, WithRoots()));

    [Fact]
    public void AFolderTheWorkspaceAlreadyHas_IsRejectedEvenSpeltDifferently()
    {
        string documents = _folder.ExistingDirectory("Documents");
        Workspace workspace = WithRoots(documents);

        Assert.Contains(
            "already a folder",
            WorkspaceValidation.ValidateRootDirectory(documents + Path.DirectorySeparatorChar, workspace));
    }

    [Fact]
    public void AFolderThatIsNotThere_IsStillAccepted()
        => Assert.Null(WorkspaceValidation.ValidateRootDirectory(_folder.MissingDirectory("Unplugged"), WithRoots()));

    [Fact]
    public void NormalizeDirectory_MakesThePathAbsoluteAndDropsATrailingSeparator()
    {
        string documents = _folder.ExistingDirectory("Documents");

        Assert.Equal(documents, WorkspaceValidation.NormalizeDirectory(documents + Path.DirectorySeparatorChar));
        Assert.Equal(documents, WorkspaceValidation.NormalizeDirectory($"  {documents}  "));
    }

    [Fact]
    public void NormalizeDirectory_KeepsARootAsARoot()
    {
        string root = Path.GetPathRoot(_folder.ExistingDirectory("Documents"))!;

        // "C:\" trimmed to "C:" would name the current directory of that drive, not the drive.
        Assert.Equal(root, WorkspaceValidation.NormalizeDirectory(root));
    }

    [Fact]
    public void SuggestName_UsesTheFoldersOwnName()
        => Assert.Equal("Documents", WorkspaceValidation.SuggestName(_folder.ExistingDirectory("Documents"), []));

    [Fact]
    public void SuggestName_NumbersItWhenTheNameIsTaken()
    {
        string documents = _folder.ExistingDirectory("Documents");

        Assert.Equal("Documents (2)", WorkspaceValidation.SuggestName(documents, [Named("Documents")]));
        Assert.Equal(
            "Documents (3)",
            WorkspaceValidation.SuggestName(documents, [Named("Documents"), Named("Documents (2)")]));
    }

    [Fact]
    public void SuggestName_AlwaysProposesSomethingTheRulesAccept()
    {
        List<Workspace> existing = [Named(new string('x', Workspace.MaxNameLength))];

        string suggested = WorkspaceValidation.SuggestName(_folder.ExistingDirectory(new string('x', 80)), existing);

        Assert.Null(WorkspaceValidation.ValidateName(suggested, existing));
    }

    [Fact]
    public void MissingRootDirectories_ListsOnlyTheOnesThatAreGone()
    {
        Workspace workspace = WithRoots(
            _folder.ExistingDirectory("Here"),
            _folder.MissingDirectory("Gone"),
            _folder.ExistingDirectory("AlsoHere"));

        Assert.Equal([_folder.MissingDirectory("Gone")], WorkspaceValidation.MissingRootDirectories(workspace));
    }

    [Fact]
    public void DirectoryName_IsTheFoldersOwnNameOrTheWholePathForARoot()
    {
        string documents = _folder.ExistingDirectory("Documents");
        string root = Path.GetPathRoot(documents)!;

        Assert.Equal("Documents", WorkspaceValidation.DirectoryName(documents));
        Assert.Equal("Documents", WorkspaceValidation.DirectoryName(documents + Path.DirectorySeparatorChar));
        Assert.Equal(Path.TrimEndingDirectorySeparator(root), WorkspaceValidation.DirectoryName(root));
    }

    [Fact]
    public void EveryArgumentIsChecked()
    {
        Assert.Throws<ArgumentNullException>(() => WorkspaceValidation.ValidateName("Any", null!));
        Assert.Throws<ArgumentNullException>(() => WorkspaceValidation.ValidateRootDirectory("Any", null!));
        Assert.Throws<ArgumentNullException>(() => WorkspaceValidation.MissingRootDirectories(null!));
        Assert.ThrowsAny<ArgumentException>(() => WorkspaceValidation.NormalizeDirectory("  "));
    }

    public void Dispose() => _folder.Dispose();

    private static Workspace Named(string name) => new() { Name = name, RootDirectories = ["/anywhere"] };

    private static Workspace WithRoots(params string[] roots)
        => new() { Name = "Workspace", RootDirectories = [.. roots] };
}
