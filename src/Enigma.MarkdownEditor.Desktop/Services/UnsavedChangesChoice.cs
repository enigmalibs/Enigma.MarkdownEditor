namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// What the user answered when told a document they are closing has unsaved changes.
/// </summary>
public enum UnsavedChangesChoice
{
    /// <summary>
    /// Back out. The document stays open and, when the question came from the window closing, the
    /// application stays running.
    /// </summary>
    Cancel,

    /// <summary>
    /// Write the changes, then close.
    /// </summary>
    Save,

    /// <summary>
    /// Throw the changes away and close.
    /// </summary>
    Discard,
}
