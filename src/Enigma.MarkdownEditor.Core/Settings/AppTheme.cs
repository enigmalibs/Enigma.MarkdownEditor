namespace Enigma.MarkdownEditor.Core.Settings;

/// <summary>
/// The theme variant the user asked for.
/// </summary>
/// <remarks>
/// Persisted by name, so the numeric values may be reordered but the member names may not.
/// </remarks>
public enum AppTheme
{
    /// <summary>
    /// Follow the operating system's light/dark preference, and keep following it while the app runs.
    /// </summary>
    System = 0,

    /// <summary>
    /// Always dark, whatever the operating system says.
    /// </summary>
    Dark = 1,

    /// <summary>
    /// Always light, whatever the operating system says.
    /// </summary>
    Light = 2,
}
