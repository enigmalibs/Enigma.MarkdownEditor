using System;

namespace Enigma.MarkdownEditor.Desktop.Controls;

/// <summary>
/// How the preview sets type: the family prose is read in, the family code is read in, the size each
/// of the two is set at, and how far apart the lines sit.
/// </summary>
/// <param name="FontFamily">
/// The family prose is set in, or empty to inherit whatever the pane around the preview is set in.
/// </param>
/// <param name="CodeFontFamily">
/// The family code spans and fenced blocks are set in, or empty to inherit.
/// </param>
/// <param name="BodyFontSize">
/// The size prose is set at. The headings and the preview's own asides are derived from it.
/// </param>
/// <param name="CodeFontSize">The size code spans and fenced blocks are set at.</param>
/// <param name="LineHeight">
/// How far apart lines sit, as a multiple of the size they are set at — so it holds for a heading as
/// well as for a paragraph.
/// </param>
/// <remarks>
/// <para>
/// The values the preview's type scale used to hardcode, gathered into one thing so that
/// changing them is one assignment rather than a rebuild of the renderer. Colour is deliberately not
/// here: it stays a set of resource keys on <see cref="MarkdownPreviewTheme"/>, bound dynamically, so
/// a theme switch still costs no re-render. Type is the opposite — a size change *is* a re-render —
/// which is why the two are separated rather than merged into one "theme".
/// </para>
/// <para>
/// Both families name one family, never a comma-separated stack: the renderer reads the first name of
/// a stack and ignores the rest, so a stack is not a fallback chain but a family name with some
/// unread text after it.
/// </para>
/// <para>
/// A family is a name that will actually render, or empty. Resolving "the best monospace family here"
/// needs the installed fonts, which is <see cref="Services.IFontCatalog"/>'s business and not a
/// control's — so whoever builds the record does the resolving, and this type only carries the
/// answer. <see cref="Default"/> therefore inherits rather than naming a family it cannot know.
/// </para>
/// </remarks>
public sealed record MarkdownPreviewTypography(
    string FontFamily,
    string CodeFontFamily,
    double BodyFontSize,
    double CodeFontSize,
    double LineHeight)
{
    /// <summary>
    /// The type a preview is set in when nobody has said otherwise: the host's own font, at the size
    /// and spacing this renderer has always used.
    /// </summary>
    /// <remarks>
    /// The sizes are 14 and 13 because that is the scale the preview was fixed at before either
    /// became a setting — code a point under prose, as it was derived — which is what makes this
    /// instance a safe fallback rather than a second opinion. The application always supplies a
    /// record built from <c>AppSettings</c>; this is what a preview built by hand — a designer, a
    /// test — gets.
    /// </remarks>
    public static MarkdownPreviewTypography Default { get; } =
        new(string.Empty, string.Empty, BodyFontSize: 14, CodeFontSize: 13, LineHeight: 1.5);

    /// <summary>
    /// Gets the size the preview's own asides are set at — the front-matter chip, an image
    /// placeholder, the inline error block. Quieter than prose, because none of them is the document.
    /// </summary>
    public double AsideFontSize => BodyFontSize - 2;

    /// <summary>
    /// Gets the size the preview draws a task item's checkbox at — the box itself, not the row it
    /// sits on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Derived rather than a setting of its own, because a box is a mark on a line of prose and
    /// nobody asked to size one independently of the prose: a box that does not follow
    /// <see cref="BodyFontSize"/> is drawn to a different scale from the bullet in the very same
    /// list, which is the half of the misalignment that the top margin cannot fix.
    /// </para>
    /// <para>
    /// The multiplier is 1.1 so the shipped 18pt default lands on 20 — exactly the size the
    /// renderer drew before the box joined the scale — and the size nobody complained about is the
    /// size it keeps. The clamp is what a multiplier cannot do on its own: below 12 the box stops
    /// being something a pointer can hit, and above 36 it reads as a button rather than as a
    /// marker.
    /// </para>
    /// </remarks>
    public double TaskBoxSize => Math.Clamp(Math.Round(BodyFontSize * 1.1), 12, 36);

    /// <summary>
    /// The line height a run of text set at <paramref name="fontSize"/> is laid out with.
    /// </summary>
    /// <param name="fontSize">The size the text is set at.</param>
    /// <returns>The line height, in the same units.</returns>
    /// <remarks>
    /// The multiplier is applied per run rather than once for the document because a heading and a
    /// paragraph are set at different sizes, and one absolute line height would crowd the first or
    /// strand the second.
    /// </remarks>
    public double LineHeightAt(double fontSize) => fontSize * LineHeight;
}
