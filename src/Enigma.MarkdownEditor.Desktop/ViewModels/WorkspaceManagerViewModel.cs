using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// State for the manage-workspaces dialog: the workspace list on the left, the selected workspace's
/// name and folders on the right.
/// </summary>
/// <remarks>
/// <para>
/// Everything the dialog can ask is answered inside the dialog. It is itself hosted by the one
/// <c>ContentDialog</c> the application owns, so a nested confirmation would replace it rather than
/// stack on top of it — which is why removing a workspace confirms in an inline bar
/// (<see cref="IsRemoveConfirmationVisible"/>) and renaming is a box in the details pane.
/// </para>
/// <para>
/// The lists are rebuilt from <see cref="IWorkspaceService"/> after every operation instead of being
/// kept in step by hand. The ViewModel does not subscribe to the service's events: it is built per
/// showing and the dialog is modal, so nothing else can change the list while it is open — and an
/// unsubscribed handler on a singleton service would outlive the dialog.
/// </para>
/// </remarks>
public sealed class WorkspaceManagerViewModel : ObservableObject
{
    private readonly IWorkspaceService _service;
    private readonly IFolderPicker _folderPicker;
    private readonly ILogger<WorkspaceManagerViewModel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceManagerViewModel"/> class.
    /// </summary>
    /// <param name="service">The workspace list this dialog edits.</param>
    /// <param name="folderPicker">Picks the folders that become root directories.</param>
    /// <param name="logger">Records an operation that failed for a reason the user cannot fix.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public WorkspaceManagerViewModel(
        IWorkspaceService service,
        IFolderPicker folderPicker,
        ILogger<WorkspaceManagerViewModel> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _folderPicker = folderPicker ?? throw new ArgumentNullException(nameof(folderPicker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        AddWorkspaceCommand = new AsyncRelayCommand(AddWorkspaceAsync);
        BeginRemoveWorkspaceCommand = new RelayCommand(() => IsRemoveConfirmationVisible = SelectedWorkspace is not null);
        CancelRemoveWorkspaceCommand = new RelayCommand(() => IsRemoveConfirmationVisible = false);
        RemoveWorkspaceCommand = new AsyncRelayCommand(RemoveWorkspaceAsync);
        RenameWorkspaceCommand = new AsyncRelayCommand(RenameWorkspaceAsync);
        AddRootCommand = new AsyncRelayCommand(AddRootAsync);
        RemoveRootCommand = new AsyncRelayCommand(RemoveRootAsync);
        MoveRootUpCommand = new AsyncRelayCommand(() => MoveRootAsync(-1));
        MoveRootDownCommand = new AsyncRelayCommand(() => MoveRootAsync(1));

        Refresh();
        SelectedWorkspace = Workspaces.FirstOrDefault(item => item.IsActive) ?? Workspaces.FirstOrDefault();
    }

    /// <summary>
    /// Gets the workspaces, in the order they are listed.
    /// </summary>
    public ObservableCollection<WorkspaceListItem> Workspaces { get; } = [];

    /// <summary>
    /// Gets the selected workspace's root directories, in the order the tree will read them.
    /// </summary>
    public ObservableCollection<WorkspaceRootItem> Roots { get; } = [];

    /// <summary>
    /// Gets or sets the selected workspace, whose name and folders fill the details pane.
    /// </summary>
    public WorkspaceListItem? SelectedWorkspace
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            IsRemoveConfirmationVisible = false;
            ErrorMessage = null;
            EditedName = value?.Name ?? string.Empty;
            RefreshRoots();
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(RemoveConfirmationText));
        }
    }

    /// <summary>
    /// Gets or sets the selected root directory, which the remove and reorder buttons act on.
    /// </summary>
    public WorkspaceRootItem? SelectedRoot
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                RaiseRootButtonStates();
            }
        }
    }

    /// <summary>
    /// Gets or sets the name in the rename box. Checked as it is typed; committed by
    /// <see cref="RenameWorkspaceCommand"/>.
    /// </summary>
    public string EditedName
    {
        get;
        set
        {
            if (!SetProperty(ref field, value ?? string.Empty))
            {
                return;
            }

            NameError = SelectedWorkspace is null
                ? null
                : WorkspaceValidation.ValidateName(field, _service.Workspaces, SelectedWorkspace.Id);

            OnPropertyChanged(nameof(CanRename));
        }
    } = string.Empty;

    /// <summary>
    /// Gets what is wrong with <see cref="EditedName"/>, or <see langword="null"/> when nothing is.
    /// </summary>
    public string? NameError
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasNameError));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the rename box is showing a problem.
    /// </summary>
    public bool HasNameError => NameError is not null;

    /// <summary>
    /// Gets the failure of the last operation, or <see langword="null"/> when it succeeded.
    /// </summary>
    public string? ErrorMessage
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the error bar is shown.
    /// </summary>
    public bool HasError => ErrorMessage is not null;

    /// <summary>
    /// Gets a value indicating whether the inline "remove this workspace?" bar is shown.
    /// </summary>
    public bool IsRemoveConfirmationVisible { get; private set => SetProperty(ref field, value); }

    /// <summary>
    /// Gets the question the inline confirmation asks.
    /// </summary>
    public string RemoveConfirmationText =>
        $"Remove '{SelectedWorkspace?.Name}' from the list? The folders themselves are not touched.";

    /// <summary>
    /// Gets a value indicating whether there is any workspace at all.
    /// </summary>
    public bool HasWorkspaces => Workspaces.Count > 0;

    /// <summary>
    /// Gets a value indicating whether a workspace is selected, which is what the details pane needs.
    /// </summary>
    public bool HasSelection => SelectedWorkspace is not null;

    /// <summary>
    /// Gets a value indicating whether the name in the box is a change worth committing.
    /// </summary>
    public bool CanRename =>
        SelectedWorkspace is not null &&
        NameError is null &&
        !string.Equals(EditedName.Trim(), SelectedWorkspace.Name, StringComparison.Ordinal);

    /// <summary>
    /// Gets a value indicating whether the selected folder can be removed — the last one cannot.
    /// </summary>
    public bool CanRemoveRoot => SelectedRoot is not null && Roots.Count > 1;

    /// <summary>
    /// Gets a value indicating whether the selected folder can move up.
    /// </summary>
    public bool CanMoveRootUp => SelectedRoot is not null && Roots.IndexOf(SelectedRoot) > 0;

    /// <summary>
    /// Gets a value indicating whether the selected folder can move down.
    /// </summary>
    public bool CanMoveRootDown
    {
        get
        {
            int index = SelectedRoot is null ? -1 : Roots.IndexOf(SelectedRoot);

            return index >= 0 && index < Roots.Count - 1;
        }
    }

    /// <summary>
    /// Gets the command that picks a folder and creates a workspace over it.
    /// </summary>
    public IAsyncRelayCommand AddWorkspaceCommand { get; }

    /// <summary>
    /// Gets the command that shows the inline removal confirmation.
    /// </summary>
    public IRelayCommand BeginRemoveWorkspaceCommand { get; }

    /// <summary>
    /// Gets the command that hides the inline removal confirmation.
    /// </summary>
    public IRelayCommand CancelRemoveWorkspaceCommand { get; }

    /// <summary>
    /// Gets the command that removes the selected workspace from the list.
    /// </summary>
    public IAsyncRelayCommand RemoveWorkspaceCommand { get; }

    /// <summary>
    /// Gets the command that commits <see cref="EditedName"/>.
    /// </summary>
    public IAsyncRelayCommand RenameWorkspaceCommand { get; }

    /// <summary>
    /// Gets the command that picks a folder and adds it to the selected workspace.
    /// </summary>
    public IAsyncRelayCommand AddRootCommand { get; }

    /// <summary>
    /// Gets the command that removes the selected folder from the selected workspace.
    /// </summary>
    public IAsyncRelayCommand RemoveRootCommand { get; }

    /// <summary>
    /// Gets the command that moves the selected folder one place earlier.
    /// </summary>
    public IAsyncRelayCommand MoveRootUpCommand { get; }

    /// <summary>
    /// Gets the command that moves the selected folder one place later.
    /// </summary>
    public IAsyncRelayCommand MoveRootDownCommand { get; }

    private async Task AddWorkspaceAsync()
    {
        string? folder = await PickFolderAsync("Choose the workspace's first folder");
        if (folder is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            Workspace created = await _service.CreateAsync(
                WorkspaceValidation.SuggestName(folder, _service.Workspaces),
                folder);

            Refresh();
            SelectedWorkspace = Workspaces.FirstOrDefault(item => item.Id == created.Id);
        });
    }

    private async Task RemoveWorkspaceAsync()
    {
        if (SelectedWorkspace is not { } selected)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _service.DeleteAsync(selected.Id);

            IsRemoveConfirmationVisible = false;
            Refresh();
            SelectedWorkspace = Workspaces.FirstOrDefault(item => item.IsActive) ?? Workspaces.FirstOrDefault();
        });
    }

    private async Task RenameWorkspaceAsync()
    {
        if (!CanRename || SelectedWorkspace is not { } selected)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _service.RenameAsync(selected.Id, EditedName);
            Refresh();
            SelectedWorkspace = Workspaces.FirstOrDefault(item => item.Id == selected.Id);
        });
    }

    private async Task AddRootAsync()
    {
        if (SelectedWorkspace is not { } selected)
        {
            return;
        }

        string? folder = await PickFolderAsync($"Add a folder to '{selected.Name}'");
        if (folder is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _service.AddRootDirectoryAsync(selected.Id, folder);
            Refresh();
            SelectedWorkspace = Workspaces.FirstOrDefault(item => item.Id == selected.Id);
            SelectedRoot = Roots.LastOrDefault();
        });
    }

    private async Task RemoveRootAsync()
    {
        if (!CanRemoveRoot || SelectedWorkspace is not { } selected || SelectedRoot is not { } root)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _service.RemoveRootDirectoryAsync(selected.Id, root.Path);
            Refresh();
            SelectedWorkspace = Workspaces.FirstOrDefault(item => item.Id == selected.Id);
        });
    }

    private async Task MoveRootAsync(int offset)
    {
        if (SelectedWorkspace is not { } selected || SelectedRoot is not { } root)
        {
            return;
        }

        int from = Roots.IndexOf(root);
        int to = from + offset;

        if (from < 0 || to < 0 || to >= Roots.Count)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await _service.MoveRootDirectoryAsync(selected.Id, from, to);
            Refresh();
            SelectedWorkspace = Workspaces.FirstOrDefault(item => item.Id == selected.Id);
            SelectedRoot = Roots.ElementAtOrDefault(to);
        });
    }

    private async Task<string?> PickFolderAsync(string title)
    {
        try
        {
            return await _folderPicker.PickAsync(title);
        }
        catch (Exception failure)
        {
            // Caught broadly and on purpose, like every other await in this ViewModel: an
            // AsyncRelayCommand parks a faulted task instead of letting the exception escape, so
            // without this the picker would simply appear to do nothing.
            _logger.LogError(failure, "The folder picker could not be opened.");
            ErrorMessage = $"The folder picker could not be opened. {failure.Message}";

            return null;
        }
    }

    private async Task RunAsync(Func<Task> operation)
    {
        try
        {
            ErrorMessage = null;
            await operation();
        }
        catch (WorkspaceValidationException invalid)
        {
            // Expected: a rule the user broke, phrased for them. Not worth a log entry.
            ErrorMessage = invalid.Message;
        }
        catch (Exception failure)
        {
            _logger.LogError(failure, "A workspace operation failed.");
            ErrorMessage = $"{failure.GetType().Name}: {failure.Message}";
        }
    }

    private void Refresh()
    {
        Guid? selectedId = SelectedWorkspace?.Id;
        Workspace? active = _service.ActiveWorkspace;

        Workspaces.Clear();
        foreach (Workspace workspace in _service.Workspaces)
        {
            Workspaces.Add(new WorkspaceListItem(workspace, workspace.Id == active?.Id));
        }

        OnPropertyChanged(nameof(HasWorkspaces));

        // The rows are new objects, so the selection has to be re-pointed at the row that stands for
        // the same workspace — otherwise the details pane empties itself after every operation.
        if (selectedId is not null && Workspaces.FirstOrDefault(item => item.Id == selectedId) is { } row)
        {
            SelectedWorkspace = row;
        }
    }

    private void RefreshRoots()
    {
        WorkspaceRootItem? selectedRoot = SelectedRoot;

        Roots.Clear();
        foreach (string directory in SelectedWorkspace?.Model.RootDirectories ?? [])
        {
            Roots.Add(new WorkspaceRootItem(directory));
        }

        SelectedRoot = Roots.FirstOrDefault(root =>
            WorkspaceValidation.PathComparer.Equals(root.Path, selectedRoot?.Path));

        RaiseRootButtonStates();
    }

    private void RaiseRootButtonStates()
    {
        OnPropertyChanged(nameof(CanRemoveRoot));
        OnPropertyChanged(nameof(CanMoveRootUp));
        OnPropertyChanged(nameof(CanMoveRootDown));
    }
}
