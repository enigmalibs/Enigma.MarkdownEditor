using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Enigma.MarkdownEditor.Core.Vim;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.Controls;
using Enigma.MarkdownEditor.Desktop.Services;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Views;

/// <summary>
/// The tab strip's own behaviour: which of the dot and the close button is showing, how the selected
/// tab is told apart from the rest, how a strip with more tabs than room is scrolled, and what
/// closing a tab leaves on screen.
/// </summary>
/// <remarks>
/// All of it is driven through a real editor pane in a shown window, and the pointer states through
/// the headless platform's own mouse input, because that is the only thing that raises
/// <c>:pointerover</c> — the pseudo-class the visibility rules are keyed on. What the resulting
/// colours look like is still a manual pass; what is asserted here is that they differ, and that they
/// resolve in both variants.
/// </remarks>
public sealed class EditorTabStripTests
{
    [AvaloniaFact]
    public async Task ATabUnderAColouredFolder_CarriesTheColourAndKeepsItsWidth()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md", "two.md");

        double before = strip.Width(0);

        Assert.Null(strip.ColorBar(0).Background);
        Assert.Null(strip.ColorBar(1).Background);

        await strip.ColourAsync(strip.Root, FolderColor.Blue);

        // Painted where the binding reached the control the strip actually draws, and the same brush
        // the tree's swatch offers — one palette, read from one place.
        Assert.Same(FolderColorPalette.Blue, strip.ColorBar(0).Background);
        Assert.Same(FolderColorPalette.Blue, strip.ColorBar(1).Background);

