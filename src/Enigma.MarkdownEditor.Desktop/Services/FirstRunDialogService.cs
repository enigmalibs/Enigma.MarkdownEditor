using System;
using System.Threading.Tasks;
using Avalonia.Media;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Icons.Avalonia;
using Enigma.Icons.Phosphor;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Enigma.MarkdownEditor.Desktop.Views;
using Microsoft.Extensions.Logging;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// The one place that knows how the first-run welcome dialog is built, shown and applied.
/// </summary>
/// <remarks>
/// <para>
/// Shaped like <see cref="AboutDialogService"/>, with <see cref="SettingsDialogService"/>'s tail: the
/// answers are written after the dialog is gone, because a write can fail and the
/// <c>ContentDialog</c> host is a single control — a message raised while the welcome dialog is still
/// up would replace it rather than appear over it.
/// </para>
/// <para>
/// The dialog's result is deliberately not read. Every exit means the same thing here: write what was
/// chosen, so that the file whose absence raised this dialog now exists and the user is never asked
/// again.
/// </para>
/// </remarks>
public sealed class FirstRunDialogService : IFirstRunDialogService
{
    /// <summary>
    /// The heading glyph, resolved once — the XAML <c>{ei:IconGeometry HandWaving}</c> is unavailable
    /// here because the dialog is configured in code rather than in markup.
    /// </summary>
    private static readonly Geometry Icon = PhosphorIconSet.Instance.GetGlyph(PhosphorIcon.HandWaving).ToGeometry();

    private readonly Func<FirstRunViewModel> _createViewModel;
    private readonly IContentDialogService _dialogService;
    private readonly ILogger<FirstRunDialogService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FirstRunDialogService"/> class.
    /// </summary>
    /// <param name="createViewModel">Builds a fresh ViewModel over the current settings.</param>
    /// <param name="dialogService">Hosts the dialog, and reports a failed write once it has closed.</param>
    /// <param name="logger">Records that a first run happened at all.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public FirstRunDialogService(
        Func<FirstRunViewModel> createViewModel,
        IContentDialogService dialogService,
        ILogger<FirstRunDialogService> logger)
    {
        _createViewModel = createViewModel ?? throw new ArgumentNullException(nameof(createViewModel));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task ShowAsync()
    {
        FirstRunViewModel viewModel = _createViewModel();

        FirstRunView view = new()
        {
            DataContext = viewModel,
        };

        _logger.LogInformation("First run: no settings file, showing the welcome dialog.");

        await _dialogService.ShowAsync(dialog =>
        {
            dialog.Title = "Welcome";
            dialog.Content = view;
            dialog.IconData = Icon;

            dialog.PrimaryButtonText = "Get started";
            dialog.CloseButtonText = "Skip";
            dialog.DefaultButton = DefaultButton.Primary;
        });

        string? failure = await viewModel.CompleteAsync();
        if (failure is not null)
        {
            await _dialogService.ShowMessageAsync("Your choices were not all saved", failure);
        }
    }
}
