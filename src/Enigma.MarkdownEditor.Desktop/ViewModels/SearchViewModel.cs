using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.MarkdownEditor.Core.Search;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// The sidebar's Search tab: the query, the results grouped by file, and what opening one does.
/// </summary>
/// <remarks>
/// <para>
/// Typing does not search. Every keystroke restarts an idle timer and the scan runs once the typing
/// stops, because a scan per character would spend the whole of a five-letter word reading the
/// workspace four times over for answers nobody would see. Pressing Enter skips the wait; changing the
/// case toggle runs immediately, since that is a decision rather than a keystroke.
/// </para>
/// <para>
/// A new scan cancels the one before it, and every result is stamped with the run that produced it: a
/// hit posted from a scan that has been superseded is dropped rather than appended to the list of its
/// successor. That is the whole of the race, and it is why the check is an integer compared on the UI
/// thread rather than a token read from a background one.
/// </para>
/// <para>
/// The hits themselves arrive from worker threads and are marshalled one at a time through
/// <see cref="IUiDispatcher"/>: what they are appended to is an <see cref="ObservableCollection{T}"/>
/// bound to a live list, which belongs to the UI thread like everything else on screen.
/// </para>
/// </remarks>
public sealed class SearchViewModel : ObservableObject, IDisposable
{
    /// <summary>
    /// How long the query box has to stay untouched before the scan runs.
    /// </summary>
    /// <remarks>
    /// Longer than the preview's refresh, and deliberately: this one reads every file in the workspace
    /// rather than re-rendering one that is already in memory.
    /// </remarks>
    public static readonly TimeSpan TypingDelay = TimeSpan.FromMilliseconds(300);

    private readonly IWorkspaceService _workspaces;
    private readonly DocumentManagerViewModel _documents;
    private readonly IIdleScheduler _idle;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<SearchViewModel> _logger;

    // The group a file's hits go into, so a second hit in the same file finds the group the first one
    // made rather than opening another one.
    private readonly Dictionary<string, SearchResultGroup> _groups = new(PathComparer);

