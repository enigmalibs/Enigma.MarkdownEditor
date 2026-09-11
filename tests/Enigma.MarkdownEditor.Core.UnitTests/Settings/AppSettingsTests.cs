using Enigma.MarkdownEditor.Core.Settings;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Settings;

/// <summary>
/// The defaults are the application's defaults — a missing or quarantined settings file falls back to
/// exactly this object — so they are asserted rather than assumed.
/// </summary>
public sealed class AppSettingsTests
{
    [Fact]
    public void Defaults_AreTheDocumentedOnes()
    {
        AppSettings settings = new();

        Assert.Equal(AppTheme.System, settings.Theme);
        Assert.True(settings.VimEnabled);
        Assert.False(settings.AutosaveEnabled);
        Assert.Equal(2, settings.AutosaveDelaySeconds);
        Assert.False(settings.ShowAllFiles);
        Assert.Equal(string.Empty, settings.EditorFontFamily);
        Assert.Equal(16, settings.EditorFontSize);
        Assert.True(settings.WordWrap);
        Assert.Equal(string.Empty, settings.PreviewFontFamily);
        Assert.Equal(18, settings.PreviewFontSize);
        Assert.Equal(string.Empty, settings.PreviewCodeFontFamily);
        Assert.Equal(14, settings.PreviewCodeFontSize);
        Assert.Equal(1.5, settings.PreviewLineHeight);
        Assert.Equal("assets", settings.ImageFolderName);
        Assert.False(settings.DebugLogging);
    }

    [Fact]
    public void TheDefaultEditorFontFamily_IsEmptyRatherThanAFamilyNameOrAStack()
    {
        // Empty is the "automatic" value, and it is the only default this assembly can hold: naming a
        // family here would name one that half the machines this runs on do not have, and naming
        // several — the comma-separated stack this setting used to default to — names one family and
        // some ignored text, because that is all a font family list is to the renderer.
        AppSettings settings = new();

        Assert.Empty(settings.EditorFontFamily);
        Assert.DoesNotContain(",", settings.EditorFontFamily);
    }

    [Fact]
    public void TheDefaultPreviewFontFamilies_AreEmptyForTheSameReason()
    {
        // Neither "the default reading font" nor "the best monospace font here" is a name this
        // assembly can write down — it holds no reference to the framework that could ask the machine
        // — so both are expressed as the value the property can hold and resolved at render time.
        AppSettings settings = new();

        Assert.Empty(settings.PreviewFontFamily);
        Assert.Empty(settings.PreviewCodeFontFamily);
    }
}
