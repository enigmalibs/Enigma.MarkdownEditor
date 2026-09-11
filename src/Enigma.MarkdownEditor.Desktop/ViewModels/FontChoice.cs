using System;

namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// One row of a font picker: what it is called, what family it selects, and what to draw its own label
/// in.
/// </summary>
/// <remarks>
/// Three rows exist, and the third is the reason this is a type rather than a list of strings. A named
/// family is drawn in itself, which is the only useful way to choose a font. "Automatic" selects no
/// family at all and is drawn in the one it would resolve to. And a family the settings file names but
/// this machine does not have has to be shown — otherwise selecting nothing would silently overwrite
/// it — but it cannot be drawn in itself, because it is not here.
/// </remarks>
public sealed class FontChoice
{
    /// <summary>
    /// The label the "automatic" row carries in a picker that offers fixed-width families. It names no
    /// family on purpose — which font it resolves to depends on the machine, and the row is drawn in
    /// that font, which says it better than any name in parentheses would.
    /// </summary>
    public const string AutomaticLabel = "Automatic (best available monospace)";

    /// <summary>
    /// The label the "automatic" row carries in a picker that offers every family. Its own wording,
    /// because what automatic means there is the font this machine reads in rather than the best code
    /// font it has.
    /// </summary>
    public const string AutomaticReadingLabel = "Automatic (the default reading font)";

    private FontChoice(string family, string label, string previewFamily)
    {
        Family = family;
        Label = label;
        PreviewFamily = previewFamily;
    }

    /// <summary>
    /// Gets the family this row selects, or an empty string for "automatic".
    /// </summary>
    /// <remarks>
    /// Written straight into <c>AppSettings.EditorFontFamily</c>, empty string included — that is what
    /// the setting's own "automatic" value is.
    /// </remarks>
    public string Family { get; }

    /// <summary>
    /// Gets the text the row shows.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the family the row's own label is drawn in. Always a family that can be rendered.
    /// </summary>
    public string PreviewFamily { get; }

    /// <summary>
    /// Gets a value indicating whether this row is the "automatic" one.
    /// </summary>
    public bool IsAutomatic => Family.Length == 0;

    /// <summary>
    /// Creates the "automatic" row for a picker that offers the fixed-width families.
    /// </summary>
    /// <param name="resolvedFamily">The family automatic currently resolves to, which the row is drawn in.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentException"><paramref name="resolvedFamily"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="resolvedFamily"/> is <see langword="null"/>.</exception>
    public static FontChoice Automatic(string resolvedFamily)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedFamily);

        return new FontChoice(string.Empty, AutomaticLabel, resolvedFamily);
    }

    /// <summary>
    /// Creates the "automatic" row for a picker that offers every installed family rather than the
    /// fixed-width ones.
    /// </summary>
    /// <param name="resolvedFamily">The family automatic currently resolves to, which the row is drawn in.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentException"><paramref name="resolvedFamily"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="resolvedFamily"/> is <see langword="null"/>.</exception>
    public static FontChoice AutomaticReading(string resolvedFamily)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resolvedFamily);

        return new FontChoice(string.Empty, AutomaticReadingLabel, resolvedFamily);
    }

    /// <summary>
    /// Creates a row for an installed family.
    /// </summary>
    /// <param name="family">The family name.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentException"><paramref name="family"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="family"/> is <see langword="null"/>.</exception>
    public static FontChoice Installed(string family)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);

        return new FontChoice(family, family, family);
    }

    /// <summary>
    /// Creates a row for a family the settings file names but this machine does not have.
    /// </summary>
    /// <param name="family">The stored family name, kept exactly as it is stored.</param>
    /// <param name="previewFamily">The family to draw the row in, since the stored one cannot be.</param>
    /// <returns>The row.</returns>
    /// <exception cref="ArgumentException">Either argument is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    public static FontChoice Missing(string family, string previewFamily)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);
        ArgumentException.ThrowIfNullOrWhiteSpace(previewFamily);

        return new FontChoice(family, $"{family} (not installed)", previewFamily);
    }
}
