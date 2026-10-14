using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Avalonia.Desktop.Controls.InfoBar;
using Enigma.Avalonia.Desktop.Services;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Documents;
using Enigma.MarkdownEditor.Core.Files;
using Enigma.MarkdownEditor.Core.Markdown;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// The workspace file tree: one branch per root directory, kept in step with the file system.
/// </summary>
/// <remarks>
/// <para>
/// Everything that touches disk happens here, off the UI thread, and everything that touches the node
/// collections happens on it: a watcher raises its events on a thread-pool thread, so every reaction
/// to one is posted through <see cref="IUiDispatcher"/> first.
/// </para>
/// <para>
/// A change is never applied event by event. The watcher hands over the directories whose contents can
/// no longer be trusted, each is listed again, and the new listing is merged into the existing nodes —
/// surviving nodes keep their identity, and with it their expansion and their own loaded children.
/// That is what lets a folder created three levels down appear without collapsing the tree around it.
/// </para>
/// <para>
/// Its subscriptions to the workspace service are never released, because both are singletons that
/// live as long as the application. The watchers are not: they are disposed and rebuilt whenever the
/// roots change, and disposed for good with this ViewModel.
/// </para>
/// <para>
/// The file operations live here rather than in a service of their own because they are the tree's
/// own context menu: they act on the selection, they need the folder to be read again afterwards, and
/// they report their failures through the same InfoBar. Every <em>decision</em> they take is in
/// <c>Enigma.MarkdownEditor.Core.Files</c> — what a name may be, what a move means, what to do about a
/// name that is taken — so what is left here is the glue: ask, plan, execute, re-read, report.
/// </para>
/// </remarks>
public sealed class FileTreeViewModel : ObservableObject, IDisposable
{
    private readonly IWorkspaceService _workspaces;
    private readonly IConfig<AppSettings> _settings;
    private readonly IDirectoryWatcherFactory _watcherFactory;
    private readonly IDocumentOpener _documentOpener;
    private readonly IFileOperationPrompts _prompts;
    private readonly ITrashService _trash;
    private readonly IUiDispatcher _dispatcher;
    private readonly IInfoBarService _infoBarService;
    private readonly ILogger<FileTreeViewModel> _logger;

    private readonly List<IDirectoryWatcher> _watchers = [];

