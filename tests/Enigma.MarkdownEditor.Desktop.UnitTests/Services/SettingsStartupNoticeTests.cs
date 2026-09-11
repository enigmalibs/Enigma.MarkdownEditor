using System;
using System.IO;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Desktop.Services;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Services;

/// <summary>
/// What the user is actually told when their settings file could not be read. The message has to say
/// three things: that defaults are in use, where their file went, and what was wrong with it.
/// </summary>
public sealed class SettingsStartupNoticeTests : IDisposable
{
    private readonly string _root;
    private readonly string _settingsFile;

    public SettingsStartupNoticeTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _settingsFile = Path.Combine(_root, AppPaths.SettingsFileName);
    }

    [Fact]
    public void ASuccessfulLoad_SaysNothing()
    {
        StartupNotice notice = SettingsStartupNotice.For(SettingsLoader.Load(_settingsFile));

        Assert.False(notice.HasNotice);
        Assert.Same(StartupNotice.None, notice);
    }

    [Fact]
    public void AQuarantinedFile_SaysWhereItWentAndWhatWasWrong()
    {
        File.WriteAllText(_settingsFile, "{ \"EditorFontSize\": 9000 }");

        StartupNotice notice = SettingsStartupNotice.For(SettingsLoader.Load(_settingsFile));

        Assert.True(notice.HasNotice);
        Assert.Equal("Settings could not be read", notice.Title);
        Assert.Contains("default settings", notice.Message);
        Assert.Contains(AppPaths.QuarantinedSettingsFileName, notice.Message);
        Assert.Contains(nameof(AppSettings.EditorFontSize), notice.Message);
    }

    [Fact]
    public void AMalformedFile_QuotesTheParserRatherThanAListOfRules()
    {
        File.WriteAllText(_settingsFile, "{ this is not json");

        StartupNotice notice = SettingsStartupNotice.For(SettingsLoader.Load(_settingsFile));

        Assert.True(notice.HasNotice);
        Assert.Contains(AppPaths.SettingsFileName, notice.Message);
    }

    [Fact]
    public void ANullResult_IsRejected()
        => Assert.Throws<ArgumentNullException>(() => SettingsStartupNotice.For(null!));

    [Fact]
    public void TheEmptyNotice_HasNoTitleOrMessage()
    {
        Assert.False(StartupNotice.None.HasNotice);
        Assert.Null(StartupNotice.None.Title);
        Assert.Null(StartupNotice.None.Message);
    }

    [Theory]
    [InlineData(null, "message")]
    [InlineData("title", null)]
    [InlineData("", "message")]
    [InlineData("title", "   ")]
    public void ANoticeWithoutBothHalves_IsRejected(string? title, string? message)
        => Assert.ThrowsAny<ArgumentException>(() => new StartupNotice(title!, message!));

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
