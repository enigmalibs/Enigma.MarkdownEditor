using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using Enigma.MarkdownEditor.Core.Documents;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.Controls;
using Enigma.MarkdownEditor.Desktop.Services;

namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// One open document: its tab, its text, whether it has been changed, and whether the file behind it
/// is still what was read.
/// </summary>
/// <remarks>
/// <para>
/// The text itself lives in an AvaloniaEdit <see cref="TextDocument"/> rather than in a string
/// property. That is what the editor control binds to, it is what carries the undo history, and it is
/// what makes the dirty flag honest: <see cref="UndoStack.IsOriginalFile"/> goes back to
/// <see langword="true"/> when the user undoes their way back to the saved state, so the dot
/// disappears exactly when the file on disk and the buffer agree again.
/// </para>
/// <para>
/// Three rules move that flag, and the reason there are three is that the undo stack goes quiet at
/// the exact moment vim needs it. <see cref="UndoStack.IsOriginalFile"/> is only recalculated once
/// an open undo group closes, and vim's insert mode holds one open for the whole insert session —
/// long enough for a user to type a paragraph, save, and close the tab over a flag that never
/// moved. So: an edit raises the flag, because an edit is proof there is something to save; a save
/// whose buffer did not move under the write clears it, said outright rather than inferred from a
/// notification that may never come; and the undo stack clears it on its own when the user undoes
/// back to the saved state, with <see cref="OnUndoStackPropertyChanged"/> correcting the one report
/// the stack gets wrong — a group closing over a save made inside it.
/// </para>
/// <para>
/// A <see cref="TextDocument"/> remembers the thread it was built on and verifies access on every
/// use, so this class is built on — and only ever touched from — the UI thread. Its watcher is the
/// one exception: that fires on a thread-pool thread, takes the file's stamp there, and marshals
/// through <see cref="IUiDispatcher"/> before it changes anything anyone is bound to.
/// </para>
/// </remarks>
public sealed class DocumentViewModel : ObservableObject, IDisposable
{
    private readonly IDocumentFileWatcherFactory _watcherFactory;
    private readonly IUiDispatcher _dispatcher;
    private readonly Lock _stampGate = new();

    private IDocumentFileWatcher? _watcher;
    private FileStamp _stamp;
    private bool _disposed;
    private bool _replacing;
    private bool _editedSinceSave;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentViewModel"/> class, and starts watching
    /// the file it was read from.
    /// </summary>
    /// <param name="filePath">The file this document was read from.</param>
    /// <param name="content">Its text and the format it has to be written back in.</param>
    /// <param name="stamp">What the file looked like at the moment it was read.</param>
    /// <param name="watcherFactory">Builds the watcher over that file.</param>
    /// <param name="dispatcher">Moves the watcher's findings onto the UI thread.</param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Any other argument is <see langword="null"/>.</exception>
    public DocumentViewModel(
        string filePath,
        DocumentContent content,
        FileStamp stamp,
        IDocumentFileWatcherFactory watcherFactory,
        IUiDispatcher dispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(content);

        _watcherFactory = watcherFactory ?? throw new ArgumentNullException(nameof(watcherFactory));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

        FilePath = Path.GetFullPath(filePath);
        Format = content.Format;
        _stamp = stamp;

        Document = new TextDocument(content.Text);
        Document.UndoStack.PropertyChanged += OnUndoStackPropertyChanged;
        Document.TextChanged += OnTextChanged;

        StartWatching();
    }

    /// <summary>
    /// Raised, on the UI thread, when the file behind this document is no longer what was read.
    /// </summary>
    /// <remarks>
    /// Only the fact is reported here. What to do about it — reload silently, or ask — depends on
    /// whether the buffer has unsaved work in it, which is the document manager's call rather than
    /// this class's.
    /// </remarks>
    public event EventHandler? ExternalChangeDetected;

    /// <summary>
    /// Raised whenever the text changes, which is what restarts the autosave idle timer.
    /// </summary>
    public event EventHandler? TextChanged;

    /// <summary>
    /// Gets the file this document is written to. Changes under Save As.
    /// </summary>
    public string FilePath { get; private set => SetProperty(ref field, value); }

