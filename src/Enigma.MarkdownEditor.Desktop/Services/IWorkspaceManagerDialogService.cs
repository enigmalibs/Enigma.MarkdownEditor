using System.Threading.Tasks;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Shows the manage-workspaces dialog, so that no ViewModel has to know how it is built.
/// </summary>
public interface IWorkspaceManagerDialogService
{
    /// <summary>
    /// Shows the dialog and waits for it to be closed.
    /// </summary>
    /// <returns>A task that completes when the user is done managing workspaces.</returns>
    /// <remarks>
    /// Nothing is returned: every edit is applied and persisted as it is made, so there is no result to
    /// carry back and nothing to commit on the way out.
    /// </remarks>
    Task ShowAsync();
}
