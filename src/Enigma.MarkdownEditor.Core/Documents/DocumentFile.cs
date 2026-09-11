using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Core.Files;

namespace Enigma.MarkdownEditor.Core.Documents;

/// <summary>
/// Reading a text file into the editor, and writing it back exactly as it was found.
/// </summary>
/// <remarks>
/// The pair is deliberately one class: a read that detects an encoding is only correct if the write
/// that mirrors it lives beside it, and splitting them is how the two drift apart.
/// </remarks>
public static class DocumentFile
{
    /// <summary>
    /// The largest file the editor opens.
    /// </summary>
    /// <remarks>
    /// A limit rather than no limit, because the whole file is held as one string and again as a
    /// document with a line index over it. The number is far above any hand-written note and far
    /// below the size at which the editor would stop responding; anything past it is a log or a data
    /// dump, and the user is told so rather than left watching a frozen window.
    /// </remarks>
    public const long MaximumBytes = 32L * 1024 * 1024;

    /// <summary>
    /// Reads a text file, normalizing its line breaks and remembering how to write it back.
    /// </summary>
    /// <param name="filePath">The file to read.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>The text with line feeds alone, plus the format it was stored in.</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is <see langword="null"/>.</exception>
    /// <exception cref="FileOperationException">The file is larger than <see cref="MaximumBytes"/>.</exception>
    /// <exception cref="IOException">The file could not be read.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    /// <exception cref="UnauthorizedAccessException">The file could not be read.</exception>
    public static async Task<DocumentContent> ReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        FileInfo file = new(filePath);

        if (file.Exists && (file.Length > MaximumBytes))
        {
            throw new FileOperationException(
                $"'{file.Name}' is {file.Length / (1024 * 1024)} MB. The editor opens files up to " +
                $"{MaximumBytes / (1024 * 1024)} MB.");
        }

        // Read as bytes rather than through a StreamReader: the byte-order mark has to survive the
        // round trip, and a reader consumes it without ever saying what it was.
        byte[] bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);

        Encoding encoding = TextFileFormat.DetectEncoding(bytes, out bool hasMark, out int preambleLength);
        string raw = encoding.GetString(bytes, preambleLength, bytes.Length - preambleLength);

        return new DocumentContent(
            LineEndings.Normalize(raw),
            new TextFileFormat(encoding, hasMark, LineEndings.Detect(raw)));
    }

    /// <summary>
    /// Writes a document back, in the encoding, byte-order mark and line endings it came with.
    /// </summary>
    /// <param name="filePath">The file to write. It is created if it is not there.</param>
    /// <param name="text">The text as the editor holds it, with line feeds alone.</param>
    /// <param name="format">What <see cref="ReadAsync"/> recorded, or <see cref="TextFileFormat.Default"/>.</param>
    /// <param name="cancellationToken">Abandons the write before it starts.</param>
    /// <returns>A task that completes once the file is on disk.</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    /// <exception cref="IOException">The file could not be written.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    /// <exception cref="UnauthorizedAccessException">The file could not be written.</exception>
    /// <remarks>
    /// Written in place rather than through a temporary file that is then swapped in. A swap would
    /// survive a crash mid-write, but it also drops a second file into the folder the user is looking
    /// at — which the tree, its watcher and any synchronizing client all see — and it replaces the
    /// file's identity, taking hard links and access rules with it.
    /// </remarks>
    public static async Task WriteAsync(
        string filePath,
        string text,
        TextFileFormat format,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(format);

        // The encoding carries its own preamble policy, so the byte-order mark — or its absence — is
        // already decided by the time the framework writes the first byte.
        await File.WriteAllTextAsync(
            filePath,
            LineEndings.Apply(text, format.LineEndings),
            format.Encoding,
            cancellationToken);
    }
}
