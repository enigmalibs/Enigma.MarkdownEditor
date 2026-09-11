using System;
using System.Globalization;
using System.Text;
using Enigma.MarkdownEditor.Desktop.Services;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Services;

/// <summary>
/// The CF_HTML envelope Windows requires around a rich-text clipboard write, and the bytes each
/// platform actually gets.
/// </summary>
/// <remarks>
/// <para>
/// Worth its own test class for one reason: every offset in the header is a byte count into the
/// document that carries it, a wrong one makes the paste arrive empty or truncated, and Windows
/// reports nothing when it happens. Nothing in a headless suite can prove what Word receives, but
/// these assertions prove the thing that goes wrong — so what is left for the manual check is whether
/// Word likes the markup, not whether the arithmetic holds.
/// </para>
/// <para>
/// The offsets are read back out of the produced document and used to index its own UTF-8 bytes, so
/// the assertions are what a consumer does rather than a restatement of how it was built.
/// </para>
/// </remarks>
public sealed class ClipboardHtmlFormatTests
{
    private const string Fragment = "<h1>Heading</h1>\n<p>Some <strong>bold</strong> text.</p>";

    // A fragment whose characters are not its bytes. This is the whole reason the header is computed
    // in bytes: every one of these offsets would be wrong if it counted chars, and every one of them
    // would still look plausible.
    private const string WideFragment = "<p>Café — naïve — 日本語 — 🎉 déjà vu</p>";

    [Fact]
    public void TheFragment_IsRequired()
    {
        Assert.Throws<ArgumentNullException>(() => ClipboardHtmlFormat.WrapCfHtml(null!));
        Assert.Throws<ArgumentNullException>(() => ClipboardHtmlFormat.Payload(null!));
    }

    [Fact]
    public void TheHeader_DeclaresTheVersionWindowsLooksFor()
        => Assert.StartsWith("Version:0.9\r\n", ClipboardHtmlFormat.WrapCfHtml(Fragment), StringComparison.Ordinal);

    [Theory]
    [InlineData(Fragment)]
    [InlineData(WideFragment)]
    [InlineData("")]
    public void StartHtml_PointsAtTheOpeningHtmlTag(string fragment)
    {
        byte[] bytes = Wrapped(fragment);

        Assert.Equal("<html>", Text(bytes, Offset(bytes, "StartHTML"), "<html>".Length));
    }

    [Theory]
    [InlineData(Fragment)]
    [InlineData(WideFragment)]
    [InlineData("")]
    public void TheFragmentOffsets_BracketExactlyTheMarkupTheyWereGiven(string fragment)
    {
        // The assertion the whole class exists for: slice the bytes the way a consumer does, and the
        // fragment must come back byte for byte — no leading marker, no trailing marker, nothing cut
        // off a multi-byte character at either end.
        byte[] bytes = Wrapped(fragment);
        int start = Offset(bytes, "StartFragment");
        int end = Offset(bytes, "EndFragment");

        Assert.Equal(fragment, Text(bytes, start, end - start));
    }

    [Theory]
    [InlineData(Fragment)]
    [InlineData(WideFragment)]
    public void TheFragmentMarkers_SitImmediatelyOutsideThoseOffsets(string fragment)
    {
        byte[] bytes = Wrapped(fragment);

        Assert.Equal(
            "<!--StartFragment-->",
            Text(bytes, Offset(bytes, "StartFragment") - "<!--StartFragment-->".Length, "<!--StartFragment-->".Length));

        Assert.Equal(
            "<!--EndFragment-->",
            Text(bytes, Offset(bytes, "EndFragment"), "<!--EndFragment-->".Length));
    }

    [Theory]
    [InlineData(Fragment)]
    [InlineData(WideFragment)]
    [InlineData("")]
    public void EndHtml_IsTheLengthOfTheWholeDocument(string fragment)
    {
        byte[] bytes = Wrapped(fragment);

        Assert.Equal(bytes.Length, Offset(bytes, "EndHTML"));
        Assert.EndsWith("</html>", Text(bytes, 0, bytes.Length), StringComparison.Ordinal);
    }

