using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// The installed fonts, classified once.
/// </summary>
/// <remarks>
/// <para>
/// Two halves, deliberately separated. This class is the questions asked of a font list — which
/// families there are, which of them are fixed-width, which one the editor should fall back to — and
/// it holds nothing but lists of strings. <see cref="ForCurrentSystem"/> is the other half: the one
/// place that reads the platform's font manager and measures glyphs.
/// </para>
/// <para>
/// The split is what makes the fallback order testable. A headless test session has no real fonts —
/// Avalonia's headless platform reports a single synthetic family — so a catalog that could only ever
/// be built from the running machine's fonts could only ever be asserted against whatever happened to
/// be installed on it.
/// </para>
/// <para>
/// Registered as a singleton, because <see cref="ForCurrentSystem"/> is expensive in a way that scales
/// with the machine: a typical Windows install has several hundred families, and each one is
/// classified by loading its typeface and measuring two glyphs.
/// </para>
/// </remarks>
public sealed class FontCatalog : IFontCatalog
{
    /// <summary>
    /// The families the editor prefers, best first. The first one installed wins.
    /// </summary>
    /// <remarks>
    /// A short list of good code fonts across the platforms this application runs on rather than a
    /// long list of everything: Cascadia Mono and Consolas cover Windows, DejaVu Sans Mono and
    /// Liberation Mono cover the common Linux distributions, JetBrains Mono is there because a
    /// developer who installed it meant it, and Courier New is the one that is always somewhere.
    /// </remarks>
    public static readonly IReadOnlyList<string> PreferredMonospaceFamilies =
    [
        "Cascadia Mono",
        "Consolas",
        "JetBrains Mono",
        "DejaVu Sans Mono",
        "Liberation Mono",
        "Courier New",
    ];

    // The pair every classification measures. 'i' is the narrowest letter of the alphabet and 'W' the
    // widest, so in a proportional family their advances differ by a lot and in a fixed-width one they
    // are equal by definition — which makes one comparison enough, with no threshold to tune.
    private const char Narrowest = 'i';
    private const char Widest = 'W';

    private readonly HashSet<string> _installed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FontCatalog"/> class over a font list that has
    /// already been gathered.
    /// </summary>
    /// <param name="allFamilies">Every installed family. Duplicates and case differences are collapsed.</param>
    /// <param name="monospaceFamilies">
    /// Those of <paramref name="allFamilies"/> that are fixed-width. Names not in
    /// <paramref name="allFamilies"/> are ignored — a font cannot be monospace and absent.
    /// </param>
    /// <param name="systemDefaultFamily">
    /// The family the rest of the interface is drawn in, used both as the last-resort editor font and
    /// to render a picker row for a font that is not here to render itself.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="systemDefaultFamily"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public FontCatalog(
        IEnumerable<string> allFamilies,
        IEnumerable<string> monospaceFamilies,
        string systemDefaultFamily)
    {
        ArgumentNullException.ThrowIfNull(allFamilies);
        ArgumentNullException.ThrowIfNull(monospaceFamilies);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemDefaultFamily);

        SystemDefaultFamily = systemDefaultFamily;

        _installed = new HashSet<string>(
            allFamilies.Where(family => !string.IsNullOrWhiteSpace(family)),
            StringComparer.OrdinalIgnoreCase);

        AllFamilies = Sorted(_installed);

        // Intersected rather than trusted: the two lists come from the same probe in production, but
        // a monospace name with no installed family behind it would put a row in the picker that
        // renders as something else entirely.
        MonospaceFamilies = Sorted(monospaceFamilies.Where(_installed.Contains));

        DefaultMonospaceFamily = ChooseDefaultMonospace();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> AllFamilies { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> MonospaceFamilies { get; }

    /// <inheritdoc />
    public string DefaultMonospaceFamily { get; }

    /// <inheritdoc />
    public string SystemDefaultFamily { get; }

    /// <summary>
    /// Reads the machine's installed fonts and classifies every one of them.
    /// </summary>
    /// <returns>A catalog of what is actually installed here.</returns>
    /// <remarks>
    /// The only method in this class that touches the platform, and the reason the rest of it takes
    /// its lists as arguments. Call it once — see the note on the class about what it costs.
    /// </remarks>
    public static FontCatalog ForCurrentSystem()
    {
        FontManager fonts = FontManager.Current;

        List<string> families = [.. fonts.SystemFonts.Select(family => family.Name)];
        List<string> monospace = [.. families.Where(IsMonospace)];

        return new FontCatalog(families, monospace, fonts.DefaultFontFamily.Name);
    }

    /// <summary>
    /// Measures <paramref name="family"/> and says whether every character in it is the same width.
    /// </summary>
    /// <param name="family">An installed family name.</param>
    /// <returns>
    /// <see langword="true"/> when the family is fixed-width; <see langword="false"/> when it is not,
    /// or when it could not be measured at all.
    /// </returns>
    /// <remarks>
    /// A family that cannot be measured is reported as proportional rather than as monospace, so a
    /// font the platform cannot load stays out of the editor's picker instead of being offered and
    /// then rendering as something else.
    /// </remarks>
    public static bool IsMonospace(string? family)
    {
        if (string.IsNullOrWhiteSpace(family))
        {
            return false;
        }

        if (!FontManager.Current.TryGetGlyphTypeface(new Typeface(new FontFamily(family)), out GlyphTypeface? glyphs) ||
            (glyphs is null))
        {
            return false;
        }

        return SameAdvance(glyphs, Narrowest, Widest);
    }

    /// <inheritdoc />
    public bool IsAvailable(string? family)
        => !string.IsNullOrWhiteSpace(family) && _installed.Contains(family);

    /// <inheritdoc />
    public string ResolveEditorFamily(string? storedFamily)
        => IsAvailable(storedFamily) ? storedFamily! : DefaultMonospaceFamily;

    private static bool SameAdvance(GlyphTypeface glyphs, char first, char second)
    {
        if (!TryGetAdvance(glyphs, first, out ushort firstAdvance) ||
            !TryGetAdvance(glyphs, second, out ushort secondAdvance))
        {
            return false;
        }

        // Zero would make a face with no metrics at all look perfectly uniform.
        return (firstAdvance > 0) && (firstAdvance == secondAdvance);
    }

    private static bool TryGetAdvance(GlyphTypeface glyphs, char character, out ushort advance)
    {
        advance = 0;

        // Glyph 0 is .notdef by definition, which a cmap answers with for a character the font has no
        // glyph for. Measuring it would compare the same missing-character box against itself and call
        // the result uniform.
        ushort glyph = glyphs.CharacterToGlyphMap[character];

        return (glyph != 0) && glyphs.TryGetHorizontalGlyphAdvance(glyph, out advance);
    }

    private static IReadOnlyList<string> Sorted(IEnumerable<string> families)
        => [.. new HashSet<string>(families, StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];

    private string ChooseDefaultMonospace()
    {
        // The preferred list first, so a machine with Cascadia Mono gets Cascadia Mono rather than
        // whatever sorts first alphabetically among a few hundred families.
        foreach (string preferred in PreferredMonospaceFamilies)
        {
            if (_installed.Contains(preferred))
            {
                return preferred;
            }
        }

        // Then any fixed-width font at all, and only then the UI font: a proportional editor is worse
        // than an unfamiliar monospace one, but it beats having nothing to render with.
        return MonospaceFamilies.Count > 0 ? MonospaceFamilies[0] : SystemDefaultFamily;
    }
}
