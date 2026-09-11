using System.ComponentModel.DataAnnotations;

namespace Enigma.MarkdownEditor.Core.Workspaces;

/// <summary>
/// One folder of a workspace and the colour it is marked with, as persisted in the workspaces file.
/// </summary>
/// <remarks>
/// A list of these rather than a path-keyed map, and settable properties rather than a record, for
/// the same reason <see cref="Workspace"/> is shaped the way it is: this class is bound to JSON and
/// mutated in place by <see cref="WorkspaceService"/>. The rules a single property must satisfy are
/// stated by the attributes below; the ones that span the list — no two entries for one folder — live
/// in <see cref="WorkspacesConfigValidator"/>.
/// </remarks>
public sealed class FolderColorEntry
{
    /// <summary>
    /// The folder, as an absolute path in the form <see cref="WorkspaceValidation.NormalizeDirectory"/>
    /// stores.
    /// </summary>
    /// <remarks>
    /// Any folder, not only a root: colouring a sub-folder is the ordinary case, and the file is not
    /// the place to insist that a path is still under one of the workspace's roots — a root that is
    /// unplugged today is back tomorrow, and an entry dropped meanwhile is a colour the user has to
    /// set again.
    /// </remarks>
    [Required(AllowEmptyStrings = false)]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// The colour the folder is marked with.
    /// </summary>
    /// <remarks>
    /// <see cref="FolderColor.None"/> never reaches the file — clearing a colour removes the whole
    /// entry — but it is accepted on the way in, because a hand-edited file holding one means "no
    /// colour" and that is not worth quarantining the file over.
    /// </remarks>
    [EnumDataType(typeof(FolderColor))]
    public FolderColor Color { get; set; }

    /// <summary>
    /// Makes an independent copy, so a mutation can be rolled back when the write that follows it
    /// fails.
    /// </summary>
    /// <returns>A copy sharing nothing mutable with this instance.</returns>
    internal FolderColorEntry Clone()
        => new()
        {
            Path = Path,
            Color = Color,
        };
}
