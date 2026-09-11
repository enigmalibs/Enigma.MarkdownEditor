using System;
using System.Text;
using Enigma.MarkdownEditor.Core.Documents;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Documents;

/// <summary>
/// Tests for byte-order-mark detection on its own, including the ordering trap: the UTF-32 little
/// endian mark starts with the whole of the UTF-16 one.
/// </summary>
public sealed class TextFileFormatTests
{
    [Theory]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF, 0x41 }, 3, "utf-8")]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x41, 0x00 }, 2, "utf-16")]
    [InlineData(new byte[] { 0xFE, 0xFF, 0x00, 0x41 }, 2, "utf-16BE")]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x00, 0x00, 0x41, 0x00, 0x00, 0x00 }, 4, "utf-32")]
    [InlineData(new byte[] { 0x00, 0x00, 0xFE, 0xFF, 0x00, 0x00, 0x00, 0x41 }, 4, "utf-32BE")]
    public void AMarkedFile_IsDetectedWithItsPreambleMeasured(byte[] bytes, int expectedPreamble, string expectedName)
    {
        Encoding encoding = TextFileFormat.DetectEncoding(bytes, out bool hasMark, out int preambleLength);

        Assert.True(hasMark);
        Assert.Equal(expectedPreamble, preambleLength);
        Assert.Equal(expectedName, encoding.WebName);
        Assert.NotEmpty(encoding.GetPreamble());
    }

    [Fact]
    public void AUtf32MarkIsNotMistakenForAUtf16One()
    {
        // FF FE 00 00 opens with FF FE, so a shorter-first test would read every UTF-32 LE file as
        // UTF-16 and produce two NUL characters per character.
        Encoding encoding = TextFileFormat.DetectEncoding(
            [0xFF, 0xFE, 0x00, 0x00],
            out _,
            out int preambleLength);

        Assert.Equal("utf-32", encoding.WebName);
        Assert.Equal(4, preambleLength);
    }

    [Theory]
    [InlineData(new byte[] { 0x41, 0x42, 0x43 })]
    [InlineData(new byte[] { 0xFF })]
    [InlineData(new byte[0])]
    public void AnUnmarkedFile_IsReadAsUtf8WithoutOne(byte[] bytes)
    {
        Encoding encoding = TextFileFormat.DetectEncoding(bytes, out bool hasMark, out int preambleLength);

        Assert.False(hasMark);
        Assert.Equal(0, preambleLength);
        Assert.Equal("utf-8", encoding.WebName);

        // The whole reason detection returns an encoding rather than a name: this one must not write
        // a mark the file never had.
        Assert.Empty(encoding.GetPreamble());
    }

    [Fact]
    public void TheDefaultFormat_IsBareUtf8WithLineFeeds()
    {
        Assert.Equal("utf-8", TextFileFormat.Default.Encoding.WebName);
        Assert.Empty(TextFileFormat.Default.Encoding.GetPreamble());
        Assert.False(TextFileFormat.Default.HasByteOrderMark);
        Assert.Equal(LineEndingStyle.Lf, TextFileFormat.Default.LineEndings);
    }

    [Fact]
    public void ChangingTheLineEndings_LeavesTheEncodingAlone()
    {
        TextFileFormat format = new(Encoding.Unicode, true, LineEndingStyle.Lf);

        TextFileFormat changed = format.With(LineEndingStyle.CrLf);

        Assert.Equal(LineEndingStyle.CrLf, changed.LineEndings);
        Assert.Same(format.Encoding, changed.Encoding);
        Assert.True(changed.HasByteOrderMark);
        Assert.Equal(LineEndingStyle.Lf, format.LineEndings);
    }

    [Fact]
    public void ItsArguments_AreChecked()
        => Assert.Throws<ArgumentNullException>(() => new TextFileFormat(null!, false, LineEndingStyle.Lf));
}
