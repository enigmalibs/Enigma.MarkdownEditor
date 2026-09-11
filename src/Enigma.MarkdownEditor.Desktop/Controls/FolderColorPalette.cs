using Avalonia.Media;
using Avalonia.Media.Immutable;
using Enigma.MarkdownEditor.Core.Workspaces;

namespace Enigma.MarkdownEditor.Desktop.Controls;

/// <summary>
/// What each <see cref="FolderColor"/> is painted with.
/// </summary>
/// <remarks>
/// <para>
/// One palette for both theme variants, and deliberately: these colours are used as small marks — a
/// 4px bar beside a tree row and beside a tab — so what they have to be is unmistakably distinct from
/// each other and legible on either ground, which a set of mid-tones is. A light/dark pair per colour
/// would mean a theme dictionary, a variant lookup, and a change notification on every theme switch
/// for eight accents nobody reads as text.
/// </para>
/// <para>
/// Here rather than in XAML so there is one source of truth: a ViewModel reads it through
/// <see cref="Brush(FolderColor)"/> and markup reads the members directly with <c>{x:Static}</c>. The
/// brushes are immutable and built once — they are shared by every row and every tab.
/// </para>
/// </remarks>
public static class FolderColorPalette
{
    /// <summary>
    /// Red.
    /// </summary>
    public static IBrush Red { get; } = Solid(0xE5, 0x48, 0x4D);

    /// <summary>
    /// Orange.
    /// </summary>
    public static IBrush Orange { get; } = Solid(0xEF, 0x6C, 0x14);

    /// <summary>
    /// Yellow — a dark gold rather than a true yellow, which is the one hue of the eight that cannot
    /// be seen on a light background at its own brightness.
    /// </summary>
    public static IBrush Yellow { get; } = Solid(0xC0, 0x92, 0x00);

    /// <summary>
    /// Green.
    /// </summary>
    public static IBrush Green { get; } = Solid(0x2E, 0xA0, 0x67);

    /// <summary>
    /// Teal.
    /// </summary>
    public static IBrush Teal { get; } = Solid(0x11, 0x9F, 0x94);

    /// <summary>
    /// Blue.
    /// </summary>
    public static IBrush Blue { get; } = Solid(0x35, 0x84, 0xE4);

    /// <summary>
    /// Purple.
    /// </summary>
    public static IBrush Purple { get; } = Solid(0x8E, 0x4E, 0xC6);

    /// <summary>
    /// Pink.
    /// </summary>
    public static IBrush Pink { get; } = Solid(0xD6, 0x40, 0x9F);

    /// <summary>
    /// The brush a colour is painted with.
    /// </summary>
    /// <param name="color">The colour to paint.</param>
    /// <returns>
    /// The brush, or <see langword="null"/> for <see cref="FolderColor.None"/> — and for any value
    /// this build does not know, which a hand-edited file can hold.
    /// </returns>
    /// <remarks>
    /// <see langword="null"/> rather than a transparent brush, because that is what the controls
    /// reading it want: a <c>Border</c> with no background paints nothing and still occupies its
    /// width, so a row or a tab is the same size coloured or not.
    /// </remarks>
    public static IBrush? Brush(FolderColor color)
        => color switch
        {
            FolderColor.Red => Red,
            FolderColor.Orange => Orange,
            FolderColor.Yellow => Yellow,
            FolderColor.Green => Green,
            FolderColor.Teal => Teal,
            FolderColor.Blue => Blue,
            FolderColor.Purple => Purple,
            FolderColor.Pink => Pink,
            _ => null,
        };

    private static IBrush Solid(byte red, byte green, byte blue)
        => new ImmutableSolidColorBrush(Color.FromRgb(red, green, blue));
}
