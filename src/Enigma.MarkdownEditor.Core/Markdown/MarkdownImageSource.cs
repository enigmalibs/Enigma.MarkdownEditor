using System;
using System.IO;

namespace Enigma.MarkdownEditor.Core.Markdown;

/// <summary>
/// Where an <c>![…](url)</c> in a document actually points, resolved against the folder the document
/// lives in.
/// </summary>
/// <remarks>
/// A decision over two strings, so it lives here rather than in the renderer: which file a note's
/// image link means is the one part of drawing an image that is worth asserting without a window.
/// </remarks>
public sealed class MarkdownImageSource
{
    private MarkdownImageSource(MarkdownImageKind kind, string display, string? localPath, bool exists)
    {
        Kind = kind;
        Display = display;
        LocalPath = localPath;
        Exists = exists;
    }

    /// <summary>
    /// What the link turned out to be.
    /// </summary>
    public MarkdownImageKind Kind { get; }

    /// <summary>
    /// The link as the document wrote it, for a placeholder to show. Never <see langword="null"/>;
    /// empty only when the link itself was.
    /// </summary>
    public string Display { get; }

    /// <summary>
    /// The absolute path of the file, when <see cref="Kind"/> is <see cref="MarkdownImageKind.Local"/>.
    /// <see langword="null"/> otherwise.
    /// </summary>
    public string? LocalPath { get; }

    /// <summary>
    /// Whether <see cref="LocalPath"/> is a file that is there right now. Always
    /// <see langword="false"/> for the other two kinds — nothing local was found to check.
    /// </summary>
    public bool Exists { get; }

    /// <summary>
    /// Resolves an image link.
    /// </summary>
    /// <param name="basePath">
    /// The folder the document lives in, which relative links are resolved against. A
    /// <see langword="null"/> or empty value leaves a relative link
    /// <see cref="MarkdownImageKind.Unresolvable"/> — an unsaved buffer has no folder, and guessing
    /// the working directory would silently show the wrong file.
    /// </param>
    /// <param name="url">The link as the document wrote it.</param>
    /// <returns>What the link points at.</returns>
    public static MarkdownImageSource Resolve(string? basePath, string? url)
    {
        string display = url ?? string.Empty;

        if (string.IsNullOrWhiteSpace(url))
        {
            return new MarkdownImageSource(MarkdownImageKind.Unresolvable, display, localPath: null, exists: false);
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? absolute))
        {
            if (absolute.IsFile)
            {
                return Local(display, absolute.LocalPath);
            }

            // Everything else with a scheme is somebody else's business. http(s) is named separately
            // from the rest only because the placeholder can say something useful about it.
            return new MarkdownImageSource(
                absolute.Scheme is "http" or "https" ? MarkdownImageKind.Remote : MarkdownImageKind.Unresolvable,
                display,
                localPath: null,
                exists: false);
        }

        try
        {
            if (Path.IsPathRooted(url))
            {
                return Local(display, url);
            }

            if (string.IsNullOrWhiteSpace(basePath))
            {
                return new MarkdownImageSource(MarkdownImageKind.Unresolvable, display, localPath: null, exists: false);
            }

            return Local(display, Path.Combine(basePath, url));
        }
        catch (ArgumentException)
        {
            // A link the operating system will not accept as a path at all. Path.GetFullPath and
            // Path.Combine both report that this way, and it is a broken link rather than a failure
            // worth propagating: the placeholder shows what was written and the note keeps rendering.
            return new MarkdownImageSource(MarkdownImageKind.Unresolvable, display, localPath: null, exists: false);
        }
    }

    private static MarkdownImageSource Local(string display, string path)
    {
        string full = Path.GetFullPath(path);

        if (File.Exists(full))
        {
            return new MarkdownImageSource(MarkdownImageKind.Local, display, full, exists: true);
        }

        // A link written the way a URL is — `my%20image.png` — is a link to a file with a space in
        // its name, and the file manager that produced it is not going to be the one that fixes it.
        // Tried second so a file whose name genuinely contains a percent sign still wins.
        string unescaped = Uri.UnescapeDataString(full);

        return (unescaped != full) && File.Exists(unescaped)
            ? new MarkdownImageSource(MarkdownImageKind.Local, display, unescaped, exists: true)
            : new MarkdownImageSource(MarkdownImageKind.Local, display, full, exists: false);
    }
}
