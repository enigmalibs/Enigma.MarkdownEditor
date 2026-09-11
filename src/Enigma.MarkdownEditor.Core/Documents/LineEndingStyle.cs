namespace Enigma.MarkdownEditor.Core.Documents;

/// <summary>
/// The line terminator a text file uses on disk.
/// </summary>
/// <remarks>
/// Only the two that are still written. A lone carriage return — classic Mac OS — is read and
/// normalized like any other break, but it is never written back as one: a file that used it comes
/// back out as <see cref="Lf"/>.
/// </remarks>
public enum LineEndingStyle
{
    /// <summary>
    /// A single line feed, <c>\n</c>. The editor's own default, and what a new file gets.
    /// </summary>
    Lf,

    /// <summary>
    /// A carriage return followed by a line feed, <c>\r\n</c>.
    /// </summary>
    CrLf,
}
