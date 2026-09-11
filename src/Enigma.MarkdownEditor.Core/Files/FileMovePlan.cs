using System;
using System.IO;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// One move, decided but not yet carried out: what would be moved where, and whether it can be.
/// </summary>
/// <remarks>
/// A drag in the tree produces this before it produces anything on disk, so the drop can be refused
/// with a sentence, or paused on a dialog, without a half-finished move to undo. The plan is a
/// snapshot: the file system can change under it, which is why <see cref="FileOperations.MoveAsync"/>
/// still has to handle a destination that appeared in the meantime.
/// </remarks>
public sealed class FileMovePlan
{
    private FileMovePlan(
        FileMovePlanStatus status,
        string sourcePath,
        string targetDirectory,
        bool isDirectory,
        string? rejection)
    {
        Status = status;
        SourcePath = sourcePath;
        TargetDirectory = targetDirectory;
        IsDirectory = isDirectory;
        Rejection = rejection;

        // Split and joined rather than resolved: the planner has already turned both paths into their
        // full form, so this is pure string work that cannot fail on a path the planner rejected.
        Name = Path.GetFileName(Path.TrimEndingDirectorySeparator(sourcePath));
        SourceDirectory = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        DestinationPath = Path.Combine(targetDirectory, Name);
    }

    /// <summary>
    /// Gets the full path of the entry being moved.
    /// </summary>
    public string SourcePath { get; }

    /// <summary>
    /// Gets the folder the entry is being moved out of. Empty when the source is a drive root, which
    /// has no folder above it.
    /// </summary>
    public string SourceDirectory { get; }

    /// <summary>
    /// Gets the entry's own name, which the move carries with it.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the folder the entry is being moved into.
    /// </summary>
    public string TargetDirectory { get; }

    /// <summary>
    /// Gets the full path the entry would take, before any answer to a conflict is applied.
    /// </summary>
    public string DestinationPath { get; }

    /// <summary>
    /// Gets a value indicating whether the entry being moved is a folder.
    /// </summary>
    public bool IsDirectory { get; }

    /// <summary>
    /// Gets what the move amounts to.
    /// </summary>
    public FileMovePlanStatus Status { get; }

    /// <summary>
    /// Gets why the move was refused, in words the user can act on. Non-<see langword="null"/> exactly
    /// when <see cref="Status"/> is <see cref="FileMovePlanStatus.Rejected"/>.
    /// </summary>
    public string? Rejection { get; }

    /// <summary>
    /// Builds a plan with nothing in its way.
    /// </summary>
    /// <param name="sourcePath">The full path of the entry being moved.</param>
    /// <param name="targetDirectory">The folder it is being moved into.</param>
    /// <param name="isDirectory">Whether the entry is a folder.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="ArgumentException">Either string is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Either string is <see langword="null"/>.</exception>
    public static FileMovePlan Ready(string sourcePath, string targetDirectory, bool isDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        return new FileMovePlan(FileMovePlanStatus.Ready, sourcePath, targetDirectory, isDirectory, rejection: null);
    }

    /// <summary>
    /// Builds a plan whose destination name is already taken.
    /// </summary>
    /// <param name="sourcePath">The full path of the entry being moved.</param>
    /// <param name="targetDirectory">The folder it is being moved into.</param>
    /// <param name="isDirectory">Whether the entry is a folder.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="ArgumentException">Either string is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Either string is <see langword="null"/>.</exception>
    public static FileMovePlan Conflict(string sourcePath, string targetDirectory, bool isDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        return new FileMovePlan(FileMovePlanStatus.Conflict, sourcePath, targetDirectory, isDirectory, rejection: null);
    }

    /// <summary>
    /// Builds a plan that cannot be carried out at all.
    /// </summary>
    /// <param name="sourcePath">The full path of the entry that was going to be moved.</param>
    /// <param name="targetDirectory">The folder it was going to be moved into.</param>
    /// <param name="isDirectory">Whether the entry is a folder.</param>
    /// <param name="rejection">Why it was refused, in words the user can act on.</param>
    /// <returns>The plan.</returns>
    /// <exception cref="ArgumentException">Any string is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Any string is <see langword="null"/>.</exception>
    public static FileMovePlan Rejected(
        string sourcePath,
        string targetDirectory,
        bool isDirectory,
        string rejection)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(rejection);

        return new FileMovePlan(FileMovePlanStatus.Rejected, sourcePath, targetDirectory, isDirectory, rejection);
    }
}
