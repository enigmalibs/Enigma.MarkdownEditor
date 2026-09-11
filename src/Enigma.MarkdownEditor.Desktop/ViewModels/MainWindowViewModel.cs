using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Avalonia.Desktop.Controls.InfoBar;
using Enigma.Avalonia.Desktop.Services;
using Enigma.MarkdownEditor.Core;
using Enigma.MarkdownEditor.Core.Session;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// State for the application shell: the window title, which of the three panes are shown, and the
/// three status-bar slots. The pane contents themselves are child ViewModels the shell only hosts.
/// </summary>
public sealed class MainWindowViewModel : ObservableObject
{
    private readonly ISettingsDialogService _settingsDialogService;
    private readonly IAboutDialogService _aboutDialogService;
    private readonly IFirstRunDialogService _firstRunDialogService;
    private readonly IInfoBarService _infoBarService;
    private readonly StartupNotice _startupNotice;
    private readonly FirstRunState _firstRun;
    private readonly ISessionService _session;
    private readonly ILogger<MainWindowViewModel> _logger;

    // What the session held when this ViewModel was built, kept apart from the live session state the
    // recorders below overwrite: the window applies these once and must not be handed a value that has
    // since become "where the window is now".
    private readonly WindowPlacement? _restoredWindow;
    private readonly PaneLayout? _restoredPanes;

