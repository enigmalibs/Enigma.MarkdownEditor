using System.IO;
using System.Threading.Tasks;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Configuration;
using Enigma.MarkdownEditor.Core.Settings;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Settings;

/// <summary>
/// The recovery path. A settings file the user broke by hand must not stop the editor from starting,
/// and it must not be thrown away either.
/// </summary>
public sealed class SettingsLoaderTests
{
    [Theory]
    [InlineData("{ not json at all", "malformed JSON")]
    [InlineData("{ \"EditorFontSize\": 9000 }", "a value outside its range")]
    [InlineData("{ \"ImageFolderName\": \"nested/folder\" }", "a value of the wrong shape")]
    [InlineData("{ \"Theme\": 42 }", "an undefined enum value")]
    public void ABrokenFile_IsQuarantinedAndTheDefaultsAreUsed(string json, string reason)
    {
        using TempSettingsFolder folder = new();
        folder.WriteSettingsFile(json);

        ConfigLoadResult<AppSettings> result = SettingsLoader.Load(folder.SettingsFile);

        Assert.NotNull(result.Failure);
        Assert.Equal(folder.QuarantinedSettingsFile, result.QuarantinedFilePath);
        Assert.False(result.PersistenceDisabled, reason);

        // The broken content is preserved, the original path is free, and reading a value works.
        Assert.False(File.Exists(folder.SettingsFile));
        Assert.Equal(json, File.ReadAllText(folder.QuarantinedSettingsFile));
        Assert.Equal(new AppSettings().EditorFontSize, result.Config.Value.EditorFontSize);
    }

    [Fact]
    public void ABrokenFile_ReportsTheValidationErrorsItFailedOn()
    {
        using TempSettingsFolder folder = new();
        folder.WriteSettingsFile("{ \"EditorFontSize\": 9000, \"ImageFolderName\": \"a/b\" }");

        ConfigLoadResult<AppSettings> result = SettingsLoader.Load(folder.SettingsFile);

        ConfigurationValidationException failure =
            Assert.IsType<ConfigurationValidationException>(result.Failure);

        // Both halves of the validation are reported together — the attribute's range and the
        // validator's shape rule — so the user fixes the file in one pass.
        Assert.Equal(2, failure.Errors.Count);
        Assert.Contains(failure.Errors, error => error.MemberPath == nameof(AppSettings.EditorFontSize));
        Assert.Contains(failure.Errors, error => error.MemberPath == nameof(AppSettings.ImageFolderName));
    }

    [Fact]
    public async Task AfterQuarantine_TheNextWriteLaysDownACleanFile()
    {
        using TempSettingsFolder folder = new();
        folder.WriteSettingsFile("{ nonsense");

        ConfigLoadResult<AppSettings> result = SettingsLoader.Load(folder.SettingsFile);
        result.Config.Value.Theme = AppTheme.Dark;
        await result.Config.WriteAsync(TestContext.Current.CancellationToken);

        // The recovered configuration still points at the real settings file, not at the quarantine
        // copy — otherwise the user's next change would vanish on restart.
        Assert.Equal(folder.SettingsFile, result.Config.FilePath);
        Assert.Equal(AppTheme.Dark, SettingsLoader.Load(folder.SettingsFile).Config.Value.Theme);
    }

    [Fact]
    public void QuarantiningTwice_OverwritesThePreviousQuarantineFile()
    {
        using TempSettingsFolder folder = new();

        folder.WriteSettingsFile("{ first broken file");
        SettingsLoader.Load(folder.SettingsFile);

        folder.WriteSettingsFile("{ second broken file");
        ConfigLoadResult<AppSettings> second = SettingsLoader.Load(folder.SettingsFile);

        Assert.Equal(folder.QuarantinedSettingsFile, second.QuarantinedFilePath);
        Assert.Equal("{ second broken file", File.ReadAllText(folder.QuarantinedSettingsFile));
    }

    [Fact]
    public void AnEmptyFile_IsTreatedAsAMissingOneRatherThanABrokenOne()
    {
        using TempSettingsFolder folder = new();
        folder.WriteSettingsFile(string.Empty);

        ConfigLoadResult<AppSettings> result = SettingsLoader.Load(folder.SettingsFile);

        Assert.Null(result.Failure);
        Assert.Null(result.QuarantinedFilePath);
        Assert.Equal(new AppSettings().Theme, result.Config.Value.Theme);
    }

    [Fact]
    public void AnUnknownKey_IsIgnoredRatherThanTreatedAsBroken()
    {
        using TempSettingsFolder folder = new();

        // Settings removed by a later version must not brick a downgrade, so binding is not strict.
        folder.WriteSettingsFile("{ \"Theme\": \"Dark\", \"SomeSettingWeDropped\": true }");

        ConfigLoadResult<AppSettings> result = SettingsLoader.Load(folder.SettingsFile);

        Assert.Null(result.Failure);
        Assert.Equal(AppTheme.Dark, result.Config.Value.Theme);
    }
}
