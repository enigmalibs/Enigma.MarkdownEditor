using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Enigma.Configuration;

namespace Enigma.MarkdownEditor.Core.Workspaces;

/// <summary>
/// The workspace list, backed by the workspaces file.
/// </summary>
/// <remarks>
/// <para>
/// Every mutation follows the same three steps: check the rules and throw a
/// <see cref="WorkspaceValidationException"/> the user interface can show, apply the change to the
/// live configuration object, then write the file. A failed write puts the previous state back before
/// rethrowing, so what the list says and what the file holds cannot drift apart.
/// </para>
/// <para>
/// The continuations deliberately do not use <c>ConfigureAwait(false)</c>: this service is called from
/// the UI thread and its events drive ViewModels, so resuming on the caller's context is what saves
/// every handler from marshalling itself back.
/// </para>
/// </remarks>
public sealed class WorkspaceService : IWorkspaceService
{
    private readonly IConfig<WorkspacesConfig> _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceService"/> class, opening the workspace
    /// that was open when the editor last closed.
    /// </summary>
    /// <param name="config">The workspaces file, already loaded (see <see cref="WorkspacesLoader"/>).</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is <see langword="null"/>.</exception>
    public WorkspaceService(IConfig<WorkspacesConfig> config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        // Falling back to the first workspace rather than to none: an identifier that matches nothing
        // means the file was hand-edited, and starting with the list's first entry is friendlier than
        // starting with an empty sidebar the user has to fix by hand.
        ActiveWorkspace = Find(Value.LastActiveWorkspaceId) ?? Value.Workspaces.FirstOrDefault();
    }

    /// <inheritdoc />
    public event EventHandler<ActiveWorkspaceChangedEventArgs>? ActiveWorkspaceChanged;

    /// <inheritdoc />
    public event EventHandler? WorkspacesChanged;

    /// <inheritdoc />
    public event EventHandler? FolderColorsChanged;

    /// <inheritdoc />
    public IReadOnlyList<Workspace> Workspaces => [.. Value.Workspaces];

    /// <inheritdoc />
    public Workspace? ActiveWorkspace { get; private set; }

    private WorkspacesConfig Value => _config.Value;

    /// <inheritdoc />
    public async Task<Workspace> CreateAsync(
        string name,
        string rootDirectory,
        CancellationToken cancellationToken = default)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        Require(WorkspaceValidation.ValidateName(trimmed, Value.Workspaces));

        string root = NormalizeOrThrow(rootDirectory);

        Workspace created = new()
        {
            Name = trimmed,
            RootDirectories = [root],
        };

        await MutateAsync(
            config =>
            {
                config.Workspaces.Add(created);
                config.LastActiveWorkspaceId = created.Id;
            },
            cancellationToken);

        SetActive(created);
        WorkspacesChanged?.Invoke(this, EventArgs.Empty);

