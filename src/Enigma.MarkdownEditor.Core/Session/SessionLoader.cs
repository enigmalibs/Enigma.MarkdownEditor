using System;
using Enigma.MarkdownEditor.Core.Configuration;
using Enigma.MarkdownEditor.Core.Settings;

namespace Enigma.MarkdownEditor.Core.Session;

/// <summary>
/// Reads the session file at start-up, and guarantees a usable session even when it cannot.
/// </summary>
/// <remarks>
/// The recovery is <see cref="ConfigRecovery"/>'s, shared with the settings and workspaces files: an
/// unreadable file is moved aside and the editor starts the way a fresh install does. Of the three
/// files this is the one where that costs least — a session is where you were, not what you chose —
/// which is exactly why it is kept apart from the settings.
/// </remarks>
public static class SessionLoader
{
    /// <summary>
    /// Loads the session file, recovering from a file that cannot be read.
    /// </summary>
    /// <param name="sessionFilePath">Absolute path of the file (see <see cref="AppPaths.SessionFile"/>).</param>
    /// <returns>
    /// A result whose <see cref="ConfigLoadResult{T}.Config"/> is always readable — holding
    /// <see cref="SessionState"/>'s own defaults when the file could not be used.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="sessionFilePath"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="sessionFilePath"/> is <see langword="null"/>.</exception>
    public static ConfigLoadResult<SessionState> Load(string sessionFilePath)
        => ConfigRecovery.Load(
            sessionFilePath,
            new SessionStateValidator(),
            AppPaths.QuarantinedSessionFileName);
}
