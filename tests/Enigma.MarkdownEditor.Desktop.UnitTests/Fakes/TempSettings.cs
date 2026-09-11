using System;
using System.IO;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Settings;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// A real settings object over a throw-away folder, for the services that only need one.
/// </summary>
/// <remarks>
/// The loader rather than a hand-built <c>AppSettings</c>: the defaults a test asserts against are the
/// ones the application actually starts with, and there is no second place where they are written
/// down.
/// </remarks>
internal sealed class TempSettings : IDisposable
{
    private readonly string _root;

    public TempSettings()
    {
        _root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        Config = SettingsLoader.Load(Path.Combine(_root, AppPaths.SettingsFileName)).Config;
    }

    public IConfig<AppSettings> Config { get; }

    public AppSettings Value => Config.Value;

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
