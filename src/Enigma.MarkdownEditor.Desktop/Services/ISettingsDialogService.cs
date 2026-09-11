using System.Threading.Tasks;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Opens the settings dialog, and persists what the user changed once it closes.
/// </summary>
/// <remarks>
/// It exists so that no ViewModel has to know how the dialog is built: the shell asks for "show
/// settings" and this service owns the view, the hosting and the save that follows.
/// </remarks>
public interface ISettingsDialogService
{
    /// <summary>
    /// Shows the settings dialog over the main window and returns once it has closed and the changes
    /// have been written.
    /// </summary>
    /// <returns>A task that completes after the save attempt, including any failure report.</returns>
    Task ShowAsync();
}
