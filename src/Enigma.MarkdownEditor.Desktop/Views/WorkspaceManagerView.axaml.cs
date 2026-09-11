using Avalonia.Controls;

namespace Enigma.MarkdownEditor.Desktop.Views;

/// <summary>
/// The content of the manage-workspaces dialog: the workspace list, and the selected workspace's name
/// and folders.
/// </summary>
/// <remarks>
/// Deliberately logic-free. Everything it does — including the inline removal confirmation, which
/// exists because this view is itself hosted by the application's one <c>ContentDialog</c> — lives in
/// <see cref="ViewModels.WorkspaceManagerViewModel"/>.
/// </remarks>
public partial class WorkspaceManagerView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceManagerView"/> class.
    /// </summary>
    public WorkspaceManagerView() => InitializeComponent();
}
