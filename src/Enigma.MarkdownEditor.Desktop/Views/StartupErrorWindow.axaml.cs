using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Enigma.MarkdownEditor.Core;

namespace Enigma.MarkdownEditor.Desktop.Views;

/// <summary>
/// The window a failed start-up opens instead of the main window: the error dialog, on a host of its
/// own, and the application ends when it is dismissed.
/// </summary>
/// <remarks>
/// <para>
/// A failure before the main window exists has no dialog host to be shown on, and without this the
/// user would be left with nothing on screen at all — the one failure nobody can report.
/// </para>
/// <para>
/// The window closes whichever way the dialog is dismissed (Close, Escape, a click on the scrim). It
/// is the lifetime's only window, so closing it ends the process under the default
/// <see cref="ShutdownMode.OnLastWindowClose"/>.
/// </para>
/// </remarks>
public partial class StartupErrorWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StartupErrorWindow"/> class with no message. For
    /// the XAML loader and the previewer, which need a parameterless constructor.
    /// </summary>
    public StartupErrorWindow()
        : this(string.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupErrorWindow"/> class.
    /// </summary>
    /// <param name="message">What happened, and where the details are.</param>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
    public StartupErrorWindow(string message)
    {
        ArgumentNullException.ThrowIfNull(message);

        Message = message;
        Title = AppInfo.DisplayName;
        InitializeComponent();
    }

    /// <summary>
    /// Gets what the dialog says.
    /// </summary>
    public string Message { get; }

    /// <inheritdoc />
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        _ = ShowErrorAsync();
    }

    private async Task ShowErrorAsync()
    {
        try
        {
            ErrorDialog.Configure(HostDialog, Message);
            await HostDialog.ShowAsync().ConfigureAwait(true);
        }
        finally
        {
            // Even if the dialog could not be shown: a window with nothing on it would keep a process
            // that failed to start running for ever.
            Close();
        }
    }
}
