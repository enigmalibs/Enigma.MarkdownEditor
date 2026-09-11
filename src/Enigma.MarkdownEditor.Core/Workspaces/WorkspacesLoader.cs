using System;
using Enigma.MarkdownEditor.Core.Configuration;
using Enigma.MarkdownEditor.Core.Settings;

namespace Enigma.MarkdownEditor.Core.Workspaces;

/// <summary>
/// Reads the workspaces file at start-up, and guarantees a usable configuration even when it cannot.
/// </summary>
/// <remarks>
/// The recovery is <see cref="ConfigRecovery"/>'s, shared with the settings file: an unreadable file is
/// moved aside and the editor starts with no workspaces rather than refusing to start. Losing the list
/// is annoying; a file that throws on every read would make the editor unusable, and the quarantined
/// copy can still be repaired by hand.
/// </remarks>
public static class WorkspacesLoader
{
    /// <summary>
    /// Loads the workspaces file, recovering from a file that cannot be read.
    /// </summary>
    /// <param name="workspacesFilePath">Absolute path of the file (see <see cref="AppPaths.WorkspacesFile"/>).</param>
    /// <returns>
    /// A result whose <see cref="ConfigLoadResult{T}.Config"/> is always readable — holding an empty
    /// workspace list when the file could not be used.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="workspacesFilePath"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="workspacesFilePath"/> is <see langword="null"/>.</exception>
    public static ConfigLoadResult<WorkspacesConfig> Load(string workspacesFilePath)
        => ConfigRecovery.Load(
            workspacesFilePath,
            new WorkspacesConfigValidator(),
            AppPaths.QuarantinedWorkspacesFileName);
}
