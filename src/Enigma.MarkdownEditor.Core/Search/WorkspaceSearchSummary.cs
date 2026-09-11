namespace Enigma.MarkdownEditor.Core.Search;

/// <summary>
/// What a finished search adds up to: the counts behind the status line, and everything it had to
/// leave out.
/// </summary>
/// <remarks>
/// The hits themselves are not here — they are reported as they are found, and a list of them would
/// only be the same five hundred objects held twice. This is what is only knowable at the end.
/// </remarks>
public sealed class WorkspaceSearchSummary
{
    /// <summary>
    /// The summary of a search that was never run — an empty query, or a workspace with no folders.
    /// </summary>
    public static readonly WorkspaceSearchSummary Empty = new(0, 0, 0, 0, 0, isTruncated: false);

    internal WorkspaceSearchSummary(
        int hitCount,
        int fileCount,
        int scannedFiles,
        int skippedOversizeFiles,
        int unreadableFiles,
        bool isTruncated)
    {
        HitCount = hitCount;
        FileCount = fileCount;
        ScannedFiles = scannedFiles;
        SkippedOversizeFiles = skippedOversizeFiles;
        UnreadableFiles = unreadableFiles;
        IsTruncated = isTruncated;
    }

    /// <summary>
    /// Gets how many hits were reported.
    /// </summary>
    public int HitCount { get; }

    /// <summary>
    /// Gets how many distinct files those hits came from.
    /// </summary>
    public int FileCount { get; }

    /// <summary>
    /// Gets how many markdown files the search looked at, whether or not they matched.
    /// </summary>
    public int ScannedFiles { get; }

    /// <summary>
    /// Gets how many files were too big to read (see <see cref="WorkspaceSearchRequest.MaxFileBytes"/>).
    /// Their names were still matched.
    /// </summary>
    public int SkippedOversizeFiles { get; }

    /// <summary>
    /// Gets how many files could not be read at all — locked, deleted mid-scan, or not permitted.
    /// </summary>
    public int UnreadableFiles { get; }

    /// <summary>
    /// Gets a value indicating whether there were more hits than <see cref="WorkspaceSearchRequest.MaxHits"/>
    /// allowed, so what was reported is not the whole answer.
    /// </summary>
    public bool IsTruncated { get; }
}