        // The bar's width is unconditional, so nothing moved: a tab that changed width would slide
        // the whole strip out from under the pointer the moment a colour was picked.
        Assert.Equal(before, strip.Width(0));
    }

    [AvaloniaFact]
    public async Task ClearingTheFoldersColour_LeavesTheBarUnpaintedAndTheTabUnmoved()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md");

        await strip.ColourAsync(strip.Root, FolderColor.Pink);
        double coloured = strip.Width(0);

        await strip.ColourAsync(strip.Root, FolderColor.None);

        Assert.Null(strip.ColorBar(0).Background);
        Assert.Equal(coloured, strip.Width(0));
    }

    [AvaloniaFact]
    public async Task AModifiedTab_ShowsTheDotAtRestAndTheCloseButtonUnderThePointer()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md", "two.md");

        strip.Edit(1);

        Assert.True(strip.Dot(1).IsVisible);
        Assert.False(strip.Close(1).IsVisible);

        strip.PointerOver(1);

        Assert.False(strip.Dot(1).IsVisible);
        Assert.True(strip.Close(1).IsVisible);

        strip.PointerAway();

        Assert.True(strip.Dot(1).IsVisible);
        Assert.False(strip.Close(1).IsVisible);
    }

    [AvaloniaFact]
    public async Task TypingInVimInsertMode_RaisesTheDotWithoutWaitingForTheModeToEnd()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md");

        Assert.False(strip.Dot(0).IsVisible);
        Assert.True(strip.FocusEditor());

        strip.Type("i");
        Assert.Equal(VimMode.Insert, strip.Mode);

        strip.Type("x");

        // Still in insert mode, so the session's undo group is still open and IsOriginalFile has not
        // been recalculated once. That is the state the dot used to be blind to: the work was on
        // screen and the tab said there was nothing to save.
        Assert.Equal(VimMode.Insert, strip.Mode);
        Assert.StartsWith("x", strip.Editor.Text);
        Assert.True(strip.Dot(0).IsVisible);
    }

    [AvaloniaFact]
    public async Task ClosingATabTypedIntoInInsertMode_AsksBeforeTakingTheWorkWithIt()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md");

        Assert.True(strip.FocusEditor());
        strip.Type("i");
        strip.Type("x");

        strip.Prompts.Choice = UnsavedChangesChoice.Cancel;

        // The dot is showing, which means the close button is not: reaching it means moving onto the
        // tab first, exactly as a user does.
        strip.PointerOver(0);
        await strip.CloseAsync(0);

        // The reported half of the defect that costs something: the close went through silently and
        // the paragraph the user had just typed went with it.
        Assert.Equal(["one.md"], strip.Prompts.AskedAbout);
        Assert.Equal(1, strip.Count);
    }

    [AvaloniaFact]
    public async Task AnUnmodifiedTab_ShowsTheCloseButtonWhereverThePointerIs()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md", "two.md");

        Assert.False(strip.Dot(0).IsVisible);
        Assert.True(strip.Close(0).IsVisible);

        strip.PointerOver(0);

        Assert.False(strip.Dot(0).IsVisible);
        Assert.True(strip.Close(0).IsVisible);
    }

    [AvaloniaFact]
    public async Task ATab_KeepsItsWidthThroughEveryOneOfThoseStates()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md", "two.md");

        double atRest = strip.Width(0);

        strip.Edit(0);
        Assert.Equal(atRest, strip.Width(0), 3);

        strip.PointerOver(0);
        Assert.Equal(atRest, strip.Width(0), 3);

        strip.PointerAway();
        Assert.Equal(atRest, strip.Width(0), 3);
    }

    [AvaloniaFact]
    public async Task TheSelectedTabsTitle_IsRaisedAboveTheRestInEitherVariant()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md", "two.md");

        strip.Select(0);

        foreach (ThemeVariant variant in new[] { ThemeVariant.Dark, ThemeVariant.Light })
        {
            strip.UseVariant(variant);

            string? selected = strip.Title(0).Foreground?.ToString();
            string? unselected = strip.Title(1).Foreground?.ToString();

            Assert.Equal(TabStrip.Brush("EnigmaForegroundBrush", variant), selected);
            Assert.Equal(TabStrip.Brush("EnigmaForegroundSecondaryBrush", variant), unselected);

            // The point of the pair, and the one thing a palette change could quietly undo.
            Assert.NotEqual(selected, unselected);
        }
    }

    [AvaloniaFact]
    public async Task AStripWithMoreTabsThanRoom_DrawsAScrollbarBesideTheTabsRatherThanOverThem()
    {
        using TabStrip strip = await TabStrip.ShowAsync(TabStrip.Many);

        Assert.True(strip.Overflows);
        Assert.Equal(ScrollBarVisibility.Auto, ScrollViewer.GetHorizontalScrollBarVisibility(strip.Tabs));
        Assert.True(strip.HorizontalScrollBar.IsVisible);

        // The whole point of the band. A bar the Fluent template floats over the content would pass
        // this on none of the tabs; one in a row of its own passes on all of them, the tabs scrolled
        // off either end included.
        Rect band = strip.ScrollBarBand;

        for (int index = 0; index < TabStrip.Many.Length; index++)
        {
            Rect tab = strip.TabBand(index);

            Assert.False(band.Intersects(tab), $"tab {index} at {tab} is under the bar at {band}");
        }
    }

    [AvaloniaFact]
    public async Task AStripWhoseTabsAllFit_DrawsNoBarAndStandsExactlyAsTall()
    {
        using TabStrip few = await TabStrip.ShowAsync("one.md", "two.md");
        using TabStrip many = await TabStrip.ShowAsync(TabStrip.Many);

        Assert.False(few.Overflows);
        Assert.False(few.HorizontalScrollBar.IsVisible);

        Assert.True(many.Overflows);
        Assert.True(many.HorizontalScrollBar.IsVisible);

        // The band is reserved either way, so the tab that first overflows the strip does not shove
        // the editor down a few pixels as it is opened — nor lift it again as it is closed.
        Assert.Equal(many.Height, few.Height, 3);
    }

    [AvaloniaFact]
    public async Task TheScrollbar_IsTheSameThicknessAtRestUnderThePointerAndUnderADrag()
    {
        using TabStrip strip = await TabStrip.ShowAsync(TabStrip.Many);

        double atRest = strip.ScrollBarBand.Height;
        Assert.True(atRest > 0, "the bar is drawn with no thickness at all");

        strip.PointerOverScrollBar();
        Assert.Equal(atRest, strip.ScrollBarBand.Height, 3);

        // Fluent grows its scrollbars on the way in and back down on the way out, which over a strip
        // this shallow would move the tabs every time the pointer crossed the band.
        strip.DragScrollBar(-40, () => Assert.Equal(atRest, strip.ScrollBarBand.Height, 3));

        Assert.Equal(atRest, strip.ScrollBarBand.Height, 3);
    }

    [AvaloniaFact]
    public async Task DraggingTheScrollbar_ScrollsTheStrip()
    {
        using TabStrip strip = await TabStrip.ShowAsync(TabStrip.Many);

        strip.Select(TabStrip.Many.Length - 1);

        double atEnd = strip.Offset;
        Assert.True(atEnd > 0, "the last tab was selected and the strip did not follow it");

        strip.DragScrollBar(-40);

        Assert.True(strip.Offset < atEnd, $"the drag left the strip at {strip.Offset}");
    }

    [AvaloniaFact]
    public async Task TheWheel_ScrollsTheStripSidewaysWhenTheTabsOverflow()
    {
        using TabStrip strip = await TabStrip.ShowAsync(TabStrip.Many);

        strip.Select(0);
        Assert.True(strip.Overflows);
        Assert.Equal(0, strip.Offset, 3);

        // Towards the user is towards the later tabs, the way it moves a vertical list's content down.
        strip.Wheel(-1);
        double moved = strip.Offset;

        Assert.True(moved > 0, $"the wheel left the strip at {moved}");

        strip.Wheel(1);

        Assert.True(strip.Offset < moved, $"the wheel back left the strip at {strip.Offset}");
    }

    [AvaloniaFact]
    public async Task TheWheel_LeavesAStripWhoseTabsAllFitAlone()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md", "two.md");

        Assert.False(strip.Overflows);

        strip.Wheel(-1);

        Assert.Equal(0, strip.Offset, 3);
    }

    [AvaloniaFact]
    public async Task SelectingATabThatIsOffScreen_BringsItIntoView()
    {
        using TabStrip strip = await TabStrip.ShowAsync(TabStrip.Many);

        strip.Select(0);
        Assert.Equal(0, strip.Offset, 3);

        strip.Select(TabStrip.Many.Length - 1);

        Assert.True(strip.Offset > 0, "the last tab was selected and the strip did not follow it");
        Assert.True(strip.IsInView(TabStrip.Many.Length - 1), "the selected tab is still outside the viewport");
    }

    [AvaloniaFact]
    public async Task ClosingTheActiveMiddleTab_ActivatesTheTabThatTookItsPlace()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md", "two.md", "three.md");

        strip.Select(1);

        await strip.CloseAsync(1);

        // Through the real strip, so the selection the ListBox clears on its way out is written back
        // into the ViewModel before the neighbour is picked — which is the whole bug.
        Assert.Equal(2, strip.Count);
        Assert.Equal("three.md", strip.ActiveTitle);
        Assert.Equal(1, strip.Tabs.SelectedIndex);
        Assert.NotNull(strip.Editor.Document);
        Assert.True(strip.Editor.IsVisible);
        Assert.False(strip.Placeholder.IsVisible);
    }

    [AvaloniaFact]
    public async Task ClosingTheActiveLastTab_ActivatesItsPredecessor()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md", "two.md");

        strip.Select(1);

        await strip.CloseAsync(1);

        Assert.Equal("one.md", strip.ActiveTitle);
        Assert.True(strip.Editor.IsVisible);
    }

    [AvaloniaFact]
    public async Task ClosingTheOnlyTab_HidesTheEditorAndShowsThePlaceholder()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md");

        await strip.CloseAsync(0);

        Assert.Equal(0, strip.Count);
        Assert.Null(strip.ActiveTitle);
        Assert.False(strip.Editor.IsVisible);
        Assert.False(strip.Strip.IsVisible);
        Assert.True(strip.Placeholder.IsVisible);
        Assert.Equal("Double-click a markdown file in the tree to open it here.", strip.Placeholder.Text);
    }

    [AvaloniaFact]
    public async Task APaneWithTabsButNothingActive_ShowsTheStripAndNoEditor()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md", "two.md");

        strip.Deactivate();

        // A strip with tabs and no selection is still a strip; an editor with no document is not an
        // editor, and must not be drawn or hit-tested.
        Assert.True(strip.Strip.IsVisible);
        Assert.False(strip.Editor.IsVisible);
        Assert.True(strip.Placeholder.IsVisible);
    }

    [AvaloniaFact]
    public async Task ClickingThePaneWhileNothingIsActive_RaisesNothing()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md", "two.md");

        strip.Deactivate();

        // The reported crash needed a visible, hit-testable text area whose document had gone. The
        // document still goes — nothing can keep a broken binding path from clearing it — so what
        // closes the hole is that there is no control left under the pointer.
        Assert.Null(strip.Editor.Document);
        Assert.False(strip.Editor.IsVisible);

        strip.ClickPane();
    }

    [AvaloniaFact]
    public async Task ClickingThePaneAfterTheLastTabIsClosed_RaisesNothing()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md");

        await strip.CloseAsync(0);

        Assert.Null(strip.Editor.Document);
        Assert.False(strip.Editor.IsVisible);

        strip.ClickPane();

        Assert.True(strip.Placeholder.IsVisible);
    }

    [AvaloniaFact]
    public async Task SavingInInsertMode_HidesTheDotAndEscDoesNotBringItBack()
    {
        using TabStrip strip = await TabStrip.ShowAsync("one.md");

        Assert.True(strip.FocusEditor());

        strip.Type("i");
        Assert.Equal(VimMode.Insert, strip.Mode);

        strip.Type("xy");
        Assert.True(strip.Dot(0).IsVisible);

        await strip.SaveAsync();

        // The point of the whole report: the bytes land either way, and the dot is what the user
        // reads the save off. It goes now, not on the Esc that is still to come.
        Assert.False(strip.Dot(0).IsVisible);
        Assert.Equal(VimMode.Insert, strip.Mode);

        strip.Press(PhysicalKey.Escape);

        Assert.Equal(VimMode.Normal, strip.Mode);
        Assert.False(strip.Dot(0).IsVisible);

        strip.PointerOver(0);
        await strip.CloseAsync(0);

        Assert.Empty(strip.Prompts.AskedAbout);
        Assert.Equal(0, strip.Count);
    }
}
