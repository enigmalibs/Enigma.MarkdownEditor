namespace Enigma.MarkdownEditor.Core.Search;

/// <summary>
/// What a search hit was found in.
/// </summary>
/// <remarks>
/// The distinction is a ranking, not a label: a note whose <em>name</em> matches is almost always the
/// one that was being looked for, so those hits are reported before any content is read at all.
/// </remarks>
public enum WorkspaceSearchHitKind
{
    /// <summary>
    /// The file's own name matched. Reported before every content hit, and only once per file.
    /// </summary>
    FileName = 0,

    /// <summary>
    /// A line of the file's text matched.
    /// </summary>
    Content = 1,
}
