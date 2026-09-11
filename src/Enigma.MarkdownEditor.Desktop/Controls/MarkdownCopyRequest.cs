using System;

namespace Enigma.MarkdownEditor.Desktop.Controls;

/// <summary>
/// A copy the reader asked for in the preview, and which of the two kinds it is: the text they had
/// selected, or the whole rendered note.
/// </summary>
/// <remarks>
/// <para>
/// The renderer resolves the kind, because it is the only side that can: whether anything is selected
/// — and whether the selection is one paragraph's or the document's — is state held per text block
/// inside the pane. What it does <em>not</em> resolve is the whole-note text, which is a projection of
/// the markdown rather than of the visuals: <see cref="SelectedText"/> is left
/// <see langword="null"/> and the handler renders the document itself.
/// </para>
/// <para>
/// Nothing travels back on this object, unlike <see cref="MarkdownTaskToggledEventArgs"/>. A copy has
/// no answer the control needs — the clipboard either took the text or it did not, and either way
/// there is nothing on screen to put back.
/// </para>
/// </remarks>
public sealed class MarkdownCopyRequest : EventArgs
{
    private MarkdownCopyRequest(string? selectedText) => SelectedText = selectedText;

    /// <summary>
    /// A request for the whole rendered note, which is what a copy means when nothing is selected and
    /// what a select-all followed by a copy means too.
    /// </summary>
    public static MarkdownCopyRequest WholeDocument { get; } = new(selectedText: null);

    /// <summary>
    /// The text the reader had selected, or <see langword="null"/> when the request is for the whole
    /// note.
    /// </summary>
    public string? SelectedText { get; }

    /// <summary>
    /// Whether this request carries a selection rather than asking for the whole note.
    /// </summary>
    public bool IsSelection => SelectedText is not null;

    /// <summary>
    /// A request for text the reader had selected.
    /// </summary>
    /// <param name="selectedText">The selected text.</param>
    /// <returns>The request.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="selectedText"/> is <see langword="null"/>.</exception>
    public static MarkdownCopyRequest ForSelection(string selectedText)
    {
        ArgumentNullException.ThrowIfNull(selectedText);

        return new MarkdownCopyRequest(selectedText);
    }
}
