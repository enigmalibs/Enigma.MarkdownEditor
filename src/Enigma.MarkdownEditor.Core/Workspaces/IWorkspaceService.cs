using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Enigma.MarkdownEditor.Core.Workspaces;

/// <summary>
/// Owns the workspace list: what exists, which one is open, and keeping both on disk.
/// </summary>
/// <remarks>
/// Every mutating method persists the whole file before it returns, so a change that is visible is a
/// change that survived the write. A write that fails takes the in-memory state back with it — the
/// list never claims something the file does not hold — and the failure is thrown for the caller to
/// show.
/// </remarks>
public interface IWorkspaceService
{
    /// <summary>
    /// Raised after the open workspace changes, including when the last one is deleted.
    /// </summary>
    event EventHandler<ActiveWorkspaceChangedEventArgs>? ActiveWorkspaceChanged;

    /// <summary>
    /// Raised after the list itself changes: a workspace added, renamed, removed, or its folders edited.
    /// </summary>
    event EventHandler? WorkspacesChanged;

    /// <summary>
    /// Raised after a folder's colour changes, in any workspace.
    /// </summary>
    /// <remarks>
    /// Its own event rather than one of the two above, and deliberately: both of those mean "what the
    /// tree is showing is no longer what the workspace says", and the tree answers them by listing
    /// every root again — which would collapse the user's expanded folders every time they picked a
    /// colour. A colour changes how the same entries are drawn and nothing about what they are, so
    /// a subscriber re-reads the list and repaints instead. No argument, for the same reason
    /// <see cref="WorkspacesChanged"/> has none: the answer is on the workspace.
    /// </remarks>
    event EventHandler? FolderColorsChanged;

    /// <summary>
    /// Gets the workspaces, in the order the switcher lists them.
    /// </summary>
    IReadOnlyList<Workspace> Workspaces { get; }

    /// <summary>
    /// Gets the workspace currently open, or <see langword="null"/> when there is none.
    /// </summary>
    Workspace? ActiveWorkspace { get; }

    /// <summary>
    /// Creates a workspace over one root directory and opens it.
    /// </summary>
    /// <param name="name">The name, which must be free (see <see cref="WorkspaceValidation.ValidateName"/>).</param>
    /// <param name="rootDirectory">The first root directory. Stored normalized.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>The workspace that was created.</returns>
    /// <exception cref="WorkspaceValidationException">The name is not usable.</exception>
    Task<Workspace> CreateAsync(string name, string rootDirectory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames a workspace.
    /// </summary>
    /// <param name="workspaceId">Which workspace.</param>
    /// <param name="name">The new name, which must be free.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the file holds the new name.</returns>
    /// <exception cref="WorkspaceValidationException">The name is not usable.</exception>
    /// <exception cref="ArgumentException">No workspace has that identifier.</exception>
    Task RenameAsync(Guid workspaceId, string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a workspace from the list. Nothing on disk is touched.
    /// </summary>
    /// <param name="workspaceId">Which workspace.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the file no longer holds it.</returns>
    /// <exception cref="ArgumentException">No workspace has that identifier.</exception>
    Task DeleteAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a root directory to a workspace, at the end of its list.
    /// </summary>
    /// <param name="workspaceId">Which workspace.</param>
    /// <param name="directory">The directory to add. Stored normalized.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the file holds the new folder.</returns>
    /// <exception cref="WorkspaceValidationException">The directory is not usable, or is already there.</exception>
    /// <exception cref="ArgumentException">No workspace has that identifier.</exception>
    Task AddRootDirectoryAsync(Guid workspaceId, string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a root directory from a workspace.
    /// </summary>
    /// <param name="workspaceId">Which workspace.</param>
    /// <param name="directory">The directory to remove, as stored or in any equivalent form.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the file no longer holds the folder.</returns>
    /// <exception cref="WorkspaceValidationException">
    /// It is the workspace's last folder — a workspace without one has nothing to show.
    /// </exception>
    /// <exception cref="ArgumentException">No workspace has that identifier, or it has no such folder.</exception>
    Task RemoveRootDirectoryAsync(Guid workspaceId, string directory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves one of a workspace's root directories to another position.
    /// </summary>
    /// <param name="workspaceId">Which workspace.</param>
    /// <param name="fromIndex">The directory's current position.</param>
    /// <param name="toIndex">The position it should end up at.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the file holds the new order.</returns>
    /// <exception cref="ArgumentException">No workspace has that identifier.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Either index is outside the workspace's folders.</exception>
    Task MoveRootDirectoryAsync(Guid workspaceId, int fromIndex, int toIndex, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a folder of a workspace with a colour, or takes its colour away.
    /// </summary>
    /// <param name="workspaceId">Which workspace.</param>
    /// <param name="folder">The folder to colour. Stored normalized; need not be a root.</param>
    /// <param name="color">
    /// The colour, or <see cref="FolderColor.None"/> to remove the folder's entry altogether.
    /// </param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the file holds the new colour.</returns>
    /// <exception cref="WorkspaceValidationException">The folder is not a usable path.</exception>
    /// <exception cref="ArgumentException">No workspace has that identifier.</exception>
    /// <remarks>
    /// A call that asks for the colour the folder already has writes nothing and raises nothing — a
    /// menu whose current choice is picked again is not a change.
    /// </remarks>
    Task SetFolderColorAsync(
        Guid workspaceId,
        string folder,
        FolderColor color,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a workspace, and remembers it as the one to reopen at the next start.
    /// </summary>
    /// <param name="workspaceId">Which workspace.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the file holds the new choice.</returns>
    /// <exception cref="ArgumentException">No workspace has that identifier.</exception>
    Task ActivateAsync(Guid workspaceId, CancellationToken cancellationToken = default);
}
