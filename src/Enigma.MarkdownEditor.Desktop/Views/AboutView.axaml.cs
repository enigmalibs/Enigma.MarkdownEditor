using Avalonia.Controls;

namespace Enigma.MarkdownEditor.Desktop.Views;

/// <summary>
/// The content of the About dialog: the application's icon, what it is, which build is running, and
/// what it is built out of.
/// </summary>
/// <remarks>
/// Deliberately logic-free, like <see cref="WorkspaceManagerView"/>. There is nothing to do here —
/// everything shown is fixed for the life of the dialog and comes from
/// <see cref="ViewModels.AboutViewModel"/>.
/// </remarks>
public partial class AboutView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AboutView"/> class.
    /// </summary>
    public AboutView() => InitializeComponent();
}
