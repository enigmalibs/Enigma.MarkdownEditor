using System;
using System.Text;

namespace Enigma.MarkdownEditor.Core.Documents;

/// <summary>
/// How a text file was stored, and therefore how it has to be stored again: its encoding, whether it
/// carried a byte-order mark, and its line terminator.
/// </summary>
/// <remarks>
/// <para>
/// Saving a file the user opened is not the same act as writing a new one. Anything this class does
/// not carry across is a change the user did not ask for and will find in their next diff, so the
/// three things a text file can differ in without differing in content are all captured here and all
/// re-applied on write.
/// </para>
/// <para>
/// <see cref="Encoding"/> is already configured to emit — or not emit — the byte-order mark the file
/// had, so writing is nothing more than handing it to the framework.
/// </para>
/// </remarks>
public sealed class TextFileFormat
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TextFileFormat"/> class.
    /// </summary>
    /// <param name="encoding">
    /// The encoding to read and write with, configured for the byte-order mark the file had.
    /// </param>
    /// <param name="hasByteOrderMark">Whether the file started with a byte-order mark.</param>
    /// <param name="lineEndings">The terminator its lines used.</param>
    /// <exception cref="ArgumentNullException"><paramref name="encoding"/> is <see langword="null"/>.</exception>
    public TextFileFormat(Encoding encoding, bool hasByteOrderMark, LineEndingStyle lineEndings)
    {
        Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        HasByteOrderMark = hasByteOrderMark;
        LineEndings = lineEndings;
    }

    /// <summary>
    /// Gets what a file with no byte-order mark and no line break is: UTF-8, bare, line feeds.
    /// </summary>
    /// <remarks>
    /// Also what a file the editor creates gets, which is why this property is the one place the
    /// application's own idea of "plain text" is written down.
    /// </remarks>
    public static TextFileFormat Default { get; } =
        new(Utf8WithoutMark(), false, Documents.LineEndings.Default);

    /// <summary>
    /// Gets the encoding to read and write the file with.
    /// </summary>
    public Encoding Encoding { get; }

    /// <summary>
    /// Gets a value indicating whether the file started with a byte-order mark.
    /// </summary>
    public bool HasByteOrderMark { get; }

    /// <summary>
    /// Gets the terminator the file's lines used.
    /// </summary>
    public LineEndingStyle LineEndings { get; }

    /// <summary>
    /// Works out how a file was stored from its first bytes.
    /// </summary>
    /// <param name="bytes">The whole file, or at least its first four bytes.</param>
    /// <param name="hasByteOrderMark">Set to whether a mark was found.</param>
    /// <param name="preambleLength">Set to the number of leading bytes that are the mark, not text.</param>
    /// <returns>The encoding to read the rest of the bytes with, and to write them back with.</returns>
    /// <remarks>
    /// Only a byte-order mark is trusted. Guessing a legacy code page from byte statistics is the one
    /// way an editor silently corrupts a file it merely opened, so a file without a mark is read as
    /// UTF-8 — which also reads plain ASCII correctly, and is what every tool that made the file this
    /// century wrote.
    /// </remarks>
    public static Encoding DetectEncoding(ReadOnlySpan<byte> bytes, out bool hasByteOrderMark, out int preambleLength)
    {
        hasByteOrderMark = true;

        // UTF-32 LE before UTF-16 LE, and not the other way round: the 32-bit mark starts with the
        // whole of the 16-bit one, so testing the shorter first would claim every UTF-32 file.
        if (StartsWith(bytes, [0xFF, 0xFE, 0x00, 0x00]))
        {
            preambleLength = 4;
            return new UTF32Encoding(bigEndian: false, byteOrderMark: true);
        }

        if (StartsWith(bytes, [0x00, 0x00, 0xFE, 0xFF]))
        {
            preambleLength = 4;
            return new UTF32Encoding(bigEndian: true, byteOrderMark: true);
        }

        if (StartsWith(bytes, [0xEF, 0xBB, 0xBF]))
        {
            preambleLength = 3;
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        }

        if (StartsWith(bytes, [0xFF, 0xFE]))
        {
            preambleLength = 2;
            return new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
        }

        if (StartsWith(bytes, [0xFE, 0xFF]))
        {
            preambleLength = 2;
            return new UnicodeEncoding(bigEndian: true, byteOrderMark: true);
        }

        hasByteOrderMark = false;
        preambleLength = 0;

        return Utf8WithoutMark();
    }

    /// <summary>
    /// Returns the same format with a different line terminator.
    /// </summary>
    /// <param name="lineEndings">The terminator to record.</param>
    /// <returns>A new instance; this one is immutable.</returns>
    public TextFileFormat With(LineEndingStyle lineEndings)
        => new(Encoding, HasByteOrderMark, lineEndings);

    // Not Encoding.UTF8: that one emits a byte-order mark, so using it would add three bytes to the
    // front of every plain file the editor saves.
    private static UTF8Encoding Utf8WithoutMark() => new(encoderShouldEmitUTF8Identifier: false);

    private static bool StartsWith(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> mark)
        => (bytes.Length >= mark.Length) && bytes[..mark.Length].SequenceEqual(mark);
}
