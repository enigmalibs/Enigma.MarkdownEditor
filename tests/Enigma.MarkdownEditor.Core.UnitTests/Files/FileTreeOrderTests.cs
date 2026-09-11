using System.Collections.Generic;
using System.Linq;
using Enigma.MarkdownEditor.Core.Files;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Files;

/// <summary>
/// Tests for the order the tree lists a directory in: directories first, then files, both by a
/// numeric-aware, case-insensitive comparison.
/// </summary>
public sealed class FileTreeOrderTests
{
    [Fact]
    public void DigitRuns_AreComparedAsNumbersRatherThanAsText()
    {
        List<string> names = ["chapter-10.md", "chapter-2.md", "chapter-1.md"];

        names.Sort(NaturalNameComparer.Instance);

        Assert.Equal(["chapter-1.md", "chapter-2.md", "chapter-10.md"], names);
    }

    [Fact]
    public void LeadingZeros_DoNotChangeTheNumber()
    {
        List<string> names = ["v007.md", "v10.md", "v8.md"];

        names.Sort(NaturalNameComparer.Instance);

        Assert.Equal(["v007.md", "v8.md", "v10.md"], names);
    }

    [Fact]
    public void ALongDigitRun_IsCompared_WithoutParsingIt()
    {
        // Longer than any integer type: parsing would overflow, so the comparison counts digits.
        const string bigger = "id-99999999999999999999999999.md";
        const string smaller = "id-99999999999999999999999998.md";

        Assert.True(NaturalNameComparer.Instance.Compare(smaller, bigger) < 0);
    }

    [Fact]
    public void Letters_AreComparedWithoutRegardToCase()
        => Assert.True(NaturalNameComparer.Instance.Compare("apple.md", "Banana.md") < 0);

    [Fact]
    public void NamesDifferingOnlyInCase_StayOrderedRatherThanBeingCalledEqual()
    {
        // Total order, so a sort over them is reproducible instead of depending on the input order.
        Assert.NotEqual(0, NaturalNameComparer.Instance.Compare("readme.md", "README.md"));
    }

    [Fact]
    public void APrefix_SortsBeforeTheLongerNameItStarts()
        => Assert.True(NaturalNameComparer.Instance.Compare("note", "note.md") < 0);

    [Fact]
    public void Nulls_AreOrderedRatherThanThrowing()
    {
        Assert.Equal(0, NaturalNameComparer.Instance.Compare(null, null));
        Assert.True(NaturalNameComparer.Instance.Compare(null, "a") < 0);
        Assert.True(NaturalNameComparer.Instance.Compare("a", null) > 0);
    }

    [Fact]
    public void Directories_ComeBeforeFilesWhateverTheirNames()
    {
        List<FileSystemEntry> entries =
        [
            new("/w/zebra.md", "zebra.md", isDirectory: false),
            new("/w/assets", "assets", isDirectory: true),
            new("/w/alpha.md", "alpha.md", isDirectory: false),
            new("/w/zzz", "zzz", isDirectory: true),
        ];

        entries.Sort(FileSystemEntryComparer.Instance);

        Assert.Equal(["assets", "zzz", "alpha.md", "zebra.md"], entries.Select(entry => entry.Name));
    }

    [Fact]
    public void TheEntryComparer_OrdersEachGroupNaturally()
    {
        List<FileSystemEntry> entries =
        [
            new("/w/part10", "part10", isDirectory: true),
            new("/w/part2", "part2", isDirectory: true),
        ];

        entries.Sort(FileSystemEntryComparer.Instance);

        Assert.Equal(["part2", "part10"], entries.Select(entry => entry.Name));
    }
}
