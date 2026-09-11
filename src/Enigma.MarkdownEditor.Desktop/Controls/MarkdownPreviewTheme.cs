using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;

namespace Enigma.MarkdownEditor.Desktop.Controls;

/// <summary>
/// How the preview is painted and sized: the palette keys every visual is bound to, and the type
/// scale the block renderers use.
/// </summary>
/// <remarks>
/// <para>
/// Every colour is a resource <em>key</em>, applied through <see cref="BindBrush"/> as a dynamic
/// resource rather than resolved to a brush here. That is what makes a theme switch free: the window
/// changes variant, every bound property re-resolves, and the rendered tree is not rebuilt. A
/// renderer that read brushes would have to re-render the whole document to change colour.
/// </para>
/// <para>
/// The scale is the one deviation from "let the theme decide": a heading has to be visibly a heading,
/// and the Enigma palette has opinions about colour rather than about type. What it is <em>not</em> is
/// a set of sizes — the reader chooses the body size, so a heading is a ratio of it (see
/// <see cref="HeadingScale"/>) and the absolute number comes out of
/// <see cref="MarkdownPreviewTypography"/>.
/// </para>
/// </remarks>
public static class MarkdownPreviewTheme
{
    /// <summary>
    /// Body text, headings, and anything else that is simply prose.
    /// </summary>
    public const string ForegroundKey = "EnigmaForegroundBrush";

    /// <summary>
    /// List markers and other structure that should read as quieter than the text it introduces.
    /// </summary>
    public const string ForegroundSecondaryKey = "EnigmaForegroundSecondaryBrush";

    /// <summary>
    /// The quietest text there is: what a placeholder says about an image that is not there.
    /// </summary>
    public const string ForegroundTertiaryKey = "EnigmaForegroundTertiaryBrush";

    /// <summary>
    /// Links, and the rule down the left of a block quote.
    /// </summary>
    public const string AccentKey = "EnigmaAccentBrush";

    /// <summary>
    /// Behind a code span, so it reads as a chip inside the sentence.
    /// </summary>
    public const string CodeBackgroundKey = "EnigmaSurfaceHighBrush";

    /// <summary>
    /// Behind a block quote — a shade off the page rather than a panel.
    /// </summary>
    public const string QuoteBackgroundKey = "EnigmaSurfaceLowBrush";

    /// <summary>
    /// Behind a fenced code block, and behind a table's header row.
    /// </summary>
    /// <remarks>
    /// The same surface a code span sits on, aliased rather than repeated: all three are the palette
    /// saying "this is not prose", and giving them separate keys would only invite them to drift
    /// apart. The alias is what the renderer names, so a future palette that does distinguish them
    /// changes here and nowhere else.
    /// </remarks>
    public const string CodeBlockBackgroundKey = CodeBackgroundKey;

    /// <inheritdoc cref="CodeBlockBackgroundKey"/>
    public const string TableHeaderBackgroundKey = CodeBackgroundKey;

    /// <summary>
    /// Behind the chip that says a note has front matter — the quietest surface there is, because the
    /// chip is not part of what was written.
    /// </summary>
    public const string MetadataBackgroundKey = QuoteBackgroundKey;

    /// <summary>
    /// Thematic breaks, and the border of an image placeholder.
    /// </summary>
    public const string BorderSubtleKey = "EnigmaBorderSubtleBrush";

    /// <summary>
    /// The three the inline error block is painted with.
    /// </summary>
    public const string ErrorBackgroundKey = "EnigmaErrorBackgroundBrush";

    /// <inheritdoc cref="ErrorBackgroundKey"/>
    public const string ErrorBorderKey = "EnigmaErrorBorderBrush";

    /// <inheritdoc cref="ErrorBackgroundKey"/>
    public const string ErrorKey = "EnigmaErrorBrush";

    /// <summary>
    /// The class the inline error block carries, so a failure is identifiable in the tree rather than
    /// only by the words in it.
    /// </summary>
    public const string ErrorBlockClass = "previewError";

    /// <summary>
    /// The class a fenced code block carries.
    /// </summary>
    public const string CodeBlockClass = "previewCode";

    /// <summary>
    /// The class a rendered table carries.
    /// </summary>
    public const string TableClass = "previewTable";

