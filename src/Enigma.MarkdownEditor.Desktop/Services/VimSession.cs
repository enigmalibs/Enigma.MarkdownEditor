using System;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Core.Vim;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// The application's one vim session: the setting, the mode, and the message beside it.
/// </summary>
/// <remarks>
/// It holds no engine and does no editing. The engine belongs to the editor control, which comes and
/// goes with the pane; what has to outlive that — and be visible to the status bar at the other end
/// of the window — is these three values and the event that says one of them moved.
/// </remarks>
public sealed class VimSession : IVimSession
{
    private readonly IConfig<AppSettings> _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="VimSession"/> class from the current settings.
    /// </summary>
    /// <param name="settings">Read for <c>VimEnabled</c>, now and whenever the settings window closes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
    public VimSession(IConfig<AppSettings> settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        IsEnabled = _settings.Value.VimEnabled;
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public bool IsEnabled { get; private set; }

    /// <inheritdoc />
    public VimMode Mode { get; private set; } = VimMode.Normal;

    /// <inheritdoc />
    public string? StatusMessage { get; private set; }

    /// <inheritdoc />
    public string? CommandLine { get; private set; }

    /// <inheritdoc />
    public void OnSettingsChanged()
    {
        if (IsEnabled == _settings.Value.VimEnabled)
        {
            return;
        }

        IsEnabled = _settings.Value.VimEnabled;

        // Switched off, the mode is not something the user can see or change any more, so it is put
        // back rather than left saying INSERT under a status bar that no longer shows it. A half-typed
        // command line goes with it, for the same reason and one more: there is nothing left to type
        // into it.
        Mode = VimMode.Normal;
        StatusMessage = null;
        CommandLine = null;

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void ReportMode(VimMode mode)
    {
        if (Mode == mode)
        {
            return;
        }

        Mode = mode;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void ReportStatus(string? message)
    {
        if (StatusMessage == message)
        {
            return;
        }

        StatusMessage = message;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void ReportCommandLine(string? line)
    {
        if (CommandLine == line)
        {
            return;
        }

        CommandLine = line;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
