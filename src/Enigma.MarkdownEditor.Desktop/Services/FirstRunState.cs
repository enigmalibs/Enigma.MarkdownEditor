using System;
using System.IO;
using Enigma.MarkdownEditor.Core.Settings;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Whether this start is the very first one, parked in a singleton for the shell to read once it has a
/// window.
/// </summary>
/// <remarks>
/// <para>
/// The signal is the absence of the settings file, and it can only be read at one moment: before
/// <see cref="SettingsLoader.Load(string)"/> runs. An unreadable file is recovered by moving it aside,
/// which leaves the path absent — so the same question asked after the load would read a corruption
/// as a first run and re-onboard someone who has been using the editor for months.
/// </para>
/// <para>
/// A type rather than a bare <see cref="bool"/> because it is a container registration: an injected
/// <c>bool</c> says nothing about which flag it is. Same reason <see cref="StartupNotice"/> is a type,
/// and it is read at the same moment, by the same ViewModel.
/// </para>
/// </remarks>
/// <param name="IsFirstRun">
/// <see langword="true"/> when the settings file was not there, which is what makes this a first run.
/// </param>
public sealed record FirstRunState(bool IsFirstRun)
{
    /// <summary>
    /// Gets the "this is not a first run" state — the normal case, and the container's fallback
    /// registration.
    /// </summary>
    public static FirstRunState Returning { get; } = new FirstRunState(false);

    /// <summary>
    /// Gets the "nothing has been configured yet" state.
    /// </summary>
    public static FirstRunState First { get; } = new FirstRunState(true);

    /// <summary>
    /// Decides whether this start is a first run, from the settings file alone.
    /// </summary>
    /// <param name="settingsFilePath">
    /// Absolute path of the settings file (see <see cref="AppPaths.SettingsFile"/>). Not read, only
    /// looked for.
    /// </param>
    /// <returns><see cref="First"/> when the file is not there; otherwise <see cref="Returning"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="settingsFilePath"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="settingsFilePath"/> is <see langword="null"/>.</exception>
    public static FirstRunState Detect(string settingsFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFilePath);

        return File.Exists(settingsFilePath) ? Returning : First;
    }
}
