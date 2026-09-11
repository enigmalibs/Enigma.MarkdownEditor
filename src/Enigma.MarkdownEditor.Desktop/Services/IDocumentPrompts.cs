using System.Threading.Tasks;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Everything the editor has to ask the user before it can close or save a document.
/// </summary>
/// <remarks>
/// A seam for the same reason <see cref="IFileOperationPrompts"/> is one: both methods end in a
/// window the platform owns — a <c>ContentDialog</c> that needs a registered host, a save dialog that
/// needs a storage provider — and the close-and-save matrix is exactly the part of the editor most
/// worth testing without either.
/// </remarks>
public interface IDocumentPrompts
{
    /// <summary>
    /// Asks what to do about a document that is being closed with changes in it.
    /// </summary>
    /// <param name="fileName">The document's name, for the question.</param>
    /// <returns>
    /// What the user chose. Dismissing the dialog is <see cref="UnsavedChangesChoice.Cancel"/> — the
    /// only safe reading of a question whose other answers both end the document's current state.
    /// </returns>
    Task<UnsavedChangesChoice> AskAboutUnsavedChangesAsync(string fileName);

    /// <summary>
    /// Asks where to write a document.
    /// </summary>
    /// <param name="suggestedFileName">The name to offer, usually the document's current one.</param>
    /// <param name="startDirectory">The folder to open in, or <see langword="null"/> for the default.</param>
    /// <returns>The chosen path, or <see langword="null"/> when the user backed out.</returns>
    Task<string?> AskWhereToSaveAsync(string suggestedFileName, string? startDirectory);
}
