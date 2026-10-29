using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Enigma.Avalonia.Desktop.Controls.ContentDialog;
using Enigma.MarkdownEditor.Core;
using Enigma.MarkdownEditor.Desktop.Views;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Views;

/// <summary>
/// The window a failed start-up opens: the error dialog as soon as it is on screen, and gone once the
/// dialog is.
/// </summary>
public sealed class StartupErrorWindowTests
{
    [AvaloniaFact]
    public void OnceOpen_TheWindowShowsTheErrorDialog()
    {
        StartupErrorWindow window = new("InvalidOperationException: boom");

        window.Show();
        Dispatcher.UIThread.RunJobs();

        ContentDialog dialog = Host(window);
        Assert.True(dialog.IsOpen);
        Assert.Equal(ErrorDialog.Title, dialog.Title);
        Assert.Equal("Close", dialog.CloseButtonText);
        Assert.Equal("InvalidOperationException: boom", Assert.IsType<SelectableTextBlock>(dialog.Content).Text);
        Assert.Equal(AppInfo.DisplayName, window.Title);
    }

    [AvaloniaFact]
    public async Task DismissingTheDialog_ClosesTheWindow()
    {
        StartupErrorWindow window = new("boom");
        bool closed = false;
        window.Closed += (_, _) => closed = true;

        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Escape and a click on the scrim resolve the dialog the same way; any dismissal ends it.
        await Host(window).HideAsync();
        Dispatcher.UIThread.RunJobs();

        Assert.True(closed);
    }

    // By name, as the other view tests do: what is asserted is the markup.
    private static ContentDialog Host(StartupErrorWindow window)
        => window.GetLogicalDescendants().OfType<ContentDialog>().Single(dialog => dialog.Name == "HostDialog");
}
