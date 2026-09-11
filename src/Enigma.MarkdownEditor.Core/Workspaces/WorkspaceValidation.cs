using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Enigma.MarkdownEditor.Core.Workspaces;

/// <summary>
/// The rules a workspace has to satisfy, as answers a user interface can show before it commits to
/// anything.
/// </summary>
/// <remarks>
/// <see cref="WorkspacesConfigValidator"/> states the same invariants to the configuration file, where
/// breaking one is a failure. These methods state them to the person typing: each returns the sentence
/// to put under the box, or <see langword="null"/> when the value is fine. Both halves have to exist —
/// the file must never hold a broken workspace, and a user must never be told so by an exception.
/// </remarks>
public static class WorkspaceValidation
{
    /// <summary>
    /// Gets the comparer for root directory paths: case-insensitive on Windows, case-sensitive
    /// elsewhere, matching how the file systems themselves behave.
    /// </summary>
    public static StringComparer PathComparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// Checks a workspace name.
    /// </summary>
    /// <param name="name">The name to check.</param>
    /// <param name="existing">The workspaces the name has to stay unique against.</param>
    /// <param name="excludedId">
    /// The workspace being renamed, so that keeping its own name is not reported as a duplicate.
    /// </param>
    /// <returns>The problem with the name, or <see langword="null"/> when there is none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="existing"/> is <see langword="null"/>.</exception>
    public static string? ValidateName(string? name, IEnumerable<Workspace> existing, Guid? excludedId = null)
    {
        ArgumentNullException.ThrowIfNull(existing);

        if (string.IsNullOrWhiteSpace(name))
        {
            return "A workspace needs a name.";
        }

        if (name.Length > Workspace.MaxNameLength)
        {
            return $"A workspace name cannot be longer than {Workspace.MaxNameLength} characters.";
        }

        bool taken = existing.Any(workspace =>
            workspace.Id != excludedId &&
            string.Equals(workspace.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

        return taken ? $"There is already a workspace called '{name.Trim()}'." : null;
    }

    /// <summary>
    /// Checks a root directory before it is added to a workspace.
    /// </summary>
    /// <param name="directory">The directory to check, as picked.</param>
    /// <param name="workspace">The workspace it would be added to.</param>
    /// <returns>The problem with the directory, or <see langword="null"/> when there is none.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="workspace"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// A directory that does not exist is deliberately accepted: a root on a network share or a
    /// removable drive is missing for as long as it is unplugged, and losing it from the workspace
    /// would be worse than showing it greyed out.
    /// </remarks>
    public static string? ValidateRootDirectory(string? directory, Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        if (string.IsNullOrWhiteSpace(directory))
        {
            return "Pick a folder to add.";
        }

        string normalized;
        try
        {
            normalized = NormalizeDirectory(directory);
        }
        catch (Exception exception) when (exception is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return $"'{directory}' is not a usable folder path.";
        }

        bool alreadyThere = workspace.RootDirectories.Any(root =>
            PathComparer.Equals(SafeNormalize(root), normalized));

        return alreadyThere ? $"'{normalized}' is already a folder of this workspace." : null;
    }

    /// <summary>
    /// Puts a directory path into the form the workspaces file stores: absolute, and without a
    /// trailing separator unless the path is a drive or filesystem root.
    /// </summary>
    /// <param name="directory">The path to normalize.</param>
    /// <returns>The normalized path.</returns>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="directory"/> is <see langword="null"/>.</exception>
    public static string NormalizeDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory.Trim()));
    }

    /// <summary>
    /// Proposes a name for a workspace about to be created over a directory: the folder's own name,
    /// numbered when that is taken.
    /// </summary>
    /// <param name="rootDirectory">The directory the workspace is created over.</param>
    /// <param name="existing">The workspaces the name has to stay unique against.</param>
    /// <returns>A name that <see cref="ValidateName"/> accepts.</returns>
    /// <exception cref="ArgumentException"><paramref name="rootDirectory"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public static string SuggestName(string rootDirectory, IEnumerable<Workspace> existing)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(existing);

        List<Workspace> workspaces = [.. existing];
        string candidate = Truncate(DirectoryName(rootDirectory));

        if (ValidateName(candidate, workspaces) is null)
        {
            return candidate;
        }

        // Give up numbering long before it becomes silly: at that point the folder name is simply not
        // a useful name, and the user is better served by a box they can type into.
        for (int suffix = 2; suffix <= 99; suffix++)
        {
            string numbered = $"{Truncate(candidate, $" ({suffix})".Length)} ({suffix})";
            if (ValidateName(numbered, workspaces) is null)
            {
                return numbered;
            }
        }

        return $"{Truncate(candidate, 9)} {Guid.NewGuid().ToString("N")[..8]}";
    }

    /// <summary>
    /// Lists the workspace's root directories that are not on disk right now.
    /// </summary>
    /// <param name="workspace">The workspace to check.</param>
    /// <returns>The missing directories, in the workspace's own order. Empty when all are present.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="workspace"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<string> MissingRootDirectories(Workspace workspace)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        return [.. workspace.RootDirectories.Where(root => !DirectoryExists(root))];
    }

    /// <summary>
    /// Determines whether a root directory is on disk right now.
    /// </summary>
    /// <param name="directory">The directory to look for.</param>
    /// <returns><see langword="true"/> when it exists.</returns>
    public static bool DirectoryExists(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        try
        {
            return Directory.Exists(directory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // An unreachable share answers with an exception rather than false. Either way the tree
            // has nothing to show, which is exactly what "missing" means here.
            return false;
        }
    }

    /// <summary>
    /// Gets the label a root directory is listed under: its own folder name, falling back to the whole
    /// path for a drive root, which has none.
    /// </summary>
    /// <param name="directory">The directory to label.</param>
    /// <returns>The label.</returns>
    public static string DirectoryName(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return string.Empty;
        }

        string trimmed = Path.TrimEndingDirectorySeparator(directory.Trim());
        string name = Path.GetFileName(trimmed);

        return string.IsNullOrEmpty(name) ? trimmed : name;
    }

    private static string SafeNormalize(string directory)
    {
        try
        {
            return NormalizeDirectory(directory);
        }
        catch (Exception exception) when (exception is ArgumentException or PathTooLongException or NotSupportedException)
        {
            // A path already in the file that cannot be normalized is compared as it is written: it
            // still has to count as a duplicate of itself.
            return directory;
        }
    }

    private static string Truncate(string name, int reserved = 0)
    {
        int limit = Workspace.MaxNameLength - reserved;
        string trimmed = name.Trim();

        return trimmed.Length <= limit ? trimmed : trimmed[..limit].TrimEnd();
    }
}