    // Set while the saved session is being put back, so that restoring a value does not immediately
    // record it again — a write per restored tab, saying what the file already said.
    private bool _restoring;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    /// <param name="workspaces">The switcher and sidebar state, which the shell only hosts.</param>
    /// <param name="fileTree">The sidebar's file tree.</param>
    /// <param name="search">The sidebar's other tab: workspace-wide search.</param>
    /// <param name="documents">The editor pane: the open tabs and everything done to them.</param>
    /// <param name="preview">The preview pane, which follows whichever tab is active.</param>
    /// <param name="settingsDialogService">Opens the settings dialog from the toolbar.</param>
    /// <param name="aboutDialogService">Opens the About dialog from the toolbar.</param>
    /// <param name="firstRunDialogService">Asks the first-run questions, on a first run only.</param>
    /// <param name="infoBarService">Surfaces <paramref name="startupNotice"/> once there is a window.</param>
    /// <param name="startupNotice">What composition needs to tell the user, if anything.</param>
    /// <param name="firstRun">Whether this is the very first start, decided during composition.</param>
    /// <param name="session">Where the editor was left, and where everything it tracks is recorded.</param>
    /// <param name="logger">Records a command that could not run.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public MainWindowViewModel(
        WorkspacesViewModel workspaces,
        FileTreeViewModel fileTree,
        SearchViewModel search,
        DocumentManagerViewModel documents,
        MarkdownPreviewViewModel preview,
        ISettingsDialogService settingsDialogService,
        IAboutDialogService aboutDialogService,
        IFirstRunDialogService firstRunDialogService,
        IInfoBarService infoBarService,
        StartupNotice startupNotice,
        FirstRunState firstRun,
        ISessionService session,
        ILogger<MainWindowViewModel> logger)
    {
        Workspaces = workspaces ?? throw new ArgumentNullException(nameof(workspaces));
        FileTree = fileTree ?? throw new ArgumentNullException(nameof(fileTree));
        Search = search ?? throw new ArgumentNullException(nameof(search));
        Documents = documents ?? throw new ArgumentNullException(nameof(documents));
        Preview = preview ?? throw new ArgumentNullException(nameof(preview));
        _settingsDialogService = settingsDialogService ?? throw new ArgumentNullException(nameof(settingsDialogService));
        _aboutDialogService = aboutDialogService ?? throw new ArgumentNullException(nameof(aboutDialogService));
        _firstRunDialogService = firstRunDialogService ?? throw new ArgumentNullException(nameof(firstRunDialogService));
        _infoBarService = infoBarService ?? throw new ArgumentNullException(nameof(infoBarService));
        _startupNotice = startupNotice ?? throw new ArgumentNullException(nameof(startupNotice));
        _firstRun = firstRun ?? throw new ArgumentNullException(nameof(firstRun));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _restoredWindow = _session.State.Window;
        _restoredPanes = _session.State.Panes;

        OpenSettingsCommand = new AsyncRelayCommand(OpenSettingsAsync);
        OpenAboutCommand = new AsyncRelayCommand(OpenAboutAsync);
        ThrowTestExceptionCommand = new RelayCommand(ThrowTestException);
        CycleViewModeCommand = new RelayCommand(CycleViewMode);

        // The status-bar slots the editor owns are mirrored rather than bound through: the status bar
        // is the shell's, and what it says about the caret, the file and the vim mode is a detail of
        // whichever tab is active.
        Documents.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(DocumentManagerViewModel.FileInfoText):
                    OnPropertyChanged(nameof(FileInfoText));

                    // Save As renames a document in place: the tab list is the same objects holding
                    // different paths, so the collection never changes and only this says so.
                    RecordDocuments();
                    break;
                case nameof(DocumentManagerViewModel.ActiveDocument):
                    RecordDocuments();
                    break;
                case nameof(DocumentManagerViewModel.CaretPositionText):
                    OnPropertyChanged(nameof(CaretPositionText));
                    break;
                case nameof(DocumentManagerViewModel.VimModeText):
                    OnPropertyChanged(nameof(VimModeText));
                    break;
                case nameof(DocumentManagerViewModel.IsVimEnabled):
                    OnPropertyChanged(nameof(IsVimEnabled));
                    break;
                case nameof(DocumentManagerViewModel.IsVimInsertMode):
                    OnPropertyChanged(nameof(IsVimInsertMode));
                    break;
                case nameof(DocumentManagerViewModel.IsVimVisualMode):
                    OnPropertyChanged(nameof(IsVimVisualMode));
                    break;
                case nameof(DocumentManagerViewModel.VimStatusText):
                    OnPropertyChanged(nameof(VimStatusText));
                    break;
                case nameof(DocumentManagerViewModel.VimCommandLineText):
                    OnPropertyChanged(nameof(VimCommandLineText));
                    break;
                case nameof(DocumentManagerViewModel.IsVimCommandLineVisible):
                    OnPropertyChanged(nameof(IsVimCommandLineVisible));
                    break;
                default:
                    break;
            }
        };

        Documents.Documents.CollectionChanged += (_, _) => RecordDocuments();

        // Applied here rather than when the window opens, and in this order: the window reads the pane
        // flags while it is being built, so a mode arriving later would show as a pane opening and
        // closing again in front of the user.
        _restoring = true;
        try
        {
            ViewMode = _session.State.ViewMode;
            SidebarTab = _session.State.SidebarTab;
            IsSidebarVisible = _session.State.IsSidebarVisible;
        }
        finally
        {
            _restoring = false;
        }
    }

    /// <summary>
    /// The window title.
    /// </summary>
    public string Title => AppInfo.DisplayName;

    /// <summary>
    /// The toolbar switcher and the sidebar's workspace state.
    /// </summary>
    /// <remarks>
    /// Held as a child ViewModel rather than flattened into this one: the shell's own job is the
    /// window — title, panes, status bar — and workspaces are a feature that happens to be shown in
    /// two of its corners.
    /// </remarks>
    public WorkspacesViewModel Workspaces { get; }

    /// <summary>
    /// The sidebar's file tree.
    /// </summary>
    public FileTreeViewModel FileTree { get; }

    /// <summary>
    /// The sidebar's Search tab: the query, and what it found across the workspace.
    /// </summary>
    public SearchViewModel Search { get; }

    /// <summary>
    /// The editor pane: the open tabs, the active document, and the save and close flows.
    /// </summary>
    public DocumentManagerViewModel Documents { get; }

    /// <summary>
    /// The preview pane: the markdown of the active document, and the folder its images resolve
    /// against.
    /// </summary>
    /// <remarks>
    /// A child ViewModel for the same reason the others are: the shell's job is the window, and what
    /// the right-hand pane shows is a feature that happens to be drawn in it. It watches
    /// <see cref="Documents"/> itself rather than being fed by this class.
    /// </remarks>
    public MarkdownPreviewViewModel Preview { get; }

    /// <summary>
    /// Whether the workspace sidebar is shown. Bound two-way to the toolbar toggle; the view mirrors
    /// it onto the pane's grid column so a collapsed pane gives its width back to the editor.
    /// </summary>
    /// <remarks>
    /// Independent of <see cref="ViewMode"/>, and deliberately: the sidebar is where files are found
    /// and the mode is about how the open one is being worked on. Reading a note full-width is a
    /// reason to hide the editor, not a reason to lose the tree.
    /// </remarks>
    public bool IsSidebarVisible
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                RecordShell();
            }
        }
    } = true;

    /// <summary>
    /// Which of the sidebar's two tabs is showing.
    /// </summary>
    /// <remarks>
    /// The same shape as <see cref="ViewMode"/> one column to the right, and for the same reason: two
    /// toggles drawn as one segmented control, where the pair has no state in which neither is chosen.
    /// </remarks>
    public SidebarTab SidebarTab
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsExplorerTab));
            OnPropertyChanged(nameof(IsSearchTab));
            RecordShell();
        }
    } = SidebarTab.Explorer;

    /// <summary>
    /// Whether the sidebar is showing the file tree. Bound to its segment.
    /// </summary>
    public bool IsExplorerTab
    {
        get => SidebarTab == SidebarTab.Explorer;
        set => SelectTab(value, SidebarTab.Explorer, nameof(IsExplorerTab));
    }

    /// <summary>
    /// Whether the sidebar is showing search. Bound to its segment.
    /// </summary>
    public bool IsSearchTab
    {
        get => SidebarTab == SidebarTab.Search;
        set => SelectTab(value, SidebarTab.Search, nameof(IsSearchTab));
    }

    /// <summary>
    /// Which of the editor and the preview the window shows.
    /// </summary>
    /// <remarks>
    /// The single source of both panes' visibility. One value rather than a flag each because there is
    /// no arrangement in which neither is shown, and two toggles would be able to express one: what
    /// the toolbar's three buttons, <see cref="CycleViewModeCommand"/> and the grid columns all read
    /// is this.
    /// </remarks>
    public ViewMode ViewMode
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsEditorVisible));
            OnPropertyChanged(nameof(IsPreviewVisible));
            OnPropertyChanged(nameof(IsEditorOnly));
            OnPropertyChanged(nameof(IsSplit));
            OnPropertyChanged(nameof(IsPreviewOnly));
            RecordShell();
        }
    } = ViewMode.Split;

    /// <summary>
    /// Whether the editor pane is shown. Derived from <see cref="ViewMode"/>; the view mirrors it onto
    /// the pane's grid column, the same way it does the sidebar's.
    /// </summary>
    public bool IsEditorVisible => ViewMode is ViewMode.EditorOnly or ViewMode.Split;

    /// <summary>
    /// Whether the markdown preview pane is shown. Derived from <see cref="ViewMode"/>.
    /// </summary>
    public bool IsPreviewVisible => ViewMode is ViewMode.PreviewOnly or ViewMode.Split;

    /// <summary>
    /// Whether the editor is the only pane. Bound to its toolbar segment.
    /// </summary>
    public bool IsEditorOnly
    {
        get => ViewMode == ViewMode.EditorOnly;
        set => Select(value, ViewMode.EditorOnly, nameof(IsEditorOnly));
    }

    /// <summary>
    /// Whether both panes are shown side by side. Bound to its toolbar segment, and to the splitter
    /// between the two panes — which has nothing to drag in either of the other modes.
    /// </summary>
    public bool IsSplit
    {
        get => ViewMode == ViewMode.Split;
        set => Select(value, ViewMode.Split, nameof(IsSplit));
    }

    /// <summary>
    /// Whether the preview is the only pane. Bound to its toolbar segment.
    /// </summary>
    public bool IsPreviewOnly
    {
        get => ViewMode == ViewMode.PreviewOnly;
        set => Select(value, ViewMode.PreviewOnly, nameof(IsPreviewOnly));
    }

    /// <summary>
    /// Status-bar slot for the current vim mode.
    /// </summary>
    public string VimModeText => Documents.VimModeText;

    /// <summary>
    /// Whether the mode slot is shown at all. Hidden while vim emulation is switched off, because
    /// then there is no mode to be in.
    /// </summary>
    public bool IsVimEnabled => Documents.IsVimEnabled;

    /// <summary>
    /// Whether the mode slot should read as insert rather than normal. The view turns it into a style
    /// class, which keeps the colours in the theme.
    /// </summary>
    public bool IsVimInsertMode => Documents.IsVimInsertMode;

    /// <summary>
    /// Whether the mode slot should read as one of the visual modes, the same way.
    /// </summary>
    public bool IsVimVisualMode => Documents.IsVimVisualMode;

    /// <summary>
    /// Status-bar slot for what the vim engine has to say, if anything.
    /// </summary>
    public string? VimStatusText => Documents.VimStatusText;

    /// <summary>
    /// The ex/search line being typed, which shares the message slot with <see cref="VimStatusText"/>.
    /// </summary>
    public string? VimCommandLineText => Documents.VimCommandLineText;

    /// <summary>
    /// Whether the command strip is up, and therefore which of the two owns the slot.
    /// </summary>
    public bool IsVimCommandLineVisible => Documents.IsVimCommandLineVisible;

    /// <summary>
    /// Status-bar slot for the caret position in the active document.
    /// </summary>
    public string CaretPositionText => Documents.CaretPositionText;

    /// <summary>
    /// Status-bar slot for the active document: the open file's name, or the empty state.
    /// </summary>
    public string FileInfoText => Documents.FileInfoText;

    /// <summary>
    /// Opens the settings dialog. Awaits it, so the command stays disabled while it is open.
    /// </summary>
    public IAsyncRelayCommand OpenSettingsCommand { get; }

    /// <summary>
    /// Opens the About dialog. Awaits it, so the command stays disabled while it is up.
    /// </summary>
    /// <remarks>
    /// From the toolbar rather than from a settings row: the <c>ContentDialog</c> host is a single
    /// control, so a dialog raised while the settings dialog is up would replace it rather than
    /// appear over it.
    /// </remarks>
    public IAsyncRelayCommand OpenAboutCommand { get; }

    /// <summary>
    /// Gets the command that raises an exception on the UI thread, to prove the crash pipeline works.
    /// </summary>
    /// <remarks>
    /// It lives on the shell, not in a settings row, because that is where the crash dialog can
    /// actually be seen: the <c>ContentDialog</c> host is a single control, so a dialog raised while
    /// the settings dialog is up would replace it rather than appear over it.
    /// </remarks>
    public IRelayCommand ThrowTestExceptionCommand { get; }

    /// <summary>
    /// Gets the command behind <c>Ctrl+Shift+V</c>: advances to the next view mode, wrapping round.
    /// </summary>
    /// <remarks>
    /// One shortcut for three modes rather than three shortcuts, because the modes are an order the
    /// hand learns — write, compare, read — and the toolbar is already there for jumping straight to
    /// one.
    /// </remarks>
    public IRelayCommand CycleViewModeCommand { get; }

    /// <summary>
    /// Gets a value indicating whether the diagnostics button is shown. Debug builds only.
    /// </summary>
    /// <remarks>
    /// <see cref="ThrowTestExceptionCommand"/> itself is compiled either way — compiled bindings are
    /// resolved at build time, so a command the XAML references cannot be conditional on the
    /// configuration — but nothing in a Release build can reach it.
    /// </remarks>
    public bool ShowDiagnostics =>
