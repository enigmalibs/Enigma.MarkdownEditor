using System;
using System.Threading.Tasks;
using Enigma.Avalonia.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Enigma.MarkdownEditor.Desktop.Views;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// The one place that knows how the manage-workspaces dialog is built and shown.
/// </summary>
/// <remarks>
/// The view and its ViewModel are resolved per showing rather than injected as instances: the dialog
/// reads the workspace list when it is built, so a reused instance would show the list as it was the
/// first time it was opened.
/// </remarks>
public sealed class WorkspaceManagerDialogService : IWorkspaceManagerDialogService
{
    private readonly Func<WorkspaceManagerViewModel> _createViewModel;
    private readonly IContentDialogService _dialogService;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceManagerDialogService"/> class.
    /// </summary>
    /// <param name="createViewModel">Builds a fresh ViewModel over the current workspace list.</param>
    /// <param name="dialogService">Hosts the dialog.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public WorkspaceManagerDialogService(
        Func<WorkspaceManagerViewModel> createViewModel,
        IContentDialogService dialogService)
    {
        _createViewModel = createViewModel ?? throw new ArgumentNullException(nameof(createViewModel));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    /// <inheritdoc />
    public async Task ShowAsync()
    {
        WorkspaceManagerView view = new()
        {
            DataContext = _createViewModel(),
        };

        await _dialogService.ShowAsync(dialog =>
        {
            dialog.Title = "Manage workspaces";
            dialog.Content = view;

            // One button, and it only dismisses: every edit is written as it is made, so there is
            // nothing here to confirm or to roll back.
            dialog.CloseButtonText = "Done";
        });
    }
}
