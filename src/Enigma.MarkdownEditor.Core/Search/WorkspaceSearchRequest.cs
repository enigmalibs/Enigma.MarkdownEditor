using System;
using System.Collections.Generic;

namespace Enigma.MarkdownEditor.Core.Search;

/// <summary>
/// What to look for, where, and the two limits that keep a search over an unknown folder bounded.
/// </summary>
/// <remarks>
/// The scope is markdown alone, whatever the tree's ShowAllFiles setting says. A search whose reach
/// changed with a display toggle would be a search nobody could trust: "it is not in my notes" has to
/// mean the same thing twice running.
/// </remarks>
public sealed class WorkspaceSearchRequest
{
    /// <summary>
    /// How many hits are reported before the search gives up and says so. High enough that a real
    /// question is answered in full, low enough that a one-letter query cannot fill the pane forever.
    /// </summary>
    public const int DefaultMaxHits = 500;

    /// <summary>
    /// The largest file whose contents are read, in bytes. A markdown file this size is not a note —
    /// it is a log, a data dump or a paste accident, and reading it would stall the whole scan.
    /// </summary>
    public const long DefaultMaxFileBytes = 10L * 1024 * 1024;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceSearchRequest"/> class.
    /// </summary>
    /// <param name="query">The text to look for. Matched as a plain substring, never as a pattern.</param>
    /// <param name="rootDirectories">
    /// The directories to search, normally the active workspace's roots. Overlapping roots are fine:
    /// a file reachable from two of them is searched once.
    /// </param>
    /// <exception cref="ArgumentNullException">Either argument is <see langword="null"/>.</exception>
    public WorkspaceSearchRequest(string query, IReadOnlyList<string> rootDirectories)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(rootDirectories);

        Query = query;
        RootDirectories = rootDirectories;
    }

    /// <summary>
    /// Gets the text to look for.
    /// </summary>
    public string Query { get; }

    /// <summary>
    /// Gets the directories to search.
    /// </summary>
    public IReadOnlyList<string> RootDirectories { get; }

    /// <summary>
    /// Gets a value indicating whether the match is case-sensitive. Case-insensitive by default,
    /// which is what someone typing a half-remembered word means.
    /// </summary>
    public bool MatchCase { get; init; }

    /// <summary>
    /// Gets how many hits to report before stopping. See <see cref="DefaultMaxHits"/>.
    /// </summary>
    public int MaxHits { get; init; } = DefaultMaxHits;

    /// <summary>
    /// Gets the largest file whose contents are read. See <see cref="DefaultMaxFileBytes"/>.
    /// </summary>
    /// <remarks>
    /// An oversize file is still matched on its <em>name</em>: that costs nothing, and a file too big
    /// to read is exactly the one whose name is the only way to find it.
    /// </remarks>
    public long MaxFileBytes { get; init; } = DefaultMaxFileBytes;
}
