using System;
using System.IO;
using System.Threading;

namespace Enigma.MarkdownEditor.Core.Documents;

/// <summary>
/// One <see cref="FileSystemWatcher"/> over the folder of a single open document, filtered to that
/// document's own name and debounced into one "look again".
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="FileSystemWatcher"/> cannot watch a file, only a folder, so this watches the parent
/// and lets <see cref="FileSystemWatcher.Filter"/> narrow it back down to the one name. That also
/// covers the file being created, deleted, or renamed to or from that name, which a watcher over the
/// file itself could not.
/// </para>
/// <para>
/// One save produces several raw events — a truncate, a write, a timestamp touch — so they are
/// collected and reported only once <see cref="DefaultDebounce"/> has passed without another. The
/// event carries nothing: what actually happened is decided by the caller from a fresh
/// <see cref="FileStamp"/>, which is the only reading that survives a watcher's missing and
/// duplicate events.
/// </para>
/// </remarks>
public sealed class DocumentFileWatcher : IDocumentFileWatcher
{
    /// <summary>
    /// How long events have to stop arriving before the file is reported as possibly changed.
    /// </summary>
    /// <remarks>
    /// Shorter than the tree's, and it can be: this watcher sees the events of one file rather than
    /// of a whole folder tree, so there is no burst worth waiting out beyond a single save.
    /// </remarks>
    public static TimeSpan DefaultDebounce { get; } = TimeSpan.FromMilliseconds(250);

    private readonly Lock _gate = new();
    private readonly TimeSpan _debounce;
    private readonly Timer _timer;
    private readonly string _directory;
    private readonly string _fileName;

    private FileSystemWatcher? _watcher;
    private bool _pending;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentFileWatcher"/> class. Watching starts at
    /// <see cref="Start"/>, not here.
    /// </summary>
    /// <param name="filePath">The file to watch.</param>
    /// <param name="debounce">How long to wait for a burst to settle. Defaults to <see cref="DefaultDebounce"/>.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="filePath"/> is empty or white-space, or names no file in a folder.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="debounce"/> is zero or negative.</exception>
    public DocumentFileWatcher(string filePath, TimeSpan? debounce = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _debounce = debounce ?? DefaultDebounce;
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_debounce, TimeSpan.Zero);

        FilePath = Path.GetFullPath(filePath);
        _directory = Path.GetDirectoryName(FilePath)
                     ?? throw new ArgumentException("The path names no folder to watch.", nameof(filePath));
        _fileName = Path.GetFileName(FilePath);

        if (string.IsNullOrEmpty(_fileName))
        {
            throw new ArgumentException("The path names no file to watch.", nameof(filePath));
        }

        _timer = new Timer(_ => Flush(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public string FilePath { get; }

    /// <inheritdoc />
    public bool IsWatching => _watcher is not null;

    /// <inheritdoc />
    /// <exception cref="ObjectDisposedException">The watcher has been disposed.</exception>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if ((_watcher is not null) || !Directory.Exists(_directory))
        {
            return;
        }

        FileSystemWatcher watcher = new(_directory)
        {
            Filter = _fileName,
            IncludeSubdirectories = false,

            // Size as well as LastWrite: a write that lands inside the file system's timestamp
            // resolution changes the length but not the clock, and a truncation to empty changes
            // nothing else at all.
            NotifyFilter = NotifyFilters.FileName
                           | NotifyFilters.LastWrite
                           | NotifyFilters.Size
                           | NotifyFilters.CreationTime,
        };

        watcher.Changed += OnEvent;
        watcher.Created += OnEvent;
        watcher.Deleted += OnEvent;
        watcher.Renamed += OnRenamed;
        watcher.Error += OnError;

        try
        {
            watcher.EnableRaisingEvents = true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The folder went away, or is on a file system that cannot be watched. The document stays
            // open and editable; it simply will not notice an edit made behind the editor's back.
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
            _pending = false;
            watcher = _watcher;
            _watcher = null;
        }

        if (watcher is not null)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Changed -= OnEvent;
            watcher.Created -= OnEvent;
            watcher.Deleted -= OnEvent;
            watcher.Renamed -= OnRenamed;
            watcher.Error -= OnError;
            watcher.Dispose();
        }

        _timer.Dispose();
        Changed = null;
    }

    private void OnEvent(object sender, FileSystemEventArgs e) => Record();

    private void OnRenamed(object sender, RenamedEventArgs e) => Record();

    // A lost-events error is reported like any other change: the answer to both is the same fresh
    // stamp, so there is nothing extra for the caller to do about it.
    private void OnError(object sender, ErrorEventArgs e) => Record();

    private void Record()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _pending = true;
            _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    private void Flush()
    {
        lock (_gate)
        {
            if (_disposed || !_pending)
            {
                return;
            }

            _pending = false;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
