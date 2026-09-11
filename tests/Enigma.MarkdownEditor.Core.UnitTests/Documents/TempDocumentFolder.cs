using System;
using System.IO;

namespace Enigma.MarkdownEditor.Core.UnitTests.Documents;

/// <summary>
/// A throw-away directory, so reading and writing documents is tested against the file system the
/// editor actually saves to rather than against a stand-in for it.
/// </summary>
internal sealed class TempDocumentFolder : IDisposable
{
    public TempDocumentFolder()
    {
        Root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string Combine(string name) => Path.Combine(Root, name);

    /// <summary>
    /// Writes a file from raw bytes, which is the only way to control its byte-order mark and its
    /// line terminators exactly.
    /// </summary>
    public string WriteBytes(string name, byte[] bytes)
    {
        string path = Combine(name);
        File.WriteAllBytes(path, bytes);

        return path;
    }

    public byte[] ReadBytes(string name) => File.ReadAllBytes(Combine(name));

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
