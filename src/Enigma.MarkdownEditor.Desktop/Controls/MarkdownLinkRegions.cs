// Using directives stay at file scope, above the namespace declaration, throughout this project:
// inside a namespace beginning with "Enigma.", the simple name "Avalonia" resolves against the
// referenced Enigma.Avalonia.* assemblies instead of Avalonia itself.
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Enigma.MarkdownEditor.Desktop.Controls;

/// <summary>
/// Which stretches of a rendered paragraph are links, and where they point.
/// </summary>
/// <remarks>
/// <para>
/// A link in the preview is a <see cref="Avalonia.Controls.Documents.Span"/>, and a span is not
/// something a pointer can be over: inlines are text elements, not input elements, so nothing in the
/// rendered document can carry a click of its own. What can is the text block around them — so the
/// renderer records the character range each link occupies, and a click is turned back into a link by
/// hit-testing the block's own text layout.
/// </para>
/// <para>
/// The alternative — a button per link inside an <c>InlineUIContainer</c> — would make every link an
/// atomic box that cannot be broken across lines. In a pane a third of the window wide that is the
/// difference between a wrapped sentence and one that runs off the edge.
/// </para>
/// <para>
/// Ranges are measured against <see cref="Avalonia.Controls.Documents.InlineCollection.Text"/> as the
/// inlines are added, never computed from the markdown: that string is exactly what the layout is
/// built from, so a line break contributing two characters on one platform and one on another is not
/// something this has to know.
/// </para>
/// </remarks>
public sealed class MarkdownLinkRegions
{
    /// <summary>
    /// Defines the attached property that carries a block's links.
    /// </summary>
    public static readonly AttachedProperty<MarkdownLinkRegions?> RegionsProperty =
        AvaloniaProperty.RegisterAttached<MarkdownLinkRegions, TextBlock, MarkdownLinkRegions?>("Regions");

    private static readonly Cursor Hand = new(StandardCursorType.Hand);

    private readonly List<Region> _regions = [];

    /// <summary>
    /// How many links the block holds.
    /// </summary>
    public int Count => _regions.Count;

    /// <summary>
    /// The links recorded for <paramref name="block"/>, or <see langword="null"/> when it holds none.
    /// </summary>
    /// <param name="block">The rendered text block.</param>
    /// <returns>The links.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is <see langword="null"/>.</exception>
    public static MarkdownLinkRegions? GetRegions(TextBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        return block.GetValue(RegionsProperty);
    }

    /// <summary>
    /// Records <paramref name="value"/> as <paramref name="block"/>'s links.
    /// </summary>
    /// <param name="block">The rendered text block.</param>
    /// <param name="value">The links, or <see langword="null"/> to clear them.</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is <see langword="null"/>.</exception>
    public static void SetRegions(TextBlock block, MarkdownLinkRegions? value)
    {
        ArgumentNullException.ThrowIfNull(block);

        block.SetValue(RegionsProperty, value);
    }

    /// <summary>
    /// The link the character at <paramref name="characterIndex"/> belongs to.
    /// </summary>
    /// <param name="characterIndex">An index into the block's text.</param>
    /// <returns>The link's URL, or <see langword="null"/> when that character is not part of one.</returns>
    public string? UrlAt(int characterIndex)
    {
        foreach (Region region in _regions)
        {
            if ((characterIndex >= region.Start) && (characterIndex < region.Start + region.Length))
            {
                return region.Url;
            }
        }

        return null;
    }

    /// <summary>
    /// The link under <paramref name="point"/>.
    /// </summary>
    /// <param name="block">The block the point is relative to.</param>
    /// <param name="point">A point in the block's own coordinates.</param>
    /// <returns>The link's URL, or <see langword="null"/> when the point is not over one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is <see langword="null"/>.</exception>
    public string? UrlAt(TextBlock block, Point point)
    {
        ArgumentNullException.ThrowIfNull(block);

        // The layout is placed inside the block's padding, so the point has to come in with it taken
        // off or every link sits a few pixels from where it is drawn.
        Point inText = new(point.X - block.Padding.Left, point.Y - block.Padding.Top);

        TextHitTestResult hit = block.TextLayout.HitTestPoint(inText);

        // IsInside is the whole guard: a click past the end of a short line lands on that line's last
        // character, and without this a link at the end of a line would answer for the empty space
        // after it.
        return hit.IsInside ? UrlAt(hit.TextPosition) : null;
    }

    /// <summary>
    /// Records a link occupying <paramref name="length"/> characters from <paramref name="start"/>.
    /// </summary>
    /// <param name="start">The index of the link's first character in the block's text.</param>
    /// <param name="length">How many characters it occupies.</param>
    /// <param name="url">Where it points, as the document wrote it.</param>
    internal void Add(int start, int length, string url)
    {
        if ((length > 0) && !string.IsNullOrWhiteSpace(url))
        {
            _regions.Add(new Region(start, length, url));
        }
    }

    /// <summary>
    /// Makes <paramref name="block"/>'s links clickable, and hovering one show a hand.
    /// </summary>
    /// <param name="block">The block that was just built.</param>
    /// <param name="activate">Told which link was clicked. Must not throw.</param>
    internal void Attach(SelectableTextBlock block, Action<string> activate)
    {
        SetRegions(block, this);

        // A visual with no background is not something Avalonia hit-tests: the pointer goes straight
        // through a text block to whatever is behind it. Transparent is invisible and solves that,
        // and it is set only on the blocks that actually have a link to click.
        block.Background ??= Brushes.Transparent;

        // handledEventsToo, and that is not belt and braces: a SelectableTextBlock handles both of
        // these itself to drive its selection, so an ordinary subscription would never be called.
        block.AddHandler(
            InputElement.PointerMovedEvent,
            (object? _, PointerEventArgs e)
                => block.Cursor = UrlAt(block, e.GetPosition(block)) is null ? null : Hand,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        block.PointerExited += (_, _) => block.Cursor = null;

        block.AddHandler(
            InputElement.PointerReleasedEvent,
            (object? _, PointerReleasedEventArgs e) =>
            {
                // A drag that ended inside a link was a selection, not a click on it — the reader was
                // copying the sentence, and opening their browser instead would be the wrong answer.
                if ((e.InitialPressMouseButton != MouseButton.Left)
                    || (block.SelectionStart != block.SelectionEnd))
                {
                    return;
                }

                if (UrlAt(block, e.GetPosition(block)) is { } url)
                {
                    activate(url);
                }
            },
            RoutingStrategies.Bubble,
            handledEventsToo: true);
    }

    private readonly record struct Region(int Start, int Length, string Url);
}
