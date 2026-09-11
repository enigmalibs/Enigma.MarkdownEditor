using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Enigma.MarkdownEditor.Core.Files;

/// <summary>
/// One <see cref="FileSystemWatcher"/> over a workspace root, with its events debounced and coalesced
/// into the directories the tree has to read again.
/// </summary>
/// <remarks>
/// <para>
/// Raw watcher events are unusable as they come: a single save can produce three of them, a folder
/// copy produces one per file, and the order is not guaranteed. They are therefore collected and only
/// reported once <see cref="DefaultDebounce"/> has passed without a new one — or, if changes never
/// stop arriving, at least every <see cref="DefaultMaximumDelay"/>, so a busy folder still updates
/// promptly instead of never.
/// </para>
/// <para>
/// Only <see cref="NotifyFilters.FileName"/> and <see cref="NotifyFilters.DirectoryName"/> are
/// watched. Content and timestamp changes do not alter what the tree draws, and subscribing to them
/// would wake this class on every keystroke the editor saves.
/// </para>
/// </remarks>
public sealed class DirectoryWatcher : IDirectoryWatcher
{
    /// <summary>
    /// How long changes have to stop arriving before a batch is reported.
    /// </summary>
    public static TimeSpan DefaultDebounce { get; } = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// The longest a change is held back while new ones keep arriving.
    /// </summary>
    public static TimeSpan DefaultMaximumDelay { get; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The watcher's own event buffer. The default 8 KB overflows on a large copy, and an overflow
    /// costs a full re-read of the root; 64 KB is the documented practical maximum.
    /// </summary>
    public const int BufferSize = 64 * 1024;

    private readonly Lock _gate = new();
    private readonly List<FileSystemChange> _pending = [];
    private readonly TimeSpan _debounce;
    private readonly TimeSpan _maximumDelay;
    private readonly Timer _timer;

    private FileSystemWatcher? _watcher;
    private long _firstPendingAt;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectoryWatcher"/> class. Watching starts at
    /// <see cref="Start"/>, not here.
    /// </summary>
    /// <param name="rootDirectory">The directory to watch, with its subdirectories.</param>
    /// <param name="debounce">How long to wait for the burst to settle. Defaults to <see cref="DefaultDebounce"/>.</param>
    /// <param name="maximumDelay">
    /// The cap on that wait while changes keep arriving. Defaults to <see cref="DefaultMaximumDelay"/>.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="rootDirectory"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="rootDirectory"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Either interval is zero or negative.</exception>
    public DirectoryWatcher(string rootDirectory, TimeSpan? debounce = null, TimeSpan? maximumDelay = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        _debounce = debounce ?? DefaultDebounce;
        _maximumDelay = maximumDelay ?? DefaultMaximumDelay;

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_debounce, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_maximumDelay, TimeSpan.Zero);

        RootDirectory = Path.TrimEndingDirectorySeparator(rootDirectory);
        _timer = new Timer(_ => Flush(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <inheritdoc />
    public event EventHandler<DirectoryChangeSet>? Changed;

    /// <inheritdoc />
    public string RootDirectory { get; }

    /// <inheritdoc />
    public bool IsWatching => _watcher is not null;

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The watcher has been disposed.</exception>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if ((_watcher is not null) || !Directory.Exists(RootDirectory))
        {
            // A root that is not on disk is not an error — it is an unplugged drive, and the tree
            // already draws it as missing. It gains a watcher when the user refreshes it back.
            return;
        }

        FileSystemWatcher watcher = new(RootDirectory)
        {
            IncludeSubdirectories = true,
            InternalBufferSize = BufferSize,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
        };

        watcher.Created += OnCreated;
        watcher.Deleted += OnDeleted;
        watcher.Renamed += OnRenamed;
        watcher.Error += OnError;

        try
        {
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The root vanished, or is on a file system that cannot be watched. The tree still shows
            // it; it simply will not update itself, which the manual refresh covers.
            watcher.Dispose();
            return;
        }

        _watcher = watcher;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        FileSystemWatcher? watcher;

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pending.Clear();
            watcher = _watcher;
            _watcher = null;
        }

        if (watcher is not null)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Created -= OnCreated;
            watcher.Deleted -= OnDeleted;
            watcher.Renamed -= OnRenamed;
            watcher.Error -= OnError;
            watcher.Dispose();
        }

        _timer.Dispose();
        Changed = null;
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
        => Record(new FileSystemChange(FileChangeKind.Created, e.FullPath));

    private void OnDeleted(object sender, FileSystemEventArgs e)
        => Record(new FileSystemChange(FileChangeKind.Deleted, e.FullPath));

    private void OnRenamed(object sender, RenamedEventArgs e)
        => Record(new FileSystemChange(FileChangeKind.Renamed, e.FullPath, e.OldFullPath));

    private void OnError(object sender, ErrorEventArgs e)
        => Record(new FileSystemChange(FileChangeKind.Reset, RootDirectory));

    private void Record(FileSystemChange change)
    {
        bool flushNow;

        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            if (_pending.Count == 0)
            {
                _firstPendingAt = Stopwatch.GetTimestamp();
            }

            _pending.Add(change);

            flushNow = Stopwatch.GetElapsedTime(_firstPendingAt) >= _maximumDelay;

            if (!flushNow)
            {
                _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
            }
        }

        if (flushNow)
        {
            Flush();
        }
    }

    private void Flush()
    {
        List<FileSystemChange> batch;

        lock (_gate)
        {
            if (_disposed || (_pending.Count == 0))
            {
                return;
            }

            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            batch = [.. _pending];
            _pending.Clear();
        }

        DirectoryChangeSet changes = FileChangeCoalescer.Coalesce(batch, RootDirectory);

        if (!changes.IsEmpty)
        {
            Changed?.Invoke(this, changes);
        }
    }
}
