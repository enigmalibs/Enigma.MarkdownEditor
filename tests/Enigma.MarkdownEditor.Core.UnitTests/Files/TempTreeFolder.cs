using System;
using System.IO;

namespace Enigma.MarkdownEditor.Core.UnitTests.Files;

/// <summary>
/// A throw-away directory tree, so the lister and the watcher are tested against the file system they
/// actually run on rather than against a stand-in for it.
/// </summary>
internal sealed class TempTreeFolder : IDisposable
{
    public TempTreeFolder()
    {
        Root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string Combine(string relativePath) => Path.Combine(Root, relativePath);

    public string AddDirectory(string relativePath)
    {
        string path = Combine(relativePath);
        Directory.CreateDirectory(path);

        return path;
    }

    public string AddFile(string relativePath, string content = "")
    {
        string path = Combine(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);

        return path;
    }

    /// <summary>
    /// Marks an entry hidden, which on Windows is how <c>.git</c> and friends are usually flagged on
    /// top of the leading dot. A no-op elsewhere, where the attribute does not exist.
    /// </summary>
    public static void Hide(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.Hidden);
        }
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A leftover temp folder is not worth failing a green test over.
        }
    }
}
