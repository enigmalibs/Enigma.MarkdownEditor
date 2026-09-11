using System;
using System.IO;
using System.Text;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Desktop.Services;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;

/// <summary>
/// A document manager wired to fakes, over a throw-away folder of real files.
/// </summary>
/// <remarks>
/// Real files rather than a stand-in file system: the whole point of the save path is what lands on
/// disk, and encoding, byte-order marks and line endings are exactly the things a fake would get
/// wrong in the same way the code under test does. What is faked is only the things a unit test
/// cannot have — the dialogs, the info bar, the UI thread and the file watchers.
/// </remarks>
internal sealed class EditorHarness : IDisposable
{
    public EditorHarness()
    {
        Root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);

        Settings = SettingsLoader.Load(Path.Combine(Root, AppPaths.SettingsFileName)).Config;

        ImagePaste = new ImagePasteService(Settings, InfoBar, NullLogger<ImagePasteService>.Instance);
        Vim = new VimSession(Settings);

        // A workspace over the harness's own folder, so a tab's colour can be resolved: without one
        // every tab would answer None and the colour rules would go untested through this route.
        Workspaces.Seed("Work", active: true, Root);

        Documents = new DocumentManagerViewModel(
            Settings,
            Workspaces,
            Prompts,
            InfoBar,
            Idle,
            Watchers,
            Dispatcher,
            Assists,
            Vim,
            ImagePaste,
            Fonts,
            NullLogger<DocumentManagerViewModel>.Instance);
    }

    public string Root { get; }

    public IConfig<AppSettings> Settings { get; }

    public FakeWorkspaceService Workspaces { get; } = new();

    public FakeDocumentPrompts Prompts { get; } = new();

    public FakeInfoBarService InfoBar { get; } = new();

    public FakeIdleScheduler Idle { get; } = new();

    public FakeDocumentFileWatcherFactory Watchers { get; } = new();

    public FakeUiDispatcher Dispatcher { get; } = new();

    public FakeEditingAssistPolicy Assists { get; } = new();

    /// <summary>
    /// A real catalog over an imaginary machine's fonts (see <see cref="TestFonts"/>): the editor's
    /// font family is resolved through it, and the running machine's own font list is neither stable
    /// across machines nor available at all off the headless platform.
    /// </summary>
    public FontCatalog Fonts { get; } = TestFonts.Catalog();

    /// <summary>
    /// The real vim session, over the harness's own settings: it holds nothing a test cannot see, and
    /// what the editor pane says about the mode is exactly what it is asked here.
    /// </summary>
    public VimSession Vim { get; }

    /// <summary>
    /// The real paste service, over the harness's own folder: what it writes and where is the point,
    /// and only the bitmap itself is faked (see <see cref="FakePastedImage"/>).
    /// </summary>
    public ImagePasteService ImagePaste { get; }

    public DocumentManagerViewModel Documents { get; }

    public string Combine(string name) => Path.Combine(Root, name);

    /// <summary>
    /// Writes a file with exactly the bytes given, so a test controls its line endings.
    /// </summary>
    public string Write(string name, string content = "# note\n")
    {
        string path = Combine(name);
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes(content));

        return path;
    }

    public string ReadRaw(string name) => Encoding.UTF8.GetString(File.ReadAllBytes(Combine(name)));

    /// <summary>
    /// Changes a file behind the editor's back and tells its watcher, which is what an edit from
    /// another application amounts to.
    /// </summary>
    public void ChangeOnDisk(string path, string content)
    {
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes(content));
        Watchers.For(path).Raise();
    }

    /// <summary>
    /// Deletes a file behind the editor's back and tells its watcher.
    /// </summary>
    public void DeleteOnDisk(string path)
    {
        File.Delete(path);
        Watchers.For(path).Raise();
    }

    public void Dispose()
    {
        Documents.Dispose();
        Idle.Dispose();

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
