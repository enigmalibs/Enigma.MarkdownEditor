namespace Enigma.MarkdownEditor.Core.Session;

/// <summary>
/// Where the window was and how it was showing, as persisted in the session file.
/// </summary>
/// <remarks>
/// <para>
/// In <em>device pixels</em>, not in the layout units the window itself is sized in. A window's
/// position is already given in device pixels by every platform, and screens report their working
/// areas in the same unit — so clamping a remembered window against the screens it has to fit on is
/// one comparison rather than three conversions, and it stays right on a machine whose monitors do
/// not share a scaling factor. Turning the size back into layout units is the view's job, once it
/// knows which screen the window landed on.
/// </para>
/// <para>
/// The bounds are always the window's <em>restored</em> ones. A maximized window is remembered as
/// "these bounds, maximized", so unmaximizing after a restart puts it back where it was rather than
/// filling the screen a second time.
/// </para>
/// </remarks>
public sealed class WindowPlacement
{
    /// <summary>
    /// The left edge, in device pixels.
    /// </summary>
    public int X { get; set; }

    /// <summary>
    /// The top edge, in device pixels.
    /// </summary>
    public int Y { get; set; }

    /// <summary>
    /// The width, in device pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// The height, in device pixels.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Whether the window was maximized over the bounds above.
    /// </summary>
    public bool IsMaximized { get; set; }
}
