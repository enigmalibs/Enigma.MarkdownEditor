using System;

namespace Enigma.MarkdownEditor.Core.Workspaces;

/// <summary>
/// Says which workspace is open now.
/// </summary>
public sealed class ActiveWorkspaceChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActiveWorkspaceChangedEventArgs"/> class.
    /// </summary>
    /// <param name="workspace">The workspace now open, or <see langword="null"/> when there is none.</param>
    public ActiveWorkspaceChangedEventArgs(Workspace? workspace) => Workspace = workspace;

    /// <summary>
    /// Gets the workspace now open, or <see langword="null"/> when the last one was deleted.
    /// </summary>
    public Workspace? Workspace { get; }
}
