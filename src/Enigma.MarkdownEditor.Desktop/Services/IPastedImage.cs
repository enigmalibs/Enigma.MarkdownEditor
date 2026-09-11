namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// An image on its way from the clipboard to a file beside the note.
/// </summary>
/// <remarks>
/// One method, and the reason it exists rather than an Avalonia <c>Bitmap</c> being passed around: a
/// bitmap cannot be built without an initialized Avalonia application, which would put the whole
/// filing rule — the folder, the name, the collision, the link — out of reach of a unit test. This is
/// the one line of it that genuinely needs the graphics stack, so it is the one line that is faked.
/// </remarks>
public interface IPastedImage
{
    /// <summary>
    /// Writes the image to a file.
    /// </summary>
    /// <param name="filePath">Where to write it. Its folder already exists.</param>
    void Save(string filePath);
}
