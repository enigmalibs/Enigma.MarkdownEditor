using System;
using System.IO;
using Enigma.MarkdownEditor.Core.Workspaces;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// Decides what a drop in the tree would do, before it does any of it.
/// </summary>
/// <remarks>
/// This reads the file system, exactly as <see cref="DirectoryLister"/> does, because most of the
/// questions a drop raises — is it still there, is the name taken — can only be answered by looking.
/// What it never does is change anything: every answer is a <see cref="FileMovePlan"/> the caller can
/// show, and a refusal is a sentence rather than an exception, because a drop on the wrong folder is
/// an ordinary thing for a user to do.
/// </remarks>
public static class FileMovePlanner
{
    /// <summary>
    /// Plans the move of one entry into one folder.
    /// </summary>
    /// <param name="sourcePath">The path of the entry being dragged.</param>
    /// <param name="targetDirectory">The folder it was dropped on.</param>
    /// <returns>What the move amounts to, and why when it amounts to nothing.</returns>
    /// <exception cref="ArgumentException">Either string is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Either string is <see langword="null"/>.</exception>
    public static FileMovePlan Plan(string sourcePath, string targetDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        string? source = SafeNormalize(sourcePath);
        string? target = SafeNormalize(targetDirectory);

        if (source is null)
        {
            return FileMovePlan.Rejected(
                sourcePath,
                targetDirectory,
                isDirectory: false,
                $"'{sourcePath}' is not a usable path.");
        }

        if (target is null)
        {
            return FileMovePlan.Rejected(
                source,
                targetDirectory,
                isDirectory: false,
                $"'{targetDirectory}' is not a usable folder path.");
        }

        bool isDirectory = Directory.Exists(source);
        string name = WorkspaceValidation.DirectoryName(source);

        if (!isDirectory && !File.Exists(source))
        {
            return FileMovePlan.Rejected(source, target, isDirectory, $"'{name}' is no longer there.");
        }

        if (!Directory.Exists(target))
        {
            return FileMovePlan.Rejected(
                source,
                target,
                isDirectory,
                $"The folder '{WorkspaceValidation.DirectoryName(target)}' is no longer there.");
        }

        // Both the folder itself and anything under it: moving a folder inside its own subtree would
        // have it contain its own parent, which the operating system answers with an error message
        // nobody can read.
        if (isDirectory && IsSameOrBeneath(target, source))
        {
            return FileMovePlan.Rejected(source, target, isDirectory, "A folder cannot be moved into itself.");
        }

        if (WorkspaceValidation.PathComparer.Equals(Path.GetDirectoryName(source) ?? string.Empty, target))
        {
            return FileMovePlan.Rejected(source, target, isDirectory, $"'{name}' is already in this folder.");
        }

        // A file and a folder cannot share a name, so either kind of entry at the destination is the
        // same conflict and asks the user the same question.
        string destination = Path.Combine(target, name);
        bool taken = File.Exists(destination) || Directory.Exists(destination);

        return taken
            ? FileMovePlan.Conflict(source, target, isDirectory)
            : FileMovePlan.Ready(source, target, isDirectory);
    }

    private static string? SafeNormalize(string path)
    {
        try
        {
            return WorkspaceValidation.NormalizeDirectory(path);
        }
        catch (Exception exception) when (exception is ArgumentException or PathTooLongException or NotSupportedException)
        {
            // A path the runtime cannot even resolve is a rejected drop, not a crash: it usually means
            // the drag carried something that was never a path in the first place.
            return null;
        }
    }

    // Walks the folder chain rather than testing a string prefix, so that the platform's rule for
    // comparing paths lives in exactly one place — WorkspaceValidation.PathComparer — and so that
    // "C:\notes-old" is not mistaken for something inside "C:\notes".
    private static bool IsSameOrBeneath(string candidate, string ancestor)
    {
        for (string? current = candidate; current is not null; current = Path.GetDirectoryName(current))
        {
            if (WorkspaceValidation.PathComparer.Equals(Path.TrimEndingDirectorySeparator(current), ancestor))
            {
                return true;
            }
        }

        return false;
    }
}
