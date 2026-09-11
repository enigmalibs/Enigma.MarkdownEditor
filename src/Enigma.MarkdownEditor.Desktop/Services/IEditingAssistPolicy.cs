namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Whether the markdown editing assists apply to the keystroke the editor is about to handle.
/// </summary>
/// <remarks>
/// <para>
/// The assists — list continuation, Tab indentation, the formatting shortcuts and the image paste —
/// belong to ordinary typing. Under vim emulation only insert mode is ordinary typing: in normal and
/// visual mode the very same keys are commands, and an editor that quietly inserted a bullet on Enter
/// there would be an editor that is not vim.
/// </para>
/// <para>
/// FEATURE-11AA is what makes that distinction, and <see cref="VimEditingAssistPolicy"/> is the
/// answer it brings: with vim switched off every keystroke is typing and the assists are always on,
/// exactly as before. The seam is here so the vim engine has one place to speak from rather than a
/// key handler to reach into.
/// </para>
/// </remarks>
public interface IEditingAssistPolicy
{
    /// <summary>
    /// Gets a value indicating whether the assists are active right now.
    /// </summary>
    bool IsActive { get; }
}
