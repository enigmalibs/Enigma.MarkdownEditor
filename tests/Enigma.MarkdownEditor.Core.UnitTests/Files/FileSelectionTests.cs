using System;
using System.IO;
using Enigma.MarkdownEditor.Core.Files;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Files;

/// <summary>
/// Tests for what a multi-selection in the tree is reduced to before a delete acts on it. Pure path
/// arithmetic: none of these paths exists, and none needs to.
/// </summary>
public sealed class FileSelectionTests
{
    private static readonly string Notes = Path.Combine(Path.GetTempPath(), "selection", "notes");

    [Fact]
    public void NothingSelected_IsNothing()
        => Assert.Empty(FileSelection.Outermost([]));

    [Fact]
    public void SeparateEntries_AreAllKeptInTheOrderGiven()
    {
        string b = Path.Combine(Notes, "b.md");
        string a = Path.Combine(Notes, "a.md");
        string drafts = Path.Combine(Notes, "drafts");

        Assert.Equal([b, a, drafts], FileSelection.Outermost([b, a, drafts]));
    }

    [Fact]
    public void ThePathTwice_IsKeptOnce_AsFirstGiven()
    {
        string drafts = Path.Combine(Notes, "drafts");

        Assert.Equal([drafts], FileSelection.Outermost([drafts, drafts + Path.DirectorySeparatorChar, drafts]));
    }

    [Fact]
    public void AnEntryDirectlyInsideASelectedFolder_IsDropped()
    {
        string drafts = Path.Combine(Notes, "drafts");

        // The child first: which one was clicked first must not decide which one survives.
        Assert.Equal([drafts], FileSelection.Outermost([Path.Combine(drafts, "idea.md"), drafts]));
    }

    [Fact]
    public void AnEntryDeepInsideASelectedFolder_IsDropped()
    {
        string deep = Path.Combine(Notes, "drafts", "2026", "october", "idea.md");
        string elsewhere = Path.Combine(Path.GetDirectoryName(Notes)!, "elsewhere.md");

        Assert.Equal([Notes, elsewhere], FileSelection.Outermost([Notes, deep, elsewhere]));
    }

    [Fact]
    public void ASiblingThatOnlySharesAPrefix_IsKept()
    {
        string notesOld = Notes + "-old";

        Assert.Equal([Notes, notesOld], FileSelection.Outermost([Notes, notesOld]));
    }

    [Fact]
    public void Case_TellsTwoPathsApartOnlyWhereThePlatformDoes()
    {
        string folder = Path.Combine(Notes, "Drafts");
        string inside = Path.Combine(Notes, "drafts", "idea.md");

        string[] expected = OperatingSystem.IsWindows() ? [folder] : [folder, inside];

        Assert.Equal(expected, FileSelection.Outermost([folder, inside]));
    }

    [Fact]
    public void NoPaths_IsRejected()
        => Assert.Throws<ArgumentNullException>(() => FileSelection.Outermost(null!));

    [Fact]
    public void ANullPath_IsRejected()
        => Assert.Throws<ArgumentNullException>(() => FileSelection.Outermost([Notes, null!]));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyPath_IsRejected(string path)
        => Assert.Throws<ArgumentException>(() => FileSelection.Outermost([Notes, path]));
}
