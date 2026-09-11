using System;
using Enigma.MarkdownEditor.Core.Vim;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Whether vim emulation is on, and what mode the editor is in right now.
/// </summary>
/// <remarks>
/// <para>
/// One small piece of state with three readers and one writer. The editor pane writes it — it is the
/// only thing that owns a <see cref="VimEngine"/> — and the status bar, the markdown assists and the
/// pane itself read it. Routing it through a service rather than through the view tree is what keeps
/// the mode indicator in the shell's status bar from having to know that an editor control exists.
/// </para>
/// <para>
/// Both halves are on one interface deliberately. Splitting the reporting off would mean two
/// registrations of the same singleton for the sake of a distinction nothing in this application can
/// act on.
/// </para>
/// </remarks>
public interface IVimSession
{
    /// <summary>
    /// Raised whenever anything below has changed.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    /// Gets a value indicating whether vim emulation is switched on.
    /// </summary>
    /// <remarks>
    /// Read from <c>AppSettings.VimEnabled</c> at start-up and re-read when the settings window
    /// closes; the pane attaches and detaches the engine to follow it.
    /// </remarks>
    bool IsEnabled { get; }

    /// <summary>
    /// Gets the mode the editor is in. Always <see cref="VimMode.Normal"/> while vim is off.
    /// </summary>
    VimMode Mode { get; }

    /// <summary>
    /// Gets the message the engine wants beside the mode indicator, if any.
    /// </summary>
    string? StatusMessage { get; }

    /// <summary>
    /// Gets the ex/search line as it is being typed, <c>:</c> or <c>/</c> included, or
    /// <see langword="null"/> when none is open.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="StatusMessage"/> although both land in the same corner of the
    /// status bar: one is something the editor is saying and the other is something the user is
    /// writing, and the strip is styled to tell them apart.
    /// </remarks>
    string? CommandLine { get; }

    /// <summary>
    /// Re-reads <c>AppSettings.VimEnabled</c>, after the settings window has been closed.
    /// </summary>
    void OnSettingsChanged();

    /// <summary>
    /// Records the mode the editor has switched to.
    /// </summary>
    /// <param name="mode">The new mode.</param>
    void ReportMode(VimMode mode);

    /// <summary>
    /// Records a message to show beside the mode, or clears the one that is there.
    /// </summary>
    /// <param name="message">What to say, or <see langword="null"/> to clear.</param>
    void ReportStatus(string? message);

    /// <summary>
    /// Records the command line as it stands, or takes it away.
    /// </summary>
    /// <param name="line">The whole line including its first character, or <see langword="null"/>.</param>
    void ReportCommandLine(string? line);
}
