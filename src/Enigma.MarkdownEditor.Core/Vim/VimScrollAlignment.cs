namespace Enigma.MarkdownEditor.Core.Vim;

/// <summary>
/// Where in the view <see cref="IVimBuffer.ScrollToLine"/> puts the line it is given.
/// </summary>
/// <remarks>
/// The three places vim's <c>z</c> commands name — <c>zt</c>, <c>zz</c>, <c>zb</c> — and the one
/// every other scroll is expressed in: <c>Ctrl+E</c> is "the line below the top one, at the top", and
/// a page down is "the line a page away, at the top", so <see cref="Top"/> is what the engine asks for
/// most and the other two exist for the <c>z</c> family alone.
/// </remarks>
public enum VimScrollAlignment
{
    /// <summary>
    /// The line becomes the first one in the view.
    /// </summary>
    Top,

    /// <summary>
    /// The line sits in the middle of the view.
    /// </summary>
    Center,

    /// <summary>
    /// The line becomes the last one in the view.
    /// </summary>
    Bottom,
}
