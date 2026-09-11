using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Session;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Core.Workspaces;
using Microsoft.Extensions.Logging;

namespace Enigma.MarkdownEditor.Desktop.Services;

/// <summary>
/// <see cref="ISessionService"/> over the session file, debounced through <see cref="IIdleScheduler"/>.
/// </summary>
/// <remarks>
/// <para>
/// The one place the session file is written, and it never throws at its caller. Every recorder is
/// called from a property setter, an event handler or a window callback — places where an exception is
/// either invisible or fatal — and a session that could not be saved is worth a log line, not a dialog
/// and certainly not a crash. That is the whole reason the write is wrapped.
/// </para>
/// <para>
/// The workspace stamp is refreshed on every record rather than by whoever happens to know it changed:
/// it is one property read, and a stamp that is only right when someone remembered to set it is worse
/// than no stamp at all.
/// </para>
/// <para>
/// The write continuations use <c>ConfigureAwait(false)</c>, unlike <c>WorkspaceService</c>'s, and the
/// difference is deliberate: nothing here resumes onto the user interface — the only thing after the
/// await is a log line — so capturing the caller's context buys nothing and costs a hop onto whatever
/// thread happened to schedule the write. On the way out that thread is the one blocked waiting for
/// this, which is the shape the shutdown sequence exists to avoid.
/// </para>
/// </remarks>
public sealed class SessionService : ISessionService
{
    /// <summary>
    /// How long the session has to stay untouched before it is written.
    /// </summary>
    /// <remarks>
    /// Long enough that a splitter drag or a run of tab closes is one write, short enough that a
    /// process killed rather than closed loses almost nothing.
    /// </remarks>
    public static readonly TimeSpan WriteDelay = TimeSpan.FromMilliseconds(500);

    private readonly IConfig<SessionState> _config;
    private readonly IWorkspaceService _workspaces;
    private readonly IIdleScheduler _idle;
    private readonly ILogger<SessionService> _logger;

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionService"/> class.
    /// </summary>
    /// <param name="config">The session file, already loaded (see <see cref="SessionLoader"/>).</param>
    /// <param name="workspaces">Read for the workspace the session is stamped with, and watched for it changing.</param>
    /// <param name="idle">The idle timer behind the debounce.</param>
    /// <param name="logger">Records a session that could not be saved, and the notes that could not be reopened.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public SessionService(
        IConfig<SessionState> config,
        IWorkspaceService workspaces,
        IIdleScheduler idle,
        ILogger<SessionService> logger)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _workspaces = workspaces ?? throw new ArgumentNullException(nameof(workspaces));
        _idle = idle ?? throw new ArgumentNullException(nameof(idle));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Opening another workspace changes nothing else the session tracks, so without this the stamp
        // would stay on the workspace the editor started in until something unrelated moved.
        _workspaces.ActiveWorkspaceChanged += OnActiveWorkspaceChanged;
    }

    /// <inheritdoc />
    public SessionState State => _config.Value;

    /// <inheritdoc />
    public SessionDocuments DocumentsToRestore()
    {
        SessionDocuments documents = SessionRestore.Resolve(State, _workspaces.ActiveWorkspace?.Id);

        if (documents.IsForAnotherWorkspace)
        {
            _logger.LogInformation(
                "The saved session belongs to workspace {SessionWorkspaceId}; the open one is {ActiveWorkspaceId}, so its tabs were not restored.",
                State.ActiveWorkspaceId,
                _workspaces.ActiveWorkspace?.Id);

            return documents;
        }

        foreach (string missing in documents.Missing)
        {
            // Information, not warning: a note deleted or moved since the editor last closed is the
            // ordinary life of a folder, and the user already knows they deleted it.
            _logger.LogInformation("{FilePath} was in the saved session but is no longer there.", missing);
        }

        return documents;
    }

    /// <inheritdoc />
    public void RecordShell(ViewMode viewMode, SidebarTab sidebarTab, bool isSidebarVisible)
    {
        SessionState state = State;

        state.ViewMode = viewMode;
        state.SidebarTab = sidebarTab;
        state.IsSidebarVisible = isSidebarVisible;

        Schedule();
    }

    /// <inheritdoc />
    public void RecordDocuments(IReadOnlyList<string> paths, int activeIndex)
    {
        ArgumentNullException.ThrowIfNull(paths);

        SessionState state = State;

        state.OpenDocuments = [.. paths];

        // Clamped rather than trusted, because this is the one pair in the file that can contradict
        // itself — and a file that contradicts itself is one the next start quarantines.
        state.ActiveDocumentIndex = (activeIndex >= 0) && (activeIndex < state.OpenDocuments.Count)
            ? activeIndex
            : -1;

        Schedule();
    }

    /// <inheritdoc />
    public void RecordWindow(WindowPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        State.Window = placement;
        Schedule();
    }

    /// <inheritdoc />
    public void RecordPanes(PaneLayout panes)
    {
        ArgumentNullException.ThrowIfNull(panes);

        State.Panes = panes;
        Schedule();
    }

    /// <inheritdoc />
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
        {
            return;
        }

        _idle.Cancel(this);
        Stamp();

        await WriteAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _workspaces.ActiveWorkspaceChanged -= OnActiveWorkspaceChanged;
        _idle.Cancel(this);
    }

    private void OnActiveWorkspaceChanged(object? sender, ActiveWorkspaceChangedEventArgs e) => Schedule();

    private void Schedule()
    {
        if (_disposed)
        {
            return;
        }

        Stamp();

        // Keyed on this service, so every kind of change shares one timer: the file is written whole,
        // and two pending writes of it would only ever be the same write twice.
        _idle.Schedule(this, WriteDelay, () => WriteAsync(CancellationToken.None));
    }

    private void Stamp() => State.ActiveWorkspaceId = _workspaces.ActiveWorkspace?.Id;

    private async Task WriteAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _config.WriteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Shutting down mid-write. The session is the least of what is being abandoned.
        }
        catch (Exception failure)
            when (failure is EnigmaConfigurationException or IOException or UnauthorizedAccessException)
        {
            // Deliberately not surfaced: there is nothing the user can do about it, and an info bar
            // saying the editor could not remember where it was would interrupt them to report a lost
            // tab order.
            _logger.LogWarning(failure, "The session could not be saved.");
        }
    }
}
