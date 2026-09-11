namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// What a row of the file tree stands for.
/// </summary>
/// <remarks>
/// The last four are not entries of the file system at all — they are the tree's way of saying
/// something about the folder that holds them, drawn as a single greyed child rather than as an empty
/// expander the user is left to interpret.
/// </remarks>
public enum FileTreeNodeKind
{
    /// <summary>
    /// A directory. Expands, and loads its children the first time it does.
    /// </summary>
    Directory,

    /// <summary>
    /// A file. Opens.
    /// </summary>
    File,

    /// <summary>
    /// A workspace root that is not on disk right now.
    /// </summary>
    Missing,

    /// <summary>
    /// Stands in for a directory's children until they have been read.
    /// </summary>
    Loading,

    /// <summary>
    /// The directory was read and holds nothing the filter shows.
    /// </summary>
    Empty,

    /// <summary>
    /// The directory exists but may not be read by this user.
    /// </summary>
    AccessDenied,

    /// <summary>
    /// The directory could not be read for some other reason.
    /// </summary>
    Unavailable,
}