#if DEBUG
        true;
#else
        false;
#endif

    /// <summary>
    /// Gets where the window was when the editor last closed, or <see langword="null"/> when nothing
    /// was remembered.
    /// </summary>
    /// <remarks>
    /// In device pixels and unclamped, because only the window knows what screens exist to clamp it
    /// against — see <see cref="Core.Session.WindowPlacements.Clamp"/>. The shell carries it rather
    /// than the window reading the session itself, so the view keeps talking to one ViewModel.
    /// </remarks>
    public WindowPlacement? RestoredWindow => _restoredWindow;

    /// <summary>
    /// Gets how the panes were divided when the editor last closed, or <see langword="null"/> when
    /// nothing was remembered.
    /// </summary>
    public PaneLayout? RestoredPanes => _restoredPanes;

    /// <summary>
    /// Records where the window is now.
    /// </summary>
    /// <param name="placement">The window's restored bounds, in device pixels, and its state.</param>
    /// <exception cref="ArgumentNullException"><paramref name="placement"/> is <see langword="null"/>.</exception>
    public void ReportWindowPlacement(WindowPlacement placement) => _session.RecordWindow(placement);

    /// <summary>
    /// Records how the panes are divided now.
    /// </summary>
    /// <param name="panes">The sidebar's width and the editor/preview ratio.</param>
    /// <exception cref="ArgumentNullException"><paramref name="panes"/> is <see langword="null"/>.</exception>
    public void ReportPaneLayout(PaneLayout panes) => _session.RecordPanes(panes);

    /// <summary>
    /// Runs the work that needs a visible window: the first-run questions, the tabs the last session
    /// left open, and whatever composition could not report.
    /// </summary>
    /// <returns>A task that completes once the tabs are back and the notice, if any, is on screen.</returns>
    /// <remarks>
    /// Called from the window's <c>Opened</c> handler rather than the constructor — the dialog and
    /// InfoBar hosts are controls in that window, so there is nothing to show into before it exists,
    /// and reopening a note reads it off the disk, which is not something to do while a window is
    /// being built.
    /// </remarks>
    public async Task OnOpenedAsync()
    {
        // First, and before the restore: on a genuine first run there is no session to put back, and
        // choosing the workspace before the tree first reads its folders saves reading them twice.
        await ShowFirstRunAsync();

        await RestoreDocumentsAsync();

        if (!_startupNotice.HasNotice)
        {
            return;
        }

        await _infoBarService.ShowAsync(bar =>
        {
            bar.Title = _startupNotice.Title;
            bar.Message = _startupNotice.Message;

            // Warning, not Error: whatever went wrong, the editor is running and usable.
            bar.Severity = InfoBarSeverity.Warning;
        });
    }

    private async Task ShowFirstRunAsync()
    {
        if (!_firstRun.IsFirstRun)
        {
            return;
        }

        try
        {
            await _firstRunDialogService.ShowAsync();

            // The same three seams the settings dialog is followed by, and for the same reason: the
            // dialog edited the very AppSettings object these hold, and nothing else tells them. The
            // theme needed no seam — it was applied as it was chosen.
            Documents.OnSettingsChanged();
            Preview.OnSettingsChanged();
            await FileTree.OnSettingsChangedAsync();
        }
        catch (Exception exception)
        {
            // Caught as broadly as the two commands below catch. This one runs before the session is
            // restored, so an unguarded failure would cost the user their tabs as well as the
            // questions — on the one start where nothing has gone right yet.
            _logger.LogError(exception, "The welcome dialog could not be shown.");

            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Setup could not be shown";
                bar.Message = $"{exception.GetType().Name}: {exception.Message}";
                bar.Severity = InfoBarSeverity.Error;
            });
        }
    }

    private void RecordShell()
    {
        if (!_restoring)
        {
            _session.RecordShell(ViewMode, SidebarTab, IsSidebarVisible);
        }
    }

    private void RecordDocuments()
    {
        if (_restoring)
        {
            return;
        }

        List<string> paths = [];

        foreach (DocumentViewModel document in Documents.Documents)
        {
            paths.Add(document.FilePath);
        }

        _session.RecordDocuments(
            paths,
            Documents.ActiveDocument is { } active ? Documents.Documents.IndexOf(active) : -1);
    }

    private async Task RestoreDocumentsAsync()
    {
        SessionDocuments restore = _session.DocumentsToRestore();
        DocumentViewModel? active = null;

        _restoring = true;
        try
        {
            for (int index = 0; index < restore.Paths.Count; index++)
            {
                int before = Documents.Documents.Count;

                // Sequentially, not all at once: the tabs have to come back in the order they were
                // left, and the editor pane appends each one as it opens.
                await Documents.OpenAsync(restore.Paths[index]);

                // A note that existed when the session was resolved and still would not open — a
                // permission that has gone, a file held by something else — has already reported
                // itself through the editor pane. Here it is one tab fewer, and the tab to activate
                // has to be taken from what did open rather than from what was asked for.
                if ((index == restore.ActiveIndex) && (Documents.Documents.Count > before))
                {
                    active = Documents.Documents[^1];
                }
            }

            if (active is not null)
            {
                Documents.ActiveDocument = active;
            }
        }
        finally
        {
            _restoring = false;
        }

        // What actually came back is the session now — so notes that have gone stop being asked for at
        // every start, and a tab list belonging to another workspace stops being carried around.
        RecordDocuments();
    }

    private async Task OpenSettingsAsync()
    {
        try
        {
            await _settingsDialogService.ShowAsync();

            // The settings dialog edits the very AppSettings object these three hold, and nothing
            // tells them it changed. This is the moment it can have: the dialog is closed, so
            // whatever was edited is final.
            Documents.OnSettingsChanged();
            Preview.OnSettingsChanged();
            await FileTree.OnSettingsChangedAsync();
        }
        catch (Exception exception)
        {
            // Caught broadly and on purpose. An AsyncRelayCommand parks a faulted task rather than
            // letting the exception escape, so without this the whole failure — dialog, save and all
            // — would vanish silently and the gear button would simply appear to do nothing.
            _logger.LogError(exception, "The settings dialog could not be opened.");

            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "Settings could not be opened";
                bar.Message = $"{exception.GetType().Name}: {exception.Message}";
                bar.Severity = InfoBarSeverity.Error;
            });
        }
    }

    private async Task OpenAboutAsync()
    {
        try
        {
            await _aboutDialogService.ShowAsync();
        }
        catch (Exception exception)
        {
            // Caught as broadly as OpenSettingsAsync catches, and for the same reason: an
            // AsyncRelayCommand parks a faulted task rather than letting it escape, so an
            // unregistered dialog host would leave the button looking simply dead.
            _logger.LogError(exception, "The About dialog could not be opened.");

            await _infoBarService.ShowAsync(bar =>
            {
                bar.Title = "About could not be opened";
                bar.Message = $"{exception.GetType().Name}: {exception.Message}";
                bar.Severity = InfoBarSeverity.Error;
            });
        }
    }

    // The toolbar's three segments are a radio group made of toggles, and a toggle can be switched
    // off. Turning the active one off would mean asking for a window with neither pane in it, so the
    // refusal is a notification that puts the button back where it was.
    private void Select(bool selected, ViewMode mode, string propertyName)
    {
        if (selected)
        {
            ViewMode = mode;
        }
        else
        {
            OnPropertyChanged(propertyName);
        }
    }

    // The sidebar's two segments are the same radio group made of toggles as the view modes above, and
    // switching the chosen one off would leave the pane showing neither. Refused the same way.
    private void SelectTab(bool selected, SidebarTab tab, string propertyName)
    {
        if (selected)
        {
            SidebarTab = tab;
        }
        else
        {
            OnPropertyChanged(propertyName);
        }
    }

    private void CycleViewMode()
        => ViewMode = ViewMode switch
        {
            ViewMode.EditorOnly => ViewMode.Split,
            ViewMode.Split => ViewMode.PreviewOnly,
            _ => ViewMode.EditorOnly,
        };

    private static void ThrowTestException()
        => throw new InvalidOperationException(
            "Test exception raised from the toolbar. If you can read this in the log and in a dialog, " +
            "the crash pipeline works.");
}
