using System;
using System.IO;

namespace Enigma.MarkdownEditor.Core.Documents;

/// <summary>
/// What a file looked like from the outside at one moment: whether it was there, when it was last
/// written, and how long it was.
/// </summary>
/// <remarks>
/// The editor takes one of these every time it reads or writes a document, and compares it against a
/// fresh one whenever the watcher says something happened. Equal stamps mean the event was the
/// editor's own save coming back around; different ones mean somebody else got there.
/// </remarks>
/// <param name="Exists">Whether the file was there.</param>
/// <param name="LastWriteTimeUtc">When it was last written, in UTC.</param>
/// <param name="Length">Its length in bytes.</param>
public readonly record struct FileStamp(bool Exists, DateTime LastWriteTimeUtc, long Length)
{
    /// <summary>
    /// Gets the stamp of a file that is not there. Equal to <see langword="default"/>.
    /// </summary>
    public static FileStamp Missing => default;

    /// <summary>
    /// Takes a file's stamp now.
    /// </summary>
    /// <param name="filePath">The file to look at.</param>
    /// <returns>Its stamp, or <see cref="Missing"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// A file that cannot be looked at — a permission the user lost, a share that went away — stamps
    /// as <see cref="Missing"/>. That is the honest answer to "is what I read still there": the
    /// editor cannot see it any more, and it says so rather than pretending nothing changed.
    /// </remarks>
    public static FileStamp For(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        try
        {
            FileInfo file = new(filePath);

            return file.Exists
                ? new FileStamp(true, file.LastWriteTimeUtc, file.Length)
                : Missing;
        }
        catch (Exception failure)
            when (failure is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Missing;
        }
    }
}