    /// <summary>
    /// Gets the file's name, which is what the tab shows.
    /// </summary>
    public string Title => Path.GetFileName(FilePath);

    /// <summary>
    /// Gets the tab's caption: the file's name, flagged when the file is no longer on disk.
    /// </summary>
    /// <remarks>
    /// A deleted file does not close its tab. The buffer is still the user's work, and saving it puts
    /// the file back — which is the whole reason the flag is a note on the tab rather than a dialog.
    /// </remarks>
    public string TabTitle => IsMissing ? $"{Title} (deleted)" : Title;

    /// <summary>
    /// Gets the full path, for the tab's tool tip.
    /// </summary>
    public string ToolTip => FilePath;

    /// <summary>
    /// Gets or sets the colour this tab carries: the one on the nearest coloured folder at or above
    /// the file, or <see cref="FolderColor.None"/>.
    /// </summary>
    /// <remarks>
    /// Inherited rather than the file's own — a file is never coloured — which is the whole point:
    /// the mark on the tab is what says which folder the note came from. Resolved and assigned by
    /// <see cref="DocumentManagerViewModel"/>, which is what knows the workspace.
    /// </remarks>
    public FolderColor Color
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(ColorBrush));
            }
        }
    }

    /// <summary>
    /// Gets the brush the tab's colour bar is painted with, or <see langword="null"/> when the file
    /// sits under no coloured folder.
    /// </summary>
    /// <remarks>
    /// <see langword="null"/> paints nothing while the bar keeps its width, so a coloured tab and an
    /// uncoloured one are exactly as wide as each other — which matters more here than in the tree,
    /// because a tab that changed width would slide the whole strip sideways.
    /// </remarks>
    public IBrush? ColorBrush => FolderColorPalette.Brush(Color);

    /// <summary>
    /// Gets the text, as the control that edits it holds it.
    /// </summary>
    public TextDocument Document { get; }

    /// <summary>
    /// Gets the encoding, byte-order mark and line endings the file is written back in.
    /// </summary>
    public TextFileFormat Format { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the buffer differs from the file that was last read or written.
    /// </summary>
    public bool IsDirty
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasUnsavedChanges));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the file is no longer on disk.
    /// </summary>
    public bool IsMissing
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(TabTitle));
                OnPropertyChanged(nameof(HasUnsavedChanges));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the file changed underneath a buffer that has its own unsaved
    /// changes, so the two cannot be reconciled without the user saying which one wins.
    /// </summary>
    public bool HasExternalChange { get; private set => SetProperty(ref field, value); }

    /// <summary>
    /// Gets a value indicating whether closing this document would lose something.
    /// </summary>
    /// <remarks>
    /// A file that was deleted from underneath counts even when the buffer was never edited: what is
    /// on screen is then the only copy left.
    /// </remarks>
    public bool HasUnsavedChanges => IsDirty || IsMissing;

    /// <summary>
    /// Gets or sets the caret's line, one-based, as the editor last reported it.
    /// </summary>
    public int CaretLine { get; set => SetProperty(ref field, value); } = 1;

    /// <summary>
    /// Gets or sets the caret's column, one-based, as the editor last reported it.
    /// </summary>
    public int CaretColumn { get; set => SetProperty(ref field, value); } = 1;

    /// <summary>
    /// Gets or sets where the caret sits, so switching tabs and coming back lands where it was left.
    /// </summary>
    /// <remarks>
    /// Kept here rather than in the editor control because there is one control for every tab: the
    /// view saves this off the outgoing document and applies it to the incoming one.
    /// </remarks>
    public int CaretOffset { get; set; }

    /// <summary>
    /// Gets the caret placement waiting to be applied to this document, if any.
    /// </summary>
    /// <remarks>
    /// Set by whoever asked for the document to be opened at a particular place — a search hit today
    /// — and consumed by the editor pane the next time it puts this document on screen. It is held
    /// here rather than pushed at the control because the control may be showing another tab, or the
    /// tab may not exist yet: a request that outlives the moment it was made is a request the view can
    /// honour whenever it gets there.
    /// </remarks>
    public DocumentSelection? PendingSelection { get; private set; }

    /// <summary>
    /// Gets or sets how far down the view was scrolled.
    /// </summary>
    public double VerticalScrollOffset { get; set; }

    /// <summary>
    /// Gets or sets how far across the view was scrolled.
    /// </summary>
    public double HorizontalScrollOffset { get; set; }

    /// <summary>
    /// Asks for the caret to be put somewhere in this document, and for what is there to be selected.
    /// </summary>
    /// <param name="selection">Where to land, and how much to select.</param>
    /// <remarks>
    /// A later request replaces an earlier one that has not been applied: two search results clicked
    /// in quick succession are one destination, not a queue of them.
    /// </remarks>
    public void RequestSelection(DocumentSelection selection) => PendingSelection = selection;

    /// <summary>
    /// Takes the pending caret placement, leaving none behind.
    /// </summary>
    /// <returns>The request, or <see langword="null"/> when there is none.</returns>
    /// <remarks>
    /// Taking rather than reading is what makes the view's two entry points — a document that has just
    /// been swapped in, and a request against the document already showing — safe to run in either
    /// order: whichever gets there first honours it, and the other finds nothing to do.
    /// </remarks>
    public DocumentSelection? TakePendingSelection()
    {
        DocumentSelection? pending = PendingSelection;
        PendingSelection = null;

        return pending;
    }

    /// <summary>
    /// Records that the buffer has been written to disk, possibly under a new name.
    /// </summary>
    /// <param name="filePath">Where it was written.</param>
    /// <param name="stamp">What the file looked like immediately afterwards.</param>
    /// <param name="written">
    /// The buffer's version at the moment the text that went to disk was read off it. It is what
    /// tells a buffer that sat still for the write from one that moved during it.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="filePath"/> or <paramref name="written"/> is <see langword="null"/>.
    /// </exception>
    public void MarkSaved(string filePath, FileStamp stamp, ITextSourceVersion written)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(written);

        string full = Path.GetFullPath(filePath);
        bool moved = !string.Equals(full, FilePath, PathComparison);

        SetStamp(stamp);
        FilePath = full;

        if (moved)
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(TabTitle));
            OnPropertyChanged(nameof(ToolTip));

            // The old file is somebody else's business now; this document follows the bytes it owns.
            StartWatching();
        }

        IsMissing = false;
        HasExternalChange = false;

        if (written.CompareAge(Document.Version) != 0)
        {
            // A keystroke landed while the write was in flight — an autosave firing as the user
            // starts typing again — so the file holds one edit less than the buffer does. Nothing is
            // cleared and no save point is marked: the document is still dirty, and the autosave
            // that same keystroke re-armed writes the rest of it.
            return;
        }

        // Telling the undo stack where the saved state is keeps undoing back to it clearing the dot.
        // Saying IsDirty outright is what an open undo group makes necessary: the stack recalculates
        // nothing until the group closes, so marking the save point can raise no notification to
        // clear the flag with — which is the whole of the reported bug.
        Document.UndoStack.MarkAsOriginalFile();
        IsDirty = false;
        _editedSinceSave = false;
    }

    /// <summary>
    /// Replaces the buffer with what is now on disk, discarding the undo history with it.
    /// </summary>
    /// <param name="content">What was read back.</param>
    /// <param name="stamp">What the file looked like at that moment.</param>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The history goes because it no longer describes anything: its entries are edits to text that
    /// is not there any more, and replaying them over the new text would produce something that never
    /// existed. The caret is kept, clamped to whatever length the file now has.
    /// </remarks>
    public void ApplyReload(DocumentContent content, FileStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(content);

        Format = content.Format;
        SetStamp(stamp);

        // Flagged for the length of the assignment: it raises TextChanged exactly as typing does, and
        // this is the one change to the buffer that is not the user's work.
        _replacing = true;

        try
        {
            Document.Text = content.Text;
        }
        finally
        {
            _replacing = false;
        }

        Document.UndoStack.ClearAll();
        Document.UndoStack.MarkAsOriginalFile();

        CaretOffset = Math.Min(CaretOffset, Document.TextLength);
        IsMissing = false;
        HasExternalChange = false;

        // Said rather than inferred: what was just read is what the file holds, whether or not
        // clearing the undo stack happened to raise a notification about it.
        IsDirty = false;
        _editedSinceSave = false;
    }

    /// <summary>
    /// Accepts the buffer as it is and stops asking about the change on disk.
    /// </summary>
    /// <remarks>
    /// The stamp is taken again so the file's current state becomes the new baseline: without that,
    /// the very next watcher event would compare against a version the user has explicitly decided to
    /// ignore and ask them all over again.
    /// </remarks>
    public void KeepMine()
    {
        SetStamp(FileStamp.For(FilePath));
        HasExternalChange = false;
    }

    /// <summary>
    /// Flags that the file changed under a buffer with unsaved work in it.
    /// </summary>
    public void FlagExternalChange() => HasExternalChange = true;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        StopWatching();

        Document.UndoStack.PropertyChanged -= OnUndoStackPropertyChanged;
        Document.TextChanged -= OnTextChanged;

        ExternalChangeDetected = null;
        TextChanged = null;
    }

    // Windows names files case-insensitively and Linux does not, so "the same file" is a different
    // question on each and has to be asked the platform's way.
    private static StringComparison PathComparison
        => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private void SetStamp(FileStamp stamp)
    {
        lock (_stampGate)
        {
            _stamp = stamp;
        }
    }

    private void StartWatching()
    {
        StopWatching();

        if (_disposed)
        {
            return;
        }

        IDocumentFileWatcher watcher = _watcherFactory.Create(FilePath);
        watcher.Changed += OnFileChanged;
        _watcher = watcher;

        watcher.Start();
    }

    private void StopWatching()
    {
        if (_watcher is null)
        {
            return;
        }

        _watcher.Changed -= OnFileChanged;
        _watcher.Dispose();
        _watcher = null;
    }

    private void OnFileChanged(object? sender, EventArgs e)
    {
        // Still on a thread-pool thread, and deliberately: the stamp is a file-system call, and the
        // usual answer is that nothing changed at all — this was the editor's own save coming back
        // around. Marshalling first would put that non-event on the UI thread on every keystroke an
        // autosave writes.
        FileStamp current = FileStamp.For(FilePath);

        lock (_stampGate)
        {
            if (current == _stamp)
            {
                return;
            }

            _stamp = current;
        }

        _dispatcher.Post(() =>
        {
            if (!_disposed)
            {
                IsMissing = !current.Exists;
                ExternalChangeDetected?.Invoke(this, EventArgs.Empty);
            }

            return Task.CompletedTask;
        });
    }

    // What clears the flag when the buffer is undone back to the saved state — which is why undoing
    // an edit away makes the dot disappear — and the one place the stack has to be corrected.
    private void OnUndoStackPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(UndoStack.IsOriginalFile))
        {
            return;
        }

        if (!Document.UndoStack.IsOriginalFile && !_editedSinceSave)
        {
            // A group closing over a save made inside it. Closing collapses the group's operations
            // into one entry and reports the difference against a counter the save already put back
            // to zero, so the stack calls "modified" text that is exactly what was written. Nothing
            // has been typed since that save, so the state the stack now stands on is the saved one:
            // say so. The notification that raises re-enters here and clears the flag.
            //
            // Only a group close can arrive here with nothing typed since the save — the stack's
            // other three routes to this report all change the text first, which sets the flag
            // below.
            Document.UndoStack.MarkAsOriginalFile();

            return;
        }

        IsDirty = !Document.UndoStack.IsOriginalFile;
    }

    private void OnTextChanged(object? sender, EventArgs e)
    {
        if (!_replacing)
        {
            // Raised here and never cleared here: an edit proves there is something to save, and the
            // undo stack cannot say so while one of its groups is still open. It is what makes the dot
            // appear on the first keystroke of a vim insert session rather than on the Esc that ends
            // it. The cost is one stale dot: while that group is open, editing back to the saved text
            // cannot clear the flag until it closes.
            IsDirty = true;
        }

        // Outside that guard on purpose: a reload is a change too, and ApplyReload clears this along
        // with the flag once it has finished. This is the one thing the undo stack cannot be asked
        // while a group is open — whether anything at all has happened since the last save — and the
        // group-close correction above turns on knowing it.
        _editedSinceSave = true;

        TextChanged?.Invoke(this, EventArgs.Empty);
    }
}
