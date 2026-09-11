using System;
using System.Threading.Tasks;
using Avalonia.Media;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Icons.Avalonia;
using Enigma.Icons.Phosphor;
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
    /// <summary>
    /// The heading glyph, resolved once. The XAML equivalent is <c>{ei:IconGeometry Info}</c>, which
    /// is unavailable here because the dialog is configured in code rather than in markup.
    /// </summary>
    private static readonly Geometry Icon = PhosphorIconSet.Instance.GetGlyph(PhosphorIcon.Info).ToGeometry();

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

        await _dialogService.ShowAsync(dialog =>
        {
            dialog.Title = "About";
            dialog.Content = view;
            dialog.IconData = Icon;

            // One button, and it only dismisses: the dialog asks nothing.
            dialog.CloseButtonText = "Close";
        });
    }
}
