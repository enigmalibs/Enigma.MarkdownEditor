using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Configuration;
using Enigma.MarkdownEditor.Core.Settings;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Settings;

/// <summary>
/// The fluent validator's one rule: an image folder must be a plain folder name. It is exercised
/// through the real configuration pipeline, which is the only way it ever runs.
/// </summary>
public sealed class AppSettingsValidatorTests
{
    [Theory]
    [InlineData("assets")]
    [InlineData("img")]
    [InlineData("_media")]
    [InlineData("images 2")]
    public void APlainFolderName_IsAccepted(string folderName)
    {
        using TempSettingsFolder folder = new();
        folder.WriteSettingsFile($"{{ \"ImageFolderName\": \"{folderName}\" }}");

        ConfigLoadResult<AppSettings> result = SettingsLoader.Load(folder.SettingsFile);

        Assert.Null(result.Failure);
        Assert.Equal(folderName, result.Config.Value.ImageFolderName);
    }

    [Theory]
    [InlineData("nested/folder", "a forward slash")]
    [InlineData("nested\\\\folder", "a backslash")]
    [InlineData("C:", "a drive")]
    [InlineData(".", "the current folder")]
    [InlineData("..", "the parent folder")]
    public void APathRatherThanAName_IsRejected(string folderName, string reason)
    {
        using TempSettingsFolder folder = new();
        folder.WriteSettingsFile($"{{ \"ImageFolderName\": \"{folderName}\" }}");

        ConfigLoadResult<AppSettings> result = SettingsLoader.Load(folder.SettingsFile);

        ConfigurationValidationException failure =
            Assert.IsType<ConfigurationValidationException>(result.Failure);
        ValidationError error = Assert.Single(failure.Errors);

        Assert.Equal(nameof(AppSettings.ImageFolderName), error.MemberPath);
        Assert.Contains("single folder name", error.Message);
        Assert.False(string.IsNullOrEmpty(reason));
    }

    [Fact]
    public void AnEmptyFolderName_IsReportedOnceByTheAttributeAndNotAlsoByTheRule()
    {
        using TempSettingsFolder folder = new();
        folder.WriteSettingsFile("{ \"ImageFolderName\": \"\" }");

        ConfigLoadResult<AppSettings> result = SettingsLoader.Load(folder.SettingsFile);

        ConfigurationValidationException failure =
            Assert.IsType<ConfigurationValidationException>(result.Failure);

        // One broken value, one message: the shape rule stays quiet on a value [Required] already
        // rejected, so the user is not told the same thing twice in two different vocabularies.
        ValidationError error = Assert.Single(failure.Errors);
        Assert.Equal(nameof(AppSettings.ImageFolderName), error.MemberPath);
        Assert.DoesNotContain("single folder name", error.Message);
    }

    [Fact]
    public void AnEmptyEditorFontFamily_IsAccepted()
    {
        using TempSettingsFolder folder = new();
        folder.WriteSettingsFile("{ \"EditorFontFamily\": \"\" }");

        ConfigLoadResult<AppSettings> result = SettingsLoader.Load(folder.SettingsFile);

        // Empty is this setting's "automatic" value rather than a missing one, so it has to survive a
        // load — otherwise the default the class declares could never be written back.
        Assert.Null(result.Failure);
        Assert.Empty(result.Config.Value.EditorFontFamily);
    }

    [Fact]
    public void AnEditorFontFamilyThatIsNotInstalledHere_IsStillAccepted()
    {
        using TempSettingsFolder folder = new();

        // The comma stack this setting used to hold, and a settings file from a machine with fonts this
        // one does not have, are the same case: the value is kept and resolved at render time, because
        // rejecting it would throw away the choice the file was carried here to express.
        folder.WriteSettingsFile(
            "{ \"EditorFontFamily\": \"Cascadia Mono, Consolas, DejaVu Sans Mono, Liberation Mono, monospace\" }");

        ConfigLoadResult<AppSettings> result = SettingsLoader.Load(folder.SettingsFile);

        Assert.Null(result.Failure);
        Assert.StartsWith("Cascadia Mono,", result.Config.Value.EditorFontFamily);
    }

    [Fact]
    public void AnOverLongEditorFontFamily_IsRejected()
    {
        using TempSettingsFolder folder = new();
        folder.WriteSettingsFile($"{{ \"EditorFontFamily\": \"{new string('x', 201)}\" }}");

        ConfigLoadResult<AppSettings> result = SettingsLoader.Load(folder.SettingsFile);

        ConfigurationValidationException failure =
            Assert.IsType<ConfigurationValidationException>(result.Failure);
        ValidationError error = Assert.Single(failure.Errors);

        Assert.Equal(nameof(AppSettings.EditorFontFamily), error.MemberPath);
    }

    [Theory]
    [InlineData("PreviewFontFamily", "\"\"")]
    [InlineData("PreviewCodeFontFamily", "\"\"")]
    [InlineData("PreviewFontSize", "6")]
    [InlineData("PreviewFontSize", "72")]
    [InlineData("PreviewCodeFontSize", "6")]
    [InlineData("PreviewCodeFontSize", "72")]
    [InlineData("PreviewLineHeight", "1.0")]
    [InlineData("PreviewLineHeight", "2.5")]
    public void APreviewSettingInsideItsRange_IsAccepted(string key, string json)
    {
        using TempSettingsFolder folder = new();
        folder.WriteSettingsFile($"{{ \"{key}\": {json} }}");

        Assert.Null(SettingsLoader.Load(folder.SettingsFile).Failure);
    }

    [Theory]
    [InlineData("PreviewFontSize", "5", "under the smallest readable size")]
    [InlineData("PreviewFontSize", "73", "past the largest")]
    [InlineData("PreviewCodeFontSize", "5", "code under the smallest readable size")]
    [InlineData("PreviewCodeFontSize", "73", "code past the largest")]
    [InlineData("PreviewLineHeight", "0.9", "lines that would overlap")]
    [InlineData("PreviewLineHeight", "2.6", "a paragraph that stops reading as one")]
    public void APreviewSettingOutsideItsRange_IsReportedAgainstItsOwnProperty(
        string key,
        string json,
        string reason)
    {
        using TempSettingsFolder folder = new();
        folder.WriteSettingsFile($"{{ \"{key}\": {json} }}");

        ConfigLoadResult<AppSettings> result = SettingsLoader.Load(folder.SettingsFile);

        // The same way every other range in this class is reported: one attribute, one message, named
        // after the property the user has to go and fix.
        ConfigurationValidationException failure =
            Assert.IsType<ConfigurationValidationException>(result.Failure);
        ValidationError error = Assert.Single(failure.Errors);

        Assert.Equal(key, error.MemberPath);
        Assert.False(string.IsNullOrEmpty(reason));
    }

    [Theory]
    [InlineData("PreviewFontFamily")]
    [InlineData("PreviewCodeFontFamily")]
    public void AnOverLongPreviewFontFamily_IsRejected(string key)
    {
        using TempSettingsFolder folder = new();
        folder.WriteSettingsFile($"{{ \"{key}\": \"{new string('x', 201)}\" }}");

        ConfigLoadResult<AppSettings> result = SettingsLoader.Load(folder.SettingsFile);

        ConfigurationValidationException failure =
            Assert.IsType<ConfigurationValidationException>(result.Failure);

        Assert.Equal(key, Assert.Single(failure.Errors).MemberPath);
    }
}
