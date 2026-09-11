namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// What the parser made of the keys it has been given so far.
/// </summary>
public enum VimParseStatus
{
    /// <summary>
    /// A command is complete and is in the result.
    /// </summary>
    Complete,

    /// <summary>
    /// The sequence could still become a command — <c>g</c> waiting for its second <c>g</c>. Nothing
    /// happens until the next key.
    /// </summary>
    Pending,

    /// <summary>
    /// The sequence can never become a command. The parser has reset itself.
    /// </summary>
    Invalid,
}
