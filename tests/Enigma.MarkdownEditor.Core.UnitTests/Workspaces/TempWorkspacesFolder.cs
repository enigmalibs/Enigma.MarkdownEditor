using System;
using System.IO;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Core.Workspaces;

namespace Enigma.MarkdownEditor.Core.UnitTests.Workspaces;

/// <summary>
/// A throw-away folder holding a real workspaces file and real directories to point it at, so the
/// workspace tests drive the actual read/write/quarantine pipeline instead of a stand-in for it.
/// </summary>
internal sealed class TempWorkspacesFolder : IDisposable
{
    private readonly string _root;

    public TempWorkspacesFolder()
    {
        _root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public string WorkspacesFile => Path.Combine(_root, AppPaths.WorkspacesFileName);

    public string QuarantinedWorkspacesFile => Path.Combine(_root, AppPaths.QuarantinedWorkspacesFileName);

    /// <summary>
    /// Creates a directory that exists, to be used as a workspace root.
    /// </summary>
    public string ExistingDirectory(string name)
    {
        string path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);

        return path;
    }

    /// <summary>
    /// Names a directory that does not exist — an unplugged drive, as far as a workspace is concerned.
    /// </summary>
    public string MissingDirectory(string name) => Path.Combine(_root, name);

    public void WriteWorkspacesFile(string json) => File.WriteAllText(WorkspacesFile, json);

    /// <summary>
    /// Makes every further write to the workspaces file fail, by putting a directory where the file
    /// belongs — which is the cheapest way to reach the rollback path of a mutation that cannot be
    /// persisted. Call it on a service that has already been built, since the load has to succeed
    /// first.
    /// </summary>
    public void BlockWrites()
    {
        if (File.Exists(WorkspacesFile))
        {
            File.Delete(WorkspacesFile);
        }

        Directory.CreateDirectory(WorkspacesFile);
    }

    public string ReadWorkspacesFile() => File.ReadAllText(WorkspacesFile);

    public string ReadQuarantinedFile() => File.ReadAllText(QuarantinedWorkspacesFile);

    public IConfig<WorkspacesConfig> LoadConfig() => WorkspacesLoader.Load(WorkspacesFile).Config;

    /// <summary>
    /// Builds a service over the file as it currently stands — called twice to model a restart.
    /// </summary>
    public WorkspaceService CreateService() => new(LoadConfig());

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A leftover temp folder is not worth failing a green test over.
        }
    }
}
