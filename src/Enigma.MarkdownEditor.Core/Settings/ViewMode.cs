namespace Enigma.MarkdownEditor.Core.Settings;

/// <summary>
/// Which of the editor and the preview the window shows.
/// </summary>
/// <remarks>
/// <para>
/// Three states rather than two independent flags, because the two panes are not independent: there
/// is no fourth arrangement in which neither is shown, and an editor with nothing to edit beside it
/// is the same window as a preview with nothing to preview. One value that cannot express the empty
/// case is what keeps the toolbar's three buttons and the shortcut that cycles them agreeing.
/// </para>
/// <para>
/// Here rather than in the Desktop project, and by name rather than by number: FEATURE-40F5 PHASE02
/// persists which mode the window was left in, so the numeric values may be reordered but the member
/// names may not.
/// </para>
/// </remarks>
public enum ViewMode
{
    /// <summary>
    /// The editor alone, filling the space the preview would have taken.
    /// </summary>
    EditorOnly = 0,

    /// <summary>
    /// Both, side by side, with the editor's scrolling tracked by the preview.
    /// </summary>
    Split = 1,

    /// <summary>
    /// The rendered preview alone — reading rather than writing.
    /// </summary>
    PreviewOnly = 2,
}
