namespace Enigma.MarkdownEditor.Core.Workspaces;

/// <summary>
/// The colour a folder can be marked with, and every tab opened from inside it carries.
/// </summary>
/// <remarks>
/// <para>
/// A fixed set rather than a free colour value, for three reasons that all point the same way: a
/// name validates and a hex string does not, a name stays readable when the workspaces file is
/// edited by hand, and eight colours chosen once read as one design where eight colours chosen
/// separately do not.
/// </para>
/// <para>
/// <see cref="None"/> is zero, so a folder nobody has coloured — and a value the file holds that this
/// build does not understand — is no colour rather than a colour the user did not pick. What each of
/// the eight actually looks like is the application's business, not the file's.
/// </para>
/// </remarks>
public enum FolderColor
{
    /// <summary>
    /// No colour. The default, and what clearing a folder's colour sets.
    /// </summary>
    None = 0,

    /// <summary>
    /// Red.
    /// </summary>
    Red,

    /// <summary>
    /// Orange.
    /// </summary>
    Orange,

    /// <summary>
    /// Yellow.
    /// </summary>
    Yellow,

    /// <summary>
    /// Green.
    /// </summary>
    Green,

    /// <summary>
    /// Teal.
    /// </summary>
    Teal,

    /// <summary>
    /// Blue.
    /// </summary>
    Blue,

    /// <summary>
    /// Purple.
    /// </summary>
    Purple,

    /// <summary>
    /// Pink.
    /// </summary>
    Pink,
}
