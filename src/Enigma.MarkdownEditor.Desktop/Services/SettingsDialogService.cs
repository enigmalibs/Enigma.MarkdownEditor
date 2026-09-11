using System;
using System.Threading.Tasks;
using Enigma.Avalonia.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Enigma.MarkdownEditor.Desktop.Views;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// The one place that knows how the settings dialog is built, shown and saved.
/// </summary>
/// <remarks>
/// Shaped like <see cref="AboutDialogService"/>: the dialog host is a control inside the main window,
/// so only something holding <see cref="IContentDialogService"/> can raise it, and a ViewModel must
/// not be that something. The view and its ViewModel are built per showing because the ViewModel
/// reads the settings as they are when it is created.
/// </remarks>
public sealed class SettingsDialogService : ISettingsDialogService
{
    private readonly Func<SettingsViewModel> _createViewModel;
    private readonly IContentDialogService _dialogService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsDialogService"/> class.
    /// </summary>
    /// <param name="createViewModel">Builds a fresh settings ViewModel over the current settings.</param>
    /// <param name="dialogService">Hosts the dialog, and reports a failed save once it is gone.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public SettingsDialogService(
        Func<SettingsViewModel> createViewModel,
        IContentDialogService dialogService)
    {
        _createViewModel = createViewModel ?? throw new ArgumentNullException(nameof(createViewModel));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
    }

    /// <inheritdoc />
    public async Task ShowAsync()
    {
        SettingsViewModel viewModel = _createViewModel();
        SettingsView view = new()
        {
            DataContext = viewModel,
        };

        await _dialogService.ShowAsync(dialog =>
        {
            dialog.Title = "Settings";
            dialog.Content = view;

            // One button, and it only dismisses: every edit is applied to the live settings as it is
            // made, so there is nothing here to confirm or to roll back. Escape and a scrim click
            // mean the same thing, which is why the result is not read.
            dialog.CloseButtonText = "Close";
        });

        // Saved after the dialog is gone, not while it closes. A write can fail — a rejected value, a
        // read-only folder — and the ContentDialog host is a single control: a message raised while
        // the settings dialog is still up would replace it rather than appear over it.
        string? failure = await viewModel.SaveAsync();
        if (failure is not null)
        {
            await _dialogService.ShowMessageAsync("Settings were not saved", failure);
        }
    }
}
