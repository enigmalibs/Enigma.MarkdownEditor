using System;
using System.Collections.Generic;
using System.Windows.Input;
using Enigma.MarkdownEditor.Core.Workspaces;

namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// One workspace as the switcher and the manage dialog list it.
/// </summary>
/// <remarks>
/// Deliberately a snapshot rather than an observable wrapper: every change to a workspace goes through
/// <see cref="IWorkspaceService"/>, which raises an event, and the owning ViewModel rebuilds its whole
/// list from it. A row that cannot go stale needs no change notification.
/// </remarks>
public sealed class WorkspaceListItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceListItem"/> class.
    /// </summary>
    /// <param name="workspace">The workspace this row shows.</param>
    /// <param name="isActive">Whether it is the one currently open.</param>
    /// <param name="activateCommand">
    /// The command the switcher runs to open it, taking this row as its parameter, or
    /// <see langword="null"/> in a list that does not open anything.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="workspace"/> is <see langword="null"/>.</exception>
    public WorkspaceListItem(Workspace workspace, bool isActive, ICommand? activateCommand = null)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        Model = workspace;
        IsActive = isActive;
        ActivateCommand = activateCommand;

        IReadOnlyList<string> missing = WorkspaceValidation.MissingRootDirectories(workspace);
        MissingRootCount = missing.Count;

        int roots = workspace.RootDirectories.Count;
        string folders = roots == 1 ? "1 folder" : $"{roots} folders";
        Summary = MissingRootCount == 0 ? folders : $"{folders} — {MissingRootCount} missing";
    }

    /// <summary>
    /// Gets the workspace behind the row.
    /// </summary>
    public Workspace Model { get; }

    /// <summary>
    /// Gets the workspace's identifier.
    /// </summary>
    public Guid Id => Model.Id;

    /// <summary>
    /// Gets the name shown.
    /// </summary>
    public string Name => Model.Name;

    /// <summary>
    /// Gets the line under the name: how many folders, and how many of them are not there.
    /// </summary>
    public string Summary { get; }

    /// <summary>
    /// Gets the number of root directories that are not on disk right now.
    /// </summary>
    public int MissingRootCount { get; }

    /// <summary>
    /// Gets a value indicating whether any root directory is missing.
    /// </summary>
    public bool HasMissingRoots => MissingRootCount > 0;

    /// <summary>
    /// Gets a value indicating whether this is the workspace currently open.
    /// </summary>
    public bool IsActive { get; }

    /// <summary>
    /// Gets the command that opens this workspace, or <see langword="null"/> when the list is not a
    /// switcher.
    /// </summary>
    public ICommand? ActivateCommand { get; }
}