        return created;
    }

    /// <inheritdoc />
    public async Task RenameAsync(Guid workspaceId, string name, CancellationToken cancellationToken = default)
    {
        Workspace workspace = Require(workspaceId);
        string trimmed = name?.Trim() ?? string.Empty;

        Require(WorkspaceValidation.ValidateName(trimmed, Value.Workspaces, workspaceId));

        if (string.Equals(workspace.Name, trimmed, StringComparison.Ordinal))
        {
            return;
        }

        await MutateAsync(_ => workspace.Name = trimmed, cancellationToken);
        WorkspacesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        Workspace workspace = Require(workspaceId);
        bool wasActive = ActiveWorkspace?.Id == workspaceId;

        await MutateAsync(
            config =>
            {
                config.Workspaces.Remove(workspace);

                if (wasActive)
                {
                    config.LastActiveWorkspaceId = config.Workspaces.FirstOrDefault()?.Id;
                }
            },
            cancellationToken);

        if (wasActive)
        {
            SetActive(Value.Workspaces.FirstOrDefault());
        }

        WorkspacesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public async Task AddRootDirectoryAsync(
        Guid workspaceId,
        string directory,
        CancellationToken cancellationToken = default)
    {
        Workspace workspace = Require(workspaceId);
        Require(WorkspaceValidation.ValidateRootDirectory(directory, workspace));

        string root = NormalizeOrThrow(directory);

        await MutateAsync(_ => workspace.RootDirectories.Add(root), cancellationToken);
        RaiseWorkspaceEdited(workspace);
    }

    /// <inheritdoc />
    public async Task RemoveRootDirectoryAsync(
        Guid workspaceId,
        string directory,
        CancellationToken cancellationToken = default)
    {
        Workspace workspace = Require(workspaceId);
        int index = IndexOfRoot(workspace, directory);

        if (index < 0)
        {
            throw new ArgumentException(
                $"'{directory}' is not a folder of workspace '{workspace.Name}'.",
                nameof(directory));
        }

        if (workspace.RootDirectories.Count == 1)
        {
            throw new WorkspaceValidationException(
                $"'{workspace.Name}' would be left without a folder. Add another one first, or remove " +
                "the whole workspace.");
        }

        await MutateAsync(_ => workspace.RootDirectories.RemoveAt(index), cancellationToken);
        RaiseWorkspaceEdited(workspace);
    }

    /// <inheritdoc />
    public async Task MoveRootDirectoryAsync(
        Guid workspaceId,
        int fromIndex,
        int toIndex,
        CancellationToken cancellationToken = default)
    {
        Workspace workspace = Require(workspaceId);
        int count = workspace.RootDirectories.Count;

        ArgumentOutOfRangeException.ThrowIfNegative(fromIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(fromIndex, count);
        ArgumentOutOfRangeException.ThrowIfNegative(toIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(toIndex, count);

        if (fromIndex == toIndex)
        {
            return;
        }

        await MutateAsync(
            _ =>
            {
                string moved = workspace.RootDirectories[fromIndex];
                workspace.RootDirectories.RemoveAt(fromIndex);
                workspace.RootDirectories.Insert(toIndex, moved);
            },
            cancellationToken);

        RaiseWorkspaceEdited(workspace);
    }

    /// <inheritdoc />
    public async Task SetFolderColorAsync(
        Guid workspaceId,
        string folder,
        FolderColor color,
        CancellationToken cancellationToken = default)
    {
        Workspace workspace = Require(workspaceId);
        string path = NormalizeOrThrow(folder);

        FolderColorEntry? existing = workspace.FolderColors.Find(
            entry => WorkspaceValidation.PathComparer.Equals(entry.Path, path));

        if ((existing?.Color ?? FolderColor.None) == color)
        {
            // The colour it already has. Writing the file to say so would cost a disk write and an
            // event for nothing, and the event is what repaints the tree and every open tab.
            return;
        }

        await MutateAsync(
            _ =>
            {
                if (existing is null)
                {
                    // Past the check above, a folder with no entry is being given a real colour:
                    // 'None' on an uncoloured folder is the no-op that already returned.
                    workspace.FolderColors.Add(new FolderColorEntry { Path = path, Color = color });
                }
                else if (color == FolderColor.None)
                {
                    workspace.FolderColors.Remove(existing);
                }
                else
                {
                    existing.Color = color;
                }
            },
            cancellationToken);

        FolderColorsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public async Task ActivateAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        Workspace workspace = Require(workspaceId);

        if (ActiveWorkspace?.Id == workspaceId && Value.LastActiveWorkspaceId == workspaceId)
        {
            return;
        }

        await MutateAsync(config => config.LastActiveWorkspaceId = workspaceId, cancellationToken);
        SetActive(workspace);
    }

    private Workspace? Find(Guid? workspaceId)
        => workspaceId is null
            ? null
            : Value.Workspaces.FirstOrDefault(workspace => workspace.Id == workspaceId);

    private Workspace Require(Guid workspaceId)
        => Find(workspaceId)
            ?? throw new ArgumentException($"No workspace has the identifier {workspaceId}.", nameof(workspaceId));

    private static void Require(string? problem)
    {
        if (problem is not null)
        {
            throw new WorkspaceValidationException(problem);
        }
    }

    private static string NormalizeOrThrow(string directory)
    {
        try
        {
            return WorkspaceValidation.NormalizeDirectory(directory);
        }
        catch (Exception exception)
            when (exception is ArgumentException or PathTooLongException or NotSupportedException)
        {
            throw new WorkspaceValidationException($"'{directory}' is not a usable folder path.", exception);
        }
    }

    private static int IndexOfRoot(Workspace workspace, string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return -1;
        }

        int asWritten = workspace.RootDirectories.FindIndex(
            root => WorkspaceValidation.PathComparer.Equals(root, directory));

        if (asWritten >= 0)
        {
            return asWritten;
        }

        // The caller may hold the path in a different but equivalent form — a trailing separator, a
        // relative path — so fall back to comparing normalized forms before giving up.
        string normalized;
        try
        {
            normalized = WorkspaceValidation.NormalizeDirectory(directory);
        }
        catch (Exception exception)
            when (exception is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return -1;
        }

        return workspace.RootDirectories.FindIndex(
            root => WorkspaceValidation.PathComparer.Equals(root, normalized));
    }

    private async Task MutateAsync(Action<WorkspacesConfig> mutate, CancellationToken cancellationToken)
    {
        WorkspacesConfig config = Value;
        List<Workspace> snapshot = config.Workspaces.ConvertAll(workspace => workspace.Clone());
        Guid? lastActive = config.LastActiveWorkspaceId;

        mutate(config);

        try
        {
            await _config.WriteAsync(cancellationToken);
        }
        catch
        {
            // The write is atomic, so the file still holds the previous content — the in-memory list
            // has to go back to matching it, or every later read is a lie. The identifiers survive the
            // restore, so the open workspace is re-resolved rather than left pointing at a detached
            // object.
            config.Workspaces = snapshot;
            config.LastActiveWorkspaceId = lastActive;
            ActiveWorkspace = Find(ActiveWorkspace?.Id);
            throw;
        }
    }

    private void RaiseWorkspaceEdited(Workspace workspace)
    {
        WorkspacesChanged?.Invoke(this, EventArgs.Empty);

        // The open workspace's own folders changed, which is what the sidebar renders — the same
        // notification the switcher needs when a different workspace is opened.
        if (ActiveWorkspace?.Id == workspace.Id)
        {
            ActiveWorkspaceChanged?.Invoke(this, new ActiveWorkspaceChangedEventArgs(workspace));
        }
    }

    private void SetActive(Workspace? workspace)
    {
        ActiveWorkspace = workspace;
        ActiveWorkspaceChanged?.Invoke(this, new ActiveWorkspaceChangedEventArgs(workspace));
    }
}
