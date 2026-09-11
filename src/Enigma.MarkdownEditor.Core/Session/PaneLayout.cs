namespace Enigma.MarkdownEditor.Core.Session;

/// <summary>
/// How the three panes were divided up, as persisted in the session file.
/// </summary>
/// <remarks>
/// Two different units, because the grid uses two. The sidebar is a fixed column, so its width is the
/// number of layout units it was dragged to. The editor and the preview share what is left in a
/// ratio, so what is remembered about them is that ratio and not a width — which is what makes the
/// split survive being restored onto a window of a different size.
/// </remarks>
public sealed class PaneLayout
{
    /// <summary>
    /// The sidebar column's width, in layout units.
    /// </summary>
    public double SidebarWidth { get; set; }

    /// <summary>
    /// The editor column's share of what the sidebar leaves.
    /// </summary>
    public double EditorWeight { get; set; }

    /// <summary>
    /// The preview column's share of the same.
    /// </summary>
    public double PreviewWeight { get; set; }
}
