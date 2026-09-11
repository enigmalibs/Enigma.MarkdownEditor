using Avalonia.Controls;

namespace Enigma.MarkdownEditor.Desktop.Views;

/// <summary>
/// The settings dialog's content: grouped <c>SettingsCard</c> rows over the persisted application
/// settings.
/// </summary>
/// <remarks>
/// Deliberately logic-free. Edits go straight to the live settings object through the ViewModel, the
/// write happens in <see cref="Services.SettingsDialogService"/> once the dialog has closed, and the
/// Close button belongs to the dialog rather than to this view — so there is nothing here at all.
/// </remarks>
public partial class SettingsView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsView"/> class.
    /// </summary>
    public SettingsView() => InitializeComponent();
}
