using System;
using System.Collections.Generic;
using System.IO;
using Enigma.MarkdownEditor.Core.Workspaces;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// Decides what a selection of several entries in the tree amounts to, before anything acts on it.
/// </summary>
/// <remarks>
/// A selection is whatever the user clicked, in the order they clicked it — and nothing stops that
/// from being a folder and a file inside the same folder. Acting on both would act twice on the file:
/// once with the folder, and once more on a path that is no longer there. This reduces a selection to
/// the entries an operation should actually be given.
/// </remarks>
public static class FileSelection
{
    /// <summary>
    /// Reduces a set of paths to its outermost entries: duplicates removed, and every path that sits
    /// inside another path of the set dropped.
    /// </summary>
    /// <param name="paths">The selected files and folders, in the order they were selected.</param>
    /// <returns>
    /// The paths that remain, in the order given and exactly as given — the first spelling wins when a
    /// path appears twice.
    /// </returns>
    /// <exception cref="ArgumentException">One of <paramref name="paths"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="paths"/>, or one of the paths in it, is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// Compared with <see cref="WorkspaceValidation.PathComparer"/>, so case does not tell two paths
    /// apart where the platform does not, and a trailing separator never does. "Inside" means beneath
    /// in the folder chain, not a matching prefix: <c>notes-old</c> is not inside <c>notes</c>. Nothing
    /// here reads the disk.
    /// </remarks>
    public static IReadOnlyList<string> Outermost(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        List<(string Given, string Key)> distinct = [];
        HashSet<string> keys = new(WorkspaceValidation.PathComparer);

        foreach (string path in paths)
        {
            ArgumentNullException.ThrowIfNull(path, nameof(paths));

            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A selected path is empty.", nameof(paths));
            }

            string key = Path.TrimEndingDirectorySeparator(path);

            if (keys.Add(key))
            {
                distinct.Add((path, key));
            }
        }

        List<string> outermost = new(distinct.Count);

        foreach ((string given, string key) in distinct)
        {
            if (!HasAncestorIn(key, keys))
            {
                outermost.Add(given);
            }
        }

        return outermost;
    }

    // Walks the folder chain, as FileMovePlanner does, so that the rule for comparing two paths stays
    // the comparer's alone and a shared prefix is never mistaken for containment.
    private static bool HasAncestorIn(string path, HashSet<string> keys)
    {
        for (string? parent = Path.GetDirectoryName(path); parent is not null; parent = Path.GetDirectoryName(parent))
        {
            if (keys.Contains(Path.TrimEndingDirectorySeparator(parent)))
            {
                return true;
            }
        }

        return false;
    }
}
