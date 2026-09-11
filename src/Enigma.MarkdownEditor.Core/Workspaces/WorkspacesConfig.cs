using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Enigma.MarkdownEditor.Core.Workspaces;

/// <summary>
/// Everything the workspaces file holds: the workspaces themselves and which one was open last.
/// </summary>
public sealed class WorkspacesConfig
{
    /// <summary>
    /// The file-format version this build writes and understands.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// The version of the file's format.
    /// </summary>
    /// <remarks>
    /// A file from a future version is rejected rather than read as best it can be: the load then
    /// quarantines it, which keeps the newer file intact instead of rewriting it in this version's
    /// shape and dropping whatever this build does not know about.
    /// </remarks>
    [Range(1, CurrentVersion)]
    public int Version { get; set; } = CurrentVersion;

    /// <summary>
    /// The workspaces, in the order the switcher lists them.
    /// </summary>
    public List<Workspace> Workspaces { get; set; } = [];

    /// <summary>
    /// The workspace to reopen at the next start, or <see langword="null"/> when there is none.
    /// </summary>
    /// <remarks>
    /// An identifier that matches no workspace is not an error — the service falls back to the first
    /// one — so that hand-deleting a workspace from the file cannot make the editor refuse to start.
    /// </remarks>
    public Guid? LastActiveWorkspaceId { get; set; }
}
