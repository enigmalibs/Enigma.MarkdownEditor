// Using directives stay at file scope, above the namespace declaration, throughout this project:
// inside a namespace beginning with "Enigma.", the simple name "Avalonia" resolves against the
// referenced Enigma.Avalonia.* assemblies instead of Avalonia itself.
using System;
using System.Collections.Generic;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Enigma.MarkdownEditor.Desktop.Controls;

/// <summary>
/// Which characters of a rendered paragraph are a code chip's padding rather than anything the
/// document said, so a copy can leave them behind.
/// </summary>
/// <remarks>
/// <para>
/// A code span is drawn as a chip — a background behind a run of text — and an Avalonia inline has no
/// padding to set: <c>Run</c> carries a background, a foreground and a font, and that is the whole
/// list. The padding is therefore two characters of the run's own text (see
/// <see cref="Pad"/>), which is what makes the chip one rectangle rather than a box that has to be
/// aligned against the glyphs beside it.
/// </para>
/// <para>
/// The cost of that is a clipboard: a reader who drags across a sentence and copies it would get the
/// padding with the code, and this is an editor whose readers paste commands into shells. So the
/// renderer records where each pad character landed and <see cref="Strip(int, string)"/> takes those
/// characters back out of a selection — by index, never by searching for the character. Searching
/// would be wrong: <c>&amp;nbsp;</c> in a note renders as a real one, and deleting the reader's own
/// text to tidy up ours is not a trade worth making.
/// </para>
/// <para>
/// Indices are measured against
/// <see cref="Avalonia.Controls.Documents.InlineCollection.Text"/> as the inlines are added, exactly
/// as <see cref="MarkdownLinkRegions"/> measures link ranges, and for the same reason: that string is
/// what the layout and the selection are both built from.
/// </para>
/// </remarks>
public sealed class MarkdownCodePads
{
    /// <summary>
    /// The padding either side of a code chip's text.
    /// </summary>
    /// <remarks>
    /// A no-break space, so a line can never be broken between a chip and its own padding, and
    /// <c>U+00A0</c> rather than one of the typographic spaces because it is the one fixed-width space
    /// every font carries — a thin or figure space would come out as a missing-glyph box inside the
    /// chip on whichever monospace family the reader has chosen.
    /// </remarks>
    public const string Pad = "\u00A0";

    /// <summary>
    /// Defines the attached property that carries a block's pad characters.
    /// </summary>
    public static readonly AttachedProperty<MarkdownCodePads?> PadsProperty =
        AvaloniaProperty.RegisterAttached<MarkdownCodePads, TextBlock, MarkdownCodePads?>("Pads");

    // A set rather than a sorted list: the indices arrive in order today, because the renderer walks
    // the inlines in order, but nothing about a copy should depend on that staying true.
    private readonly HashSet<int> _indices = [];

    /// <summary>
    /// How many pad characters the block holds — two per code span.
    /// </summary>
    public int Count => _indices.Count;

    /// <summary>
    /// The pads recorded for <paramref name="block"/>, or <see langword="null"/> when it holds none.
    /// </summary>
    /// <param name="block">The rendered text block.</param>
    /// <returns>The pads.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is <see langword="null"/>.</exception>
    public static MarkdownCodePads? GetPads(TextBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);

        return block.GetValue(PadsProperty);
    }

    /// <summary>
    /// Records <paramref name="value"/> as <paramref name="block"/>'s pads.
    /// </summary>
    /// <param name="block">The rendered text block.</param>
    /// <param name="value">The pads, or <see langword="null"/> to clear them.</param>
    /// <exception cref="ArgumentNullException"><paramref name="block"/> is <see langword="null"/>.</exception>
    public static void SetPads(TextBlock block, MarkdownCodePads? value)
    {
        ArgumentNullException.ThrowIfNull(block);

        block.SetValue(PadsProperty, value);
    }

    /// <summary>
    /// <paramref name="selected"/> without whatever of <paramref name="block"/>'s chip padding falls
    /// inside it.
    /// </summary>
    /// <param name="block">The block the selection was read off.</param>
    /// <param name="start">The index of the selection's first character in the block's text.</param>
    /// <param name="selected">The selected text.</param>
    /// <returns>The text to copy.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="block"/> or <paramref name="selected"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// A block with no chips in it carries no pads, and this hands the selection straight back.
    /// </remarks>
    public static string Strip(TextBlock block, int start, string selected)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(selected);

        return GetPads(block)?.Strip(start, selected) ?? selected;
    }

    /// <summary>
    /// Whether the character at <paramref name="characterIndex"/> is a chip's padding.
    /// </summary>
    /// <param name="characterIndex">An index into the block's text.</param>
    /// <returns><see langword="true"/> when it is padding.</returns>
    public bool IsPad(int characterIndex) => _indices.Contains(characterIndex);

    /// <summary>
    /// <paramref name="selected"/> without whatever padding falls inside it.
    /// </summary>
    /// <param name="start">The index of the selection's first character in the block's text.</param>
    /// <param name="selected">The selected text.</param>
    /// <returns>The text to copy.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selected"/> is <see langword="null"/>.</exception>
    public string Strip(int start, string selected)
    {
        ArgumentNullException.ThrowIfNull(selected);

        if ((_indices.Count == 0) || (selected.Length == 0))
        {
            return selected;
        }

        StringBuilder kept = new(selected.Length);

        for (int offset = 0; offset < selected.Length; offset++)
        {
            if (!IsPad(start + offset))
            {
                kept.Append(selected[offset]);
            }
        }

        // The common case by far — most paragraphs hold no chip, and a selection that missed the ones
        // they do hold is just as untouched.
        return kept.Length == selected.Length ? selected : kept.ToString();
    }

    /// <summary>
    /// Records the pad character at <paramref name="characterIndex"/>.
    /// </summary>
    /// <param name="characterIndex">Its index in the block's text.</param>
    internal void Add(int characterIndex)
    {
        if (characterIndex >= 0)
        {
            _indices.Add(characterIndex);
        }
    }

    /// <summary>
    /// Hands <paramref name="block"/> its pads, so the copy path can find them again.
    /// </summary>
    /// <param name="block">The block that was just built.</param>
    internal void Attach(SelectableTextBlock block) => SetPads(block, this);
}
