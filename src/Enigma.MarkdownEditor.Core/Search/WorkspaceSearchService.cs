using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Core.Files;

namespace Enigma.MarkdownEditor.Core.Search;

/// <summary>
/// Plain-substring search over every markdown file in a set of workspace roots.
/// </summary>
/// <remarks>
/// <para>
/// Static and stateless, like the other readers in this assembly (<see cref="DirectoryLister"/>,
/// <see cref="Documents.DocumentFile"/>): a search holds nothing between calls, and everything that
/// varies is in the <see cref="WorkspaceSearchRequest"/>.
/// </para>
/// <para>
/// Two passes, in this order. The first walks the roots and reports every file whose <em>name</em>
/// matches — those come out before a single byte of content has been read, because a note whose name
/// matches is nearly always the one being looked for. The second reads the files themselves, several
/// at a time, and reports each file's hits as one contiguous run, so a result list grouped by file
/// never has to reopen a group it has already closed.
/// </para>
/// <para>
/// There is no index, on purpose: a cold scan of a notes-sized workspace costs milliseconds, and an
/// index would have to be kept true against a tree that changes under the editor. If it ever stops
/// being fast enough, an index is the documented fallback and the shape of this API does not change.
/// </para>
/// <para>
/// Cancellation is by exception, the usual way: a cancelled search throws
/// <see cref="OperationCanceledException"/> rather than returning a partial summary, because a partial
/// answer to a question nobody is asking any more is not an answer at all.
/// </para>
/// </remarks>
public static class WorkspaceSearchService
{
    // Reading is the bottleneck and it is the disk's, not the CPU's: past a handful of concurrent
    // readers a spinning disk gets slower rather than faster, and an SSD is already saturated.
    private const int MaxConcurrentReads = 8;

    /// <summary>
    /// Runs a search off the calling thread, reporting each hit as it is found.
    /// </summary>
    /// <param name="request">What to look for, and where.</param>
    /// <param name="onHit">
    /// Called once per hit, in report order. It is invoked from a worker thread and under the
    /// reporter's own lock, so it must be quick and must not call back into the search: marshal to the
    /// UI thread and return.
    /// </param>
    /// <param name="cancellationToken">Abandons the search.</param>
    /// <returns>What the finished search added up to.</returns>
    /// <exception cref="ArgumentNullException">Either <paramref name="request"/> or <paramref name="onHit"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="WorkspaceSearchRequest.MaxHits"/> is below one.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    /// <remarks>
    /// Offloaded with <see cref="Task.Run{TResult}(Func{TResult}, CancellationToken)"/> rather than
    /// awaiting an asynchronous API, for the same reason <see cref="DirectoryLister"/> is: directory
    /// enumeration is synchronous all the way down, and a note is one buffer. The offload is what
    /// keeps a cold workspace off the UI thread.
    /// </remarks>
    public static Task<WorkspaceSearchSummary> SearchAsync(
        WorkspaceSearchRequest request,
        Action<WorkspaceSearchHit> onHit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onHit);

        return Task.Run(() => Search(request, onHit, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Runs a search on the calling thread.
    /// </summary>
    /// <param name="request">What to look for, and where.</param>
    /// <param name="onHit">Called once per hit, in report order.</param>
    /// <param name="cancellationToken">Abandons the search.</param>
    /// <returns>What the finished search added up to.</returns>
    /// <exception cref="ArgumentNullException">Either <paramref name="request"/> or <paramref name="onHit"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="WorkspaceSearchRequest.MaxHits"/> is below one.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    public static WorkspaceSearchSummary Search(
        WorkspaceSearchRequest request,
        Action<WorkspaceSearchHit> onHit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onHit);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.MaxHits, 1);

        // An empty query is not a search that found nothing — it is no question having been asked.
        if (request.Query.Length == 0)
        {
            return WorkspaceSearchSummary.Empty;
        }

        List<(string Path, long Length)> candidates = Collect(request.RootDirectories, cancellationToken);

        StringComparison comparison = request.MatchCase
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        HitSink sink = new(onHit, request.MaxHits);

        int oversize = ReportFileNames(candidates, request, comparison, sink, cancellationToken);

        ReportContent(candidates, request, comparison, sink, cancellationToken);

