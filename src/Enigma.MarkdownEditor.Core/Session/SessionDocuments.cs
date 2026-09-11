using System.Collections.Generic;

namespace Enigma.MarkdownEditor.Core.Session;

/// <summary>
/// What a saved session's tab list came to when it was measured against the disk: the notes that can
/// still be opened, which of them to put on screen, and the ones that are no longer there.
/// </summary>
public sealed class SessionDocuments
{
    internal SessionDocuments(
        IReadOnlyList<string> paths,
        int activeIndex,
        IReadOnlyList<string> missing,
        bool isForAnotherWorkspace)
    {
        Paths = paths;
        ActiveIndex = activeIndex;
        Missing = missing;
        IsForAnotherWorkspace = isForAnotherWorkspace;
    }

    /// <summary>
    /// Gets the "nothing to reopen" answer.
    /// </summary>
    public static SessionDocuments None { get; } = new([], -1, [], isForAnotherWorkspace: false);

    /// <summary>
    /// Gets the notes to reopen, in tab order. Every one of them existed when this was resolved.
    /// </summary>
    public IReadOnlyList<string> Paths { get; }

    /// <summary>
    /// Gets the position in <see cref="Paths"/> to activate, or <c>-1</c> when there is nothing to
    /// open at all.
    /// </summary>
    /// <remarks>
    /// Renumbered against <see cref="Paths"/> rather than carried over from the file: notes that have
    /// gone missing shift everything after them, and the tab the user was reading has to survive that.
    /// </remarks>
    public int ActiveIndex { get; }

    /// <summary>
    /// Gets the notes the session listed that are no longer there. Worth a log line and nothing more.
    /// </summary>
    public IReadOnlyList<string> Missing { get; }

    /// <summary>
    /// Gets a value indicating whether the tab list was dropped because it belongs to a workspace
    /// other than the one that opened.
    /// </summary>
    public bool IsForAnotherWorkspace { get; }
}
