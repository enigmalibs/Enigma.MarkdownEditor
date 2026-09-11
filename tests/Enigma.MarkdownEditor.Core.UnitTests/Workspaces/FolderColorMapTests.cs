using System;
using System.Collections.Generic;
using System.IO;
using Enigma.MarkdownEditor.Core.Workspaces;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Workspaces;

/// <summary>
/// Which colour a path answers to: the tree's question (is this folder the one carrying it?) and the
/// tab's (which coloured folder does this file belong to?).
/// </summary>
/// <remarks>
/// All of it is a decision over strings, so nothing here touches disk — which is also why an
/// unplugged root still answers with its colour. Paths are built from the running platform's own
/// temporary folder rather than written as literals, so the cases mean the same thing on Windows and
/// on Linux.
/// </remarks>
public sealed class FolderColorMapTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "colours"));

    [Fact]
    public void TheFolderThatCarriesTheColour_AnswersWithIt()
    {
        List<FolderColorEntry> entries = [Entry(Under("work"), FolderColor.Blue)];

        Assert.Equal(FolderColor.Blue, FolderColorMap.ColorOf(entries, Under("work")));
        Assert.Equal(FolderColor.Blue, FolderColorMap.Resolve(entries, Under("work")));
    }

    [Fact]
    public void AFolderInsideAColouredOne_InheritsButDoesNotCarry()
    {
        List<FolderColorEntry> entries = [Entry(Under("work"), FolderColor.Blue)];
        string child = Under("work", "projects");

        // The whole reason there are two methods: the tree shows where the colour was set, the tab
        // shows which coloured folder a file belongs to.
        Assert.Equal(FolderColor.None, FolderColorMap.ColorOf(entries, child));
        Assert.Equal(FolderColor.Blue, FolderColorMap.Resolve(entries, child));
    }

    [Fact]
    public void AFileUnderAColouredFolder_InheritsThroughEveryLevel()
    {
        List<FolderColorEntry> entries = [Entry(Under("work"), FolderColor.Teal)];

        Assert.Equal(
            FolderColor.Teal,
            FolderColorMap.Resolve(entries, Under("work", "projects", "q3", "notes.md")));
    }

    [Fact]
    public void AColouredSubFolder_OverridesItsColouredParent()
    {
        List<FolderColorEntry> entries =
        [
            Entry(Under("work"), FolderColor.Blue),
            Entry(Under("work", "projects"), FolderColor.Red),
        ];

        // Deepest wins: a colour set on a sub-folder is a deliberate override, not a mistake.
        Assert.Equal(FolderColor.Red, FolderColorMap.Resolve(entries, Under("work", "projects", "a.md")));
        Assert.Equal(FolderColor.Blue, FolderColorMap.Resolve(entries, Under("work", "a.md")));
    }

    [Fact]
    public void AFolderWhoseNameMerelyStartsTheSameWay_IsNotAChild()
    {
        List<FolderColorEntry> entries = [Entry(Under("work"), FolderColor.Blue)];

        // The case a prefix comparison gets wrong, which is why the walk goes through
        // Path.GetDirectoryName instead.
        Assert.Equal(FolderColor.None, FolderColorMap.Resolve(entries, Under("workshop", "a.md")));
    }

    [Fact]
    public void ATrailingSeparator_IsTheSameFolder()
    {
        List<FolderColorEntry> entries = [Entry(Under("work") + Path.DirectorySeparatorChar, FolderColor.Green)];

        Assert.Equal(FolderColor.Green, FolderColorMap.ColorOf(entries, Under("work")));
        Assert.Equal(FolderColor.Green, FolderColorMap.Resolve(entries, Under("work", "a.md")));
    }

    [Fact]
    public void ARelativePath_IsComparedAsTheFolderItResolvesTo()
    {
        List<FolderColorEntry> entries = [Entry(Under("work"), FolderColor.Purple)];

        Assert.Equal(
            FolderColor.Purple,
            FolderColorMap.ColorOf(entries, Path.Combine(Under("work", "projects"), "..")));
    }

    [Fact]
    public void CaseOnlyDiffers_WhereTheFileSystemSaysItDoes()
    {
        List<FolderColorEntry> entries = [Entry(Under("work"), FolderColor.Pink)];
        FolderColor answer = FolderColorMap.ColorOf(entries, Under("WORK"));

        // Windows folders are case-insensitive and Linux ones are not, and the comparer the rest of
        // the workspace code uses already says which is which.
        Assert.Equal(OperatingSystem.IsWindows() ? FolderColor.Pink : FolderColor.None, answer);
    }

    [Fact]
    public void AnEntrySetToNone_IsNoColourRatherThanAMatchToStopAt()
    {
        List<FolderColorEntry> entries =
        [
            Entry(Under("work"), FolderColor.Blue),
            Entry(Under("work", "projects"), FolderColor.None),
        ];

        // A hand-edited file can hold one. It must not shadow the colour above it.
        Assert.Equal(FolderColor.None, FolderColorMap.ColorOf(entries, Under("work", "projects")));
        Assert.Equal(FolderColor.Blue, FolderColorMap.Resolve(entries, Under("work", "projects", "a.md")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoPathAtAll_HasNoColour(string? path)
    {
        List<FolderColorEntry> entries = [Entry(Under("work"), FolderColor.Blue)];

        Assert.Equal(FolderColor.None, FolderColorMap.ColorOf(entries, path));
        Assert.Equal(FolderColor.None, FolderColorMap.Resolve(entries, path));
    }

    [Fact]
    public void NothingColoured_HasNoColour()
    {
        Assert.Equal(FolderColor.None, FolderColorMap.ColorOf([], Under("work")));
        Assert.Equal(FolderColor.None, FolderColorMap.Resolve([], Under("work", "a.md")));
        Assert.Equal(FolderColor.None, FolderColorMap.ColorOf(null, Under("work")));
        Assert.Equal(FolderColor.None, FolderColorMap.Resolve(null, Under("work", "a.md")));
    }

    [Fact]
    public void AWalkThatReachesTheDriveRoot_Stops()
    {
        // The termination case: nothing above the root carries a colour, and asking for its parent
        // has to end the walk rather than loop on it.
        Assert.Equal(
            FolderColor.None,
            FolderColorMap.Resolve([Entry(Under("work"), FolderColor.Blue)], Path.GetPathRoot(Root)));
    }

    private static string Under(params string[] segments) => Path.Combine([Root, .. segments]);

    private static FolderColorEntry Entry(string path, FolderColor color) => new() { Path = path, Color = color };
}
