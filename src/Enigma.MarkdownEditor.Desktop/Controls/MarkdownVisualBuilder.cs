// Using directives stay at file scope, above the namespace declaration, throughout this project:
// inside a namespace beginning with "Enigma.", the simple name "Avalonia" resolves against the
// referenced Enigma.Avalonia.* assemblies instead of Avalonia itself.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Enigma.MarkdownEditor.Core.Markdown;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

// Both namespaces define an Inline, so neither name is used bare anywhere in this file.
using MarkdigInline = Markdig.Syntax.Inlines.Inline;
using UiInline = Avalonia.Controls.Documents.Inline;

namespace Enigma.MarkdownEditor.Desktop.Controls;

/// <summary>
/// Turns a parsed markdown document into the control tree the preview shows.
/// </summary>
/// <remarks>
/// <para>
/// One instance per render, because it is scoped to one document's folder: an image link means a
/// different file for every note, and a builder that outlived a render would have to be told so.
/// </para>
/// <para>
/// Every block is built inside its own <c>try</c>. A renderer failure must not cost the reader the
/// rest of the note — let alone the application — so a block that throws becomes an error block in
/// its own place and the walk carries on. The failures reach the caller through the callback rather
/// than through an exception for the same reason.
/// </para>
/// <para>
/// What this does <em>not</em> render is as deliberate as what it does: raw HTML is shown as the
/// literal text it was written as, which is the documented v1 behaviour, and a block kind none of the
/// arms below names is skipped rather than half-drawn.
/// </para>
/// </remarks>
internal sealed class MarkdownVisualBuilder
{
    private const double BlockSpacing = 8;
    private const double MarkerColumnFloor = 22;
    private const double MarkerGap = 4;
    private const double CellPaddingX = 8;
    private const double CellPaddingY = 4;

    // What the Fluent theme draws a checkbox as, and the key it pads that box out to a form row
    // with. Overriding the key with the box's own size is what puts a task on the same line height
    // as every other list item; the size itself is a constant because it is the theme's, not
    // ours — it is the denominator the reader's own box size is scaled against. See TaskBox.
    private const double ThemeTaskBoxSize = 20;
    private const string TaskBoxHeightKey = "CheckBoxMinHeight";

    private readonly string? _basePath;
    private readonly MarkdownPreviewTypography _type;
    private readonly Action<Exception> _onFailure;
    private readonly Action<string> _onLinkActivated;
    private readonly Func<int, bool, bool> _onTaskToggled;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownVisualBuilder"/> class.
    /// </summary>
    /// <param name="basePath">The folder the document lives in, or <see langword="null"/> for none.</param>
    /// <param name="typography">The type every block is set in.</param>
    /// <param name="onFailure">
    /// Told about anything a block or an image load threw. Must not throw itself.
    /// </param>
    /// <param name="onLinkActivated">
    /// Told the URL of a link the reader clicked, as the document wrote it. Must not throw itself.
    /// </param>
    /// <param name="onTaskToggled">
    /// Told the source offset of a task box the reader ticked and the state they asked for; answers
    /// whether the document took it, so a box aimed at a stale offset can be put back. Must not
    /// throw itself.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="typography"/>, <paramref name="onFailure"/>,
    /// <paramref name="onLinkActivated"/> or <paramref name="onTaskToggled"/> is
    /// <see langword="null"/>.
    /// </exception>
    public MarkdownVisualBuilder(
        string? basePath,
        MarkdownPreviewTypography typography,
        Action<Exception> onFailure,
        Action<string> onLinkActivated,
        Func<int, bool, bool> onTaskToggled)
    {
        _basePath = basePath;
        _type = typography ?? throw new ArgumentNullException(nameof(typography));
        _onFailure = onFailure ?? throw new ArgumentNullException(nameof(onFailure));
        _onLinkActivated = onLinkActivated ?? throw new ArgumentNullException(nameof(onLinkActivated));
        _onTaskToggled = onTaskToggled ?? throw new ArgumentNullException(nameof(onTaskToggled));
    }

