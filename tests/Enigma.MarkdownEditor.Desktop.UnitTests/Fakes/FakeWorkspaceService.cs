using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Core.Workspaces;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// The workspace list without a file behind it, for the ViewModels that only read it and raise
/// commands against it.
/// </summary>
/// <remarks>
/// The real <see cref="WorkspaceService"/> is used where persistence is the point — see the manage
/// dialog's tests. This one exists so that the switcher's tests can make a write fail on demand, which
/// a real file makes awkward.
/// </remarks>
internal sealed class FakeWorkspaceService : IWorkspaceService
{
    private readonly List<Workspace> _workspaces = [];

    public event EventHandler<ActiveWorkspaceChangedEventArgs>? ActiveWorkspaceChanged;

    public event EventHandler? WorkspacesChanged;

    public event EventHandler? FolderColorsChanged;

    /// <summary>
    /// Set to make every mutating call fail, standing in for an unwritable file.
    /// </summary>
    public Exception? Failure { get; init; }

    public IReadOnlyList<Workspace> Workspaces => _workspaces;

    public Workspace? ActiveWorkspace { get; private set; }

    /// <summary>
    /// Seeds a workspace without going through the service, so a test can start from a given list.
    /// </summary>
    public Workspace Seed(string name, bool active = false, params string[] roots)
    {
        Workspace workspace = new()
        {
            Name = name,
            RootDirectories = roots.Length == 0 ? ["/tmp/anywhere"] : [.. roots],
        };

        _workspaces.Add(workspace);

        if (active)
        {
            ActiveWorkspace = workspace;
        }

        return workspace;
    }

    public Task<Workspace> CreateAsync(string name, string rootDirectory, CancellationToken cancellationToken = default)
    {
        if (Failure is not null)
        {
            return Task.FromException<Workspace>(Failure);
        }

        Workspace created = Seed(name, active: true, rootDirectory);
        ActiveWorkspaceChanged?.Invoke(this, new ActiveWorkspaceChangedEventArgs(created));
        WorkspacesChanged?.Invoke(this, EventArgs.Empty);

        return Task.FromResult(created);
    }

    public Task RenameAsync(Guid workspaceId, string name, CancellationToken cancellationToken = default)
        => Mutate(workspace => workspace.Name = name, workspaceId);

    public Task DeleteAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        => Mutate(workspace => _workspaces.Remove(workspace), workspaceId);

    public Task AddRootDirectoryAsync(Guid workspaceId, string directory, CancellationToken cancellationToken = default)
        => Mutate(workspace => workspace.RootDirectories.Add(directory), workspaceId);

    public Task RemoveRootDirectoryAsync(Guid workspaceId, string directory, CancellationToken cancellationToken = default)
        => Mutate(workspace => workspace.RootDirectories.Remove(directory), workspaceId);

    public Task MoveRootDirectoryAsync(
        Guid workspaceId,
        int fromIndex,
        int toIndex,
        CancellationToken cancellationToken = default)
        => Mutate(
            workspace =>
            {
                string moved = workspace.RootDirectories[fromIndex];
                workspace.RootDirectories.RemoveAt(fromIndex);
                workspace.RootDirectories.Insert(toIndex, moved);
            },
            workspaceId);

    public Task SetFolderColorAsync(
        Guid workspaceId,
        string folder,
        FolderColor color,
        CancellationToken cancellationToken = default)
    {
        if (Failure is not null)
        {
            return Task.FromException(Failure);
        }

        Workspace workspace = Find(workspaceId)
            ?? throw new ArgumentException("Unknown workspace.", nameof(workspaceId));

        FolderColorEntry? existing = workspace.FolderColors.Find(
            entry => WorkspaceValidation.PathComparer.Equals(entry.Path, folder));

        if ((existing?.Color ?? FolderColor.None) == color)
        {
            return Task.CompletedTask;
        }

        if (existing is null)
        {
            workspace.FolderColors.Add(new FolderColorEntry { Path = folder, Color = color });
        }
        else if (color == FolderColor.None)
        {
            workspace.FolderColors.Remove(existing);
        }
        else
        {
            existing.Color = color;
        }

        // The one event a colour raises, exactly as the real service does: neither of the other two,
        // because both of those make the tree list every root again.
        FolderColorsChanged?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }

    public Task ActivateAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        if (Failure is not null)
        {
            return Task.FromException(Failure);
        }

        ActiveWorkspace = Find(workspaceId);
        ActiveWorkspaceChanged?.Invoke(this, new ActiveWorkspaceChangedEventArgs(ActiveWorkspace));

        return Task.CompletedTask;
    }

    private Task Mutate(Action<Workspace> change, Guid workspaceId)
    {
        if (Failure is not null)
        {
            return Task.FromException(Failure);
        }

        change(Find(workspaceId) ?? throw new ArgumentException("Unknown workspace.", nameof(workspaceId)));
        WorkspacesChanged?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }

    private Workspace? Find(Guid workspaceId)
        => _workspaces.FirstOrDefault(workspace => workspace.Id == workspaceId);
}
