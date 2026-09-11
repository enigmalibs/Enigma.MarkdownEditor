using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Enigma.MarkdownEditor.Desktop.Controls;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Controls;

/// <summary>
/// What a GFM pipe table comes out as.
/// </summary>
/// <remarks>
/// A <see cref="Grid"/> of bordered cells inside a scroller of its own: the structure is what a test
/// can hold, and it is also the whole of the acceptance criterion — the right cells, in the right
/// places, with the alignment the separator row asked for.
/// </remarks>
public sealed class MarkdownTableTests
{
    private const string Table =
        """
        | left | centre | right |
        |:-----|:------:|------:|
        | one  | two    | three |
        | a    | b      | c     |
        """;

    [AvaloniaFact]
    public void ATable_IsAGridOfEveryCellInIt()
    {
        Grid grid = TableGrid(Table);

        Assert.Equal(3, grid.ColumnDefinitions.Count);
        Assert.Equal(3, grid.RowDefinitions.Count);

        Assert.Equal(
            ["left", "centre", "right", "one", "two", "three", "a", "b", "c"],
            Cells(grid).Select(PreviewTree.Text));
    }

    [AvaloniaFact]
    public void EveryCell_IsWhereItsRowAndColumnSayItIs()
    {
        Grid grid = TableGrid(Table);

        Assert.Equal("one", PreviewTree.Text(CellAt(grid, row: 1, column: 0)));
        Assert.Equal("b", PreviewTree.Text(CellAt(grid, row: 2, column: 1)));
        Assert.Equal("right", PreviewTree.Text(CellAt(grid, row: 0, column: 2)));
    }

    [AvaloniaFact]
    public void TheSeparatorRow_DecidesEachColumnsAlignment()
    {
        Grid grid = TableGrid(Table);

        Assert.Equal(TextAlignment.Left, Alignment(grid, column: 0));
        Assert.Equal(TextAlignment.Center, Alignment(grid, column: 1));
        Assert.Equal(TextAlignment.Right, Alignment(grid, column: 2));
    }

    [AvaloniaFact]
    public void AColumnWithNoAlignmentOfItsOwn_ReadsLeft()
    {
        Grid grid = TableGrid(
            """
            | a | b |
            |---|---|
            | 1 | 2 |
            """);

        Assert.Equal(TextAlignment.Left, Alignment(grid, column: 0));
        Assert.Equal(TextAlignment.Left, Alignment(grid, column: 1));
    }

    [AvaloniaFact]
    public void TheHeaderRow_IsEmphasisedAndTheBodyIsNot()
    {
        Grid grid = TableGrid(Table);

        Assert.Equal(FontWeight.SemiBold, Text(CellAt(grid, row: 0, column: 0)).FontWeight);
        Assert.NotNull(CellAt(grid, row: 0, column: 0).Background);

        Assert.NotEqual(FontWeight.SemiBold, Text(CellAt(grid, row: 1, column: 0)).FontWeight);
        Assert.Null(CellAt(grid, row: 1, column: 0).Background);
    }

    [AvaloniaFact]
    public void ACell_KeepsTheInlineFormattingInsideIt()
    {
        Grid grid = TableGrid(
            """
            | what     | how          |
            |----------|--------------|
            | **bold** | `code`       |
            """);

        Assert.Equal("bold", PreviewTree.Text(CellAt(grid, row: 1, column: 0)));

        Span emphasis = Assert.Single(PreviewTree.Inlines<Span>(Text(CellAt(grid, row: 1, column: 0))));

        Assert.Equal(FontWeight.Bold, emphasis.FontWeight);
    }

    [AvaloniaFact]
    public void ARowWithFewerCellsThanTheHeader_LeavesTheRestEmptyRatherThanShifting()
    {
        Grid grid = TableGrid(
            """
            | a | b | c |
            |---|---|---|
            | 1 |   |   |
            """);

        Assert.Equal(3, grid.ColumnDefinitions.Count);
        Assert.Equal("1", PreviewTree.Text(CellAt(grid, row: 1, column: 0)));
    }

    [AvaloniaFact]
    public void ATableWiderThanThePane_ScrollsRatherThanBeingClipped()
    {
        ScrollViewer scroller = PreviewTree.Block<ScrollViewer>(Table);

        // The page itself never scrolls sideways (see MarkdownPreview), so a table that does not fit
        // has to bring its own bar or lose its last columns to the edge of the pane.
        Assert.Equal(ScrollBarVisibility.Auto, scroller.HorizontalScrollBarVisibility);
        Assert.Equal(ScrollBarVisibility.Disabled, scroller.VerticalScrollBarVisibility);
    }

    [AvaloniaFact]
    public void TheBlocksAroundATable_StillRender()
    {
        IReadOnlyList<Control> blocks = PreviewTree.Blocks($"Before.\n\n{Table}\n\nAfter.");

        Assert.Equal(3, blocks.Count);
        Assert.Equal("Before.", PreviewTree.Text(blocks[0]));
        Assert.Equal("After.", PreviewTree.Text(blocks[2]));
    }

    private static Grid TableGrid(string markdown)
        => Assert.Single(PreviewTree.Descendants<Grid>(PreviewTree.Block<ScrollViewer>(markdown)));

    private static IReadOnlyList<Border> Cells(Grid grid) => [.. grid.Children.OfType<Border>()];

    private static Border CellAt(Grid grid, int row, int column)
        => Assert.Single(
            Cells(grid),
            cell => (Grid.GetRow(cell) == row) && (Grid.GetColumn(cell) == column));

    private static SelectableTextBlock Text(Border cell)
        => Assert.Single(PreviewTree.Descendants<SelectableTextBlock>(cell));

    private static TextAlignment Alignment(Grid grid, int column) => Text(CellAt(grid, row: 0, column)).TextAlignment;
}
