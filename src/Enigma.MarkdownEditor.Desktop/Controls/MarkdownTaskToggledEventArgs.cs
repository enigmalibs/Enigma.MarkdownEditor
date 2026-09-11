using System;

namespace Enigma.MarkdownEditor.Desktop.Controls;

/// <summary>
/// A task box the reader ticked or unticked in the preview: where its state character is in the
/// source, and what they asked it to become.
/// </summary>
/// <remarks>
/// <para>
/// The answer travels back on the same object. The renderer has already flipped the box — the reader
/// clicked it and expects it to move — but only the side holding the buffer can say whether the
/// character was actually written, and a toggle aimed at a stale offset is refused. So the handler
/// sets <see cref="Applied"/>, and a box nobody applied is put back the way the document has it.
/// </para>
/// <para>
/// The default is <see langword="false"/> on purpose: a preview with no handler attached, or one
/// whose handler threw, must leave the box showing what the document says rather than what the click
/// hoped for.
/// </para>
/// </remarks>
public sealed class MarkdownTaskToggledEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownTaskToggledEventArgs"/> class.
    /// </summary>
    /// <param name="sourceOffset">
    /// The offset, in the markdown that was rendered, of the character between the brackets.
    /// </param>
    /// <param name="isChecked">The state the reader asked for.</param>
    public MarkdownTaskToggledEventArgs(int sourceOffset, bool isChecked)
    {
        SourceOffset = sourceOffset;
        IsChecked = isChecked;
    }

    /// <summary>
    /// The offset, in the markdown that was rendered, of the character between the brackets — the
    /// single character a toggle rewrites.
    /// </summary>
    /// <remarks>
    /// Recorded when the box was built, so it describes the text of that render. The buffer may have
    /// moved on since: whoever applies this has to check the position still holds a task box before
    /// writing to it.
    /// </remarks>
    public int SourceOffset { get; }

    /// <summary>
    /// The state the reader asked for: <see langword="true"/> for a ticked box.
    /// </summary>
    public bool IsChecked { get; }

    /// <summary>
    /// Set by the handler to say the document took the change. Left <see langword="false"/> to have
    /// the box put back the way the document has it.
    /// </summary>
    public bool Applied { get; set; }
}
