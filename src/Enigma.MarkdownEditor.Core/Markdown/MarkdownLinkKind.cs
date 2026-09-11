namespace Enigma.MarkdownEditor.Core.Markdown;

/// <summary>
/// What a <c>[…](url)</c> in a markdown document turned out to point at.
/// </summary>
public enum MarkdownLinkKind
{
    /// <summary>
    /// An address for somebody else's application: <c>http</c>, <c>https</c>, <c>mailto</c> — anything
    /// absolute that is not a file. <see cref="MarkdownLinkTarget.Address"/> holds it.
    /// </summary>
    External,

    /// <summary>
    /// A file on this machine. <see cref="MarkdownLinkTarget.LocalPath"/> holds its absolute path.
    /// Whether it is there is not decided here — opening it is what finds that out.
    /// </summary>
    Local,

    /// <summary>
    /// A link into the document itself — <c>#a-heading</c> and nothing else.
    /// </summary>
    /// <remarks>
    /// Named rather than folded into <see cref="Unresolvable"/> because it is the one broken-looking
    /// link that is not broken: the preview has nowhere to scroll to yet (that is PHASE03's index),
    /// but resolving it as a relative path would try to open a file called <c>#a-heading</c>.
    /// </remarks>
    Fragment,

    /// <summary>
    /// Nothing this application can open: an empty link, or a relative one with no folder to resolve
    /// it against.
    /// </summary>
    Unresolvable,
}