    [Fact]
    public void TheOffsets_AreByteCountsRatherThanCharacterCounts()
    {
        // Stated as its own claim rather than left implicit in the slicing tests: the wide fragment's
        // document is longer in bytes than in characters, and EndHTML follows the bytes.
        string document = ClipboardHtmlFormat.WrapCfHtml(WideFragment);

        Assert.True(Encoding.UTF8.GetByteCount(document) > document.Length);
        Assert.Equal(Encoding.UTF8.GetByteCount(document), Offset(Wrapped(WideFragment), "EndHTML"));
    }

    [Fact]
    public void TheOffsets_AreZeroPaddedToAFixedWidth()
    {
        // Fixed width is what makes the arithmetic possible at all: a header whose length depended on
        // the values in it would need StartHTML to compute StartHTML.
        string document = ClipboardHtmlFormat.WrapCfHtml(Fragment);

        Assert.Contains("StartHTML:0000000105\r\n", document, StringComparison.Ordinal);
        Assert.Contains("StartFragment:0000000139\r\n", document, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDocumentItself_CarriesNoTrailingNul()
        => Assert.DoesNotContain("\0", ClipboardHtmlFormat.WrapCfHtml(Fragment), StringComparison.Ordinal);

    [Fact]
    public void ThePayload_IsWrappedAndNulTerminatedOnWindows_AndBareEverywhereElse()
    {
        byte[] payload = ClipboardHtmlFormat.Payload(Fragment);

        if (ClipboardHtmlFormat.WrapsFragment)
        {
            Assert.Equal(0, payload[^1]);
            Assert.Equal(
                ClipboardHtmlFormat.WrapCfHtml(Fragment),
                Encoding.UTF8.GetString(payload, 0, payload.Length - 1));
        }
        else
        {
            // X11 takes the markup on the text/html target with no envelope at all.
            Assert.Equal(Fragment, Encoding.UTF8.GetString(payload));
        }
    }

    [Fact]
    public void ThePayload_IsUtf8RatherThanUtf16()
    {
        // The reason the flavour travels as a DataFormat<byte[]>: Avalonia writes a string format to
        // the Windows clipboard as UTF-16, and CF_HTML counting its own bytes has to be UTF-8. A
        // UTF-16 payload of ASCII markup would carry a zero byte between every character.
        const string fragment = "<p>ab</p>";

        byte[] payload = ClipboardHtmlFormat.Payload(fragment);

        Assert.True(payload.AsSpan().IndexOf(Encoding.UTF8.GetBytes(fragment)) >= 0);
        Assert.True(payload.AsSpan().IndexOf(Encoding.Unicode.GetBytes(fragment)) < 0);
    }

    [Fact]
    public void TheIdentifier_IsTheNameThisPlatformKnowsHtmlBy()
        => Assert.Equal(
            ClipboardHtmlFormat.WrapsFragment ? "HTML Format" : "text/html",
            ClipboardHtmlFormat.Identifier);

    private static byte[] Wrapped(string fragment)
        => Encoding.UTF8.GetBytes(ClipboardHtmlFormat.WrapCfHtml(fragment));

    private static string Text(byte[] bytes, int start, int length)
        => Encoding.UTF8.GetString(bytes, start, length);

    // Reads an offset back out of the header the way a consumer does, rather than recomputing it.
    private static int Offset(byte[] bytes, string key)
    {
        string header = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 128));
        int at = header.IndexOf(key + ":", StringComparison.Ordinal);

        Assert.True(at >= 0, $"the header carries no {key}");

        return int.Parse(header.Substring(at + key.Length + 1, 10), CultureInfo.InvariantCulture);
    }
}
