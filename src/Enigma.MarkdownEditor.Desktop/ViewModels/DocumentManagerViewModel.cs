using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Avalonia.Desktop.Controls.InfoBar;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Documents;
using Enigma.MarkdownEditor.Core.Files;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Core.Vim;
using Enigma.MarkdownEditor.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// The editor pane: the open documents, which one is on screen, and every way one of them is opened,
/// written or closed.
/// </summary>
/// <remarks>
/// <para>
/// The one place that decides what happens to a buffer. Opening de-duplicates by path, so activating
/// the same file twice activates its tab rather than opening it again; closing a changed document
/// asks first and takes "cancel" for an answer, all the way up to the window refusing to close.
/// </para>
/// <para>
/// Every failure that reaches a user is reported through the info bar rather than thrown: these
/// methods are behind commands and a window-closing handler, and an exception escaping either is one
/// nobody sees. Failures that only a developer can act on go to the log as well.
/// </para>
/// <para>
/// It is a ViewModel rather than the plain service the plan named it — the pane binds to its
/// collection, its active document and its three appearance properties, so it is one either way and
/// the suffix says which.
/// </para>
/// </remarks>
public sealed class DocumentManagerViewModel : ObservableObject, IDisposable
{
    private const string NoFileOpen = "No file open";

    private readonly IConfig<AppSettings> _settings;
    private readonly IWorkspaceService _workspaces;
    private readonly IDocumentPrompts _prompts;
    private readonly IInfoBarService _infoBarService;
    private readonly IIdleScheduler _autosave;
    private readonly IDocumentFileWatcherFactory _watcherFactory;
    private readonly IUiDispatcher _dispatcher;
    private readonly IEditingAssistPolicy _assists;
    private readonly IVimSession _vim;
    private readonly IImagePasteService _imagePaste;
    private readonly IFontCatalog _fonts;
    private readonly ILogger<DocumentManagerViewModel> _logger;

    // A document whose autosave failed is not retried until the user saves it themselves. Without
    // this, a file that has become read-only would raise one info bar per keystroke.
    private readonly HashSet<DocumentViewModel> _autosaveFailed = [];

    private bool _promptOpen;
    private bool _disposed;
    private int _highlightingFailureLogged;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentManagerViewModel"/> class.
    /// </summary>
    /// <param name="settings">Read for the editor's appearance and for the autosave policy.</param>
    /// <param name="workspaces">Supplies the folder colours each tab's own colour is resolved from.</param>
    /// <param name="prompts">Asks about unsaved changes, and where to save.</param>
    /// <param name="infoBarService">Reports a document that could not be opened, read or written.</param>
    /// <param name="autosave">The per-document idle timers.</param>
    /// <param name="watcherFactory">Handed to each document so it can watch its own file.</param>
    /// <param name="dispatcher">Handed to each document so its watcher can marshal.</param>
    /// <param name="assists">Says whether the editor's markdown assists apply right now.</param>
    /// <param name="vim">The vim session the pane drives and the status bar shows.</param>
    /// <param name="imagePaste">Files an image pasted into the active document.</param>
    /// <param name="fonts">
    /// Turns the stored font family into one this machine actually has (see <see cref="FontFamily"/>).
    /// </param>
    /// <param name="logger">Records the same failures for the log file.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public DocumentManagerViewModel(
        IConfig<AppSettings> settings,
        IWorkspaceService workspaces,
        IDocumentPrompts prompts,
        IInfoBarService infoBarService,
        IIdleScheduler autosave,
        IDocumentFileWatcherFactory watcherFactory,
        IUiDispatcher dispatcher,
        IEditingAssistPolicy assists,
        IVimSession vim,
        IImagePasteService imagePaste,
        IFontCatalog fonts,
        ILogger<DocumentManagerViewModel> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _workspaces = workspaces ?? throw new ArgumentNullException(nameof(workspaces));
        _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
        _infoBarService = infoBarService ?? throw new ArgumentNullException(nameof(infoBarService));
        _autosave = autosave ?? throw new ArgumentNullException(nameof(autosave));
        _watcherFactory = watcherFactory ?? throw new ArgumentNullException(nameof(watcherFactory));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _assists = assists ?? throw new ArgumentNullException(nameof(assists));
        _vim = vim ?? throw new ArgumentNullException(nameof(vim));
        _imagePaste = imagePaste ?? throw new ArgumentNullException(nameof(imagePaste));
        _fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        SaveCommand = new AsyncRelayCommand<DocumentViewModel?>(document => SaveAsync(document), CanSave);
        SaveAsCommand = new AsyncRelayCommand<DocumentViewModel?>(SaveAsAsync, CanSave);
        SaveAllCommand = new AsyncRelayCommand(SaveAllAsync, () => HasUnsavedChanges);
        CloseCommand = new AsyncRelayCommand<DocumentViewModel?>(document => CloseAsync(document), HasAnyDocument);
        NextDocumentCommand = new RelayCommand(() => Step(1), () => Documents.Count > 1);
        PreviousDocumentCommand = new RelayCommand(() => Step(-1), () => Documents.Count > 1);
        ReloadCommand = new AsyncRelayCommand<DocumentViewModel?>(ReloadFromDiskAsync);
        KeepMineCommand = new RelayCommand<DocumentViewModel?>(KeepMine);

