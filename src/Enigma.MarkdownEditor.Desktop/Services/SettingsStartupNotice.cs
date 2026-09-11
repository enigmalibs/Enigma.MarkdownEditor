using System;
using Enigma.MarkdownEditor.Core.Configuration;
using Enigma.MarkdownEditor.Core.Settings;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Turns a settings load that went wrong into something worth showing the user.
/// </summary>
public static class SettingsStartupNotice
{
    /// <summary>
    /// Builds the notice for a settings load.
    /// </summary>
    /// <param name="result">What <see cref="SettingsLoader.Load(string)"/> returned.</param>
    /// <returns>
    /// <see cref="StartupNotice.None"/> when the file loaded normally; otherwise a notice saying what
    /// was wrong, where the old file went, and whether this session's changes will be kept.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    public static StartupNotice For(ConfigLoadResult<AppSettings> result)
        => ConfigStartupNotice.For(result, "Settings could not be read", "with default settings");
}
