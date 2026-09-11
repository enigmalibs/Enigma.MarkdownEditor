using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Core.Workspaces;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// The file system half of the tree's context menu: create, rename, move, and the delete of last
/// resort.
/// </summary>
/// <remarks>
/// Every method is offloaded with <see cref="Task.Run{TResult}(Func{TResult}, CancellationToken)"/>
/// for the reason <see cref="DirectoryLister"/> is: there is no genuinely asynchronous file system API
/// underneath, and a folder on a sleeping network share takes seconds to answer. The offload is what
/// keeps that off the user interface thread.
/// <para>
/// Names are checked with <see cref="FileNameValidation"/> before anything is touched, and every
/// failure the file system reports is turned into a <see cref="FileOperationException"/> carrying a
/// sentence rather than a stack trace. The check can never be enough on its own — between it and the
/// operation the folder can change — so each operation is also written to fail rather than to
/// overwrite something silently.
/// </para>
/// </remarks>
public static class FileOperations
{
    /// <summary>
    /// Creates an empty file in a folder.
    /// </summary>
    /// <param name="directory">The folder to create it in, as a full path.</param>
    /// <param name="name">The file's name, as the user typed it.</param>
    /// <param name="cancellationToken">Abandons the operation before it starts.</param>
    /// <returns>The full path of the new file.</returns>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="directory"/> is <see langword="null"/>.</exception>
    /// <exception cref="FileOperationException">
    /// The name breaks a rule, is already taken, or the file could not be written.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    /// <remarks>
    /// <paramref name="name"/> is deliberately not guarded like an argument: it comes from a text box,
    /// so an empty one is something to tell the user about, not a caller's bug. It comes back as the
    /// same sentence <see cref="FileNameValidation"/> would have shown under that box.
    /// </remarks>
    public static Task<string> CreateFileAsync(
        string directory,
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        return Task.Run(() => CreateFile(directory, name), cancellationToken);
    }

