using System;
using System.Collections.Generic;
using System.IO;

namespace Enigma.MarkdownEditor.Core.Session;

/// <summary>
/// Turns a saved session's tab list into the one that can actually be reopened now.
/// </summary>
/// <remarks>
/// The whole of the tolerance the plan asks for, in one pure place so it can be tested without a
/// window: a session is written when the editor closes and read back who knows when, and in between
/// notes are renamed, moved, deleted and synced away. None of that is an error — it is the ordinary
/// life of a folder — so the answer is always a list, never an exception.
/// </remarks>
public static class SessionRestore
{
    /// <summary>
    /// Works out which of a session's documents to reopen.
    /// </summary>
    /// <param name="state">The session as it was read from disk.</param>
    /// <param name="activeWorkspaceId">
    /// The workspace that actually opened, from <see cref="Workspaces.IWorkspaceService"/> — the
    /// authority on that, which this only checks the session against.
    /// </param>
    /// <returns>The notes to open, renumbered, plus what was dropped and why.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> is <see langword="null"/>.</exception>
    public static SessionDocuments Resolve(SessionState state, Guid? activeWorkspaceId)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.ActiveWorkspaceId != activeWorkspaceId)
        {
            // The session was written in a different workspace — the list has since been switched, or
            // the workspace it named was deleted. The paths are absolute and would all still open,
            // which is exactly the problem: they would be somebody else's notes in this workspace's
            // window.
            return new SessionDocuments([], -1, [], isForAnotherWorkspace: true);
        }

        List<string> paths = [];
        List<string> missing = [];
        int activeIndex = -1;

        for (int index = 0; index < state.OpenDocuments.Count; index++)
        {
            string path = state.OpenDocuments[index];

            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            if (!Exists(path))
            {
                missing.Add(path);
                continue;
            }

            if (index == state.ActiveDocumentIndex)
            {
                activeIndex = paths.Count;
            }

            paths.Add(path);
        }

        // The tab that was on screen is gone: the first surviving one is the least surprising place to
        // land, and it is never nothing when there is something to show.
        if ((activeIndex < 0) && (paths.Count > 0))
        {
            activeIndex = 0;
        }

        return new SessionDocuments(paths, activeIndex, missing, isForAnotherWorkspace: false);
    }

    private static bool Exists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch (Exception failure)
            when (failure is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // A path the file system will not even answer about — an unmapped drive letter, a share
            // that went away — is a note that is not there, which is the case this method exists for.
            return false;
        }
    }
}
