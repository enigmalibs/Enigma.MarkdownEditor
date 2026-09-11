using System;
using Enigma.MarkdownEditor.Core.Configuration;
using Enigma.MarkdownEditor.Core.Session;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Turns a session load that went wrong into something worth showing the user.
/// </summary>
public static class SessionStartupNotice
{
    /// <summary>
    /// Builds the notice for a session load.
    /// </summary>
    /// <param name="result">What <see cref="SessionLoader.Load(string)"/> returned.</param>
    /// <returns>
    /// <see cref="StartupNotice.None"/> when the file loaded normally; otherwise a notice saying the
    /// editor came up as it would on a first run. Worth saying rather than swallowing: the user left
    /// six tabs open and is looking at none, and without this they would think the editor lost them.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="result"/> is <see langword="null"/>.</exception>
    public static StartupNotice For(ConfigLoadResult<SessionState> result)
        => ConfigStartupNotice.For(result, "The last session could not be restored", "without reopening anything");
}
