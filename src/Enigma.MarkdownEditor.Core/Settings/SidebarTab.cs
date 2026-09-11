namespace Enigma.MarkdownEditor.Core.Settings;

/// <summary>
/// Which of the sidebar's two tabs is showing.
/// </summary>
/// <remarks>
/// Here rather than in the Desktop project, and by name rather than by number, for the same reason as
/// <see cref="ViewMode"/>: FEATURE-40F5 PHASE02 persists which tab the sidebar was left on, so the
/// numeric values may be reordered but the member names may not.
/// </remarks>
public enum SidebarTab
{
    /// <summary>
    /// The workspace file tree.
    /// </summary>
    Explorer = 0,

    /// <summary>
    /// Workspace-wide search: the query box and its results.
    /// </summary>
    Search = 1,
}
