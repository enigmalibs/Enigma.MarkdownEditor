using System.Threading;
using System.Threading.Tasks;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Decides what happens when the tree activates a file, and makes it happen.
/// </summary>
/// <remarks>
/// The tree's job ends at "the user asked for this file"; deciding whether that means the editor or
/// the operating system, and reporting a failure either way, is this service's.
/// </remarks>
public interface IDocumentOpener
{
    /// <summary>
    /// Opens a file — in the editor when it is one the editor handles, otherwise with the operating
    /// system's default application.
    /// </summary>
    /// <param name="filePath">The file to open.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>A task that completes once the file is on screen, or its failure has been reported.</returns>
    Task OpenAsync(string filePath, CancellationToken cancellationToken = default);
}
