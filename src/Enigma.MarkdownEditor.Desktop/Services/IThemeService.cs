using Enigma.MarkdownEditor.Core.Settings;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Applies the persisted theme choice to the running application.
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Switches the application to <paramref name="theme"/>, live.
    /// </summary>
    /// <param name="theme">The variant the user asked for.</param>
    /// <remarks>
    /// Every brush in the app's markup is a <c>DynamicResource</c>, so the window repaints without
    /// being rebuilt and without a restart.
    /// </remarks>
    void Apply(AppTheme theme);
}
