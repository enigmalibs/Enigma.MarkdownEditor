namespace Enigma.MarkdownEditor.Core.Markdown;

/// <summary>
/// What kind of list a line's marker starts, if any.
/// </summary>
public enum ListMarkerKind
{
    /// <summary>
    /// No list marker at all. A block quote's own lines parse this way until one of them carries a
    /// marker of its own.
    /// </summary>
    None = 0,

    /// <summary>
    /// An unordered item: <c>-</c>, <c>*</c> or <c>+</c>.
    /// </summary>
    Bullet,

    /// <summary>
    /// An ordered item: digits followed by <c>.</c> or <c>)</c>.
    /// </summary>
    Ordered,
}
