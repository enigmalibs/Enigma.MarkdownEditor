using System;
using System.Collections.Generic;
using Enigma.MarkdownEditor.Core.Markdown;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Markdown;

/// <summary>
/// The continuation matrix: what Enter, Tab and Shift+Tab do on every kind of markdown item.
/// </summary>
/// <remarks>
/// These are the rules a user meets on every second keystroke, and the ones a wrong answer in is
/// noticed immediately — a bullet that does not repeat, a number that does not count on, a list that
/// cannot be ended. Everything here is a decision over strings, so all of it is reachable without an
/// editor.
/// </remarks>
public sealed class ListContinuationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("just a line")]
    [InlineData("# a heading")]
    [InlineData("---")]
    [InlineData("***")]
    [InlineData("**bold** at the start")]
    [InlineData("-x")]
    [InlineData("1.no space")]
    [InlineData("1a. not a number")]
    public void ALineThatIsNotAnItem_DoesNotParse(string? line) => Assert.Null(ListContinuation.Parse(line));

    [Theory]
    [InlineData("- item", ListMarkerKind.Bullet, "-")]
    [InlineData("* item", ListMarkerKind.Bullet, "*")]
    [InlineData("+ item", ListMarkerKind.Bullet, "+")]
    [InlineData("1. item", ListMarkerKind.Ordered, "1.")]
    [InlineData("42) item", ListMarkerKind.Ordered, "42)")]
    [InlineData("> quoted", ListMarkerKind.None, "")]
    public void AnItem_ParsesToItsMarker(string line, ListMarkerKind kind, string marker)
    {
        ListItemPrefix prefix = Assert.IsType<ListItemPrefix>(ListContinuation.Parse(line));

        Assert.Equal(kind, prefix.Kind);
        Assert.Equal(marker, prefix.Marker);
    }

    [Fact]
    public void ATaskItem_ParsesItsBoxSeparatelyFromItsText()
    {
        ListItemPrefix prefix = Assert.IsType<ListItemPrefix>(ListContinuation.Parse("- [x] done"));

        Assert.True(prefix.IsTask);
        Assert.Equal("[x]", prefix.Checkbox);
        Assert.Equal("done", prefix.Content);
        Assert.Equal(6, prefix.ContentColumn);
    }

    [Theory]
    [InlineData("- item", "- item\n- ")]
    [InlineData("* item", "* item\n* ")]
    [InlineData("+ item", "+ item\n+ ")]
    [InlineData("  - nested", "  - nested\n  - ")]
    [InlineData("> quoted", "> quoted\n> ")]
    [InlineData(">> deep", ">> deep\n>> ")]
    [InlineData("> - quoted item", "> - quoted item\n> - ")]
    [InlineData("-  wider spacing", "-  wider spacing\n-  ")]
    public void EnterAtTheEndOfAnItem_RepeatsItsMarker(string line, string expected)
        => Assert.Equal(expected, Edits.Apply(line, ListContinuation.OnEnter(line, line.Length)));

    [Theory]
    [InlineData("1. item", "1. item\n2. ")]
    [InlineData("2. item", "2. item\n3. ")]
    [InlineData("9) item", "9) item\n10) ")]
    [InlineData("09. item", "09. item\n10. ")]
    [InlineData("007. item", "007. item\n008. ")]
    public void EnterAtTheEndOfANumberedItem_CountsOn(string line, string expected)
        => Assert.Equal(expected, Edits.Apply(line, ListContinuation.OnEnter(line, line.Length)));

    [Theory]
    [InlineData("- [ ] to do", "- [ ] to do\n- [ ] ")]
    [InlineData("- [x] done", "- [x] done\n- [ ] ")]
    [InlineData("- [X] done", "- [X] done\n- [ ] ")]
    [InlineData("1. [x] done", "1. [x] done\n2. [ ] ")]
    public void EnterAtTheEndOfATaskItem_StartsAnUncheckedOne(string line, string expected)
        => Assert.Equal(expected, Edits.Apply(line, ListContinuation.OnEnter(line, line.Length)));

    [Fact]
    public void EnterAtTheEndOfAnItem_LeavesTheCaretAfterTheNewMarker()
        => Assert.Equal("- item\n- ".Length, Edits.Caret(ListContinuation.OnEnter("- item", 6)));

    [Theory]
    [InlineData("- ")]
    [InlineData("* ")]
    [InlineData("1. ")]
    [InlineData("  - ")]
    [InlineData("> ")]
    [InlineData("-")]
    [InlineData("- [ ]")]
    [InlineData("- [ ] ")]
    [InlineData("> - ")]
    public void EnterOnAnEmptyItem_EndsTheList(string line)
    {
        TextEdit? edit = ListContinuation.OnEnter(line, line.Length);

        Assert.Equal(string.Empty, Edits.Apply(line, edit));
        Assert.Equal(0, Edits.Caret(edit));
    }

    [Fact]
    public void EnterInsideAnItemsMarker_IsLeftToTheEditor()
    {
        // The caret is between the bullet and its space. Splitting there would leave half a marker on
        // the line above and half on the line below.
        Assert.Null(ListContinuation.OnEnter("- item", 1));
        Assert.Null(ListContinuation.OnEnter("- [ ] task", 4));
    }

    [Fact]
    public void EnterInTheMiddleOfAnItem_ContinuesItWhereTheCaretIs()
        => Assert.Equal("- hello\n- world", Edits.Apply("- helloworld", ListContinuation.OnEnter("- helloworld", 7)));

    [Theory]
    [InlineData(null, 0)]
    [InlineData("plain text", 5)]
    [InlineData("- item", -1)]
    [InlineData("- item", 7)]
    public void EnterSomewhereThatIsNotAnItem_IsLeftToTheEditor(string? line, int column)
        => Assert.Null(ListContinuation.OnEnter(line, column));

    [Theory]
    [InlineData("- item", "- ")]
    [InlineData("* item", "* ")]
    [InlineData("1. item", "2. ")]
    [InlineData("9. item", "10. ")]
    [InlineData("09. item", "10. ")]
    [InlineData("42) item", "43) ")]
    [InlineData("> quoted", "> ")]
    [InlineData("- [ ] todo", "- [ ] ")]
    [InlineData("- [x] done", "- [ ] ")]
    [InlineData("  - nested", "  - ")]
    [InlineData("> - quoted item", "> - ")]
    public void OpeningBelowAnItem_ContinuesIt(string line, string expected)
        => Assert.Equal(expected, ListContinuation.OpenBelow(line));

    [Theory]
    [InlineData("- item", "- ")]
    // The item's own marker rather than the next one: the new line goes before this item and takes
    // its place in the list, so counting on would number it after the one it precedes.
    [InlineData("1. item", "1. ")]
    [InlineData("2. item", "2. ")]
    [InlineData("42) item", "42) ")]
    [InlineData("> quoted", "> ")]
    [InlineData("- [x] done", "- [ ] ")]
    [InlineData("  - nested", "  - ")]
    public void OpeningAboveAnItem_RepeatsItsOwnMarker(string line, string expected)
        => Assert.Equal(expected, ListContinuation.OpenAbove(line));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("just a line")]
    [InlineData("# a heading")]
    [InlineData("---")]
    [InlineData("-x")]
    // An empty item is a list the user is ending. Enter clears it, because the caret is on it; 'o'
    // and 'O' never edit the line they are on, so they simply continue nothing.
    [InlineData("- ")]
    [InlineData("1. ")]
    [InlineData("> ")]
    [InlineData("- [ ] ")]
    public void OpeningWhereThereIsNothingToContinue_AnswersWithNothing(string? line)
    {
        Assert.Equal(string.Empty, ListContinuation.OpenBelow(line));
        Assert.Equal(string.Empty, ListContinuation.OpenAbove(line));
    }

    [Theory]
    // The item above is the parent, so the item lines up with that parent's own text.
    [InlineData("- b", "- a", "  - b")]
    [InlineData("- b", "* a", "  - b")]
    [InlineData("- b", "1. a", "   - b")]
    [InlineData("- b", "100. a", "     - b")]
    [InlineData("- b", "- [ ] a", "      - b")]
    [InlineData("1. b", "- a", "  1. b")]
    public void TabUnderAnItem_LinesUpWithThatItemsText(string line, string above, string expected)
        => Assert.Equal(expected, Edits.Apply(line, ListContinuation.OnIndent(line, line.Length, [above])));

    [Fact]
    public void TabSkipsPastItemsDeeperThanTheOneBeingIndented()
    {
        // "- c" is a sibling of "- a", so Tab makes it a sibling of "- b" rather than a child of it.
        string[] above = ["  - b", "- a"];

        Assert.Equal("  - c", Edits.Apply("- c", ListContinuation.OnIndent("- c", 3, above)));
    }

    [Fact]
    public void TabOnAnItemThatIsAlreadyAChildOfTheOneAboveIt_ChangesNothing()
    {
        // Markdown has no deeper level to move it to: nesting is expressed by lining up with the
        // parent's text, and it is already there.
        Assert.Null(ListContinuation.OnIndent("  - b", 5, ["- a"]));
    }

    [Fact]
    public void TabOnAnItemWithNothingAboveIt_FallsBackToTheDefaultWidth()
        => Assert.Equal("  - a", Edits.Apply("- a", ListContinuation.OnIndent("- a", 3, [])));

    [Fact]
    public void TabLooksPastTheBlankLinesOfALooseList()
        => Assert.Equal("  - b", Edits.Apply("- b", ListContinuation.OnIndent("- b", 3, ["", "- a"])));

    [Fact]
    public void TabStopsLookingAtTheFirstLineThatIsNotAnItem()
    {
        // The paragraph is where this list begins, so there is no item above to nest under.
        string[] above = ["a paragraph", "- a"];

        Assert.Equal("  - b", Edits.Apply("- b", ListContinuation.OnIndent("- b", 3, above)));
    }

    [Fact]
    public void TabKeepsTheCaretsDistanceFromTheItemsText()
    {
        // The caret was one character into "b"; it still is after two columns are added in front.
        Assert.Equal(5, Edits.Caret(ListContinuation.OnIndent("- b", 3, ["- a"])));
    }

    [Theory]
    [InlineData("  - b", "- a", "- b")]
    [InlineData("   - b", "1. a", "- b")]
    [InlineData("\t- b", "- a", "- b")]
    public void ShiftTabOnANestedItem_TakesItBackToTheLevelAbove(string line, string above, string expected)
        => Assert.Equal(expected, Edits.Apply(line, ListContinuation.OnOutdent(line, line.Length, [above])));

    [Fact]
    public void ShiftTabTakesAnItemToTheNearestShallowerLevelRatherThanToTheMargin()
    {
        string[] above = ["  - b", "- a"];

        Assert.Equal("  - c", Edits.Apply("    - c", ListContinuation.OnOutdent("    - c", 7, above)));
    }

    [Fact]
    public void ShiftTabOnAnItemAlreadyAtTheMargin_ChangesNothing()
        => Assert.Null(ListContinuation.OnOutdent("- a", 3, ["- z"]));

    [Fact]
    public void IndentationIsComparedInColumnsSoATabCountsAsFour()
    {
        Assert.Equal(4, ListItemPrefix.ColumnAfter("\t", 0));
        Assert.Equal(4, ListItemPrefix.ColumnAfter("\t", 1));
        Assert.Equal(8, ListItemPrefix.ColumnAfter("\t\t", 0));
        Assert.Equal(4, Assert.IsType<ListItemPrefix>(ListContinuation.Parse("\t- b")).IndentWidth);
    }

    [Fact]
    public void IndentingSomethingThatIsNotAnItem_IsLeftToTheEditor()
    {
        Assert.Null(ListContinuation.OnIndent("plain text", 4, []));
        Assert.Null(ListContinuation.OnOutdent("plain text", 4, []));
        Assert.Null(ListContinuation.OnIndent(null, 0, []));
    }

    [Fact]
    public void IndentingWithoutTheLinesAboveIt_IsRejectedRatherThanGuessedAt()
    {
        Assert.Throws<ArgumentNullException>(() => ListContinuation.OnIndent("- a", 3, null!));
        Assert.Throws<ArgumentNullException>(() => ListContinuation.OnOutdent("- a", 3, null!));
    }

    [Fact]
    public void TheLinesAboveAreReadOnlyAsFarAsTheAnswer()
    {
        // The editor walks the document backwards to produce these, so a rule that read all of them
        // would cost the whole file on every Tab.
        Counting above = new(["- a", "- z", "- y"]);

        Assert.Equal("  - b", Edits.Apply("- b", ListContinuation.OnIndent("- b", 3, above)));
        Assert.Equal(1, above.Read);
    }

    private sealed class Counting(IEnumerable<string> lines) : IEnumerable<string>
    {
        public int Read { get; private set; }

        public IEnumerator<string> GetEnumerator()
        {
            foreach (string line in lines)
            {
                Read++;
                yield return line;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