        return sink.Summarize(candidates.Count, oversize);
    }

    // One walk per root, iterative rather than recursive so a pathological tree costs heap rather than
    // stack. Directories are pruned by the same rule the tree uses with ShowAllFiles off, which is
    // what makes the search's reach predictable from what the sidebar shows.
    private static List<(string Path, long Length)> Collect(
        IReadOnlyList<string> roots,
        CancellationToken cancellationToken)
    {
        List<(string Path, long Length)> candidates = [];
        HashSet<string> seenFiles = new(PathComparer);
        HashSet<string> visitedDirectories = new(PathComparer);
        Stack<string> pending = new();

        EnumerationOptions options = new()
        {
            RecurseSubdirectories = false,

            // One unreadable subfolder must not empty its parent.
            IgnoreInaccessible = true,

            // Nothing is skipped by attribute here — the hidden/system rule belongs to FileTreeFilter,
            // which is the one place the scope is decided and tested.
            AttributesToSkip = 0,
        };

        foreach (string root in roots)
        {
            if (!string.IsNullOrWhiteSpace(root))
            {
                pending.Push(Path.GetFullPath(root));
            }
        }

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string directory = pending.Pop();

            // Two roots can overlap, and a junction can point back up its own tree; either way a
            // directory is walked once.
            if (!visitedDirectories.Add(directory))
            {
                continue;
            }

            DirectoryInfo info = new(directory);

            if (!info.Exists)
            {
                continue;
            }

            try
            {
                foreach (FileSystemInfo entry in info.EnumerateFileSystemInfos("*", options))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    FileAttributes attributes = SafeAttributes(entry);
                    bool isDirectory = (attributes & FileAttributes.Directory) != 0;
                    bool isHidden = (attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0;

                    if (!FileTreeFilter.Includes(entry.Name, isDirectory, isHidden, showAllFiles: false))
                    {
                        continue;
                    }

                    if (isDirectory)
                    {
                        // A symlinked or junctioned folder is not descended into. Its target is either
                        // inside the workspace already — in which case it is walked there — or outside
                        // it, in which case it is not what was asked for.
                        if ((attributes & FileAttributes.ReparsePoint) == 0)
                        {
                            pending.Push(entry.FullName);
                        }

                        continue;
                    }

                    if (seenFiles.Add(entry.FullName))
                    {
                        candidates.Add((entry.FullName, SafeLength(entry)));
                    }
                }
            }
            catch (Exception failure) when (failure is UnauthorizedAccessException or IOException)
            {
                // A folder that cannot be enumerated contributes nothing, and is not a failed search.
            }
        }

        // Enumeration order is the file system's, which differs between runs and between roots. Sorted
        // so the same query over the same workspace produces the same list twice running.
        candidates.Sort(static (left, right) => string.Compare(left.Path, right.Path, PathComparison));

        return candidates;
    }

    // Pass one: names only, in path order, before anything is read. Returns how many files were too
    // big to have their contents read, which this pass is the natural place to count.
    private static int ReportFileNames(
        List<(string Path, long Length)> candidates,
        WorkspaceSearchRequest request,
        StringComparison comparison,
        HitSink sink,
        CancellationToken cancellationToken)
    {
        int oversize = 0;

        foreach ((string path, long length) in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (length > request.MaxFileBytes)
            {
                oversize++;
            }

            string name = Path.GetFileName(path);
            int index = name.IndexOf(request.Query, comparison);

            if (index < 0)
            {
                continue;
            }

            // Once per file: what matched is the name, and a second hit on the same name is the same
            // answer said twice.
            WorkspaceSearchHit hit = WorkspaceSearchHit.InFileName(path, name, index, request.Query.Length);

            if (!sink.Report([hit]))
            {
                break;
            }
        }

        return oversize;
    }

    // Pass two: the contents, several files at a time, each file's hits reported as one run.
    private static void ReportContent(
        List<(string Path, long Length)> candidates,
        WorkspaceSearchRequest request,
        StringComparison comparison,
        HitSink sink,
        CancellationToken cancellationToken)
    {
        if (sink.IsFull)
        {
            return;
        }

        ParallelOptions options = new()
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Max(1, Math.Min(Environment.ProcessorCount, MaxConcurrentReads)),
        };

        Parallel.ForEach(
            candidates,
            options,
            (candidate, state) =>
            {
                if (state.ShouldExitCurrentIteration || (candidate.Length > request.MaxFileBytes))
                {
                    return;
                }

                List<WorkspaceSearchHit>? hits = Scan(candidate.Path, request.Query, comparison, cancellationToken);

                if (hits is null)
                {
                    sink.RecordUnreadable();
                    return;
                }

                if ((hits.Count > 0) && !sink.Report(hits))
                {
                    state.Stop();
                }
            });
    }

    // Every occurrence on every line, non-overlapping — "aa" appears once in "aaa", which is what a
    // reader counting them would say. Null means the file could not be read at all.
    private static List<WorkspaceSearchHit>? Scan(
        string path,
        string query,
        StringComparison comparison,
        CancellationToken cancellationToken)
    {
        List<WorkspaceSearchHit> hits = [];

        try
        {
            // UTF-8 unless a byte-order mark says otherwise, which is the same guess DocumentFile
            // makes when the file is opened for editing.
            using StreamReader reader = new(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            int lineNumber = 0;

            while (reader.ReadLine() is { } line)
            {
                cancellationToken.ThrowIfCancellationRequested();
                lineNumber++;

                int index = line.IndexOf(query, comparison);

                while (index >= 0)
                {
                    hits.Add(WorkspaceSearchHit.InContent(path, lineNumber, line, index, query.Length));

                    int next = index + query.Length;

                    if (next >= line.Length)
                    {
                        break;
                    }

                    index = line.IndexOf(query, next, comparison);
                }
            }
        }
        catch (Exception failure)
            when (failure is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // One file that cannot be read is one file that contributes nothing — counted, and not
            // allowed to fail the search. A workspace with a locked file is still searchable.
            return null;
        }

        return hits;
    }

    private static FileAttributes SafeAttributes(FileSystemInfo info)
    {
        try
        {
            return info.Attributes;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A broken symlink throws from the property rather than from the enumerator. Treated as
            // hidden, so it is skipped rather than opened.
            return FileAttributes.Hidden;
        }
    }

    private static long SafeLength(FileSystemInfo info)
    {
        try
        {
            return info is FileInfo file ? file.Length : 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Unknown rather than oversize: the read that follows will fail honestly if it must.
            return 0;
        }
    }

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    // The one place hits leave the search, and therefore the one place the cap can be enforced without
    // a race: several readers finish at once, and "is there room for one more" has to be asked and
    // answered in the same breath as taking that room.
    private sealed class HitSink
    {
        private readonly Action<WorkspaceSearchHit> _onHit;
        private readonly int _maxHits;
        private readonly Lock _gate = new();
        private readonly HashSet<string> _files = new(PathComparer);

        private int _hitCount;
        private int _unreadable;
        private bool _truncated;

        public HitSink(Action<WorkspaceSearchHit> onHit, int maxHits)
        {
            _onHit = onHit;
            _maxHits = maxHits;
        }

        public bool IsFull
        {
            get
            {
                lock (_gate)
                {
                    return _hitCount >= _maxHits;
                }
            }
        }

        /// <summary>
        /// Reports one file's hits as a contiguous run, and answers whether there is room for more.
        /// </summary>
        public bool Report(IReadOnlyList<WorkspaceSearchHit> hits)
        {
            lock (_gate)
            {
                foreach (WorkspaceSearchHit hit in hits)
                {
                    if (_hitCount >= _maxHits)
                    {
                        // Set here and nowhere else: reaching the cap exactly, with nothing left to
                        // say, is a complete answer rather than a truncated one.
                        _truncated = true;
                        return false;
                    }

                    _onHit(hit);
                    _hitCount++;
                    _files.Add(hit.FilePath);
                }

                return _hitCount < _maxHits;
            }
        }

        public void RecordUnreadable()
        {
            lock (_gate)
            {
                _unreadable++;
            }
        }

        public WorkspaceSearchSummary Summarize(int scannedFiles, int oversizeFiles)
        {
            lock (_gate)
            {
                return new WorkspaceSearchSummary(
                    _hitCount,
                    _files.Count,
                    scannedFiles,
                    oversizeFiles,
                    _unreadable,
                    _truncated);
            }
        }
    }
}
