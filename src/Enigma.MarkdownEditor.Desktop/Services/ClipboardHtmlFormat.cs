using System;
using System.Globalization;
using System.Text;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// How the rich-text flavour of a clipboard write has to be shaped for the platform it is written on.
/// </summary>
/// <remarks>
/// <para>
/// The one place in the application that knows a paste target's conventions, kept beside
/// <see cref="ClipboardWriter"/> rather than inside it so the offsets below can be asserted by a test
/// — a wrong offset is exactly what makes a paste arrive empty, and it is silent when it happens.
/// </para>
/// <para>
/// <b>Bytes, never a string.</b> Avalonia writes a <c>DataFormat&lt;string&gt;</c> to the Windows
/// clipboard as UTF-16 (<c>OleDataObjectHelper.WriteStringToHGlobal</c> copies the <c>char</c> buffer
/// verbatim), and CF_HTML must be UTF-8 — its own header counts UTF-8 <em>bytes</em>. A
/// <c>DataFormat&lt;byte[]&gt;</c> is written through untouched on both backends, so the encoding is
/// decided here and nowhere else. This is why <see cref="Payload"/> answers bytes.
/// </para>
/// <para>
/// Two platforms, because two are shipped: Windows wants the registered <c>HTML Format</c> with the
/// CF_HTML envelope around the markup, X11 wants the <c>text/html</c> target with the markup and
/// nothing else.
/// </para>
/// </remarks>
public static class ClipboardHtmlFormat
{
    // Every offset is written zero-padded to a fixed ten digits precisely so the header's own length
    // does not depend on the values in it — otherwise computing StartHTML would depend on StartHTML.
    private const string Header =
        "Version:0.9\r\n"
        + "StartHTML:{0:D10}\r\n"
        + "EndHTML:{1:D10}\r\n"
        + "StartFragment:{2:D10}\r\n"
        + "EndFragment:{3:D10}\r\n";

    private const string FragmentPrefix = "<html><body>\r\n<!--StartFragment-->";

    private const string FragmentSuffix = "<!--EndFragment-->\r\n</body></html>";

    // Measured rather than written down as 105: the two must agree, and a constant would only agree
    // until somebody edited the template above.
    private static readonly int HeaderLength =
        string.Format(CultureInfo.InvariantCulture, Header, 0, 0, 0, 0).Length;

    /// <summary>
    /// The identifier the platform knows the HTML flavour by, passed to the system as written.
    /// </summary>
    /// <remarks>
    /// <c>HTML Format</c> is the name Windows registers CF_HTML under and the name Word, Outlook and
    /// every browser look for. Elsewhere it is the <c>text/html</c> mime type, which is what X11
    /// targets and what GTK and Qt applications advertise.
    /// </remarks>
    public static string Identifier { get; } = OperatingSystem.IsWindows() ? "HTML Format" : "text/html";

    /// <summary>
    /// Whether this platform wraps the markup in the CF_HTML envelope rather than writing it bare.
    /// </summary>
    public static bool WrapsFragment => OperatingSystem.IsWindows();

    /// <summary>
    /// The bytes to put on the clipboard under <see cref="Identifier"/>.
    /// </summary>
    /// <param name="html">The HTML fragment.</param>
    /// <returns>
    /// UTF-8 bytes: the CF_HTML document on Windows, the fragment as written everywhere else. A
    /// trailing <c>NUL</c> on Windows, which is what a consumer that reads the handle as a C string
    /// expects to find and what keeps it inside the allocation.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="html"/> is <see langword="null"/>.</exception>
    public static byte[] Payload(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        return WrapsFragment
            ? Encoding.UTF8.GetBytes(WrapCfHtml(html) + '\0')
            : Encoding.UTF8.GetBytes(html);
    }

    /// <summary>
    /// Wraps <paramref name="html"/> in the CF_HTML envelope Windows requires.
    /// </summary>
    /// <param name="html">The HTML fragment.</param>
    /// <returns>
    /// The CF_HTML document: the <c>Version</c>/<c>StartHTML</c>/<c>EndHTML</c>/<c>StartFragment</c>/
    /// <c>EndFragment</c> header, then the markup between its two comment markers. Without the
    /// trailing <c>NUL</c> — that belongs to the bytes, not to the document, and every offset in the
    /// header is measured against the document.
    /// </returns>
    /// <remarks>
    /// Callable on any platform, and not gated on <see cref="WrapsFragment"/>, so that the offsets
    /// can be asserted by a test wherever the suite happens to run.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="html"/> is <see langword="null"/>.</exception>
    public static string WrapCfHtml(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        // Every offset counts bytes from the first byte of the header, so the two ASCII wrappers are
        // measured the same way the fragment is. They happen to be ASCII, but measuring them as text
        // would leave the count right for the wrong reason.
        int startHtml = HeaderLength;
        int startFragment = startHtml + Encoding.UTF8.GetByteCount(FragmentPrefix);
        int endFragment = startFragment + Encoding.UTF8.GetByteCount(html);
        int endHtml = endFragment + Encoding.UTF8.GetByteCount(FragmentSuffix);

        return string.Format(CultureInfo.InvariantCulture, Header, startHtml, endHtml, startFragment, endFragment)
            + FragmentPrefix
            + html
            + FragmentSuffix;
    }
}