    /// <summary>
    /// The class the front-matter chip carries.
    /// </summary>
    public const string MetadataChipClass = "previewMetadata";

    /// <summary>
    /// Every distinct key above, for a test to assert that the palette still defines all of them.
    /// </summary>
    /// <remarks>
    /// A renamed key in the theme package costs one wrong colour at runtime and nothing else — every
    /// visual falls back to an inherited brush — which is exactly the kind of quiet regression worth
    /// one test.
    /// <para>
    /// The aliases above are deliberately absent: they are the same key strings under a second name,
    /// and listing them would say nothing more about the palette while breaking the assertion that
    /// this list holds no duplicates.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> BrushKeys { get; } =
    [
        ForegroundKey,
        ForegroundSecondaryKey,
        ForegroundTertiaryKey,
        AccentKey,
        CodeBackgroundKey,
        QuoteBackgroundKey,
        BorderSubtleKey,
        ErrorBackgroundKey,
        ErrorBorderKey,
        ErrorKey,
    ];

    /// <summary>
    /// The body size the scale below is written at.
    /// </summary>
    public const double ReferenceBodyFontSize = 14;

    /// <summary>
    /// The size a heading of each level is set at when prose is set at
    /// <see cref="ReferenceBodyFontSize"/>, h1 first. Any other body size scales these in proportion.
    /// </summary>
    /// <remarks>
    /// The sizes this renderer used when the body size was fixed, kept as the scale's own reference
    /// point rather than converted into six ratios. Same shape either way, but a ratio is a rounded
    /// number and these are not: written this way, a document set at 14 comes out at exactly the sizes
    /// below rather than a hair off each of them.
    /// <para>
    /// A table of absolute sizes on its own could not survive the body becoming a setting — an h3 of
    /// 17 under an 18pt paragraph would be a heading smaller than the text it introduces — which is
    /// what <see cref="HeadingFontSize"/> scaling them is for.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<double> HeadingSizesAtReferenceBody { get; } = [24, 20, 17, 15.5, 14.5, 14];

    /// <summary>
    /// The size a heading of <paramref name="level"/> is set at.
    /// </summary>
    /// <param name="level">The heading level, 1–6. Anything outside that is clamped.</param>
    /// <param name="bodyFontSize">The size prose is set at, which the heading is scaled against.</param>
    /// <returns>The font size.</returns>
    /// <remarks>
    /// Multiplied before it is divided, deliberately: the same arithmetic done as a ratio first loses
    /// the last bit of a size like 14.5, and 14.500000000000002 is the kind of value that survives into
    /// a layout and never quite matches anything again.
    /// </remarks>
    public static double HeadingFontSize(int level, double bodyFontSize)
    {
        double reference = HeadingSizesAtReferenceBody[
            Math.Clamp(level, 1, HeadingSizesAtReferenceBody.Count) - 1];

        return reference * bodyFontSize / ReferenceBodyFontSize;
    }

    /// <summary>
    /// The weight a heading of <paramref name="level"/> is set in.
    /// </summary>
    /// <param name="level">The heading level, 1–6. Anything outside that is clamped.</param>
    /// <returns>The font weight.</returns>
    /// <remarks>
    /// The deepest levels stay bold rather than growing smaller than the body text: an h6 that is
    /// both small and light stops being a heading at all.
    /// </remarks>
    public static FontWeight HeadingFontWeight(int level) => level <= 2 ? FontWeight.Bold : FontWeight.SemiBold;

    /// <summary>
    /// Binds <paramref name="property"/> on <paramref name="target"/> to the theme resource
    /// <paramref name="key"/>, so it follows the window's variant for as long as the visual lives.
    /// </summary>
    /// <param name="target">The visual — a control or a text inline.</param>
    /// <param name="property">The brush property to bind.</param>
    /// <param name="key">One of the keys on this class.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static void BindBrush(AvaloniaObject target, AvaloniaProperty property, string key)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(property);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        // A key that does not resolve leaves the property unset, so the visual inherits whatever the
        // pane around it is painted in. That is the right failure for a palette that arrives from a
        // package: one wrong colour rather than invisible text.
        target.Bind(property, new DynamicResourceExtension(key));
    }
}
