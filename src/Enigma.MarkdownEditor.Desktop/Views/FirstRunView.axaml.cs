using Avalonia.Controls;

namespace Enigma.MarkdownEditor.Desktop.Views;

/// <summary>
/// The content of the welcome dialog: the three settings a first run asks about.
/// </summary>
/// <remarks>
/// Deliberately logic-free, like <see cref="AboutView"/> and <see cref="WorkspaceManagerView"/>.
/// Everything the rows do — including the folder picker — is
/// <see cref="ViewModels.FirstRunViewModel"/>'s.
/// </remarks>
public partial class FirstRunView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FirstRunView"/> class.
    /// </summary>
    public FirstRunView() => InitializeComponent();
}
