using System;
using System.Threading.Tasks;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.Avalonia.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Enigma.MarkdownEditor.Desktop.Views;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// The one place that knows how the About dialog is built and shown.
/// </summary>
/// <remarks>
/// Shaped like <see cref="WorkspaceManagerDialogService"/>, and for the same reason: the dialog host
/// is a control inside the main window, so only something holding <see cref="IContentDialogService"/>
/// can raise it, and a ViewModel must not be that something.
/// </remarks>
public sealed class AboutDialogService : IAboutDialogService
{
    private readonly Func<AboutViewModel> _createViewModel;
    private readonly IContentDialogService _dialogService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AboutDialogService"/> class.
    /// </summary>
    /// <param name="createViewModel">Builds a fresh ViewModel over the running build.</param>
    /// <param name="dialogService">Hosts the dialog.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public AboutDialogService(Func<AboutViewModel> createViewModel, IContentDialogService dialogService)
    {
        _createViewModel = createViewModel ?? throw new ArgumentNullException(nameof(createViewModel));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    /// <inheritdoc />
    public async Task ShowAsync()
    {
        AboutView view = new()
        {
            DataContext = _createViewModel(),
        };

        // The service resets the host before configuring it — buttons, commands, icon and its brush —
        // so only what this dialog has is set.
        await _dialogService.ShowAsync(dialog =>
        {
            // No title and no icon, as in every Enigma app's About dialog: the card draws them as a
            // column beside the content, and the view already shows the application's own icon and
            // name above everything else.
            dialog.Title = null;
            dialog.Content = view;

            // One button, and it only dismisses: the dialog asks nothing.
            dialog.CloseButtonText = "Close";
            dialog.DefaultButton = DefaultButton.Close;
        }).ConfigureAwait(true);
    }
}
