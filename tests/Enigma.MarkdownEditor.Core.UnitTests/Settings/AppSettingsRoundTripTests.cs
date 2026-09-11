using System.IO;
using System.Threading.Tasks;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Configuration;
using Enigma.MarkdownEditor.Core.Settings;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Settings;

/// <summary>
/// Round-trips the settings against a real file in a temp folder: what the editor persists must be
/// what the next start reads back, and the file must stay hand-editable while it is at it.
/// </summary>
public sealed class AppSettingsRoundTripTests
{
    [Fact]
    public async Task WrittenSettings_AreReadBackByAFreshConfiguration()
    {
        using TempSettingsFolder folder = new();
        ConfigLoadResult<AppSettings> first = SettingsLoader.Load(folder.SettingsFile);

        first.Config.Value.Theme = AppTheme.Dark;
        first.Config.Value.EditorFontSize = 21;
        first.Config.Value.VimEnabled = false;
        first.Config.Value.ImageFolderName = "images";
        first.Config.Value.PreviewFontFamily = "Georgia";
        first.Config.Value.PreviewFontSize = 22;
        first.Config.Value.PreviewCodeFontFamily = "Consolas";
        first.Config.Value.PreviewCodeFontSize = 12;
        first.Config.Value.PreviewLineHeight = 1.8;
        await first.Config.WriteAsync(TestContext.Current.CancellationToken);

        AppSettings reread = SettingsLoader.Load(folder.SettingsFile).Config.Value;

        Assert.Equal(AppTheme.Dark, reread.Theme);
        Assert.Equal(21, reread.EditorFontSize);
        Assert.False(reread.VimEnabled);
        Assert.Equal("images", reread.ImageFolderName);
        Assert.Equal("Georgia", reread.PreviewFontFamily);
        Assert.Equal(22, reread.PreviewFontSize);
        Assert.Equal("Consolas", reread.PreviewCodeFontFamily);

        // Deliberately not 14: a round trip that happens to assert the default proves nothing about
        // the key having been written and read back at all.
        Assert.Equal(12, reread.PreviewCodeFontSize);
        Assert.Equal(1.8, reread.PreviewLineHeight);
    }

    [Fact]
    public async Task TheWrittenFile_IsHandEditableJson()
    {
        using TempSettingsFolder folder = new();
        IConfig<AppSettings> config = SettingsLoader.Load(folder.SettingsFile).Config;

        config.Value.Theme = AppTheme.Light;
        await config.WriteAsync(TestContext.Current.CancellationToken);

        string json = folder.ReadSettingsFile();

        // The enum is written by name, not as a number: a user opening this file has to be able to
        // tell what it says, and to change it to "Dark" without consulting the source.
        Assert.Contains("\"Theme\": \"Light\"", json);
        Assert.Contains("\"EditorFontFamily\"", json);
        Assert.Contains("\"PreviewCodeFontSize\"", json);
    }

    [Fact]
    public void AMissingFile_YieldsTheDefaultsAndReportsNoFailure()
    {
        using TempSettingsFolder folder = new();

        ConfigLoadResult<AppSettings> result = SettingsLoader.Load(folder.SettingsFile);

        Assert.Null(result.Failure);
        Assert.Null(result.QuarantinedFilePath);
        Assert.False(result.PersistenceDisabled);
        Assert.Equal(new AppSettings().Theme, result.Config.Value.Theme);
        Assert.False(File.Exists(folder.SettingsFile));
    }

    [Fact]
    public void APartialFile_KeepsTheDefaultsForEverythingItDoesNotMention()
    {
        using TempSettingsFolder folder = new();
        folder.WriteSettingsFile("{ \"Theme\": \"Dark\" }");

        AppSettings settings = SettingsLoader.Load(folder.SettingsFile).Config.Value;

        Assert.Equal(AppTheme.Dark, settings.Theme);
        Assert.Equal(16, settings.EditorFontSize);
        Assert.True(settings.VimEnabled);
    }

    [Fact]
    public void ASettingsFileWrittenBeforeThePreviewSettingsExisted_LoadsWithTheirDefaults()
    {
        using TempSettingsFolder folder = new();

        // Every key the file was written with before this item, and none of the ones it did not know
        // about: a settings file the user already has must keep working and simply pick the defaults
        // up, rather than be quarantined for what it does not say.
        folder.WriteSettingsFile(
            """
            {
              "Theme": "Dark",
              "VimEnabled": false,
              "AutosaveEnabled": true,
              "AutosaveDelaySeconds": 5,
              "ShowAllFiles": true,
              "EditorFontFamily": "",
              "EditorFontSize": 18,
              "WordWrap": false,
              "ImageFolderName": "images",
              "DebugLogging": true
            }
            """);

        ConfigLoadResult<AppSettings> result = SettingsLoader.Load(folder.SettingsFile);
        AppSettings settings = result.Config.Value;

        Assert.Null(result.Failure);
        Assert.Null(result.QuarantinedFilePath);

        // What the file says is kept — and 18 is what such a file held, back when it was the editor's
        // default. It is deliberately not the current default of 16: an assertion that happens to
        // match the default proves nothing about the key having been read at all.
        Assert.Equal(18, settings.EditorFontSize);
        Assert.Equal("images", settings.ImageFolderName);

        // ...and what it does not mention is the new defaults.
        Assert.Empty(settings.PreviewFontFamily);
        Assert.Equal(18, settings.PreviewFontSize);
        Assert.Empty(settings.PreviewCodeFontFamily);
        Assert.Equal(14, settings.PreviewCodeFontSize);
        Assert.Equal(1.5, settings.PreviewLineHeight);
    }
}
