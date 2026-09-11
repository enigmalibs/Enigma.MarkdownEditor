using System;
using System.IO;
using Enigma.MarkdownEditor.Core.Session;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Desktop.Services;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Services;

/// <summary>
/// What the user is told when the session could not be read. Worth telling rather than swallowing:
/// they left six tabs open and are looking at none, and silence reads as an editor that lost them.
/// </summary>
public sealed class SessionStartupNoticeTests : IDisposable
{
    private readonly string _root;
    private readonly string _sessionFile;

    public SessionStartupNoticeTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _sessionFile = Path.Combine(_root, AppPaths.SessionFileName);
    }

    [Fact]
    public void ASuccessfulLoad_SaysNothing()
    {
        StartupNotice notice = SessionStartupNotice.For(SessionLoader.Load(_sessionFile));

        Assert.False(notice.HasNotice);
        Assert.Same(StartupNotice.None, notice);
    }

    [Fact]
    public void AQuarantinedFile_SaysNothingWasReopenedAndWhereTheFileWent()
    {
        File.WriteAllText(_sessionFile, "{ not json at all");

        StartupNotice notice = SessionStartupNotice.For(SessionLoader.Load(_sessionFile));

        Assert.True(notice.HasNotice);
        Assert.Equal("The last session could not be restored", notice.Title);
        Assert.Contains("without reopening anything", notice.Message, StringComparison.Ordinal);
        Assert.Contains(AppPaths.QuarantinedSessionFileName, notice.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileThatBreaksARule_NamesTheRuleRatherThanTheParser()
    {
        File.WriteAllText(
            _sessionFile,
            $$"""{ "Version": {{SessionState.CurrentVersion + 1}} }""");

        StartupNotice notice = SessionStartupNotice.For(SessionLoader.Load(_sessionFile));

        Assert.True(notice.HasNotice);
        Assert.Contains(nameof(SessionState.Version), notice.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoFilesFailingAtOnce_AreStillOneMessage()
    {
        File.WriteAllText(_sessionFile, "{ broken");

        StartupNotice combined = StartupNotice.Combine(
            new StartupNotice("Settings could not be read", "Running on defaults."),
            SessionStartupNotice.For(SessionLoader.Load(_sessionFile)));

        // The InfoBar shows one message: a second ShowAsync replaces the first rather than queueing.
        Assert.True(combined.HasNotice);
        Assert.Contains("Settings could not be read", combined.Message, StringComparison.Ordinal);
        Assert.Contains("The last session could not be restored", combined.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANullResult_IsRejected()
        => Assert.Throws<ArgumentNullException>(() => SessionStartupNotice.For(null!));

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
