using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Core.Session;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// The session without a file behind it: a state a test can seed, and a list of everything that was
/// recorded into it.
/// </summary>
/// <remarks>
/// <see cref="DocumentsToRestore"/> goes through the real <see cref="SessionRestore"/> rather than
/// answering with a canned list — that policy is Core's and is tested there, and running it here means
/// a shell test that lists a note it never created gets the same answer the application would.
/// </remarks>
internal sealed class FakeSessionService : ISessionService
{
    public SessionState State { get; } = new();

    /// <summary>
    /// The workspace the shell is to be told opened, which the session's own stamp is checked against.
    /// </summary>
    public Guid? WorkspaceId { get; set; }

    public List<(ViewMode ViewMode, SidebarTab SidebarTab, bool IsSidebarVisible)> Shell { get; } = [];

    public List<(IReadOnlyList<string> Paths, int ActiveIndex)> Documents { get; } = [];

    public List<WindowPlacement> Windows { get; } = [];

    public List<PaneLayout> Panes { get; } = [];

    public int FlushCount { get; private set; }

    public bool IsDisposed { get; private set; }

    public SessionDocuments DocumentsToRestore() => SessionRestore.Resolve(State, WorkspaceId);

    public void RecordShell(ViewMode viewMode, SidebarTab sidebarTab, bool isSidebarVisible)
        => Shell.Add((viewMode, sidebarTab, isSidebarVisible));

    public void RecordDocuments(IReadOnlyList<string> paths, int activeIndex)
    {
        ArgumentNullException.ThrowIfNull(paths);

        Documents.Add((paths, activeIndex));
    }

    public void RecordWindow(WindowPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        Windows.Add(placement);
    }

    public void RecordPanes(PaneLayout panes)
    {
        ArgumentNullException.ThrowIfNull(panes);

        Panes.Add(panes);
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        FlushCount++;

        return Task.CompletedTask;
    }

    public void Dispose() => IsDisposed = true;
}
