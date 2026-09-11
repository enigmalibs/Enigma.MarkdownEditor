using System;
using Enigma.MarkdownEditor.Core.Configuration;
using Enigma.MarkdownEditor.Core.Workspaces;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Turns a workspaces load that went wrong into something worth showing the user.
/// </summary>
public static class WorkspacesStartupNotice
{
    /// <summary>
    /// Builds the notice for a workspaces load.
    /// </summary>
    /// <param name="result">What <see cref="WorkspacesLoader.Load(string)"/> returned.</param>
    /// <returns>
    /// <see cref="StartupNotice.None"/> when the file loaded normally; otherwise a notice saying what
    /// was wrong and where the old file went — the list can be rebuilt, so this is a warning, not a
    /// reason to refuse to start.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    public static StartupNotice For(ConfigLoadResult<WorkspacesConfig> result)
        => ConfigStartupNotice.For(result, "Workspaces could not be read", "with no workspaces");
}
