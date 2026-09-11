using System;
using Enigma.MarkdownEditor.Core.Workspaces;

namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// One root directory of a workspace, as the manage dialog and the sidebar list it.
/// </summary>
/// <remarks>
/// A snapshot, like <see cref="WorkspaceListItem"/>: whether the folder is there is read once, when
/// the list is built, rather than polled per frame.
/// </remarks>
public sealed class WorkspaceRootItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceRootItem"/> class.
    /// </summary>
    /// <param name="path">The root directory, as the workspace stores it.</param>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    public WorkspaceRootItem(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Path = path;
        Name = WorkspaceValidation.DirectoryName(path);
        IsMissing = !WorkspaceValidation.DirectoryExists(path);
    }

    /// <summary>
    /// Gets the full path, which is also the row's tooltip.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the folder's own name, which is what the row shows.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets a value indicating whether the folder is not on disk right now.
    /// </summary>
    public bool IsMissing { get; }

    /// <summary>
    /// Gets the tooltip: the path, and why it is greyed out when it is.
    /// </summary>
    public string Tooltip => IsMissing ? $"{Path}\nThis folder is not there right now." : Path;
}
