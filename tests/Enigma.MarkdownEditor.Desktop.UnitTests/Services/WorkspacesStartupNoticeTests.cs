using System;
using System.IO;
using Enigma.MarkdownEditor.Core.Settings;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.Services;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.Services;

/// <summary>
/// What the user is told when the workspaces file could not be read — and what they are told when both
/// files fail at once, since the shell has one InfoBar to say it in.
/// </summary>
public sealed class WorkspacesStartupNoticeTests : IDisposable
{
    private readonly string _root;
    private readonly string _workspacesFile;
    private readonly string _settingsFile;

    public WorkspacesStartupNoticeTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _workspacesFile = Path.Combine(_root, AppPaths.WorkspacesFileName);
        _settingsFile = Path.Combine(_root, AppPaths.SettingsFileName);
    }

    [Fact]
    public void ASuccessfulLoad_SaysNothing()
    {
        StartupNotice notice = WorkspacesStartupNotice.For(WorkspacesLoader.Load(_workspacesFile));

        Assert.False(notice.HasNotice);
        Assert.Same(StartupNotice.None, notice);
    }

    [Fact]
    public void AQuarantinedFile_SaysWhereItWentAndWhatWasWrong()
    {
        File.WriteAllText(_workspacesFile, "{ not json");

        StartupNotice notice = WorkspacesStartupNotice.For(WorkspacesLoader.Load(_workspacesFile));

        Assert.True(notice.HasNotice);
        Assert.Equal("Workspaces could not be read", notice.Title);
        Assert.Contains("with no workspaces", notice.Message);
        Assert.Contains(AppPaths.QuarantinedWorkspacesFileName, notice.Message);
    }

    [Fact]
    public void ARejectedWorkspace_IsNamedInTheMessage()
    {
        File.WriteAllText(_workspacesFile, $$"""
            {
              "Version": 1,
              "Workspaces": [{ "Id": "{{Guid.NewGuid()}}", "Name": "Documents", "RootDirectories": [] }]
            }
            """);

        StartupNotice notice = WorkspacesStartupNotice.For(WorkspacesLoader.Load(_workspacesFile));

        Assert.Contains("at least one folder", notice.Message);
    }

    [Fact]
    public void ANullResult_IsRejected()
        => Assert.Throws<ArgumentNullException>(() => WorkspacesStartupNotice.For(null!));

    [Fact]
    public void CombiningWithNothing_KeepsTheOneNoticeAsItIs()
    {
        StartupNotice notice = new("Settings could not be read", "Running on defaults.");

        Assert.Same(notice, StartupNotice.Combine(notice, StartupNotice.None));
        Assert.Same(notice, StartupNotice.Combine(StartupNotice.None, notice));
        Assert.Same(StartupNotice.None, StartupNotice.Combine(StartupNotice.None, StartupNotice.None));
    }

    [Fact]
    public void WhenBothFilesFail_TheUserGetsOneMessageSayingBoth()
    {
        File.WriteAllText(_settingsFile, "{ broken");
        File.WriteAllText(_workspacesFile, "{ broken too");

        StartupNotice notice = StartupNotice.Combine(
            SettingsStartupNotice.For(SettingsLoader.Load(_settingsFile)),
            WorkspacesStartupNotice.For(WorkspacesLoader.Load(_workspacesFile)));

        // One InfoBar, one notice: a second ShowAsync would replace the first rather than queue.
        Assert.True(notice.HasNotice);
        Assert.Contains("Settings could not be read", notice.Message);
        Assert.Contains("Workspaces could not be read", notice.Message);
    }

    [Fact]
    public void CombiningNothingAtAll_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => StartupNotice.Combine(null!, StartupNotice.None));
        Assert.Throws<ArgumentNullException>(() => StartupNotice.Combine(StartupNotice.None, null!));
    }

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
