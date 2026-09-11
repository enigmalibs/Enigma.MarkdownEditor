using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Core.Session;
using Enigma.MarkdownEditor.Core.Settings;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// Remembers where the editor was left, and puts it back.
/// </summary>
/// <remarks>
/// <para>
/// Written to constantly and read once. Every <c>Record…</c> call updates the state in memory and
/// restarts one idle timer, so a splitter dragged across the window or a tab closed in a run of five
/// costs one write once the hand stops moving rather than one per event.
/// </para>
/// <para>
/// The recorders are deliberately dumb: they take values, not sources. What is worth remembering is a
/// decision the shell and the window make — they are the ones that know a pane was collapsed rather
/// than merely resized — and this only has to be told.
/// </para>
/// </remarks>
public interface ISessionService : IDisposable
{
    /// <summary>
    /// Gets the session as it stands: what was read at start-up, plus everything recorded since.
    /// </summary>
    SessionState State { get; }

    /// <summary>
    /// Works out which of the session's documents can still be reopened.
    /// </summary>
    /// <returns>
    /// The notes to open and which to activate, with anything that has gone missing — or that belongs
    /// to another workspace — already dropped and logged.
    /// </returns>
    SessionDocuments DocumentsToRestore();

    /// <summary>
    /// Records the shell's own state: the panes and the sidebar tab.
    /// </summary>
    /// <param name="viewMode">Which of the editor and the preview the window is showing.</param>
    /// <param name="sidebarTab">Which of the sidebar's two tabs is showing.</param>
    /// <param name="isSidebarVisible">Whether the sidebar is shown at all.</param>
    void RecordShell(ViewMode viewMode, SidebarTab sidebarTab, bool isSidebarVisible);

    /// <summary>
    /// Records the open tabs.
    /// </summary>
    /// <param name="paths">The open documents' absolute paths, in tab order.</param>
    /// <param name="activeIndex">
    /// Which of them is on screen, or <c>-1</c> when none is. An index outside
    /// <paramref name="paths"/> is stored as <c>-1</c> rather than kept, so the file can never
    /// contradict itself.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="paths"/> is <see langword="null"/>.</exception>
    void RecordDocuments(IReadOnlyList<string> paths, int activeIndex);

    /// <summary>
    /// Records where the window is.
    /// </summary>
    /// <param name="placement">The window's restored bounds, in device pixels, and its state.</param>
    /// <exception cref="ArgumentNullException"><paramref name="placement"/> is <see langword="null"/>.</exception>
    void RecordWindow(WindowPlacement placement);

    /// <summary>
    /// Records how the panes are divided.
    /// </summary>
    /// <param name="panes">The sidebar's width and the editor/preview ratio.</param>
    /// <exception cref="ArgumentNullException"><paramref name="panes"/> is <see langword="null"/>.</exception>
    void RecordPanes(PaneLayout panes);

    /// <summary>
    /// Writes the session now, rather than when the timer would have.
    /// </summary>
    /// <param name="cancellationToken">Abandons the write.</param>
    /// <returns>A task that completes once the file holds the current state, or the attempt failed.</returns>
    /// <remarks>
    /// What the application calls on the way out: the last thing recorded is usually the window's own
    /// closing, which is half a second short of the debounce it would otherwise wait for.
    /// </remarks>
    Task FlushAsync(CancellationToken cancellationToken = default);
}