    private CancellationTokenSource? _running;
    private int _runId;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchViewModel"/> class.
    /// </summary>
    /// <param name="workspaces">Read for the folders to search, and watched for the workspace changing.</param>
    /// <param name="documents">The editor pane, which opens a hit and puts the caret on it.</param>
    /// <param name="idle">The idle timer behind the debounce.</param>
    /// <param name="dispatcher">Moves each hit from the scan's worker thread onto the UI thread.</param>
    /// <param name="logger">Records a scan that failed outright.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public SearchViewModel(
        IWorkspaceService workspaces,
        DocumentManagerViewModel documents,
        IIdleScheduler idle,
        IUiDispatcher dispatcher,
        ILogger<SearchViewModel> logger)
    {
        _workspaces = workspaces ?? throw new ArgumentNullException(nameof(workspaces));
        _documents = documents ?? throw new ArgumentNullException(nameof(documents));
        _idle = idle ?? throw new ArgumentNullException(nameof(idle));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        SearchCommand = new RelayCommand(RunNow);
        ClearCommand = new RelayCommand(Clear);
        OpenHitCommand = new AsyncRelayCommand<SearchHitItem?>(OpenAsync);

        // Results name files in one workspace; in another they name nothing. Cleared rather than
        // re-run, because switching workspace is not asking the question again.
        _workspaces.ActiveWorkspaceChanged += OnActiveWorkspaceChanged;
    }

    /// <summary>
    /// Gets the results, one group per file, in the order the files were first hit.
    /// </summary>
    public ObservableCollection<SearchResultGroup> Groups { get; } = [];

    /// <summary>
    /// Gets or sets the text being looked for. Assigning it restarts the debounce.
    /// </summary>
    public string Query
    {
        get;
        set
        {
            if (!SetProperty(ref field, value ?? string.Empty))
            {
                return;
            }

            OnPropertyChanged(nameof(HasQuery));
            Schedule();
        }
    } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the match is case-sensitive. Assigning it re-runs the
    /// search at once — it is a decision about the answer, not a step towards asking the question.
    /// </summary>
    public bool MatchCase
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                RunNow();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether there is anything to search for.
    /// </summary>
    public bool HasQuery => Query.Length > 0;

    /// <summary>
    /// Gets a value indicating whether the last search found anything.
    /// </summary>
    public bool HasResults => Groups.Count > 0;

    /// <summary>
    /// Gets a value indicating whether a scan is running, which the view shows as a busy line.
    /// </summary>
    public bool IsSearching { get; private set => SetProperty(ref field, value); }

    /// <summary>
    /// Gets the line under the query box: how much was found, and anything the search had to leave
    /// out. <see langword="null"/> when nothing has been asked.
    /// </summary>
    public string? StatusText
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasStatus));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether there is a status line to show.
    /// </summary>
    public bool HasStatus => StatusText is not null;

    /// <summary>
    /// Gets the command behind Enter in the query box: searches now rather than after the debounce.
    /// </summary>
    public IRelayCommand SearchCommand { get; }

    /// <summary>
    /// Gets the command that empties the query and the results.
    /// </summary>
    public IRelayCommand ClearCommand { get; }

    /// <summary>
    /// Gets the command a result row runs: opens the file, puts the caret on the match and selects it.
    /// </summary>
    public IAsyncRelayCommand<SearchHitItem?> OpenHitCommand { get; }

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
        CancelRunning();
    }

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    // Which folder to show beside a file's name: the one it sits in, relative to whichever root it was
    // found under. The deepest matching root wins, because roots may be nested and the nearest one is
    // the one whose path is short enough to read.
    private static string LocationOf(IReadOnlyList<string> roots, string filePath)
    {
        string? directory = Path.GetDirectoryName(filePath);

        if (string.IsNullOrEmpty(directory))
        {
            return string.Empty;
        }

        string? best = null;

        foreach (string root in roots)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            string full = Path.GetFullPath(root);

            if (IsUnder(directory, full) && ((best is null) || (full.Length > best.Length)))
            {
                best = full;
            }
        }

        if (best is null)
        {
            // Not under any root any more — the workspace changed under the results. The whole folder
            // is more useful than nothing.
            return directory;
        }

        string relative = Path.GetRelativePath(best, directory);

        return relative == "." ? string.Empty : relative;
    }

    private static bool IsUnder(string directory, string root)
    {
        if (string.Equals(directory, root, PathComparison))
        {
            return true;
        }

        return directory.StartsWith(root, PathComparison) &&
               (directory.Length > root.Length) &&
               ((directory[root.Length] == Path.DirectorySeparatorChar) ||
                (directory[root.Length] == Path.AltDirectorySeparatorChar));
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static string Describe(WorkspaceSearchSummary summary)
    {
        if (summary.HitCount == 0)
        {
            return "No results";
        }

        string results = summary.HitCount == 1 ? "1 result" : $"{summary.HitCount} results";
        string files = summary.FileCount == 1 ? "1 file" : $"{summary.FileCount} files";
        string line = $"{results} in {files}";

        if (summary.IsTruncated)
        {
            // Said plainly rather than as a count, because the count of what was not reported is the
            // one number the search deliberately did not go on to find out.
            line += " — truncated";
        }

        if (summary.SkippedOversizeFiles > 0)
        {
            string skipped = summary.SkippedOversizeFiles == 1 ? "1 file" : $"{summary.SkippedOversizeFiles} files";
            line += $" · {skipped} too big to search";
        }

        if (summary.UnreadableFiles > 0)
        {
            string unreadable = summary.UnreadableFiles == 1 ? "1 file" : $"{summary.UnreadableFiles} files";
            line += $" · {unreadable} could not be read";
        }

        return line;
    }

    // The same question over another workspace is another question, and its answer is another set of
    // files. Asked again rather than merely dropped, so the box does not sit there holding a query
    // with an empty list under it.
    private void OnActiveWorkspaceChanged(object? sender, ActiveWorkspaceChangedEventArgs e) => RunNow();

    private void Schedule()
    {
        // An emptied box is not a search waiting to happen: the results go at once, so the pane never
        // shows answers to a question that is no longer on screen.
        if (!HasQuery)
        {
            _idle.Cancel(this);
            Abandon();
            return;
        }

        _idle.Schedule(this, TypingDelay, RunAsync);
    }

    // Through the same timer as a keystroke, with no wait on it. Every run arriving by one road is
    // what keeps "search now" from being a second, subtly different way of starting a scan — and the
    // timer is a restart rather than a queue, so whatever the typing had pending is displaced by this.
    private void RunNow() => _idle.Schedule(this, TimeSpan.Zero, RunAsync);

    // The query's own setter does the rest: emptying it is what clears the results, and that path has
    // to work whether the box was emptied from here or with the keyboard.
    private void Clear() => Query = string.Empty;

    private void Abandon()
    {
        CancelRunning();
        Reset();

        StatusText = null;
        IsSearching = false;
    }

    private void Reset()
    {
        _groups.Clear();
        Groups.Clear();
        OnPropertyChanged(nameof(HasResults));
    }

    private void CancelRunning()
    {
        // Bumped whether or not a scan is running: it is what tells a hit already in flight that the
        // list it was going to be appended to is gone.
        _runId++;

        if (_running is null)
        {
            return;
        }

        _running.Cancel();
        _running.Dispose();
        _running = null;
    }

    private async Task RunAsync()
    {
        if (_disposed)
        {
            return;
        }

        if (!HasQuery)
        {
            Abandon();
            return;
        }

        CancelRunning();

        IReadOnlyList<string> roots = _workspaces.ActiveWorkspace?.RootDirectories ?? [];

        Reset();

        if (roots.Count == 0)
        {
            StatusText = "Open a workspace to search in it.";
            return;
        }

        int runId = _runId;
        CancellationTokenSource cancellation = new();
        _running = cancellation;

        IsSearching = true;
        StatusText = "Searching…";

        try
        {
            WorkspaceSearchRequest request = new(Query, roots) { MatchCase = MatchCase };

            WorkspaceSearchSummary summary = await WorkspaceSearchService.SearchAsync(
                request,
                hit => _dispatcher.Post(() =>
                {
                    Append(runId, roots, hit);

                    return Task.CompletedTask;
                }),
                cancellation.Token);

            if (runId == _runId)
            {
                StatusText = Describe(summary);
            }
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer query, or shut down. Neither is a failure, and the run that
            // replaced this one owns the status line now.
        }
        catch (Exception failure)
        {
            // Caught broadly and on purpose: the scan walks folders this application does not own, and
            // a search box that silently does nothing is the one outcome the user cannot act on.
            _logger.LogError(failure, "The workspace search failed.");

            if (runId == _runId)
            {
                StatusText = $"The search failed — {failure.Message}";
            }
        }
        finally
        {
            if (ReferenceEquals(_running, cancellation))
            {
                _running.Dispose();
                _running = null;
            }

            if (runId == _runId)
            {
                IsSearching = false;
            }
        }
    }

    private void Append(int runId, IReadOnlyList<string> roots, WorkspaceSearchHit hit)
    {
        // The hit was found by a scan that has since been replaced. Dropping it here is what keeps a
        // slow answer to an old question out of the new one's results.
        if (runId != _runId)
        {
            return;
        }

        if (!_groups.TryGetValue(hit.FilePath, out SearchResultGroup? group))
        {
            group = new SearchResultGroup(hit.FilePath, LocationOf(roots, hit.FilePath));
            _groups[hit.FilePath] = group;
            Groups.Add(group);
            OnPropertyChanged(nameof(HasResults));
        }

        group.Add(new SearchHitItem(hit, OpenHitCommand));
    }

    private async Task OpenAsync(SearchHitItem? hit)
    {
        if (hit is null)
        {
            return;
        }

        await _documents.RevealAsync(hit.FilePath, hit.Selection);
    }
}
