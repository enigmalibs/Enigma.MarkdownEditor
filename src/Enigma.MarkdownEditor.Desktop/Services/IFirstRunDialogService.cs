using System.Threading.Tasks;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Shows the first-run welcome dialog, so that no ViewModel has to know how it is built.
/// </summary>
public interface IFirstRunDialogService
{
    /// <summary>
    /// Shows the dialog, waits for it to be dismissed, and applies what it decided.
    /// </summary>
    /// <returns>A task that completes once the answers have been written, or reported as unwritable.</returns>
    /// <remarks>
    /// Nothing is returned: every way out of the dialog is terminal — the primary button, the close
    /// button, <c>Escape</c> and a click on the scrim all persist the settings, because their absence
    /// is what would bring the dialog back at the next start. What the caller has to do afterwards is
    /// the same either way: re-read the settings.
    /// </remarks>
    Task ShowAsync();
}