    private CancellationTokenSource _work = new();
    private bool _appliedShowAllFiles;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileTreeViewModel"/> class.
    /// </summary>
    /// <param name="workspaces">Supplies the root directories, and says when they change.</param>
    /// <param name="settings">The live settings, read and written for ShowAllFiles.</param>
    /// <param name="watcherFactory">Builds the watcher for each root.</param>
    /// <param name="documentOpener">Takes over once the user activates a file.</param>
    /// <param name="prompts">Asks the user everything a file operation has to ask.</param>
    /// <param name="trash">Where a deleted entry goes, when the platform has somewhere to put it.</param>
    /// <param name="dispatcher">Marshals a watcher's thread-pool callback onto the UI thread.</param>
    /// <param name="infoBarService">Reports a folder that could not be read.</param>
    /// <param name="logger">Records the same failure for the log file.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public FileTreeViewModel(
        IWorkspaceService workspaces,
        IConfig<AppSettings> settings,
        IDirectoryWatcherFactory watcherFactory,
        IDocumentOpener documentOpener,
        IFileOperationPrompts prompts,
        ITrashService trash,
        IUiDispatcher dispatcher,
        IInfoBarService infoBarService,
        ILogger<FileTreeViewModel> logger)
    {
        _workspaces = workspaces ?? throw new ArgumentNullException(nameof(workspaces));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _watcherFactory = watcherFactory ?? throw new ArgumentNullException(nameof(watcherFactory));
        _documentOpener = documentOpener ?? throw new ArgumentNullException(nameof(documentOpener));
        _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
        _trash = trash ?? throw new ArgumentNullException(nameof(trash));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _infoBarService = infoBarService ?? throw new ArgumentNullException(nameof(infoBarService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _appliedShowAllFiles = _settings.Value.ShowAllFiles;

        RefreshCommand = new AsyncRelayCommand(() =>
        {
            Supersede();
            return RunAsync(RefreshAsync, "The file tree could not be read");
        });
        OpenCommand = new AsyncRelayCommand<FileTreeNodeViewModel?>(OpenAsync);

        NewFileCommand = new AsyncRelayCommand(
            () => RunAsync(NewFileAsync, "The file could not be created"),
            () => TargetDirectory is not null);
        NewFolderCommand = new AsyncRelayCommand(
            () => RunAsync(NewFolderAsync, "The folder could not be created"),
            () => TargetDirectory is not null);
        RenameCommand = new AsyncRelayCommand(
            () => RunAsync(RenameAsync, "This could not be renamed"),
            () => CanActOnSelection);
        DeleteCommand = new AsyncRelayCommand(
            () => RunAsync(DeleteAsync, "This could not be deleted"),
            () => CanActOnSelection);
        SetColorCommand = new AsyncRelayCommand<FolderColor?>(
            color => RunAsync(token => SetColorAsync(color, token), "The colour could not be saved"),
            _ => ColorTarget is not null);

        _workspaces.ActiveWorkspaceChanged += (_, _) => PostRefresh();
        _workspaces.WorkspacesChanged += (_, _) => PostRefresh();

        // Not PostRefresh, and that is the whole point of the colour having its own event: a colour
        // changes how the same entries are drawn, so the nodes on screen are re-tinted where they
        // stand rather than re-listed — which would collapse every folder the user had opened.
        _workspaces.FolderColorsChanged += (_, _) => ApplyColors();

        // Posted rather than awaited: a constructor cannot await, and the tree has nothing to show
        // until the first listing comes back anyway.
        PostRefresh();
    }

    /// <summary>
    /// Gets the tree's top-level nodes: one per root directory of the open workspace.
    /// </summary>
    public ObservableCollection<FileTreeNodeViewModel> Roots { get; } = [];

    /// <summary>
    /// Gets or sets the selected node, which is what the keyboard and the context menu act on.
    /// </summary>
    public FileTreeNodeViewModel? SelectedNode
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            OnPropertyChanged(nameof(CanActOnSelection));
            NotifyOperationsChanged();
        }
    }

    /// <summary>
    /// Gets a value indicating whether there is a tree to show.
    /// </summary>
    public bool HasRoots => Roots.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the selection is something that can be renamed or deleted.
    /// </summary>
    /// <remarks>
    /// A workspace root is deliberately excluded. Renaming or deleting one from here would leave the
    /// workspaces file pointing at a folder that is no longer called that, or no longer there at all —
    /// the manage-workspaces dialog is where a root is added and removed, and it is the only place
    /// that keeps the two in step.
    /// </remarks>
    public bool CanActOnSelection => SelectedNode is { IsRoot: false } node && (node.IsDirectory || node.IsFile);

    /// <summary>
    /// Gets or sets a value indicating whether the tree lists every file rather than markdown alone.
    /// </summary>
    /// <remarks>
    /// Backed directly by the live <see cref="AppSettings"/> object — the same one the settings window
    /// edits — so the two controls can never disagree. Setting it re-reads every open folder and then
    /// writes the settings file, in that order: the user sees the filter change immediately, and a
    /// file that cannot be written is reported without holding the tree back.
    /// </remarks>
    public bool ShowAllFiles
    {
        get => _settings.Value.ShowAllFiles;
        set
        {
            if (_settings.Value.ShowAllFiles == value)
            {
                return;
            }

            _settings.Value.ShowAllFiles = value;
            _appliedShowAllFiles = value;
            OnPropertyChanged();

            Supersede();
            _dispatcher.Post(() => RunAsync(ApplyFilterAsync, "The file filter could not be applied"));
        }
    }

    /// <summary>
    /// Gets the command that reads every open folder again. The one way back for a root that was
    /// unplugged while the editor was running, since a directory that is not there cannot be watched.
    /// </summary>
    public IAsyncRelayCommand RefreshCommand { get; }

    /// <summary>
    /// Gets the command a double-click or Enter runs: opens a file, toggles a directory.
    /// </summary>
    public IAsyncRelayCommand<FileTreeNodeViewModel?> OpenCommand { get; }

    /// <summary>
    /// Gets the command that creates a file in the selected folder and opens it.
    /// </summary>
    public IAsyncRelayCommand NewFileCommand { get; }

    /// <summary>
    /// Gets the command that creates a folder in the selected folder.
    /// </summary>
    public IAsyncRelayCommand NewFolderCommand { get; }

    /// <summary>
    /// Gets the command that renames the selection. Bound to F2.
    /// </summary>
    public IAsyncRelayCommand RenameCommand { get; }

    /// <summary>
    /// Gets the command that sends the selection to the trash. Bound to Delete.
    /// </summary>
    public IAsyncRelayCommand DeleteCommand { get; }

    /// <summary>
    /// Gets the command that marks the selected folder with a colour, or — for
    /// <see cref="FolderColor.None"/> — takes its colour away.
    /// </summary>
    /// <remarks>
    /// The parameter is nullable so that a menu item whose own parameter has not been bound yet is
    /// merely a no-op rather than an argument exception: with a non-nullable enum, the toolkit's
    /// command refuses a null parameter outright and the item would be drawn disabled.
    /// </remarks>
    public IAsyncRelayCommand<FolderColor?> SetColorCommand { get; }

    /// <summary>
    /// Gets a value indicating whether the selection is a folder whose colour can be set.
    /// </summary>
    /// <remarks>
    /// A workspace root is included, unlike <see cref="CanActOnSelection"/>: a colour is written to
    /// the workspaces file rather than to the file system, so colouring a root cannot leave the two
    /// out of step. A root that is not on disk is excluded — it lists nothing that could inherit the
    /// colour, so there is nothing for it to do.
    /// </remarks>
    public bool CanColorSelection => ColorTarget is not null;

    /// <summary>
    /// Gets the folder a new file or folder would be created in: the selected directory, the selected
    /// file's own folder, or — when nothing is selected in a workspace with a single root — that root.
    /// </summary>
    /// <remarks>
    /// An informational row answers with the folder it describes, so that "New file" works while the
    /// <em>Nothing here</em> row of an empty folder is selected, which is exactly when a user reaches
    /// for it. A root that is not on disk answers with nothing: there is no folder to create into.
    /// </remarks>
    private string? TargetDirectory
    {
        get
        {
            if (SelectedNode is { } node)
            {
                return node switch
                {
                    { IsDirectory: true } => node.FullPath,
                    { IsFile: true } => Path.GetDirectoryName(node.FullPath),
                    { IsInformational: true } => node.FullPath,
                    _ => null,
                };
            }

            return (Roots.Count == 1) && Roots[0].IsDirectory ? Roots[0].FullPath : null;
        }
    }

    // The folder a colour would be set on: the selected directory and nothing else. Unlike
    // TargetDirectory there is no fallback to a lone root — "colour this folder" has to mean the
    // folder the user right-clicked, or it means whichever one the tree happens to hold.
    private string? ColorTarget
        => (_workspaces.ActiveWorkspace is not null) && (SelectedNode is { IsDirectory: true } node)
            ? node.FullPath
            : null;

    /// <summary>
    /// Rebuilds the whole tree from the open workspace, and rebuilds its watchers with it.
    /// </summary>
    /// <param name="cancellationToken">Abandons the rebuild.</param>
    /// <returns>A task that completes once the roots are on screen.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        StopWatching();

        Roots.Clear();
        SelectedNode = null;

        Workspace? active = _workspaces.ActiveWorkspace;

        foreach (string directory in active?.RootDirectories ?? [])
        {
            FileTreeNodeViewModel root = FileTreeNodeViewModel.Root(directory, OnNodeExpanded);
            Roots.Add(root);
            StartWatching(directory);
        }

        OnPropertyChanged(nameof(HasRoots));
        ApplyColors();

        // The roots are half of what decides whether there is anywhere to create into — a workspace
        // over one folder needs no selection at all — so the four have to be re-asked here as well as
        // on a selection change.
        NotifyOperationsChanged();

        // A workspace over one folder has nothing to choose between, so that folder opens itself.
        // Awaited rather than left to the expansion callback, so that a caller — a test, or the
        // start-up path — knows when the tree is actually populated.
        if ((Roots.Count == 1) && Roots[0].IsDirectory)
        {
            await LoadChildrenAsync(Roots[0], cancellationToken);
            Roots[0].IsExpanded = true;
        }
    }

    /// <summary>
    /// Re-reads what the settings window may have changed while it was open.
    /// </summary>
    /// <param name="cancellationToken">Abandons the reload.</param>
    /// <returns>A task that completes once the tree matches the settings again.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    public async Task OnSettingsChangedAsync(CancellationToken cancellationToken = default)
    {
        if (_appliedShowAllFiles == _settings.Value.ShowAllFiles)
        {
            return;
        }

        _appliedShowAllFiles = _settings.Value.ShowAllFiles;
        OnPropertyChanged(nameof(ShowAllFiles));

        await ReloadOpenFoldersAsync(cancellationToken);
    }

    /// <summary>
    /// Moves an entry into a folder, asking the user about a name that is already taken.
    /// </summary>
    /// <param name="sourcePath">The entry being moved, as dragged.</param>
    /// <param name="targetDirectory">The folder it was dropped on.</param>
    /// <returns>A task that completes once the tree shows the result.</returns>
    /// <exception cref="ArgumentException">Either string is empty or white-space.</exception>
    /// <exception cref="ArgumentNullException">Either string is <see langword="null"/>.</exception>
    /// <remarks>
    /// Public because a drag and drop is not a command: the source and the target both come from the
    /// pointer, and neither is the tree's selection. Everything it can refuse, it refuses through
    /// <see cref="FileMovePlanner"/> rather than by trusting the view's own check — see
    /// <see cref="CanAcceptDrop"/>, which is only there to shape the drag cursor.
    /// </remarks>
    public Task MoveAsync(string sourcePath, string targetDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);

        return RunAsync(token => MoveAsync(sourcePath, targetDirectory, token), "This could not be moved");
    }

    /// <summary>
    /// Answers, without touching the disk, whether dropping an entry on a folder could possibly mean
    /// anything — so that the drag cursor can say no before the button is released.
    /// </summary>
    /// <param name="sourcePath">The entry being dragged.</param>
    /// <param name="targetDirectory">The folder under the pointer.</param>
    /// <returns><see langword="false"/> for a drop that would be a no-op or a nonsense.</returns>
    /// <remarks>
    /// The cheap half of <see cref="FileMovePlanner"/>'s rules, and deliberately only that: this runs
    /// on every pointer move, so it compares paths and nothing else. The planner is still what
    /// decides — a drop this lets through can be rejected there.
    /// </remarks>
    public static bool CanAcceptDrop(string? sourcePath, string? targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(targetDirectory))
        {
            return false;
        }

        string source = Path.TrimEndingDirectorySeparator(sourcePath);
        string target = Path.TrimEndingDirectorySeparator(targetDirectory);

        // Onto itself, into its own folder (where it already is), or into something it contains.
        return !WorkspaceValidation.PathComparer.Equals(source, target) &&
               !WorkspaceValidation.PathComparer.Equals(Path.GetDirectoryName(source), target) &&
               !IsUnder(target, source);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        StopWatching();
        _work.Cancel();
        _work.Dispose();
    }

    // Whether `path` is the same as `ancestor` or sits anywhere beneath it. Compared as text, because
    // that is all a drag cursor can afford; the separator check is what stops '/notes-old' from
    // counting as being under '/notes'.
    private static bool IsUnder(string path, string ancestor)
    {
        if (WorkspaceValidation.PathComparer.Equals(path, ancestor))
        {
            return true;
        }

        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return path.StartsWith(ancestor, comparison) &&
               (path.Length > ancestor.Length) &&
               ((path[ancestor.Length] == Path.DirectorySeparatorChar) ||
                (path[ancestor.Length] == Path.AltDirectorySeparatorChar));
    }

    private async Task NewFileAsync(CancellationToken cancellationToken)
    {
        if (TargetDirectory is not { } directory)
        {
            return;
        }

        if (await _prompts.AskForNewFileNameAsync(directory) is not { } name)
        {
            return;
        }

        string created = await FileOperations.CreateFileAsync(directory, name, cancellationToken);

        // A note opens with its own title already in it. Written as a second step rather than through
        // the create, so that FileMode.CreateNew stays the only thing standing between a name that
        // was taken since the check and a file being overwritten — and so a write that fails leaves
        // exactly the empty file this used to produce.
        if (MarkdownScaffold.For(name) is { Length: > 0 } scaffold)
        {
            await DocumentFile.WriteAsync(created, scaffold, TextFileFormat.Default, cancellationToken);
        }

        await RevealAsync(directory, created, cancellationToken);

        // Only when this application is the one that would show it. Creating an empty file and having
        // the operating system launch something else for it is not what "open the new file" means
        // here — and an empty '.png' has nothing to open anyway.
        if (FileTreeFilter.OpensInEditor(created, _settings.Value.ShowAllFiles))
        {
            await _documentOpener.OpenAsync(created, cancellationToken);
        }
    }

    private async Task NewFolderAsync(CancellationToken cancellationToken)
    {
        if (TargetDirectory is not { } directory)
        {
            return;
        }

        if (await _prompts.AskForNewFolderNameAsync(directory) is not { } name)
        {
            return;
        }

        string created = await FileOperations.CreateDirectoryAsync(directory, name, cancellationToken);

        await RevealAsync(directory, created, cancellationToken);
    }

    private async Task RenameAsync(CancellationToken cancellationToken)
    {
        if (!CanActOnSelection || (SelectedNode is not { } node))
        {
            return;
        }

        if (await _prompts.AskForNewNameAsync(node.FullPath, node.IsDirectory) is not { } name)
        {
            return;
        }

        string renamed = await FileOperations.RenameAsync(node.FullPath, name, cancellationToken);

        // Keeping the current name is a legitimate answer, not a failure: the operation returns the
        // path it started from, and there is nothing to re-read.
        if (string.Equals(renamed, node.FullPath, StringComparison.Ordinal))
        {
            return;
        }

        if (Path.GetDirectoryName(node.FullPath) is { } parent)
        {
            await RevealAsync(parent, renamed, cancellationToken);
        }
    }

    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (!CanActOnSelection || (SelectedNode is not { } node))
        {
            return;
        }

        string? parent = Path.GetDirectoryName(node.FullPath);
        bool trashed = _trash.IsAvailable && await _trash.MoveToTrashAsync(node.FullPath, cancellationToken);

        if (!trashed)
        {
            // The trash declined, or there is none. This is the only delete that asks, because it is
            // the only one that cannot be undone.
            if (!await _prompts.ConfirmPermanentDeleteAsync(node.Name, node.IsDirectory))
            {
                return;
            }

            await FileOperations.DeleteAsync(node.FullPath, cancellationToken);
        }

        // Nothing is reported on success: the InfoBar is for what went wrong, and a delete that
        // happened is announced by its row leaving the tree.
        if (parent is not null)
        {
            await ReloadDirectoryAsync(parent, cancellationToken);
        }
    }

    private async Task MoveAsync(string sourcePath, string targetDirectory, CancellationToken cancellationToken)
    {
        FileMovePlan plan = FileMovePlanner.Plan(sourcePath, targetDirectory);

        if (plan.Status == FileMovePlanStatus.Rejected)
        {
            await ReportAsync(
                "This could not be moved",
                plan.Rejection ?? "The move does not make sense.",
                InfoBarSeverity.Warning);

            return;
        }

        // Ready means the destination is free, so the resolution is never consulted — but it must not
        // be Cancel, which the operation refuses outright as a move that should never have started.
        MoveResolution resolution = MoveResolution.Overwrite;

        if (plan.Status == FileMovePlanStatus.Conflict)
        {
            resolution = await _prompts.AskHowToResolveAsync(plan.Name, plan.IsDirectory, plan.TargetDirectory);

            if (resolution == MoveResolution.Cancel)
            {
                return;
            }
        }

        string moved = await FileOperations.MoveAsync(plan, resolution, cancellationToken);

        await ReloadDirectoryAsync(plan.SourceDirectory, cancellationToken);
        await RevealAsync(plan.TargetDirectory, moved, cancellationToken);
    }

    // Re-reads a folder the application itself has just changed, rather than waiting out the watcher's
    // debounce. The watcher's own event arrives a moment later and re-reads the same folder onto the
    // same nodes: the merge is by path, so applying it twice is applying it once.
    private async Task ReloadDirectoryAsync(string directory, CancellationToken cancellationToken)
    {
        if (FindNode(directory) is { IsLoaded: true } node)
        {
            await LoadChildrenAsync(node, cancellationToken);
        }
    }

    // Re-reads a folder and puts the selection on what the operation just produced, opening the folder
    // if it was closed — an entry created into a folder nobody has expanded is otherwise created out
    // of sight.
    private async Task RevealAsync(string directory, string path, CancellationToken cancellationToken)
    {
        if (FindNode(directory) is not { } node)
        {
            return;
        }

        await LoadChildrenAsync(node, cancellationToken);

        // Set after the load, so the expansion callback finds the folder already read and does not
        // list it a second time.
        node.IsExpanded = true;

        SelectedNode = node.Children.FirstOrDefault(child =>
            WorkspaceValidation.PathComparer.Equals(child.FullPath, path));
    }

    // The four operations are shown in one context menu and whether each applies is decided by the
    // same two things — what is selected, and what roots there are — so they are always re-asked
    // together rather than one at a time.
    private void NotifyOperationsChanged()
    {
        NewFileCommand.NotifyCanExecuteChanged();
        NewFolderCommand.NotifyCanExecuteChanged();
        RenameCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        SetColorCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanColorSelection));
    }

    private async Task SetColorAsync(FolderColor? color, CancellationToken cancellationToken)
    {
        // The same three conditions ColorTarget answers, spelled out because the workspace and the
        // node are both needed here rather than just the path.
        if ((color is not { } chosen) ||
            (_workspaces.ActiveWorkspace is not { } workspace) ||
            (SelectedNode is not { IsDirectory: true } node))
        {
            return;
        }

        await _workspaces.SetFolderColorAsync(workspace.Id, node.FullPath, chosen, cancellationToken);
    }

    // Re-reads the open workspace's colours onto the nodes already on screen. Cheap enough to do for
    // the whole tree rather than for one folder: it touches only the directories that are loaded, and
    // it runs on a listing or a colour change — never on a pointer move.
    private void ApplyColors()
    {
        List<FolderColorEntry> colours = _workspaces.ActiveWorkspace?.FolderColors ?? [];

        foreach (FileTreeNodeViewModel root in Roots)
        {
            ApplyColors(root, colours);
        }
    }

    private static void ApplyColors(FileTreeNodeViewModel node, List<FolderColorEntry> colours)
    {
        if (node.IsDirectory)
        {
            // ColorOf, not Resolve: the row shows the colour set on this very folder, so that the
            // tree says where a colour lives rather than tinting everything under it. What a file
            // inherits is the tab's business.
            node.Color = FolderColorMap.ColorOf(colours, node.FullPath);
        }

        foreach (FileTreeNodeViewModel child in node.Children)
        {
            ApplyColors(child, colours);
        }
    }

    private void PostRefresh()
    {
        Supersede();
        _dispatcher.Post(() => RunAsync(RefreshAsync, "The file tree could not be read"));
    }

    // Abandons whatever is still reading a folder that is about to stop being part of the tree. Called
    // before the work is posted, never from inside it: an operation must not cancel its own token.
    private void Supersede()
    {
        if (_disposed)
        {
            return;
        }

        CancellationTokenSource superseded = _work;
        _work = new CancellationTokenSource();

        superseded.Cancel();
        superseded.Dispose();
    }

    private void OnNodeExpanded(FileTreeNodeViewModel node)
    {
        if (node.IsLoaded)
        {
            return;
        }

        _dispatcher.Post(() => RunAsync(
            token => LoadChildrenAsync(node, token),
            $"'{node.Name}' could not be read"));
    }

    private async Task OpenAsync(FileTreeNodeViewModel? node)
    {
        if (node is null)
        {
            return;
        }

        if (node.IsDirectory)
        {
            node.IsExpanded = !node.IsExpanded;
            return;
        }

        if (!node.IsFile)
        {
            return;
        }

        try
        {
            await _documentOpener.OpenAsync(node.FullPath, _work.Token);
        }
        catch (OperationCanceledException)
        {
            // The workspace changed under the open. Nothing to report.
        }
        catch (Exception failure)
        {
            // Caught broadly and on purpose: an AsyncRelayCommand parks a faulted task rather than
            // letting it escape, so without this a double-click would simply appear to do nothing.
            await ReportAsync(failure, "This file could not be opened");
        }
    }

    private async Task ApplyFilterAsync(CancellationToken cancellationToken)
    {
        await ReloadOpenFoldersAsync(cancellationToken);

        // After the tree, not before: the setting is the user's, and they should see it take effect
        // whether or not the file behind it can be written.
        await _settings.WriteAsync(cancellationToken);
    }

    private async Task ReloadOpenFoldersAsync(CancellationToken cancellationToken)
    {
        foreach (FileTreeNodeViewModel root in Roots)
        {
            await ReloadLoadedAsync(root, cancellationToken);
        }
    }

    private async Task ApplyAsync(
        IDirectoryWatcher watcher,
        DirectoryChangeSet changes,
        CancellationToken cancellationToken)
    {
        if (changes.RequiresFullRefresh)
        {
            // The watcher lost events, so nothing about this root is known. Everything already read
            // under it is read again; what was never expanded costs nothing.
            _logger.LogWarning(
                "The watcher on {RootDirectory} lost events; the whole root is being read again.",
                watcher.RootDirectory);

            if (FindNode(watcher.RootDirectory) is { } root)
            {
                await ReloadLoadedAsync(root, cancellationToken);
            }

            return;
        }

        foreach (string directory in changes.Directories)
        {
            // A folder nobody has expanded yet has nothing to update: it reads itself, current, the
            // first time it is opened.
            if (FindNode(directory) is { IsLoaded: true } node)
            {
                await LoadChildrenAsync(node, cancellationToken);
            }
        }
    }

    private async Task ReloadLoadedAsync(FileTreeNodeViewModel node, CancellationToken cancellationToken)
    {
        if (!node.IsLoaded)
        {
            return;
        }

        await LoadChildrenAsync(node, cancellationToken);

        // Snapshot: the merge above rewrote the very collection this walks.
        FileTreeNodeViewModel[] children = [.. node.Children];

        foreach (FileTreeNodeViewModel child in children)
        {
            await ReloadLoadedAsync(child, cancellationToken);
        }
    }

    private async Task LoadChildrenAsync(FileTreeNodeViewModel node, CancellationToken cancellationToken)
    {
        DirectoryListing listing = await DirectoryLister.ListAsync(
            node.FullPath,
            _settings.Value.ShowAllFiles,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        Merge(node, listing);
        node.IsLoaded = true;

        // The nodes Merge just built have no colour yet, and a node it kept may have had one set
        // while the folder was closed.
        ApplyColors();
    }

    private void Merge(FileTreeNodeViewModel node, DirectoryListing listing)
    {
        if ((listing.Status != DirectoryListingStatus.Ok) || listing.IsEmpty)
        {
            node.Children.Clear();
            node.Children.Add(FileTreeNodeViewModel.For(node.FullPath, listing.Status));

            return;
        }

        // Index what is already on screen by path, so an entry that only moved position keeps the very
        // node object the view has expanded — replacing it would collapse everything under it.
        Dictionary<string, FileTreeNodeViewModel> survivors = new(WorkspaceValidation.PathComparer);
        foreach (FileTreeNodeViewModel child in node.Children)
        {
            if (!child.IsInformational)
            {
                survivors[child.FullPath] = child;
            }
        }

        List<FileTreeNodeViewModel> wanted = new(listing.Entries.Count);
        foreach (FileSystemEntry entry in listing.Entries)
        {
            // A path that flipped between file and directory is a different thing under the same name,
            // so the node is rebuilt rather than reused.
            wanted.Add(
                survivors.TryGetValue(entry.FullPath, out FileTreeNodeViewModel? kept) &&
                (kept.IsDirectory == entry.IsDirectory)
                    ? kept
                    : FileTreeNodeViewModel.For(entry, OnNodeExpanded));
        }

        HashSet<FileTreeNodeViewModel> keep = [.. wanted];

        // Back to front, so the indexes ahead of the one being removed stay valid.
        for (int index = node.Children.Count - 1; index >= 0; index--)
        {
            if (!keep.Contains(node.Children[index]))
            {
                node.Children.RemoveAt(index);
            }
        }

        for (int index = 0; index < wanted.Count; index++)
        {
            int current = node.Children.IndexOf(wanted[index]);

            if (current < 0)
            {
                node.Children.Insert(index, wanted[index]);
            }
            else if (current != index)
            {
                node.Children.Move(current, index);
            }
        }
    }

    private FileTreeNodeViewModel? FindNode(string directory)
    {
        foreach (FileTreeNodeViewModel root in Roots)
        {
            if (Find(root) is { } found)
            {
                return found;
            }
        }

        return null;

        FileTreeNodeViewModel? Find(FileTreeNodeViewModel node)
        {
            if (!node.IsDirectory)
            {
                return null;
            }

            if (WorkspaceValidation.PathComparer.Equals(node.FullPath, directory))
            {
                return node;
            }

            // Only walk into what has been read: an unexpanded branch cannot hold the node, because
            // its children do not exist yet.
            if (!node.IsLoaded)
            {
                return null;
            }

            foreach (FileTreeNodeViewModel child in node.Children)
            {
                if (Find(child) is { } found)
                {
                    return found;
                }
            }

            return null;
        }
    }

    private void StartWatching(string directory)
    {
        IDirectoryWatcher watcher = _watcherFactory.Create(directory);
        watcher.Changed += OnWatcherChanged;
        _watchers.Add(watcher);

        watcher.Start();
    }

    private void StopWatching()
    {
        foreach (IDirectoryWatcher watcher in _watchers)
        {
            watcher.Changed -= OnWatcherChanged;
            watcher.Dispose();
        }

        _watchers.Clear();
    }

    private void OnWatcherChanged(object? sender, DirectoryChangeSet changes)
    {
        if (sender is not IDirectoryWatcher watcher)
        {
            return;
        }

        _dispatcher.Post(() => RunAsync(
            token => ApplyAsync(watcher, changes, token),
            "The file tree could not be updated"));
    }

    private async Task RunAsync(Func<CancellationToken, Task> work, string failureTitle)
    {
        if (_disposed)
        {
            return;
        }

        // Read once, here: a refresh that starts while this one is running replaces the source, and
        // the token already handed to the work is the one that gets cancelled.
        CancellationToken token = _work.Token;

        try
        {
            await work(token);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a later refresh, or the ViewModel is going away.
        }
        catch (Exception failure)
        {
            await ReportAsync(failure, failureTitle);
        }
    }

    private async Task ReportAsync(Exception failure, string title)
    {
        if (failure is FileOperationException expected)
        {
            // Its message is written for the user and describes a rule they broke, not a fault of the
            // application's. Prefixing it with a type name would turn advice into an incident, and it
            // is not worth a line in the log either.
            await ReportAsync(title, expected.Message, InfoBarSeverity.Warning);
            return;
        }

        _logger.LogError(failure, "{Title}.", title);

        await ReportAsync(title, $"{failure.GetType().Name}: {failure.Message}", InfoBarSeverity.Error);
    }

    private Task ReportAsync(string title, string message, InfoBarSeverity severity)
        => _infoBarService.ShowAsync(bar =>
        {
            bar.Title = title;
            bar.Message = message;
            bar.Severity = severity;
        });
}
