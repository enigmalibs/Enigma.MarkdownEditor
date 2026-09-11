using System;
using System.Globalization;
using System.IO;

namespace Enigma.MarkdownEditor.Core.Markdown;

/// <summary>
/// Where a pasted image is filed, and the link that points at it from the note.
/// </summary>
/// <remarks>
/// Naming and linking are separated from the writing on purpose: what the file is called and how the
/// note refers to it are rules, and rules are worth testing without a clipboard, a bitmap or a disk.
/// </remarks>
public static class ImageLink
{
    /// <summary>
    /// The format a pasted image is written in. PNG because a screenshot is what is nearly always on
    /// the clipboard, and re-encoding one as JPEG would soften every line of text in it.
    /// </summary>
    public const string Extension = ".png";

    /// <summary>
    /// What is appended to the note's own name to keep two pastes apart, to the second.
    /// </summary>
    public const string TimestampFormat = "yyyyMMddHHmmss";

    /// <summary>
    /// The name a pasted image takes when the note it belongs to has none of its own.
    /// </summary>
    public const string FallbackStem = "image";

    // A markdown link ends at the first space unless the destination is wrapped in angle brackets, and
    // a bare parenthesis closes it early. Both are reachable from a note whose name holds one.
    private static readonly char[] NeedsAngleBrackets = [' ', '\t', '(', ')', '<', '>'];

    /// <summary>
    /// Builds the file name a pasted image takes.
    /// </summary>
    /// <param name="documentPath">The note the image is being pasted into.</param>
    /// <param name="timestamp">When the paste happened.</param>
    /// <returns>The bare file name, e.g. <c>meeting-20260828114500.png</c>.</returns>
    /// <remarks>
    /// Named after the note rather than after the image, because a folder of screenshots is only ever
    /// read by someone trying to work out which note a file belongs to.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="documentPath"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="documentPath"/> is <see langword="null"/>.</exception>
    public static string FileName(string documentPath, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);

        string stem = Path.GetFileNameWithoutExtension(documentPath);

        if (stem.Length == 0)
        {
            stem = FallbackStem;
        }

        return $"{stem}-{timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture)}{Extension}";
    }

    /// <summary>
    /// Works out how the note refers to the image beside it.
    /// </summary>
    /// <param name="documentPath">The note the link is written into.</param>
    /// <param name="imagePath">The image the link points at.</param>
    /// <returns>The path relative to the note's folder, with forward slashes.</returns>
    /// <remarks>
    /// Relative and forward-slashed so the note survives being moved with its folder, opened on the
    /// other operating system, or rendered by anything that treats the link as a URL — which a
    /// backslash is not part of.
    /// </remarks>
    /// <exception cref="ArgumentException">Either path is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Either path is <see langword="null"/>.</exception>
    public static string RelativePath(string documentPath, string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        string? directory = Path.GetDirectoryName(Path.GetFullPath(documentPath));

        if (string.IsNullOrEmpty(directory))
        {
            return Path.GetFileName(imagePath);
        }

        return Path.GetRelativePath(directory, Path.GetFullPath(imagePath)).Replace('\\', '/');
    }

    /// <summary>
    /// Builds the image link itself.
    /// </summary>
    /// <param name="relativePath">What <see cref="RelativePath(string, string)"/> returned.</param>
    /// <returns>The markdown to insert at the caret.</returns>
    /// <remarks>
    /// The alt text is left empty rather than guessed at: it is the one part only the person pasting
    /// can write, and an invented one is worse than none for anybody reading the note aloud.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="relativePath"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="relativePath"/> is <see langword="null"/>.</exception>
    public static string Markdown(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);

        return relativePath.IndexOfAny(NeedsAngleBrackets) >= 0
            ? $"![](<{relativePath}>)"
            : $"![]({relativePath})";
    }
}