    /// <summary>
    /// Creates a folder in a folder.
    /// </summary>
    /// <param name="directory">The folder to create it in, as a full path.</param>
    /// <param name="name">The new folder's name, as the user typed it.</param>
    /// <param name="cancellationToken">Abandons the operation before it starts.</param>
    /// <returns>The full path of the new folder.</returns>
    /// <exception cref="ArgumentException"><paramref name="directory"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="directory"/> is <see langword="null"/>.</exception>
    /// <exception cref="FileOperationException">
    /// The name breaks a rule, is already taken, or the folder could not be created.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    public static Task<string> CreateDirectoryAsync(
        string directory,
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        return Task.Run(() => CreateDirectory(directory, name), cancellationToken);
    }

    /// <summary>
    /// Renames a file or a folder in place.
    /// </summary>
    /// <param name="path">The full path of the entry to rename.</param>
    /// <param name="newName">The name it should have, as the user typed it.</param>
    /// <param name="cancellationToken">Abandons the operation before it starts.</param>
    /// <returns>The full path the entry now has.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="FileOperationException">
    /// The entry is gone, the name breaks a rule or is already taken, or the rename failed.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    /// <remarks>
    /// Renaming an entry to the name it already has is a no-op rather than an error: a user who opens
    /// the rename box and presses Enter has asked for nothing, and has not made a mistake.
    /// </remarks>
    public static Task<string> RenameAsync(
        string path,
        string newName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Task.Run(() => Rename(path, newName), cancellationToken);
    }

    /// <summary>
    /// Carries out a planned move.
    /// </summary>
    /// <param name="plan">The move, as <see cref="FileMovePlanner"/> decided it.</param>
    /// <param name="resolution">What the user answered when the plan reported a conflict.</param>
    /// <param name="cancellationToken">Abandons the operation before it starts.</param>
    /// <returns>The full path the entry now has, which is not the planned one when it was numbered.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="plan"/> is <see langword="null"/>.</exception>
    /// <exception cref="FileOperationException">The plan was refused, or the move failed.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="resolution"/> is
    /// <see cref="MoveResolution.Cancel"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    /// <remarks>
    /// <see cref="MoveResolution.Cancel"/> is a programming error here rather than a quiet return: the
    /// caller is the one holding the dialog, and a move the user called off has to stop there, where
    /// there is still something to tell them. Reaching this method with it means the answer was
    /// collected and then ignored.
    /// </remarks>
    public static Task<string> MoveAsync(
        FileMovePlan plan,
        MoveResolution resolution,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        if (resolution == MoveResolution.Cancel)
        {
            throw new InvalidOperationException("A move the user cancelled must not be carried out.");
        }

        if (plan.Status == FileMovePlanStatus.Rejected)
        {
            throw new FileOperationException(plan.Rejection!);
        }

        return Task.Run(() => Move(plan, resolution), cancellationToken);
    }

    /// <summary>
    /// Deletes a file, or a folder with everything under it, for good.
    /// </summary>
    /// <param name="path">The full path of the entry to delete.</param>
    /// <param name="cancellationToken">Abandons the operation before it starts.</param>
    /// <returns>A task that completes once the entry is gone.</returns>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="FileOperationException">The entry is there and could not be deleted.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    /// <remarks>
    /// This is the destructive fallback, not the ordinary delete. Deleting from the tree normally means
    /// moving the entry to the platform's trash, where the user can get it back; this method is what
    /// runs when the platform has no trash to offer, and only after the user has confirmed in a dialog
    /// that they understand nothing will be recoverable.
    /// <para>
    /// An entry that is already gone is not an error. The caller asked for it not to be there, and it
    /// is not there — reporting a failure would only make a second click on the same tree node, or a
    /// delete that raced the file system, look like a problem.
    /// </para>
    /// </remarks>
    public static Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Task.Run(() => Delete(path), cancellationToken);
    }

    private static string CreateFile(string directory, string name)
    {
        string? problem = FileNameValidation.ValidateIn(name, directory, isDirectory: false);
        if (problem is not null)
        {
            throw new FileOperationException(problem);
        }

        string path = Path.Combine(directory, name);

        try
        {
            // FileMode.CreateNew is what makes this a create rather than a truncate: should the name
            // have been taken since the check above, the file that is there is left untouched. The
            // handle is closed at once and nothing is written through it — a new note starts empty.
            File.Open(path, FileMode.CreateNew, FileAccess.Write, FileShare.None).Dispose();
        }
        catch (IOException exception) when (File.Exists(path) || Directory.Exists(path))
        {
            throw new FileOperationException($"There is already something called '{name}' here.", exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new FileOperationException($"'{name}' could not be created here.", exception);
        }

        return path;
    }

    private static string CreateDirectory(string directory, string name)
    {
        string? problem = FileNameValidation.ValidateIn(name, directory, isDirectory: true);
        if (problem is not null)
        {
            throw new FileOperationException(problem);
        }

        string path = Path.Combine(directory, name);

        // Directory.CreateDirectory accepts a folder that already exists and returns it, so the race
        // the file branch gets for free from FileMode.CreateNew has to be asked for here. The window
        // between this and the create cannot be closed, but silently adopting somebody else's folder
        // is worse than the rare wrong answer.
        if (Directory.Exists(path) || File.Exists(path))
        {
            throw new FileOperationException($"There is already something called '{name}' here.");
        }

        try
        {
            Directory.CreateDirectory(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new FileOperationException($"'{name}' could not be created here.", exception);
        }

        return path;
    }

    private static string Rename(string path, string newName)
    {
        string full = Normalize(path);
        bool isDirectory = Directory.Exists(full);

        if (!isDirectory && !File.Exists(full))
        {
            throw new FileOperationException($"'{WorkspaceValidation.DirectoryName(full)}' is no longer there.");
        }

        string currentName = WorkspaceValidation.DirectoryName(full);
        string parent = Path.GetDirectoryName(full)
            ?? throw new FileOperationException($"'{currentName}' is not somewhere anything can be renamed.");

        if (string.Equals(newName, currentName, StringComparison.Ordinal))
        {
            return full;
        }

        string? problem = FileNameValidation.ValidateIn(newName, parent, isDirectory, currentName);
        if (problem is not null)
        {
            throw new FileOperationException(problem);
        }

        string destination = Path.Combine(parent, newName);

        // Changing only the case of a name is a real rename that a case-insensitive file system reads
        // as a move onto the entry itself, and refuses. Going out to a name nothing else can have and
        // back in is how "notes.md" becomes "Notes.md" on Windows; elsewhere the two names differ
        // already and the move is the ordinary one.
        bool caseOnly = WorkspaceValidation.PathComparer.Equals(currentName, newName);
        string staging = caseOnly ? StagingPath(parent) : string.Empty;

        try
        {
            if (caseOnly)
            {
                MoveEntry(full, staging, isDirectory);
                MoveEntry(staging, destination, isDirectory);
            }
            else
            {
                MoveEntry(full, destination, isDirectory);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            if (caseOnly)
            {
                Restore(staging, full, isDirectory);
            }

            throw new FileOperationException($"'{currentName}' could not be renamed to '{newName}'.", exception);
        }

        return destination;
    }

    private static string Move(FileMovePlan plan, MoveResolution resolution)
    {
        string destination = plan.DestinationPath;

        try
        {
            if (plan.Status != FileMovePlanStatus.Conflict)
            {
                // The plan saw the destination free. It can have been taken since, in which case the
                // move fails rather than replaces — the user was never asked about it.
                MoveEntry(plan.SourcePath, destination, plan.IsDirectory);
            }
            else if (resolution == MoveResolution.Overwrite)
            {
                Overwrite(plan.SourcePath, destination, plan.IsDirectory);
            }
            else
            {
                destination = Path.Combine(
                    plan.TargetDirectory,
                    UniqueName.In(plan.TargetDirectory, plan.Name, plan.IsDirectory));

                MoveEntry(plan.SourcePath, destination, plan.IsDirectory);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new FileOperationException(
                $"'{plan.Name}' could not be moved into '{WorkspaceValidation.DirectoryName(plan.TargetDirectory)}'.",
                exception);
        }

        return destination;
    }

    private static void Delete(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new FileOperationException(
                $"'{WorkspaceValidation.DirectoryName(path)}' could not be deleted.",
                exception);
        }
    }

    private static void MoveEntry(string source, string destination, bool isDirectory)
    {
        if (isDirectory)
        {
            Directory.Move(source, destination);
        }
        else
        {
            File.Move(source, destination);
        }
    }

    // The one genuinely destructive branch in this class: whatever is at the destination is gone
    // afterwards, and a folder takes everything under it with it. It is reached only because the user
    // picked Overwrite in the conflict dialog, which is why that dialog names what would be replaced.
    private static void Overwrite(string source, string destination, bool isDirectory)
    {
        // Directory.Move will not write over anything, so a folder in the way has to be removed first.
        if (Directory.Exists(destination))
        {
            Directory.Delete(destination, recursive: true);
        }

        if (isDirectory)
        {
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }

            Directory.Move(source, destination);
        }
        else
        {
            // A file replaces a file in one step, which beats deleting first: if the move fails, the
            // destination is still there.
            File.Move(source, destination, overwrite: true);
        }
    }

    // A name nothing else can be holding, dotted so that the tree's filter hides it should the watcher
    // notice the intermediate state of a rename.
    private static string StagingPath(string parent)
    {
        string path;

        do
        {
            path = Path.Combine(parent, $".rename-{Guid.NewGuid():N}");
        }
        while (File.Exists(path) || Directory.Exists(path));

        return path;
    }

    // Only reached when the second half of a two-step rename failed: the entry is sitting under a name
    // the user never chose and cannot find, so putting it back is worth one attempt.
    private static void Restore(string staging, string original, bool isDirectory)
    {
        try
        {
            if (isDirectory ? Directory.Exists(staging) : File.Exists(staging))
            {
                MoveEntry(staging, original, isDirectory);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Nothing further to try. The failure the caller is told about is the rename's own.
        }
    }

    private static string Normalize(string path)
    {
        try
        {
            return WorkspaceValidation.NormalizeDirectory(path);
        }
        catch (Exception exception) when (exception is ArgumentException or PathTooLongException or NotSupportedException)
        {
            throw new FileOperationException($"'{path}' is not a usable path.", exception);
        }
    }
}
