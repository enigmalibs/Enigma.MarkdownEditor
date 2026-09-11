using System.Threading.Tasks;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Shows the About dialog, so that no ViewModel has to know how it is built.
/// </summary>
public interface IAboutDialogService
{
    /// <summary>
    /// Shows the dialog and waits for it to be closed.
    /// </summary>
    /// <returns>A task that completes when the user has dismissed it.</returns>
    /// <remarks>
    /// Nothing is returned: the dialog states what the application is and offers no choice to carry
    /// back.
    /// </remarks>
    Task ShowAsync();
}
