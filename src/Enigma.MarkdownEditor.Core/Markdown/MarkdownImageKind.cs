namespace Enigma.MarkdownEditor.Core.Markdown;

/// <summary>
/// What an image link in a markdown document turned out to point at.
/// </summary>
public enum MarkdownImageKind
{
    /// <summary>
    /// A file on this machine. <see cref="MarkdownImageSource.LocalPath"/> holds its absolute path,
    /// and <see cref="MarkdownImageSource.Exists"/> says whether it is there.
    /// </summary>
    Local,

    /// <summary>
    /// An absolute <c>http</c> or <c>https</c> address. The preview does not fetch those.
    /// </summary>
    Remote,

    /// <summary>
    /// Nothing this application can open: an empty link, a relative link with no folder to resolve it
    /// against, a scheme that is not <c>file</c>, or a path the operating system rejects.
    /// </summary>
    Unresolvable,
}
