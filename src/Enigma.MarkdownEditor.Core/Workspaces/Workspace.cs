using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Enigma.MarkdownEditor.Core.Workspaces;

/// <summary>
/// A named set of root directories the editor works in, as persisted in the workspaces file.
/// </summary>
/// <remarks>
/// The properties are settable because this class is bound to JSON and mutated in place by
/// <see cref="WorkspaceService"/>; nothing outside that service is expected to write to it. The rules
/// a single property must satisfy are stated by the attributes below; the ones that span a workspace
/// or the whole list live in <see cref="WorkspacesConfigValidator"/>.
/// </remarks>
public sealed class Workspace
{
    /// <summary>
    /// The longest workspace name accepted. Long enough for a sentence-ish label, short enough to fit
    /// the switcher without eliding.
    /// </summary>
    public const int MaxNameLength = 60;

    /// <summary>
    /// Identity, stable across renames — which is what the last-active reference is keyed by.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The name shown in the switcher. Unique across the list, case-insensitively.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    [MaxLength(MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The workspace's root directories, in the order the tree lists them. Always at least one; a
    /// directory that has since been deleted or unmounted is a warning, not an error.
    /// </summary>
    public List<string> RootDirectories { get; set; } = [];

    /// <summary>
    /// The folders inside this workspace that carry a colour, in no particular order. Empty for a
    /// workspace nobody has coloured anything in — which is every workspace written before
    /// <c>FEATURE-A225</c>, and why the key is additive rather than a file-format change.
    /// </summary>
    /// <remarks>
    /// Not restricted to the root directories: colouring a sub-folder is the ordinary case, and
    /// <see cref="FolderColorMap"/> is what decides which of these a given file inherits from.
    /// </remarks>
    public List<FolderColorEntry> FolderColors { get; set; } = [];

    /// <summary>
    /// Makes an independent copy, so a mutation can be rolled back when the write that follows it
    /// fails.
    /// </summary>
    /// <returns>A copy sharing nothing mutable with this instance.</returns>
    internal Workspace Clone()
        => new()
        {
            Id = Id,
            Name = Name,
            RootDirectories = [.. RootDirectories],

            // The entries themselves, not just the list: a rollback that restored the same instances
            // would restore the objects the failed mutation had already edited.
            FolderColors = [.. FolderColors.ConvertAll(entry => entry.Clone())],
        };
}