    /// <summary>
    /// Builds the visual for every block of <paramref name="document"/> that this version renders.
    /// </summary>
    /// <param name="document">The parsed document.</param>
    /// <returns>The block visuals, in document order, each with the source line it came from.</returns>
    /// <remarks>
    /// The source line is carried out with the visual because this walk is the only place the two are
    /// known together: the arms that return <see langword="null"/> drop a block from the output, so
    /// the positions in this list do not line up with the document's own blocks. It is what the
    /// preview's line-to-block index is built from.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<MarkdownBlockVisual> Build(MarkdownDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        List<MarkdownBlockVisual> visuals = [];

        foreach (Block block in document)
        {
            if (Isolated(block) is { } visual)
            {
                // Markdig counts lines from zero and the editor from one, and the whole point of the
                // index is to answer a question the editor asked.
                visuals.Add(new MarkdownBlockVisual(visual, block.Line + 1));
            }
        }

        return visuals;
    }

    /// <summary>
    /// The inline error block: what the reader gets in place of something that could not be drawn.
    /// </summary>
    /// <param name="failure">What went wrong.</param>
    /// <param name="typography">The type it is set in.</param>
    /// <returns>The visual.</returns>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    public static Control ErrorBlock(Exception failure, MarkdownPreviewTypography typography)
    {
        ArgumentNullException.ThrowIfNull(failure);
        ArgumentNullException.ThrowIfNull(typography);

        SelectableTextBlock text = new()
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = typography.CodeFontSize,
        };

        text.Inlines?.Add(new Run("This part could not be previewed. ") { FontWeight = FontWeight.SemiBold });
        text.Inlines?.Add(new Run($"{failure.GetType().Name}: {failure.Message}"));

        MarkdownPreviewTheme.BindBrush(text, TextBlock.ForegroundProperty, MarkdownPreviewTheme.ErrorKey);

        Border border = new()
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8),
            Child = text,
        };

        border.Classes.Add(MarkdownPreviewTheme.ErrorBlockClass);
        MarkdownPreviewTheme.BindBrush(border, Border.BackgroundProperty, MarkdownPreviewTheme.ErrorBackgroundKey);
        MarkdownPreviewTheme.BindBrush(border, Border.BorderBrushProperty, MarkdownPreviewTheme.ErrorBorderKey);

        return border;
    }

    private Control? Isolated(Block block)
    {
        try
        {
            return Visual(block);
        }
        catch (Exception failure)
        {
            // Caught broadly and on purpose: this is the error-isolation requirement. The AST comes
            // from arbitrary text in someone's notes, the failures a visual can raise are not a
            // documented set, and the useful outcome is one bordered sentence where the block was
            // rather than a dead pane or a crash dialog.
            _onFailure(failure);

            return ErrorBlock(failure, _type);
        }
    }

    private Control? Visual(Block block) => block switch
    {
        // Before the HtmlBlock arm below, because Markdig models front matter as one. Its contents are
        // never drawn — metadata rendered as content would make the preview wrong, not merely
        // unfinished — so all that is left of it is the chip saying the note has some.
        YamlFrontMatterBlock front => FrontMatterChip(front),
        HeadingBlock heading => Heading(heading),
        ParagraphBlock paragraph => Paragraph(paragraph),
        ListBlock list => List(list),
        QuoteBlock quote => Quote(quote),
        Table table => TableVisual(table),

        // Before the CodeBlock arm: a fenced block is one, and only a fence carries the language that
        // decides the colours.
        FencedCodeBlock fenced => CodeVisual(fenced.Lines.ToString(), fenced.Info),
        CodeBlock code => CodeVisual(code.Lines.ToString(), language: null),
        ThematicBreakBlock => ThematicBreak(),
        HtmlBlock html => Literal(html.Lines.ToString()),
        _ => null,
    };

    private Control Heading(HeadingBlock heading)
    {
        SelectableTextBlock text = Prose(heading.Inline);

        text.FontSize = MarkdownPreviewTheme.HeadingFontSize(heading.Level, _type.BodyFontSize);
        text.FontWeight = MarkdownPreviewTheme.HeadingFontWeight(heading.Level);

        // After the size, and only here: Prose sets the body line height, and a heading is set at a
        // size of its own, so the spacing it inherited would be the paragraph's rather than its own.
        text.LineHeight = _type.LineHeightAt(text.FontSize);

        // Above only: the block spacing below is already the paragraph's own.
        text.Margin = new Thickness(0, BlockSpacing / 2, 0, 0);

        return text;
    }

    private Control Paragraph(ParagraphBlock paragraph)
    {
        // A paragraph that is nothing but images is how a note actually writes one — it is what the
        // editor's own paste inserts — and as a block the image can be held to the pane's width.
        // Inside a sentence it has to stay an inline, where it keeps its natural size.
        if (ImagesOnly(paragraph.Inline) is { Count: > 0 } images)
        {
            if (images.Count == 1)
            {
                return ImageVisual(images[0]);
            }

            StackPanel panel = new() { Spacing = BlockSpacing / 2 };

            foreach (LinkInline image in images)
            {
                panel.Children.Add(ImageVisual(image));
            }

            return panel;
        }

        return Prose(paragraph.Inline);
    }

    private Control List(ListBlock list)
    {
        // A loose list has blank lines between its items in the source, and reads as paragraphs; a
        // tight one reads as a list. Keeping the difference is the whole point of Markdig telling us.
        StackPanel panel = new() { Spacing = list.IsLoose ? BlockSpacing / 2 : 2 };

        foreach (Block child in list)
        {
            if (child is ListItemBlock item)
            {
                panel.Children.Add(ListItem(list, item));
            }
        }

        return panel;
    }

    private Control ListItem(ListBlock list, ListItemBlock item)
    {
        Grid row = new()
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
        };

        Control marker = TaskOf(item) is { } task ? TaskBox(task) : Marker(list, item);

        StackPanel content = new() { Spacing = 2 };

        foreach (Block child in item)
        {
            if (Isolated(child) is { } visual)
            {
                content.Children.Add(visual);
            }
        }

        Grid.SetColumn(marker, 0);
        Grid.SetColumn(content, 1);
        row.Children.Add(marker);
        row.Children.Add(content);

        return row;
    }

    /// <summary>
    /// How wide a list item's marker column is, at the narrowest: the width bullets and numbers have
    /// always been set to, or the task box plus the gap after it once the box outgrows that.
    /// </summary>
    /// <remarks>
    /// A floor rather than a fixed width, and read by <see cref="Marker"/> as well as by
    /// <see cref="TaskBox"/>, because the two share a column: a box grown past the old 22 would
    /// otherwise indent every task in a list one step further than the bullet under it. The grid
    /// column is still <see cref="GridLength.Auto"/>, so a wide ordered marker such as <c>10.</c>
    /// grows past this on its own.
    /// </remarks>
    private double MarkerColumnWidth => Math.Max(MarkerColumnFloor, _type.TaskBoxSize + 2);

    private Control Marker(ListBlock list, ListItemBlock item)
    {
        SelectableTextBlock marker = new()
        {
            Text = list.IsOrdered
                ? string.Create(CultureInfo.InvariantCulture, $"{item.Order}{list.OrderedDelimiter}")
                : "•",
            MinWidth = MarkerColumnWidth,
            FontSize = _type.BodyFontSize,
            LineHeight = _type.LineHeightAt(_type.BodyFontSize),
            Margin = new Thickness(0, 0, MarkerGap, 0),
        };

        MarkdownPreviewTheme.BindBrush(
            marker,
            TextBlock.ForegroundProperty,
            MarkdownPreviewTheme.ForegroundSecondaryKey);

        return marker;
    }

    /// <summary>
    /// The checkbox a task item is marked with — the box IS the marker, because a bullet as well
    /// would say the same thing twice and the box is what the reader is looking for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A real <see cref="CheckBox"/> rather than a glyph, so the reader can tick a task off from the
    /// pane they are reading it in. It carries no content: the item's own text is already in the
    /// column beside it, and putting it inside the box would wrap it under the marker instead of
    /// under itself.
    /// </para>
    /// <para>
    /// The theme's checkbox is built for a form: padded, and half as tall again as a line of prose,
    /// because it reserves a touch target around a box that is only ever 20 wide and 20 high. Left
    /// as it comes, every task in a list would be spaced further apart than every bullet, and a list
    /// holding both would step at each task. So the padding goes, and the height the theme pads to
    /// is overridden — on this control alone — down to the size of the box it draws.
    /// </para>
    /// <para>
    /// The box is then scaled to the reader's own type through a
    /// <see cref="LayoutTransformControl"/> rather than by resizing the thing the theme draws. That
    /// is not a matter of taste: the Fluent template assigns the drawn box its 20×20 and its
    /// column that width as <em>local values</em>, and a local value outranks every style, so no
    /// selector reaching <c>NormalRectangle</c> can move it. A uniform scale takes the box, its
    /// tick and its hit area together, and asks nothing of the template's internals.
    /// </para>
    /// <para>
    /// Vertically the box is placed by arithmetic — top-aligned, then pushed down half the
    /// difference between the first line box and itself — rather than by being given the line's
    /// height and left to a <c>Center</c> alignment. Centring would put the box wherever the
    /// template centres it inside its own bounds, which is the same private detail again; a margin
    /// puts it where this renderer says.
    /// </para>
    /// <para>
    /// <see cref="CheckBox.IsChecked"/> is assigned before the toggle is attached, so drawing the
    /// document's own state is never mistaken for the reader ticking it.
    /// </para>
    /// </remarks>
    private Control TaskBox(TaskList task)
    {
        CheckBox box = new()
        {
            IsChecked = task.Checked,
            MinHeight = 0,
            Padding = new Thickness(0),
        };

        // A local resource rather than a style: the theme sizes the box's own cell from this key, and
        // there is no part of the template a style could reach that is not private to it. Local, so
        // it is this checkbox that is shrunk and not every one in the application.
        box.Resources[TaskBoxHeightKey] = ThemeTaskBoxSize;

        // Markdig spans the whole box, so Span.Start is the `[`: the character a toggle rewrites is
        // the one after it.
        MarkdownTaskBox.Attach(box, task.Span.Start + 1, _onTaskToggled);

        double size = _type.TaskBoxSize;
        double scale = size / ThemeTaskBoxSize;

        return new LayoutTransformControl
        {
            LayoutTransform = new ScaleTransform(scale, scale),
            Child = box,

            // On the wrapper, not on the box: the column is measured in the row's own pixels, and a
            // minimum handed to the box would come back out of the transform multiplied by it.
            MinWidth = MarkerColumnWidth,
            Margin = new Thickness(0, TaskBoxOffset(size), MarkerGap, 0),
            VerticalAlignment = VerticalAlignment.Top,
        };
    }

    /// <summary>
    /// How far below the top of its row a task box of <paramref name="size"/> sits, so that it is
    /// centred on the first line of the text beside it.
    /// </summary>
    /// <remarks>
    /// A line of text is centred inside its own line box rather than sat at the top of it, so a
    /// top-aligned box of a different height than that line box is out by half the difference — an
    /// error that grows with the font size and with the line height, which is what the reader
    /// reported. Never negative: at the small end the box is held at its floor while the line box
    /// keeps shrinking, and pulling the box above the row would only trade one misalignment for a
    /// worse one.
    /// </remarks>
    private double TaskBoxOffset(double size)
        => Math.Max(0, (_type.LineHeightAt(_type.BodyFontSize) - size) / 2);

    private static TaskList? TaskOf(ListItemBlock item)
        => (item.Count > 0) && (item[0] is ParagraphBlock { Inline: { } inline })
            ? inline.FirstChild as TaskList
            : null;

    private Control Quote(QuoteBlock quote)
    {
        StackPanel content = new() { Spacing = BlockSpacing / 2 };

        foreach (Block child in quote)
        {
            if (Isolated(child) is { } visual)
            {
                content.Children.Add(visual);
            }
        }

        Border border = new()
        {
            BorderThickness = new Thickness(3, 0, 0, 0),
            CornerRadius = new CornerRadius(2),
            Padding = new Thickness(10, 6),
            Child = content,
        };

        MarkdownPreviewTheme.BindBrush(border, Border.BorderBrushProperty, MarkdownPreviewTheme.AccentKey);
        MarkdownPreviewTheme.BindBrush(border, Border.BackgroundProperty, MarkdownPreviewTheme.QuoteBackgroundKey);

        return border;
    }

    private Control TableVisual(Table table)
    {
        int columns = ColumnCount(table);

        Grid grid = new();

        for (int column = 0; column < columns; column++)
        {
            // Auto rather than Star: a table in a note is a handful of short values, and columns that
            // shared the pane's width equally would give a two-character column the same room as a
            // sentence. Anything wider than the pane goes behind the scroll bar below.
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        }

        int rowIndex = 0;

        foreach (Block child in table)
        {
            if (child is not TableRow row)
            {
                continue;
            }

            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            int next = 0;

            foreach (Block content in row)
            {
                if (content is not TableCell cell)
                {
                    continue;
                }

                int column = (cell.ColumnIndex >= 0) ? cell.ColumnIndex : next;
                int span = Math.Clamp(cell.ColumnSpan, 1, Math.Max(1, columns - column));
                Control visual = Cell(table, cell, row.IsHeader, column);

                Grid.SetRow(visual, rowIndex);
                Grid.SetColumn(visual, column);
                Grid.SetColumnSpan(visual, span);
                grid.Children.Add(visual);

                next = column + span;
            }

            rowIndex++;
        }

        // The cells draw their top and left edges, this draws the two the grid has no cell beyond, so
        // every rule is one pixel wide instead of two everywhere they meet.
        Border frame = new()
        {
            BorderThickness = new Thickness(0, 0, 1, 1),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = grid,
        };

        frame.Classes.Add(MarkdownPreviewTheme.TableClass);
        MarkdownPreviewTheme.BindBrush(frame, Border.BorderBrushProperty, MarkdownPreviewTheme.BorderSubtleKey);

        return new ScrollViewer
        {
            Content = frame,
            HorizontalAlignment = HorizontalAlignment.Left,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };
    }

    private Control Cell(Table table, TableCell cell, bool isHeader, int column)
    {
        StackPanel content = new() { Spacing = 2 };

        foreach (Block child in cell)
        {
            if (Isolated(child) is not { } visual)
            {
                continue;
            }

            if (visual is SelectableTextBlock text)
            {
                // The cell is as wide as its column, and the column is as wide as its widest cell, so
                // the alignment the separator row asked for is something the reader can actually see.
                text.TextWrapping = TextWrapping.NoWrap;
                text.TextAlignment = Alignment(table, column);

                if (isHeader)
                {
                    text.FontWeight = FontWeight.SemiBold;
                }
            }

            content.Children.Add(visual);
        }

        Border box = new()
        {
            BorderThickness = new Thickness(1, 1, 0, 0),
            Padding = new Thickness(CellPaddingX, CellPaddingY),
            Child = content,
        };

        MarkdownPreviewTheme.BindBrush(box, Border.BorderBrushProperty, MarkdownPreviewTheme.BorderSubtleKey);

        if (isHeader)
        {
            MarkdownPreviewTheme.BindBrush(
                box,
                Border.BackgroundProperty,
                MarkdownPreviewTheme.TableHeaderBackgroundKey);
        }

        return box;
    }

    private static TextAlignment Alignment(Table table, int column)
    {
        if ((column < 0) || (column >= table.ColumnDefinitions.Count))
        {
            return TextAlignment.Left;
        }

        return table.ColumnDefinitions[column].Alignment switch
        {
            TableColumnAlign.Center => TextAlignment.Center,
            TableColumnAlign.Right => TextAlignment.Right,
            _ => TextAlignment.Left,
        };
    }

    private static int ColumnCount(Table table)
    {
        // The separator row is what Markdig builds the definitions from, and a row is allowed to have
        // more cells than it declared columns — so the widest row wins, and a table with no separator
        // at all still gets a grid.
        int columns = table.ColumnDefinitions.Count;

        foreach (Block child in table)
        {
            if (child is not TableRow row)
            {
                continue;
            }

            int next = 0;

            foreach (Block content in row)
            {
                if (content is TableCell cell)
                {
                    // The same walk the row is laid out with, so the grid is never narrower than what
                    // is about to be put in it.
                    int column = (cell.ColumnIndex >= 0) ? cell.ColumnIndex : next;
                    next = column + Math.Max(1, cell.ColumnSpan);
                }
            }

            columns = Math.Max(columns, next);
        }

        return Math.Max(columns, 1);
    }

    private Control CodeVisual(string code, string? language) => new MarkdownCodeBlock
    {
        Typography = _type,
        Code = code,

        // Only the first word: an info string can carry more than a language — `csharp title=Note.cs`
        // is a convention several editors write — and the rest is not a grammar's name.
        Language = language?.Split(' ', StringSplitOptions.RemoveEmptyEntries) is [string first, ..] ? first : null,
    };

    private Control FrontMatterChip(YamlFrontMatterBlock front)
    {
        IReadOnlyList<string> keys = MarkdownFrontMatter.Keys(front.Lines.ToString());

        SelectableTextBlock text = new()
        {
            // The keys, not the values: the chip's job is to say the note has metadata and roughly
            // what about, in one line, without becoming a second rendering of the block it replaces.
            Text = keys.Count > 0 ? $"Front matter · {string.Join(" · ", keys)}" : "Front matter",
            TextWrapping = TextWrapping.Wrap,
            FontSize = _type.AsideFontSize,
        };

        MarkdownPreviewTheme.BindBrush(
            text,
            TextBlock.ForegroundProperty,
            MarkdownPreviewTheme.ForegroundTertiaryKey);

        Border chip = new()
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = text,
        };

        chip.Classes.Add(MarkdownPreviewTheme.MetadataChipClass);
        MarkdownPreviewTheme.BindBrush(chip, Border.BackgroundProperty, MarkdownPreviewTheme.MetadataBackgroundKey);

        return chip;
    }

    private static Control ThematicBreak()
    {
        Border rule = new()
        {
            Height = 1,
            Margin = new Thickness(0, BlockSpacing / 2),
        };

        MarkdownPreviewTheme.BindBrush(rule, Border.BackgroundProperty, MarkdownPreviewTheme.BorderSubtleKey);

        return rule;
    }

    private Control Literal(string text)
    {
        SelectableTextBlock block = new()
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = _type.CodeFontSize,
            LineHeight = _type.LineHeightAt(_type.CodeFontSize),
        };

        ApplyCodeFamily(block);

        MarkdownPreviewTheme.BindBrush(
            block,
            TextBlock.ForegroundProperty,
            MarkdownPreviewTheme.ForegroundSecondaryKey);

        return block;
    }

    private SelectableTextBlock Prose(ContainerInline? inlines)
    {
        SelectableTextBlock text = new()
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = _type.BodyFontSize,
            LineHeight = _type.LineHeightAt(_type.BodyFontSize),
        };

        MarkdownLinkRegions links = new();

        if (text.Inlines is { } target)
        {
            Append(target, inlines, origin: 0, links);
        }

        // Only where there is something to click. Most of a note is paragraphs with no links in them,
        // and hit-testing every pointer move across all of them would be work for nothing.
        if (links.Count > 0)
        {
            links.Attach(text, _onLinkActivated);
        }

        return text;
    }

    /// <param name="origin">
    /// The index, in the text block's own text, at which <paramref name="target"/>'s content begins —
    /// so a link nested inside emphasis still records where it is on screen rather than where it is
    /// inside its span.
    /// </param>
    private void Append(InlineCollection target, MarkdigInline? first, int origin, MarkdownLinkRegions links)
    {
        bool afterTaskBox = false;

        for (MarkdigInline? current = first is ContainerInline container ? container.FirstChild : first;
             current is not null;
             current = current.NextSibling)
        {
            if (current is TaskList)
            {
                // The box is drawn as the item's marker (see TaskBox), so the inline itself
                // contributes nothing — but the source wrote `- [x] done`, and the space after the
                // box is still in the literal that follows. Left in, it indents a task's text one
                // space further than an ordinary bullet's in the same list.
                afterTaskBox = true;

                continue;
            }

            // Measured off the collection rather than counted off the markdown, before and after, so
            // that whatever an inline turns out to contribute to the laid-out text — two characters
            // for a line break, none for an image — is what the link range says it did.
            int before = target.Text?.Length ?? 0;
            UiInline? converted = Convert(current, origin + before, links);

            if (afterTaskBox)
            {
                afterTaskBox = false;

                if (converted is Run { Text: { } text } run)
                {
                    run.Text = text.TrimStart(' ');
                }
            }

            if (converted is null)
            {
                continue;
            }

            target.Add(converted);

            if (UrlOf(current) is { } url)
            {
                links.Add(origin + before, (target.Text?.Length ?? 0) - before, url);
            }
        }
    }

    private static string? UrlOf(MarkdigInline inline) => inline switch
    {
        LinkInline { IsImage: false } link => link.Url,
        AutolinkInline autolink => autolink.Url,
        _ => null,
    };

    private UiInline? Convert(MarkdigInline inline, int start, MarkdownLinkRegions links)
    {
        switch (inline)
        {
            case LiteralInline literal:
                return new Run(literal.Content.ToString());

            case EmphasisInline emphasis:
                return Emphasis(emphasis, start, links);

            case CodeInline code:
                return Code(code);

            case LinkInline { IsImage: true } image:
                return new InlineUIContainer(ImageVisual(image));

            case LinkInline link:
                return Link(link.Url, link, start, links);

            case AutolinkInline autolink:
                return Link(autolink.Url, container: null, start, links);

            case LineBreakInline line:
                // A soft break is a space: the paragraph is wrapped by the pane's width, not by where
                // the author happened to end the source line.
                return line.IsHard ? new LineBreak() : new Run(" ");

            // Reached only for a task box outside a list item, which Markdig does not produce today.
            // Inside one it never gets here — Append takes it, because the space after the box has
            // to be taken with it, and the box itself is the item's marker.
            case TaskList:
                return null;

            case HtmlEntityInline entity:
                return new Run(entity.Transcoded.ToString());

            case HtmlInline html:
                // Raw HTML as literal text — the documented v1 behaviour, so a `<div>` in a note is
                // visible rather than silently swallowed.
                return new Run(html.Tag);

            case ContainerInline nested:
                Span span = new();
                Append(span.Inlines, nested, start, links);
                return span;

            default:
                return null;
        }
    }

    private UiInline Emphasis(EmphasisInline emphasis, int start, MarkdownLinkRegions links)
    {
        Span span = new();

        if (emphasis.DelimiterChar == '~')
        {
            span.TextDecorations = TextDecorations.Strikethrough;
        }
        else if (emphasis.DelimiterCount >= 2)
        {
            span.FontWeight = FontWeight.Bold;
        }
        else
        {
            span.FontStyle = FontStyle.Italic;
        }

        // Nested rather than flattened, so `***both***` — which Markdig models as one emphasis inside
        // another — comes out bold and italic instead of only the outer one.
        Append(span.Inlines, emphasis, start, links);

        return span;
    }

    private UiInline Code(CodeInline code)
    {
        Run run = new(code.Content);

        // The size as well as the family, unlike when the code size was derived from the prose one:
        // the point of a setting for it is that every piece of code obeys it, and a span left to
        // inherit would be the one place in the preview where code came out at the prose size.
        run.FontSize = _type.CodeFontSize;

        if (_type.CodeFontFamily is { Length: > 0 } family)
        {
            run.FontFamily = new FontFamily(family);
        }

        MarkdownPreviewTheme.BindBrush(run, TextElement.BackgroundProperty, MarkdownPreviewTheme.CodeBackgroundKey);

        return run;
    }

    // An empty family means "inherit whatever the pane is set in", and leaving the property unset is
    // how a visual inherits one — an empty family name is not something the renderer can resolve.
    private void ApplyCodeFamily(TextBlock target)
    {
        if (_type.CodeFontFamily is { Length: > 0 } family)
        {
            target.FontFamily = new FontFamily(family);
        }
    }

    private UiInline Link(string? url, ContainerInline? container, int start, MarkdownLinkRegions links)
    {
        Span span = new() { TextDecorations = TextDecorations.Underline };

        MarkdownPreviewTheme.BindBrush(span, TextElement.ForegroundProperty, MarkdownPreviewTheme.AccentKey);

        Append(span.Inlines, container, start, links);

        // An autolink carries no children — the address IS the text.
        if (span.Inlines.Count == 0)
        {
            span.Inlines.Add(new Run(url ?? string.Empty));
        }

        return span;
    }

    private Control ImageVisual(LinkInline image)
    {
        MarkdownImageSource source = MarkdownImageSource.Resolve(_basePath, image.Url);

        if ((source.Kind != MarkdownImageKind.Local) || !source.Exists || (source.LocalPath is null))
        {
            return ImagePlaceholder(source);
        }

        Image visual = new()
        {
            // Uniform + DownOnly is what "constrained to the available width" means for a note's
            // screenshot: it shrinks to fit the pane and is never blown up past its own pixels.
            Stretch = Stretch.Uniform,
            StretchDirection = StretchDirection.DownOnly,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        // Deliberately not awaited: the tree is built synchronously so the reader sees the note now,
        // and each image appears as it decodes. LoadAsync never throws.
        _ = LoadAsync(visual, source.LocalPath);

        return visual;
    }

    private async Task LoadAsync(Image target, string path)
    {
        try
        {
            // Decoded off the UI thread — a screenshot in a note can be several megapixels, and the
            // pane must not stop scrolling while one is read. The continuation comes back to the UI
            // thread, which is where the assignment has to happen.
            Bitmap bitmap = await Task.Run(() => new Bitmap(path)).ConfigureAwait(true);

            target.Source = bitmap;
        }
        catch (Exception failure)
        {
            // Caught broadly for the same reason as a block: decoding goes through the platform's
            // graphics stack, whose failures are not a documented set, and an image that cannot be
            // decoded is worth a log entry and an empty space rather than the whole preview.
            _onFailure(failure);
        }
    }

    private Control ImagePlaceholder(MarkdownImageSource source)
    {
        SelectableTextBlock text = new()
        {
            Text = Describe(source),
            TextWrapping = TextWrapping.Wrap,
            FontSize = _type.CodeFontSize,
        };

        MarkdownPreviewTheme.BindBrush(
            text,
            TextBlock.ForegroundProperty,
            MarkdownPreviewTheme.ForegroundTertiaryKey);

        Border border = new()
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 8),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = text,
        };

        MarkdownPreviewTheme.BindBrush(border, Border.BorderBrushProperty, MarkdownPreviewTheme.BorderSubtleKey);

        return border;
    }

    private static string Describe(MarkdownImageSource source) => source.Kind switch
    {
        // The link as written, not the resolved absolute path: what the reader can act on is the text
        // that is in their document.
        MarkdownImageKind.Local => $"Image not found: {source.Display}",
        MarkdownImageKind.Remote => $"Remote image not shown: {source.Display}",
        _ => string.IsNullOrWhiteSpace(source.Display)
            ? "Image with no link"
            : $"Image link cannot be resolved: {source.Display}",
    };

    private static List<LinkInline>? ImagesOnly(ContainerInline? inlines)
    {
        if (inlines is null)
        {
            return null;
        }

        List<LinkInline> images = [];

        for (MarkdigInline? current = inlines.FirstChild; current is not null; current = current.NextSibling)
        {
            switch (current)
            {
                case LinkInline { IsImage: true } image:
                    images.Add(image);
                    break;

                // Whitespace between two images, and the line break after them, are layout in the
                // source rather than content: they must not make the paragraph "mixed".
                case LiteralInline literal when string.IsNullOrWhiteSpace(literal.Content.ToString()):
                case LineBreakInline:
                    break;

                default:
                    return null;
            }
        }

        return images;
    }
}