        Documents.CollectionChanged += OnDocumentsChanged;
        _vim.Changed += OnVimChanged;

        // Both, and for two different reasons: a colour change re-resolves the open tabs against the
        // same workspace, and opening another workspace re-resolves them against a different set of
        // colours. Never released — this ViewModel and the workspace service are both singletons that
        // live as long as the window.
        _workspaces.FolderColorsChanged += (_, _) => ApplyColors();
        _workspaces.ActiveWorkspaceChanged += (_, _) => ApplyColors();
    }

    /// <summary>
    /// Raised once a document's buffer has been replaced by what is on disk, so the view can put the
    /// caret and the scroll position back.
    /// </summary>
    public event EventHandler<DocumentViewModel>? DocumentReloaded;

    /// <summary>
    /// Raised when a caret placement has been asked for on a document (see
    /// <see cref="DocumentViewModel.PendingSelection"/>), so the view can honour it even when nothing
    /// else about the editor changed.
    /// </summary>
    /// <remarks>
    /// Opening a search hit in a tab that is already the one on screen swaps no document and moves no
    /// binding, so without this the request would sit on the document unread. The handler and the
    /// document-swap path both <em>take</em> the request, so the two are safe in either order.
    /// </remarks>
    public event EventHandler<DocumentViewModel>? SelectionRequested;

    /// <summary>
    /// Gets the open documents, in the order their tabs appear.
    /// </summary>
    public ObservableCollection<DocumentViewModel> Documents { get; } = [];

    /// <summary>
    /// Gets or sets the document the editor is showing.
    /// </summary>
    public DocumentViewModel? ActiveDocument
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnActiveDocumentChanged();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether anything is open at all.
    /// </summary>
    public bool HasDocuments => Documents.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the editor has a document to show.
    /// </summary>
    /// <remarks>
    /// A different question from <see cref="HasDocuments"/>, and the one the editor's visibility is
    /// keyed on: the tab strip's two-way selection binding clears <see cref="ActiveDocument"/> by
    /// itself the moment the selected tab leaves <see cref="Documents"/>, and a text editor whose
    /// document has gone throws as soon as the pointer reaches it. Notified from the collection as
    /// well as from the property, because that write arrives from the view rather than from here.
    /// </remarks>
    public bool HasActiveDocument => ActiveDocument is not null;

    /// <summary>
    /// Gets a value indicating whether closing the editor right now would lose something.
    /// </summary>
    public bool HasUnsavedChanges
    {
        get
        {
            foreach (DocumentViewModel document in Documents)
            {
                if (document.HasUnsavedChanges)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Gets the status bar's document slot: the active file's name, or the empty state.
    /// </summary>
    public string FileInfoText => ActiveDocument?.TabTitle ?? NoFileOpen;

    /// <summary>
    /// Gets the status bar's caret slot.
    /// </summary>
    public string CaretPositionText => ActiveDocument is { } document
        ? $"Ln {document.CaretLine}, Col {document.CaretColumn}"
        : "Ln 1, Col 1";

    /// <summary>
    /// Gets the font family the editor renders with — one installed family, never a list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Resolved rather than passed through. The setting is empty on a fresh install, meaning "the best
    /// monospace font here", and a settings file carried from another machine can name a family this
    /// one does not have; both come out of the catalog as something that will actually render. What the
    /// file holds is left alone either way — see <see cref="IFontCatalog.ResolveEditorFamily"/>.
    /// </para>
    /// <para>
    /// A <see cref="Avalonia.Media.FontFamily"/> and not the resolved name it is built from, because
    /// that is the type the editor's <c>FontFamily</c> is: compiled bindings are on, and a compiled
    /// binding does not run the reflection binding's string conversions, so a name offered here would
    /// never reach the control and the editor would silently keep its inherited font.
    /// </para>
    /// </remarks>
    public FontFamily FontFamily => new(_fonts.ResolveEditorFamily(_settings.Value.EditorFontFamily));

    /// <summary>
    /// Gets the font size the editor renders with.
    /// </summary>
    public int FontSize => _settings.Value.EditorFontSize;

    /// <summary>
    /// Gets a value indicating whether the editor wraps long lines.
    /// </summary>
    public bool WordWrap => _settings.Value.WordWrap;

    /// <summary>
    /// Gets a value indicating whether the markdown editing assists apply to the keystroke the editor
    /// is handling.
    /// </summary>
    /// <remarks>
    /// Read per keystroke rather than bound: it is a question about the editor's mode at this instant,
    /// and under vim emulation (FEATURE-11AA) the answer changes several times a sentence.
    /// </remarks>
    public bool AssistsActive => _assists.IsActive;

    /// <summary>
    /// Gets the vim session, for the pane's own editor behaviour to attach the engine to.
    /// </summary>
    /// <remarks>
    /// Handed on rather than hidden: the engine belongs to the editor control, which is built by the
    /// view and has no container to ask, so the pane's ViewModel is where it reaches the session from.
    /// </remarks>
    public IVimSession Vim => _vim;

    /// <summary>
    /// Gets a value indicating whether vim emulation is switched on, and therefore whether the status
    /// bar has a mode to show at all.
    /// </summary>
    public bool IsVimEnabled => _vim.IsEnabled;

    /// <summary>
    /// Gets the status bar's mode slot.
    /// </summary>
    /// <remarks>
    /// Six characters whichever mode it is, so the chip beside it does not change width as the mode
    /// does. <c>V-LINE</c> is vim's own abbreviation for linewise visual.
    /// </remarks>
    public string VimModeText => _vim.Mode switch
    {
        VimMode.Insert => "INSERT",
        VimMode.Visual => "VISUAL",
        VimMode.VisualLine => "V-LINE",
        _ => "NORMAL",
    };

    /// <summary>
    /// Gets a value indicating whether the mode indicator should read as insert rather than normal.
    /// </summary>
    /// <remarks>
    /// A flag rather than a colour: the view turns it into a style class, which keeps the brushes in
    /// the theme where every other colour in this application lives.
    /// </remarks>
    public bool IsVimInsertMode => _vim.IsEnabled && (_vim.Mode == VimMode.Insert);

    /// <summary>
    /// Gets a value indicating whether the mode indicator should read as one of the visual modes.
    /// </summary>
    /// <remarks>
    /// One flag for both of them: what tells <c>VISUAL</c> from <c>V-LINE</c> is the word, and a
    /// second accent colour for a difference the text already carries would only be a third thing to
    /// learn.
    /// </remarks>
    public bool IsVimVisualMode => _vim.IsEnabled && (_vim.Mode is VimMode.Visual or VimMode.VisualLine);

    /// <summary>
    /// Gets what the engine has to say beside the mode, if anything.
    /// </summary>
    public string? VimStatusText => _vim.StatusMessage;

    /// <summary>
    /// Gets the ex/search line as it is being typed, or <see langword="null"/> when none is open.
    /// </summary>
    public string? VimCommandLineText => _vim.CommandLine;

    /// <summary>
    /// Gets a value indicating whether the command strip is up.
    /// </summary>
    /// <remarks>
    /// It shares the status message's slot, and the two are mutually exclusive by construction: the
    /// engine clears the message before it opens a command line. The flag is still what the view
    /// switches on, so the strip is never the thing left showing after vim is switched off.
    /// </remarks>
    public bool IsVimCommandLineVisible => _vim.IsEnabled && (_vim.CommandLine is not null);

    /// <summary>
    /// Gets the command that writes a document — the parameter, or the active one — to its own file.
    /// </summary>
    public IAsyncRelayCommand<DocumentViewModel?> SaveCommand { get; }

    /// <summary>
    /// Gets the command that asks where to write a document and writes it there.
    /// </summary>
    public IAsyncRelayCommand<DocumentViewModel?> SaveAsCommand { get; }

    /// <summary>
    /// Gets the command that writes every changed document.
    /// </summary>
    public IAsyncRelayCommand SaveAllCommand { get; }

    /// <summary>
    /// Gets the command that closes a document — the parameter, or the active one — asking first if
    /// it has unsaved changes.
    /// </summary>
    public IAsyncRelayCommand<DocumentViewModel?> CloseCommand { get; }

    /// <summary>
    /// Gets the command that activates the next tab, wrapping round.
    /// </summary>
    public IRelayCommand NextDocumentCommand { get; }

    /// <summary>
    /// Gets the command that activates the previous tab, wrapping round.
    /// </summary>
    public IRelayCommand PreviousDocumentCommand { get; }

    /// <summary>
    /// Gets the command that throws a changed buffer away and reads the file again.
    /// </summary>
    public IAsyncRelayCommand<DocumentViewModel?> ReloadCommand { get; }

    /// <summary>
    /// Gets the command that keeps the buffer and stops asking about the file underneath.
    /// </summary>
    public IRelayCommand<DocumentViewModel?> KeepMineCommand { get; }

    /// <summary>
    /// Opens a file in a tab, or activates the tab it is already open in.
    /// </summary>
    /// <param name="filePath">The file to open.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>A task that completes once the file, or the reason it could not be read, is on screen.</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    public Task OpenAsync(string filePath, CancellationToken cancellationToken = default)
        => OpenAsync(filePath, selection: null, cancellationToken);

    /// <summary>
    /// Opens a file — or activates the tab it is already open in — and puts the caret on a particular
    /// place in it, selecting what is there.
    /// </summary>
    /// <param name="filePath">The file to open.</param>
    /// <param name="selection">Where to land, and how much to select.</param>
    /// <param name="cancellationToken">Abandons the read.</param>
    /// <returns>A task that completes once the file, or the reason it could not be read, is on screen.</returns>
    /// <exception cref="ArgumentException"><paramref name="filePath"/> is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    /// <remarks>
    /// What a search result does. The placement is left on the document for the view to honour rather
    /// than applied here: this class knows nothing about a text editor control, and the tab being
    /// opened may not have one behind it for another dispatcher pass.
    /// </remarks>
    public Task RevealAsync(string filePath, DocumentSelection selection, CancellationToken cancellationToken = default)
        => OpenAsync(filePath, selection, cancellationToken);

    private async Task OpenAsync(string filePath, DocumentSelection? selection, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string full = Path.GetFullPath(filePath);

        if (Find(full) is { } already)
        {
            // De-duplication, and the reason it is here rather than in the tree: the same file can be
            // reached from two roots, from a search hit and from a restored session, and only this
            // class knows what is already open.
            if (selection is { } placement)
            {
                already.RequestSelection(placement);
            }

            ActiveDocument = already;
            Reveal(already, selection);

            return;
        }

        try
        {
            DocumentContent content = await DocumentFile.ReadAsync(full, cancellationToken);

            // Taken after the read, so an edit made while the bytes were in flight is caught by the
            // very first watcher event rather than being adopted as the baseline.
            DocumentViewModel document = new(full, content, FileStamp.For(full), _watcherFactory, _dispatcher);

            document.Color = ColorFor(full);
            document.ExternalChangeDetected += OnExternalChangeDetected;
            document.TextChanged += OnDocumentTextChanged;
            document.PropertyChanged += OnDocumentPropertyChanged;

            // Before the tab is activated, so the view finds the request already on the document the
            // moment the editor swaps to it.
            if (selection is { } pending)
            {
                document.RequestSelection(pending);
            }

            Documents.Add(document);
            ActiveDocument = document;
            Reveal(document, selection);
        }
        catch (FileOperationException expected)
        {
            // Its message is written for the user and states a limit they ran into, not a fault of
            // the application's.
            await ReportAsync("This file could not be opened", expected.Message, InfoBarSeverity.Warning);
        }
        catch (Exception failure)
            when (failure is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _logger.LogError(failure, "{FilePath} could not be read.", full);

            await ReportAsync(
                "This file could not be opened",
                $"{Path.GetFileName(full)} — {failure.Message}",
                InfoBarSeverity.Error);
        }
    }

    /// <summary>
    /// Closes every open document, asking about each one that has unsaved changes.
    /// </summary>
    /// <returns>
    /// <see langword="false"/> as soon as the user cancels one of the questions, leaving that document
    /// and everything after it open.
    /// </returns>
    /// <remarks>
    /// What the window's closing handler calls. A single "cancel" has to abort the whole shutdown, so
    /// the loop stops at the first one rather than carrying on and closing the rest.
    /// </remarks>
    public async Task<bool> ConfirmCloseAllAsync()
    {
        foreach (DocumentViewModel document in Documents.ToArray())
        {
            // Activated first: a question about a file the user cannot see is one they cannot answer.
            ActiveDocument = document;

            if (!await CloseAsync(document))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Files an image pasted into the active document and returns the link to insert.
    /// </summary>
    /// <param name="image">What was on the clipboard.</param>
    /// <returns>
    /// The markdown to insert at the caret, or <see langword="null"/> when nothing is open or the
    /// image could not be filed — in which case the user has already been told why.
    /// </returns>
    /// <remarks>
    /// Here rather than in the view because the image is filed beside a note, and which note that is
    /// is this class's question. The view brings the bitmap, because reading the clipboard needs the
    /// window it is on.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is <see langword="null"/>.</exception>
    public Task<string?> PasteImageAsync(IPastedImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        return ActiveDocument is { } document
            ? _imagePaste.SaveAsync(image, document.FilePath)
            : Task.FromResult<string?>(null);
    }

    /// <summary>
    /// Carries out an ex command typed into the editor's vim command line.
    /// </summary>
    /// <param name="command">What was asked for.</param>
    /// <returns>A task that completes when the save, the close or both have finished.</returns>
    /// <remarks>
    /// <para>
    /// Through the same two methods the tab strip and the context menu use, on purpose: <c>:q</c> on
    /// a document with unsaved changes has to raise the same Save/Discard/Cancel question that
    /// clicking the tab's close button does, and Cancel there has to leave the tab open. An ex
    /// command is a second way in, not a second set of rules.
    /// </para>
    /// <para>
    /// <c>:wq</c> closes only if the save worked. A write that failed has already told the user why,
    /// and closing over the top of that message would throw away the work it was about.
    /// </para>
    /// <para>
    /// <c>:q!</c> is the one command that does not ask. That is what the <c>!</c> means, and the
    /// engine only raises it for a line that spelled it out in full — so the tab goes, unsaved work
    /// and all, and nothing is said.
    /// </para>
    /// </remarks>
    public async Task ExecuteExCommandAsync(VimExCommand command)
    {
        if (ActiveDocument is not { } target)
        {
            return;
        }

        switch (command)
        {
            case VimExCommand.Write:
                await SaveAsync(target);
                break;

            case VimExCommand.Quit:
                await CloseAsync(target);
                break;

            case VimExCommand.QuitDiscard:
                await CloseAsync(target, discard: true);
                break;

            case VimExCommand.WriteQuit:
                if (await SaveAsync(target))
                {
                    await CloseAsync(target);
                }

                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Re-reads the appearance settings, after the settings window has been closed.
    /// </summary>
    public void OnSettingsChanged()
    {
        // First, because switching vim off has to reach the editor's key handlers whatever else in
        // this method does or does not change.
        _vim.OnSettingsChanged();

        OnPropertyChanged(nameof(FontFamily));
        OnPropertyChanged(nameof(FontSize));
        OnPropertyChanged(nameof(WordWrap));

        // Autosave may have just been switched on or off, or its delay changed. Re-arming from here
        // is what makes that take effect on the documents already open rather than only on the next.
        foreach (DocumentViewModel document in Documents)
        {
            ArmAutosave(document);
        }
    }

    /// <summary>
    /// Records a failure from the editor's syntax highlighting.
    /// </summary>
    /// <param name="failure">What the tokenizer threw.</param>
    /// <remarks>
    /// <para>
    /// Nothing reaches the user. The text is still there, still correct and still editable; only its
    /// colours are missing, and there is nothing they could do about it.
    /// </para>
    /// <para>
    /// Once per run, and interlocked because tokenizing happens on a background thread. A grammar
    /// that throws throws for every line of every document, so without the guard the log file would
    /// be nothing else.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is <see langword="null"/>.</exception>
    public void OnHighlightingFailed(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        if (Interlocked.Exchange(ref _highlightingFailureLogged, 1) != 0)
        {
            return;
        }

        _logger.LogError(failure, "Syntax highlighting failed. Further failures this run are not logged.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _autosave.CancelAll();
        Documents.CollectionChanged -= OnDocumentsChanged;
        _vim.Changed -= OnVimChanged;

        foreach (DocumentViewModel document in Documents)
        {
            Detach(document);
            document.Dispose();
        }

        Documents.Clear();
        DocumentReloaded = null;
    }

    private static StringComparison PathComparison
        => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    // Raised whether or not activating the tab moved anything, because the one case that moves nothing
    // — the wanted file is already the tab on screen — is the case the view would otherwise never hear
    // about. Honouring the request is idempotent, so a document swap that has already taken it leaves
    // this handler with nothing to do.
    private void Reveal(DocumentViewModel document, DocumentSelection? selection)
    {
        if (selection is not null)
        {
            SelectionRequested?.Invoke(this, document);
        }
    }

    private DocumentViewModel? Find(string fullPath)
    {
        foreach (DocumentViewModel document in Documents)
        {
            if (string.Equals(document.FilePath, fullPath, PathComparison))
            {
                return document;
            }
        }

        return null;
    }

    private bool HasAnyDocument(DocumentViewModel? document) => (document ?? ActiveDocument) is not null;

    private bool CanSave(DocumentViewModel? document) => HasAnyDocument(document);

    private async Task<bool> SaveAsync(DocumentViewModel? document)
    {
        if ((document ?? ActiveDocument) is not { } target)
        {
            return false;
        }

        return await WriteAsync(target, target.FilePath, isAutosave: false);
    }

    private async Task SaveAsAsync(DocumentViewModel? document)
    {
        if ((document ?? ActiveDocument) is not { } target)
        {
            return;
        }

        string? chosen = await AskAsync(() => _prompts.AskWhereToSaveAsync(
            target.Title,
            Path.GetDirectoryName(target.FilePath)));

        if (string.IsNullOrWhiteSpace(chosen))
        {
            return;
        }

        await WriteAsync(target, chosen, isAutosave: false);
    }

    private async Task SaveAllAsync()
    {
        foreach (DocumentViewModel document in Documents.ToArray())
        {
            if (document.HasUnsavedChanges)
            {
                await WriteAsync(document, document.FilePath, isAutosave: false);
            }
        }
    }

    private async Task<bool> WriteAsync(DocumentViewModel document, string targetPath, bool isAutosave)
    {
        _autosave.Cancel(document);

        try
        {
            // Read on this thread on purpose: the document verifies that whoever touches it is the
            // thread that built it, and that is this one. The version is taken with the text and
            // travels with it: the write is awaited, and a keystroke can land while it is in flight.
            string text = document.Document.Text;
            ITextSourceVersion written = document.Document.Version;

            await DocumentFile.WriteAsync(targetPath, text, document.Format);

            document.MarkSaved(targetPath, FileStamp.For(targetPath), written);
            _autosaveFailed.Remove(document);

            return true;
        }
        catch (Exception failure)
            when (failure is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _logger.LogError(failure, "{FilePath} could not be written.", targetPath);

            if (isAutosave)
            {
                // Told once, then left alone: an autosave that cannot write will not be able to write
                // on the next keystroke either, and the user has an explicit Save to try with.
                _autosaveFailed.Add(document);
            }

            await ReportAsync(
                isAutosave ? "This file could not be saved automatically" : "This file could not be saved",
                $"{Path.GetFileName(targetPath)} — {failure.Message}",
                InfoBarSeverity.Error);

            return false;
        }
    }

    // 'discard' is the one way past the unsaved-changes question, and it is only ever true for a vim
    // ':q!' or 'ZQ': the user has typed the request to lose the work, so asking again would be asking
    // the same question twice.
    private async Task<bool> CloseAsync(DocumentViewModel? document, bool discard = false)
    {
        if ((document ?? ActiveDocument) is not { } target)
        {
            return true;
        }

        if (target.HasUnsavedChanges && !discard)
        {
            UnsavedChangesChoice choice = await AskAsync(
                () => _prompts.AskAboutUnsavedChangesAsync(target.TabTitle));

            switch (choice)
            {
                case UnsavedChangesChoice.Cancel:
                    ArmAutosave(target);
                    return false;

                case UnsavedChangesChoice.Save when !await WriteAsync(target, target.FilePath, isAutosave: false):

                    // The save the user asked for failed and has already been reported. Closing now
                    // would throw away the work they just tried to keep.
                    return false;

                default:
                    break;
            }
        }

        Remove(target);

        return true;
    }

    private void Remove(DocumentViewModel document)
    {
        int index = Documents.IndexOf(document);

        // Read before the collection is touched, and not after. The tab strip binds its selection to
        // ActiveDocument two-way, so taking the selected tab out of Documents makes the ListBox clear
        // its own selection and write that null straight back here — leaving nothing to compare the
        // closed document against by the time the neighbour has to be picked.
        bool wasActive = ReferenceEquals(ActiveDocument, document);

        _autosave.Cancel(document);
        _autosaveFailed.Remove(document);
        Detach(document);
        Documents.Remove(document);

        if (wasActive)
        {
            // The tab that took its place, or the one before it when it was the last: closing a tab
            // should leave the neighbour on screen rather than nothing.
            ActiveDocument = Documents.Count == 0
                ? null
                : Documents[Math.Min(index, Documents.Count - 1)];
        }

        document.Dispose();
    }

    private void Detach(DocumentViewModel document)
    {
        document.ExternalChangeDetected -= OnExternalChangeDetected;
        document.TextChanged -= OnDocumentTextChanged;
        document.PropertyChanged -= OnDocumentPropertyChanged;
    }

    private void Step(int offset)
    {
        if ((Documents.Count == 0) || (ActiveDocument is null))
        {
            return;
        }

        int index = Documents.IndexOf(ActiveDocument);
        int next = ((index + offset) % Documents.Count + Documents.Count) % Documents.Count;

        ActiveDocument = Documents[next];
    }

    private void KeepMine(DocumentViewModel? document) => (document ?? ActiveDocument)?.KeepMine();

    private async Task ReloadFromDiskAsync(DocumentViewModel? document)
    {
        if ((document ?? ActiveDocument) is { } target)
        {
            await ReloadAsync(target);
        }
    }

    private async Task ReloadAsync(DocumentViewModel document)
    {
        try
        {
            DocumentContent content = await DocumentFile.ReadAsync(document.FilePath);

            if (!Documents.Contains(document))
            {
                // Closed while the read was in flight.
                return;
            }

            document.ApplyReload(content, FileStamp.For(document.FilePath));
            _autosave.Cancel(document);

            DocumentReloaded?.Invoke(this, document);
        }
        catch (FileOperationException expected)
        {
            await ReportAsync("This file could not be re-read", expected.Message, InfoBarSeverity.Warning);
        }
        catch (Exception failure)
            when (failure is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _logger.LogError(failure, "{FilePath} could not be re-read.", document.FilePath);

            await ReportAsync(
                "This file could not be re-read",
                $"{document.Title} — {failure.Message}",
                InfoBarSeverity.Error);
        }
    }

    private void OnExternalChangeDetected(object? sender, EventArgs e)
    {
        if (sender is not DocumentViewModel document)
        {
            return;
        }

        if (document.IsMissing)
        {
            // Nothing to read back. The tab says so and the buffer stays; saving it puts the file
            // back, which is the only useful thing left to offer.
            return;
        }

        if (document.HasUnsavedChanges)
        {
            // Two versions and no way to tell which the user wants. The bar over the editor asks;
            // until it is answered the buffer is left exactly as it is.
            document.FlagExternalChange();
            return;
        }

        // Nothing to lose, so nothing to ask: an unchanged buffer is silently brought up to date, the
        // way an editor is expected to behave when a file is regenerated underneath it.
        _dispatcher.Post(() => ReloadAsync(document));
    }

    private void OnDocumentTextChanged(object? sender, EventArgs e)
    {
        if (sender is DocumentViewModel document)
        {
            ArmAutosave(document);
        }
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(DocumentViewModel.IsDirty):
            case nameof(DocumentViewModel.IsMissing):
                OnPropertyChanged(nameof(HasUnsavedChanges));
                SaveAllCommand.NotifyCanExecuteChanged();

                // Armed from the flag as well as from the text changing, so the timer follows what
                // the flag says: it is cancelled the moment a save, an undo back to the saved state
                // or a reload leaves this document with nothing to write.
                if (sender is DocumentViewModel changed)
                {
                    ArmAutosave(changed);
                }

                if (sender == ActiveDocument)
                {
                    OnPropertyChanged(nameof(FileInfoText));
                }

                break;

            case nameof(DocumentViewModel.CaretLine):
            case nameof(DocumentViewModel.CaretColumn):
                if (sender == ActiveDocument)
                {
                    OnPropertyChanged(nameof(CaretPositionText));
                }

                break;

            case nameof(DocumentViewModel.FilePath):

                // A Save As can move the file into another folder, and the tab's colour follows the
                // file rather than the buffer it started as.
                if (sender is DocumentViewModel moved)
                {
                    moved.Color = ColorFor(moved.FilePath);
                }

                if (sender == ActiveDocument)
                {
                    OnPropertyChanged(nameof(FileInfoText));
                }

                break;

            default:
                break;
        }
    }

    // The colour a file inherits from the open workspace's coloured folders. Resolve, not ColorOf:
    // a file is never coloured itself, and the mark on the tab is there to name the folder it came
    // from.
    private FolderColor ColorFor(string filePath)
        => FolderColorMap.Resolve(_workspaces.ActiveWorkspace?.FolderColors, filePath);

    private void ApplyColors()
    {
        foreach (DocumentViewModel document in Documents)
        {
            document.Color = ColorFor(document.FilePath);
        }
    }

    private void OnDocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasDocuments));
        OnPropertyChanged(nameof(HasActiveDocument));
        OnPropertyChanged(nameof(HasUnsavedChanges));

        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
        SaveAllCommand.NotifyCanExecuteChanged();
        CloseCommand.NotifyCanExecuteChanged();
        NextDocumentCommand.NotifyCanExecuteChanged();
        PreviousDocumentCommand.NotifyCanExecuteChanged();
    }

    private void OnActiveDocumentChanged()
    {
        OnPropertyChanged(nameof(HasActiveDocument));
        OnPropertyChanged(nameof(FileInfoText));
        OnPropertyChanged(nameof(CaretPositionText));

        SaveCommand.NotifyCanExecuteChanged();
        SaveAsCommand.NotifyCanExecuteChanged();
        CloseCommand.NotifyCanExecuteChanged();
    }

    private void ArmAutosave(DocumentViewModel document)
    {
        if (_promptOpen
            || _disposed
            || !_settings.Value.AutosaveEnabled
            || !document.IsDirty
            || _autosaveFailed.Contains(document))
        {
            _autosave.Cancel(document);
            return;
        }

        _autosave.Schedule(
            document,
            TimeSpan.FromSeconds(_settings.Value.AutosaveDelaySeconds),
            () => AutosaveAsync(document));
    }

    private async Task AutosaveAsync(DocumentViewModel document)
    {
        if (_promptOpen || _disposed || !document.IsDirty || !Documents.Contains(document))
        {
            return;
        }

        // Never over a file that changed underneath: the buffer and the disk disagree, the user has
        // been asked which one wins, and writing before they answer would answer it for them.
        if (document.HasExternalChange)
        {
            return;
        }

        await WriteAsync(document, document.FilePath, isAutosave: true);
    }

    // Every question to the user goes through here so that no autosave fires behind an open dialog:
    // the user is being asked about this very document, and a timer writing it underneath the
    // question would make their answer meaningless.
    private async Task<T> AskAsync<T>(Func<Task<T>> ask)
    {
        _promptOpen = true;
        _autosave.CancelAll();

        try
        {
            return await ask();
        }
        finally
        {
            _promptOpen = false;

            foreach (DocumentViewModel document in Documents)
            {
                ArmAutosave(document);
            }
        }
    }

    private void OnVimChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsVimEnabled));
        OnPropertyChanged(nameof(VimModeText));
        OnPropertyChanged(nameof(IsVimInsertMode));
        OnPropertyChanged(nameof(IsVimVisualMode));
        OnPropertyChanged(nameof(VimStatusText));
        OnPropertyChanged(nameof(VimCommandLineText));
        OnPropertyChanged(nameof(IsVimCommandLineVisible));
    }

    private Task ReportAsync(string title, string message, InfoBarSeverity severity)
        => _infoBarService.ShowAsync(bar =>
        {
            bar.Title = title;
            bar.Message = message;
            bar.Severity = severity;
        });
}
