using System.Threading.Tasks;
using Avalonia.Headless.XUnit;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Enigma.MarkdownEditor.Desktop.Views;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Services;

/// <summary>
/// How the About dialog is configured on the shared host: the chrome every Enigma app's About dialog
/// has, around this app's view.
/// </summary>
public sealed class AboutDialogServiceTests
{
    [AvaloniaFact]
    public async Task TheDialog_HasNoTitleAndNoIcon()
    {
        ContentDialog dialog = await ShowAsync();

        // The view already shows the application's icon and name; a title column beside it would be
        // a different layout from the other Enigma apps'.
        Assert.Null(dialog.Title);
        Assert.Null(dialog.IconData);
    }

    [AvaloniaFact]
    public async Task TheDialog_OnlyCloses()
    {
        ContentDialog dialog = await ShowAsync();

        Assert.Equal("Close", dialog.CloseButtonText);
        Assert.Equal(DefaultButton.Close, dialog.DefaultButton);
        Assert.Null(dialog.PrimaryButtonText);
        Assert.Null(dialog.SecondaryButtonText);
    }

    [AvaloniaFact]
    public async Task TheDialog_ShowsTheAboutViewOverAFreshViewModel()
    {
        AboutViewModel viewModel = new("1.2.3", "abcdef1", "Copyright © 2026 Josué Clément");
        FakeContentDialogService dialogs = new() { Result = DialogResult.Close };
        AboutDialogService service = new(() => viewModel, dialogs);

        await service.ShowAsync();

        AboutView view = Assert.IsType<AboutView>(Assert.Single(dialogs.Shown).Content);
        Assert.Same(viewModel, view.DataContext);
    }

    private static async Task<ContentDialog> ShowAsync()
    {
        FakeContentDialogService dialogs = new() { Result = DialogResult.Close };
        AboutDialogService service = new(() => new AboutViewModel("1.2.3", "abcdef1", "c"), dialogs);

        await service.ShowAsync();

        return Assert.Single(dialogs.Shown);
    }
}
