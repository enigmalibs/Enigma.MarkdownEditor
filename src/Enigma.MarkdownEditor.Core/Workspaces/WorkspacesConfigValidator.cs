using System;
using System.Collections.Generic;
using System.Linq;
using Enigma.Configuration;

namespace Enigma.MarkdownEditor.Core.Workspaces;

/// <summary>
/// The half of <see cref="WorkspacesConfig"/>' validation that DataAnnotations cannot express.
/// </summary>
/// <remarks>
/// A single workspace's own shape — a name that is there and not too long, a supported file version —
/// is stated by the attributes on the classes themselves, and the library evaluates those all the way
/// down into the list. What is left is everything that spans more than one property: uniqueness across
/// the list, and the rule that a workspace without a root directory is not a workspace.
/// </remarks>
public sealed class WorkspacesConfigValidator : ConfigValidator<WorkspacesConfig>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspacesConfigValidator"/> class.
    /// </summary>
    public WorkspacesConfigValidator()
        => RuleFor(config => config.Workspaces)
            .Must(HasUniqueNames, "must not name two workspaces the same.")
            .Must(HasUniqueIds, "must not give two workspaces the same identifier.")
            .Must(AllHaveARootDirectory, "requires every workspace to have at least one folder.")
            .Must(HasUsableRootDirectories, "must not repeat or blank out a workspace's folders.")
            .Must(HasUsableFolderColors, "must not colour a blank folder, or one folder twice.");

    private static bool HasUniqueNames(List<Workspace> workspaces)
        => workspaces
            .Select(workspace => workspace.Name?.Trim() ?? string.Empty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == workspaces.Count;

    private static bool HasUniqueIds(List<Workspace> workspaces)
        => workspaces.Select(workspace => workspace.Id).Distinct().Count() == workspaces.Count;

    private static bool AllHaveARootDirectory(List<Workspace> workspaces)
        => workspaces.TrueForAll(workspace => workspace.RootDirectories.Count > 0);

    private static bool HasUsableRootDirectories(List<Workspace> workspaces)
        => workspaces.TrueForAll(workspace =>
            !workspace.RootDirectories.Exists(string.IsNullOrWhiteSpace) &&
            workspace.RootDirectories.Distinct(WorkspaceValidation.PathComparer).Count()
                == workspace.RootDirectories.Count);

    // The same two rules the root directories get, and for the same reason: a blank path names no
    // folder, and two entries for one folder leave the colour a file inherits down to list order.
    private static bool HasUsableFolderColors(List<Workspace> workspaces)
        => workspaces.TrueForAll(workspace =>
            !workspace.FolderColors.Exists(entry => string.IsNullOrWhiteSpace(entry.Path)) &&
            workspace.FolderColors
                .Select(entry => entry.Path)
                .Distinct(WorkspaceValidation.PathComparer)
                .Count() == workspace.FolderColors.Count);
}
