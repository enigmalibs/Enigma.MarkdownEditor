using System;
using System.IO;

namespace Enigma.MarkdownEditor.Core.Markdown;

/// <summary>
/// Where a <c>[…](url)</c> in a document actually points, resolved against the folder the document
/// lives in.
/// </summary>
/// <remarks>
/// The link half of <see cref="MarkdownImageSource"/>, and here for the same reason: which of the
/// three things a click means — the browser, a tab, the operating system — is a decision over two
/// strings, and the only part of activating a link that is worth asserting without a window.
/// </remarks>
public sealed class MarkdownLinkTarget
{
    private MarkdownLinkTarget(MarkdownLinkKind kind, string display, Uri? address, string? localPath)
    {
        Kind = kind;
        Display = display;
        Address = address;
        LocalPath = localPath;
    }

    /// <summary>
    /// What the link turned out to be.
    /// </summary>
    public MarkdownLinkKind Kind { get; }

    /// <summary>
    /// The link as the document wrote it, for a log entry to name. Never <see langword="null"/>;
    /// empty only when the link itself was.
    /// </summary>
    public string Display { get; }

    /// <summary>
    /// The address to hand the platform, when <see cref="Kind"/> is
    /// <see cref="MarkdownLinkKind.External"/>. <see langword="null"/> otherwise.
    /// </summary>
    public Uri? Address { get; }

    /// <summary>
    /// The absolute path of the file, when <see cref="Kind"/> is
    /// <see cref="MarkdownLinkKind.Local"/>. <see langword="null"/> otherwise.
    /// </summary>
    public string? LocalPath { get; }

    /// <summary>
    /// Resolves a link.
    /// </summary>
    /// <param name="basePath">
    /// The folder the document lives in, which relative links are resolved against. A
    /// <see langword="null"/> or empty value leaves a relative link
    /// <see cref="MarkdownLinkKind.Unresolvable"/> — an unsaved buffer has no folder, and guessing the
    /// working directory would open the wrong file.
    /// </param>
    /// <param name="url">The link as the document wrote it.</param>
    /// <returns>What the link points at.</returns>
    public static MarkdownLinkTarget Resolve(string? basePath, string? url)
    {
        string display = url ?? string.Empty;

        if (string.IsNullOrWhiteSpace(url))
        {
            return new MarkdownLinkTarget(MarkdownLinkKind.Unresolvable, display, address: null, localPath: null);
        }

        string trimmed = url.Trim();

        if (trimmed.StartsWith('#'))
        {
            return new MarkdownLinkTarget(MarkdownLinkKind.Fragment, display, address: null, localPath: null);
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out Uri? absolute))
        {
            // A Windows path — C:\notes\other.md — parses as an absolute URI with the file scheme, so
            // this arm catches it too, which is why the file test comes before the external one.
            return absolute.IsFile
                ? Local(display, absolute.LocalPath)
                : new MarkdownLinkTarget(MarkdownLinkKind.External, display, absolute, localPath: null);
        }

        // `other-note.md#a-heading` is a link to a file, not to a file whose name ends in a heading.
        // The fragment is dropped rather than kept: nothing scrolls to it yet, and carrying it into a
        // path would make every anchored link a missing file.
        int fragment = trimmed.IndexOf('#', StringComparison.Ordinal);
        string path = fragment >= 0 ? trimmed[..fragment] : trimmed;

        if (path.Length == 0)
        {
            return new MarkdownLinkTarget(MarkdownLinkKind.Fragment, display, address: null, localPath: null);
        }

        try
        {
            if (Path.IsPathRooted(path))
            {
                return Local(display, path);
            }

            return string.IsNullOrWhiteSpace(basePath)
                ? new MarkdownLinkTarget(MarkdownLinkKind.Unresolvable, display, address: null, localPath: null)
                : Local(display, Path.Combine(basePath, path));
        }
        catch (ArgumentException)
        {
            // A link the operating system will not accept as a path at all. Path.Combine and
            // Path.GetFullPath both report that this way, and it is a broken link rather than a
            // failure worth propagating — the click does nothing and says so in the log.
            return new MarkdownLinkTarget(MarkdownLinkKind.Unresolvable, display, address: null, localPath: null);
        }
    }

    private static MarkdownLinkTarget Local(string display, string path)
    {
        string full = Path.GetFullPath(path);

        if (File.Exists(full) || Directory.Exists(full))
        {
            return new MarkdownLinkTarget(MarkdownLinkKind.Local, display, address: null, full);
        }

        // A link written the way a URL is — `my%20note.md` — names a file with a space in it, and the
        // editor that produced it is not going to be the one that fixes it. Tried second so a file
        // whose name genuinely contains a percent sign still wins.
        string unescaped = Uri.UnescapeDataString(full);

        return new MarkdownLinkTarget(
            MarkdownLinkKind.Local,
            display,
            address: null,
            (unescaped != full) && File.Exists(unescaped) ? unescaped : full);
    }
}
