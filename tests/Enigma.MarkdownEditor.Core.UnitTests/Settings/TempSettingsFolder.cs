using System;
using System.IO;
using Enigma.MarkdownEditor.Core.Settings;

namespace Enigma.MarkdownEditor.Core.UnitTests.Settings;

/// <summary>
/// A throw-away folder to bind a real settings file into, so the settings tests exercise the actual
/// read/write/quarantine pipeline instead of a stand-in for it.
/// </summary>
internal sealed class TempSettingsFolder : IDisposable
{
    private readonly string _root;

    public TempSettingsFolder()
    {
        _root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public string SettingsFile => Path.Combine(_root, AppPaths.SettingsFileName);

    public string QuarantinedSettingsFile => Path.Combine(_root, AppPaths.QuarantinedSettingsFileName);

    public void WriteSettingsFile(string json) => File.WriteAllText(SettingsFile, json);

    public string ReadSettingsFile() => File.ReadAllText(SettingsFile);

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
