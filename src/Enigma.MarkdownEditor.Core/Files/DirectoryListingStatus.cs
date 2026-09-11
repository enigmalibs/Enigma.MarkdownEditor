namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// How a directory answered when the tree asked for its contents.
/// </summary>
/// <remarks>
/// Everything but <see cref="Ok"/> is a node the tree still draws — greyed, with an icon and a
/// tooltip. A folder that cannot be read is information; a folder that silently vanishes is a bug
/// report.
/// </remarks>
public enum DirectoryListingStatus
{
    /// <summary>
    /// The directory was listed. It may still hold no visible entry.
    /// </summary>
    Ok,

    /// <summary>
    /// The directory is not there — deleted, renamed, or on a drive that is not plugged in.
    /// </summary>
    Missing,

    /// <summary>
    /// The directory exists but this user may not read it.
    /// </summary>
    AccessDenied,

    /// <summary>
    /// The directory could not be read for some other reason — an unreachable share, an IO error.
    /// </summary>
    Unavailable,
}
