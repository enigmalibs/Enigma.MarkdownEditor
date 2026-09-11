using System;
using System.IO;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Session;
using Enigma.MarkdownEditor.Core.Settings;

namespace Enigma.MarkdownEditor.Core.UnitTests.Session;

/// <summary>
/// A throw-away folder holding a real session file and real notes for it to point at, so the session
/// tests drive the actual read/write/quarantine pipeline — and the actual disk, which is the whole
/// point of the "the note is no longer there" cases.
/// </summary>
internal sealed class TempSessionFolder : IDisposable
{
    private readonly string _root;

    public TempSessionFolder()
    {
        _root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public string SessionFile => Path.Combine(_root, AppPaths.SessionFileName);

    public string QuarantinedSessionFile => Path.Combine(_root, AppPaths.QuarantinedSessionFileName);

    /// <summary>
    /// Creates a note that exists, to be listed as an open document.
    /// </summary>
    public string ExistingNote(string name)
    {
        string path = Path.Combine(_root, name);
        File.WriteAllText(path, $"# {name}");

        return path;
    }

    /// <summary>
    /// Names a note that does not exist — one deleted since the session was written.
    /// </summary>
    public string MissingNote(string name) => Path.Combine(_root, name);

    public void WriteSessionFile(string json) => File.WriteAllText(SessionFile, json);

    public string ReadSessionFile() => File.ReadAllText(SessionFile);

    public string ReadQuarantinedFile() => File.ReadAllText(QuarantinedSessionFile);

    public IConfig<SessionState> LoadConfig() => SessionLoader.Load(SessionFile).Config;

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
