using System;
using Enigma.MarkdownEditor.Core.Configuration;

namespace Enigma.MarkdownEditor.Core.Settings;

/// <summary>
/// Reads the settings file at start-up, and guarantees a usable configuration even when it cannot.
/// </summary>
/// <remarks>
/// The recovery itself — quarantine the unreadable file, rebuild over the now-absent path, fall back
/// to a scratch path when even the move fails — lives in <see cref="ConfigRecovery"/>, which the
/// workspaces file uses as well. This class is only the settings file's half of it: which path, which
/// validator, which quarantine name.
/// </remarks>
public static class SettingsLoader
{
    /// <summary>
    /// Loads the settings file, recovering from a file that cannot be read.
    /// </summary>
    /// <param name="settingsFilePath">Absolute path of the settings file (see <see cref="AppPaths.SettingsFile"/>).</param>
    /// <returns>
    /// A result whose <see cref="ConfigLoadResult{T}.Config"/> is always readable — holding the
    /// <see cref="AppSettings"/> defaults when the file could not be used.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="settingsFilePath"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="settingsFilePath"/> is <see langword="null"/>.</exception>
    public static ConfigLoadResult<AppSettings> Load(string settingsFilePath)
        => ConfigRecovery.Load(
            settingsFilePath,
            new AppSettingsValidator(),
            AppPaths.QuarantinedSettingsFileName);
}
