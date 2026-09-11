using Avalonia.Controls;

namespace Enigma.MarkdownEditor.Desktop.Views;

/// <summary>
/// The sidebar's Search tab: the query box, the status line and the results.
/// </summary>
/// <remarks>
/// No code behind it, and that is the point. Enter and Escape are key bindings on the box, each hit is
/// a <see cref="Button"/> the keyboard already knows how to press, and everything else — the debounce,
/// the grouping, the highlighted match — is decided in <see cref="ViewModels.SearchViewModel"/> and
/// bound. There is nothing here a binding could not express.
/// </remarks>
public partial class SearchPaneView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SearchPaneView"/> class.
    /// </summary>
    public SearchPaneView() => InitializeComponent();
}
