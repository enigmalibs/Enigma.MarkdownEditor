using Avalonia.Controls;

namespace Enigma.MarkdownEditor.Desktop.Views;

/// <summary>
/// The window shown while the application composes itself: the icon, what is starting, and which
/// version of it.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately logic-free, like <see cref="AboutView"/>, and deliberately without a ViewModel: it
/// shows two constants and lives for about a second. <see cref="App"/> owns everything about it that
/// is not markup — when it appears, how long it is held, and the hand-over to the main window.
/// </para>
/// <para>
/// It is also the one window in the application that is not resolved from the container. It has to be
/// on screen <em>before</em> the host is built, which is the very thing it covers.
/// </para>
/// </remarks>
public partial class SplashWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SplashWindow"/> class.
    /// </summary>
    public SplashWindow() => InitializeComponent();
}
