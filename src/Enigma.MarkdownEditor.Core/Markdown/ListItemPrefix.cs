using System;
using System.Globalization;

namespace Enigma.MarkdownEditor.Core.Markdown;

/// <summary>
/// A list, task or quote line taken apart: everything before the item's own text, plus that text.
/// </summary>
/// <param name="Indent">The line's leading white-space, exactly as it was written.</param>
/// <param name="Quote">The block-quote markers, <c>"&gt; &gt; "</c> and the like, or empty.</param>
/// <param name="Marker">The list marker — <c>-</c>, <c>*</c>, <c>+</c>, <c>1.</c>, <c>1)</c> — or empty on a quote line.</param>
/// <param name="MarkerSpacing">The white-space between the marker and what follows it.</param>
/// <param name="Checkbox">A task box, <c>[ ]</c> or <c>[x]</c>, or empty.</param>
/// <param name="CheckboxSpacing">The white-space after the task box.</param>
/// <param name="Content">The item's own text, which is what decides whether the item is empty.</param>
/// <remarks>
/// Every part is kept verbatim rather than normalized, because the point of the split is to write the
/// next line the way the user wrote this one: their bullet character, their spacing, their quote
/// depth. The one part that is not copied is an ordered marker's number, which is counted on.
/// </remarks>
public sealed record ListItemPrefix(
    string Indent,
    string Quote,
    string Marker,
    string MarkerSpacing,
    string Checkbox,
    string CheckboxSpacing,
    string Content)
{
    /// <summary>
    /// How many columns a tab advances. CommonMark's own figure, and the one every indentation
    /// comparison here is made in so that a tab-indented file nests the same way a space-indented one
    /// does.
    /// </summary>
    public const int TabWidth = 4;

    /// <summary>
    /// The task box a continued item starts with, whatever state the item above it is in: continuing
    /// a done item with another done item is never what was meant.
    /// </summary>
    public const string UncheckedBox = "[ ]";

    /// <summary>
    /// Gets what kind of list this line's marker starts.
    /// </summary>
    public ListMarkerKind Kind => Marker.Length == 0
        ? ListMarkerKind.None
        : char.IsAsciiDigit(Marker[0]) ? ListMarkerKind.Ordered : ListMarkerKind.Bullet;

    /// <summary>
    /// Gets a value indicating whether this is a task item.
    /// </summary>
    public bool IsTask => Checkbox.Length > 0;

    /// <summary>
    /// Gets how many characters of the line come before <see cref="Content"/>.
    /// </summary>
    public int Length => Indent.Length
        + Quote.Length
        + Marker.Length
        + MarkerSpacing.Length
        + Checkbox.Length
        + CheckboxSpacing.Length;

    /// <summary>
    /// Gets a value indicating whether the item has nothing in it — which is what ends a list.
    /// </summary>
    public bool IsEmpty => string.IsNullOrWhiteSpace(Content);

    /// <summary>
    /// Gets the column the line's indentation ends at, tabs expanded.
    /// </summary>
    public int IndentWidth => ColumnAfter(Indent, 0);

    /// <summary>
    /// Gets the column <see cref="Content"/> starts at, tabs expanded.
    /// </summary>
    /// <remarks>
    /// This is what a nested item indents to. CommonMark reads a list under an item as nested exactly
    /// when it is indented to at least the parent's content column, so lining a child up with the text
    /// of the item above it is both what looks right and what the parser needs.
    /// </remarks>
    public int ContentColumn
    {
        get
        {
            int column = ColumnAfter(Indent, 0);
            column = ColumnAfter(Quote, column);
            column = ColumnAfter(Marker, column);
            column = ColumnAfter(MarkerSpacing, column);
            column = ColumnAfter(Checkbox, column);

            return ColumnAfter(CheckboxSpacing, column);
        }
    }

    /// <summary>
    /// Gets the text the next line of this list starts with: the same indent, quote depth and spacing,
    /// the next marker, and an empty task box when this item has one.
    /// </summary>
    public string Continuation => IsTask
        ? string.Concat(Indent, Quote, NextMarker, MarkerSpacing, UncheckedBox, CheckboxSpacing)
        : string.Concat(Indent, Quote, NextMarker, MarkerSpacing);

    /// <summary>
    /// Gets the text a line opened <em>above</em> this item starts with: everything
    /// <see cref="Continuation"/> repeats, but with this item's own marker rather than the next one.
    /// </summary>
    /// <remarks>
    /// What vim's <c>O</c> writes. A line inserted before an item takes that item's place in the list
    /// and pushes it down, so counting on would number the new item after the one it precedes —
    /// <c>3.</c> above <c>2.</c>, which the user then has to correct by hand. The task box is still
    /// the empty one, for the same reason <see cref="Continuation"/>'s is.
    /// </remarks>
    public string Opening => IsTask
        ? string.Concat(Indent, Quote, Marker, MarkerSpacing, UncheckedBox, CheckboxSpacing)
        : string.Concat(Indent, Quote, Marker, MarkerSpacing);

    /// <summary>
    /// Works out the column a run of text ends at, expanding tabs to the next multiple of
    /// <see cref="TabWidth"/>.
    /// </summary>
    /// <param name="text">The run to measure.</param>
    /// <param name="startColumn">The column it starts at.</param>
    /// <returns>The column immediately after it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    public static int ColumnAfter(string text, int startColumn)
    {
        ArgumentNullException.ThrowIfNull(text);

        int column = startColumn;

        foreach (char character in text)
        {
            column = character == '\t' ? ((column / TabWidth) + 1) * TabWidth : column + 1;
        }

        return column;
    }

    // A bullet repeats; a number counts on. Leading zeros are kept only when the user wrote them, so
    // "09." becomes "10." rather than "010." and "9." becomes "10.".
    private string NextMarker
    {
        get
        {
            if (Kind != ListMarkerKind.Ordered)
            {
                return Marker;
            }

            string digits = Marker[..^1];
            string delimiter = Marker[^1..];

            if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out int number))
            {
                return Marker;
            }

            string next = (number + 1).ToString(CultureInfo.InvariantCulture);

            return string.Concat(digits[0] == '0' ? next.PadLeft(digits.Length, '0') : next, delimiter);
        }
    }
}
