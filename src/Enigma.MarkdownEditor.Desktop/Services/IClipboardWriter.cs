using System.Threading.Tasks;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Puts text on the system clipboard.
/// </summary>
/// <remarks>
/// A seam for the same reason <see cref="IFileLauncher"/> is one: the real implementation needs a
/// window to reach the platform's clipboard through, and a unit test has none. It is also what keeps
/// the write out of the preview control — the renderer reports that a copy was asked for, and the
/// pane's ViewModel is what reaches a service.
/// </remarks>
public interface IClipboardWriter
{
    /// <summary>
    /// Replaces the clipboard's contents with <paramref name="text"/>.
    /// </summary>
    /// <param name="text">The text to put on the clipboard.</param>
    /// <returns>
    /// <see langword="true"/> when the text was written. <see langword="false"/> when this platform
    /// has no clipboard to write to — a headless run, or a window that is not on screen yet.
    /// </returns>
    Task<bool> WriteTextAsync(string text);

    /// <summary>
    /// Replaces the clipboard's contents with both flavours of one copy: <paramref name="text"/> for
    /// an application that takes plain text, <paramref name="html"/> for one that can keep the
    /// formatting.
    /// </summary>
    /// <param name="text">The plain-text flavour.</param>
    /// <param name="html">The HTML flavour, as a fragment.</param>
    /// <returns>
    /// <see langword="true"/> when both were written. <see langword="false"/> when this platform has
    /// no clipboard to write to.
    /// </returns>
    /// <remarks>
    /// One write carrying two formats, not two writes: the second write would replace the first, and
    /// the receiving application is what chooses between them at paste time — Notepad takes the text,
    /// Word takes the HTML, and neither is asked in advance.
    /// </remarks>
    Task<bool> WriteTextAndHtmlAsync(string text, string html);
}
