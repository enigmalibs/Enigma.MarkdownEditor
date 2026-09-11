using System.Threading;
using System.Threading.Tasks;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Files an image pasted into a note, and hands back the markdown that points at it.
/// </summary>
public interface IImagePasteService
{
    /// <summary>
    /// Writes the image beside the note and builds its link.
    /// </summary>
    /// <param name="image">What was on the clipboard.</param>
    /// <param name="documentPath">The note it is being pasted into.</param>
    /// <param name="cancellationToken">Abandons the write before it starts.</param>
    /// <returns>
    /// The markdown to insert at the caret, or <see langword="null"/> when the image could not be
    /// filed — in which case the user has already been told why.
    /// </returns>
    Task<string?> SaveAsync(IPastedImage image, string documentPath, CancellationToken cancellationToken = default);
}
