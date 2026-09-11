using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Enigma.Avalonia.Desktop.Controls.InfoBar;
using Enigma.Avalonia.Desktop.Services;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace Enigma.MarkdownEditor.Desktop.ViewModels;

/// <summary>
/// State for the two places the shell shows workspaces: the toolbar switcher and the sidebar.
/// </summary>
/// <remarks>
/// It owns no workspace state of its own — <see cref="IWorkspaceService"/> does — and rebuilds its
/// lists from the service whenever the service says something changed. The subscriptions are never
/// released because this ViewModel and the service are both singletons that live as long as the
/// application; there is no window for a stale handler to keep alive.
/// </remarks>
public sealed class WorkspacesViewModel : ObservableObject
{
    private readonly IWorkspaceService _service;
    private readonly IWorkspaceManagerDialogService _managerDialogService;
    private readonly IInfoBarService _infoBarService;
    private readonly ILogger<WorkspacesViewModel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspacesViewModel"/> class.
    /// </summary>
    /// <param name="service">The workspace list the switcher shows and opens from.</param>
    /// <param name="managerDialogService">Opens the manage-workspaces dialog.</param>
    /// <param name="infoBarService">Reports an operation that could not be completed.</param>
    /// <param name="logger">Records the same failure for the log file.</param>
    /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
    public WorkspacesViewModel(
        IWorkspaceService service,
        IWorkspaceManagerDialogService managerDialogService,
        IInfoBarService infoBarService,
        ILogger<WorkspacesViewModel> logger)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _managerDialogService = managerDialogService ?? throw new ArgumentNullException(nameof(managerDialogService));
        _infoBarService = infoBarService ?? throw new ArgumentNullException(nameof(infoBarService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        ActivateCommand = new AsyncRelayCommand<WorkspaceListItem>(ActivateAsync);
        ManageCommand = new AsyncRelayCommand(ManageAsync);

        _service.WorkspacesChanged += (_, _) => Refresh();
        _service.ActiveWorkspaceChanged += (_, _) => Refresh();

        Refresh();
    }

    /// <summary>
    /// Gets the workspaces the switcher flyout lists, each carrying the command that opens it.
    /// </summary>
    public ObservableCollection<WorkspaceListItem> Items { get; } = [];

    /// <summary>
    /// Gets the name of the open workspace, or an invitation to make one.
    /// </summary>
    public string ActiveWorkspaceName => _service.ActiveWorkspace?.Name ?? "No workspace";

    /// <summary>
    /// Gets the sidebar's caption: the open workspace's name, or the generic heading when there is none.
    /// </summary>
    public string SidebarHeader => (_service.ActiveWorkspace?.Name ?? "Workspace").ToUpperInvariant();

    /// <summary>
    /// Gets a value indicating whether any workspace exists — what the sidebar's empty state is keyed on.
    /// </summary>
    public bool HasWorkspaces => Items.Count > 0;

    /// <summary>
    /// Gets a value indicating whether a workspace is open.
    /// </summary>
    public bool HasActiveWorkspace => _service.ActiveWorkspace is not null;

    /// <summary>
    /// Gets the warning about the open workspace's missing folders, or <see langword="null"/> when they
    /// are all there.
    /// </summary>
    /// <remarks>
    /// A missing folder never stops a workspace from opening: a root on a share or a removable drive is
    /// gone only for as long as it is unplugged.
    /// </remarks>
    public string? MissingRootsWarning
    {
        get
        {
            if (_service.ActiveWorkspace is not { } active)
            {
                return null;
            }

            IReadOnlyList<string> missing = WorkspaceValidation.MissingRootDirectories(active);

            return missing.Count switch
            {
                0 => null,
                1 => $"This folder is not there right now: {missing[0]}",
                _ => $"{missing.Count} folders are not there right now: {string.Join(", ", missing)}",
            };
        }
    }

    /// <summary>
    /// Gets a value indicating whether the missing-folder warning is shown.
    /// </summary>
    public bool HasMissingRoots => MissingRootsWarning is not null;

    /// <summary>
    /// Gets the command that opens a workspace, taking the switcher row as its parameter.
    /// </summary>
    public IAsyncRelayCommand<WorkspaceListItem> ActivateCommand { get; }

    /// <summary>
    /// Gets the command that opens the manage-workspaces dialog.
    /// </summary>
    public IAsyncRelayCommand ManageCommand { get; }

    private async Task ActivateAsync(WorkspaceListItem? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            await _service.ActivateAsync(item.Id);
        }
        catch (Exception failure)
        {
            // Caught broadly and on purpose: an AsyncRelayCommand parks a faulted task rather than
            // letting the exception escape, so an unwritable workspaces file would otherwise leave the
            // switcher silently doing nothing.
            await ReportAsync(failure, "The workspace could not be opened");
        }
    }

    private async Task ManageAsync()
    {
        try
        {
            await _managerDialogService.ShowAsync();
        }
        catch (Exception failure)
        {
            await ReportAsync(failure, "Workspaces could not be managed");
        }

        // The dialog edits through the service, which raises the events this ViewModel listens to, so
        // the lists are already current by the time it closes.
    }

    private async Task ReportAsync(Exception failure, string title)
    {
        _logger.LogError(failure, "{Title}.", title);

        await _infoBarService.ShowAsync(bar =>
        {
            bar.Title = title;
            bar.Message = $"{failure.GetType().Name}: {failure.Message}";
            bar.Severity = InfoBarSeverity.Error;
        });
    }

    private void Refresh()
    {
        Workspace? active = _service.ActiveWorkspace;

        Items.Clear();
        foreach (Workspace workspace in _service.Workspaces)
        {
            Items.Add(new WorkspaceListItem(workspace, workspace.Id == active?.Id, ActivateCommand));
        }

        OnPropertyChanged(nameof(ActiveWorkspaceName));
        OnPropertyChanged(nameof(SidebarHeader));
        OnPropertyChanged(nameof(HasWorkspaces));
        OnPropertyChanged(nameof(HasActiveWorkspace));
        OnPropertyChanged(nameof(MissingRootsWarning));
        OnPropertyChanged(nameof(HasMissingRoots));
    }
}
