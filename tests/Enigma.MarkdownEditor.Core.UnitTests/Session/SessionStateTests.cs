using System;
using System.IO;
using System.Threading.Tasks;
using Enigma.Configuration;
using Enigma.MarkdownEditor.Core.Configuration;
using Enigma.MarkdownEditor.Core.Session;
using Enigma.MarkdownEditor.Core.Settings;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Session;

/// <summary>
/// The session file itself: what round-trips through a restart, what a hand-edited or future file
/// does, and what is left when it cannot be read at all. The last one matters more here than for the
/// other two files — a session that fails must cost a session and nothing else.
/// </summary>
public sealed class SessionStateTests : IDisposable
{
    private readonly TempSessionFolder _folder = new();

    [Fact]
    public void AMissingFile_YieldsAFirstRunAndReportsNoFailure()
    {
        ConfigLoadResult<SessionState> result = SessionLoader.Load(_folder.SessionFile);

        Assert.Null(result.Failure);
        Assert.Null(result.QuarantinedFilePath);
        Assert.False(result.PersistenceDisabled);

        SessionState state = result.Config.Value;

        Assert.Equal(SessionState.CurrentVersion, state.Version);
        Assert.Empty(state.OpenDocuments);
        Assert.Equal(-1, state.ActiveDocumentIndex);
        Assert.Null(state.ActiveWorkspaceId);
        Assert.Equal(ViewMode.Split, state.ViewMode);
        Assert.Equal(SidebarTab.Explorer, state.SidebarTab);
        Assert.True(state.IsSidebarVisible);
        Assert.Null(state.Window);
        Assert.Null(state.Panes);
        Assert.False(File.Exists(_folder.SessionFile));
    }

    [Fact]
    public async Task TheFile_RoundTripsEverythingARestartHasToPutBack()
    {
        Guid workspace = Guid.NewGuid();
        string first = _folder.ExistingNote("first.md");
        string second = _folder.ExistingNote("second.md");

        IConfig<SessionState> config = _folder.LoadConfig();
        SessionState written = config.Value;

        written.ActiveWorkspaceId = workspace;
        written.OpenDocuments = [first, second];
        written.ActiveDocumentIndex = 1;
        written.ViewMode = ViewMode.PreviewOnly;
        written.SidebarTab = SidebarTab.Search;
        written.IsSidebarVisible = false;
        written.Window = new WindowPlacement { X = 120, Y = 80, Width = 1600, Height = 900, IsMaximized = true };
        written.Panes = new PaneLayout { SidebarWidth = 320, EditorWeight = 2, PreviewWeight = 1 };

        await config.WriteAsync(TestContext.Current.CancellationToken);

        // A second load over the same path is the restart: nothing is carried over in memory.
        SessionState reread = SessionLoader.Load(_folder.SessionFile).Config.Value;

        Assert.Equal(workspace, reread.ActiveWorkspaceId);
        Assert.Equal([first, second], reread.OpenDocuments);
        Assert.Equal(1, reread.ActiveDocumentIndex);
        Assert.Equal(ViewMode.PreviewOnly, reread.ViewMode);
        Assert.Equal(SidebarTab.Search, reread.SidebarTab);
        Assert.False(reread.IsSidebarVisible);

        Assert.NotNull(reread.Window);
        Assert.Equal(120, reread.Window.X);
        Assert.Equal(80, reread.Window.Y);
        Assert.Equal(1600, reread.Window.Width);
        Assert.Equal(900, reread.Window.Height);
        Assert.True(reread.Window.IsMaximized);

        Assert.NotNull(reread.Panes);
        Assert.Equal(320, reread.Panes.SidebarWidth);
        Assert.Equal(2, reread.Panes.EditorWeight);
        Assert.Equal(1, reread.Panes.PreviewWeight);
    }

    [Fact]
    public async Task TheFile_StaysHandEditable()
    {
        IConfig<SessionState> config = _folder.LoadConfig();
        config.Value.ViewMode = ViewMode.EditorOnly;
        config.Value.SidebarTab = SidebarTab.Search;

        await config.WriteAsync(TestContext.Current.CancellationToken);

        string json = _folder.ReadSessionFile();

        // By name, not by number — which is why both enums live in Core rather than beside the
        // controls that draw them: the numeric values may be reordered, the names may not.
        Assert.Contains("\"ViewMode\": \"EditorOnly\"", json, StringComparison.Ordinal);
        Assert.Contains("\"SidebarTab\": \"Search\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void APartialFile_KeepsTheDefaultsForEverythingItDoesNotMention()
    {
        _folder.WriteSessionFile("""{ "SidebarTab": "Search" }""");

        SessionState state = SessionLoader.Load(_folder.SessionFile).Config.Value;

        Assert.Equal(SidebarTab.Search, state.SidebarTab);
        Assert.Equal(ViewMode.Split, state.ViewMode);
        Assert.True(state.IsSidebarVisible);
        Assert.Equal(-1, state.ActiveDocumentIndex);
    }

    [Fact]
    public void APropertyThisBuildDoesNotKnow_IsIgnoredRatherThanFatal()
    {
        // The forward half of the version story: a field added by a later build, in a file whose
        // Version this build still understands, is skipped. Only the Version itself decides whether a
        // file is readable at all.
        _folder.WriteSessionFile("""
            {
              "Version": 1,
              "SidebarTab": "Search",
              "SomethingALaterBuildAdded": { "Nested": [1, 2, 3] }
            }
            """);

        ConfigLoadResult<SessionState> result = SessionLoader.Load(_folder.SessionFile);

        Assert.Null(result.Failure);
        Assert.Equal(SidebarTab.Search, result.Config.Value.SidebarTab);
    }

    [Fact]
    public void AFileFromANewerVersion_IsRejectedRatherThanReadAsBestItCan()
    {
        _folder.WriteSessionFile($$"""
            {
              "Version": {{SessionState.CurrentVersion + 1}},
              "OpenDocuments": []
            }
            """);

        AssertRejected(nameof(SessionState.Version));
    }

    [Fact]
    public void ADocumentWithoutAPath_IsRejected()
    {
        _folder.WriteSessionFile("""
            {
              "Version": 1,
              "OpenDocuments": ["   "],
              "ActiveDocumentIndex": 0
            }
            """);

        AssertRejected(nameof(SessionState.OpenDocuments), "without a path");
    }

    [Fact]
    public void AnActiveTabPastTheEndOfTheTabList_IsRejected()
    {
        _folder.WriteSessionFile("""
            {
              "Version": 1,
              "OpenDocuments": ["/tmp/one.md"],
              "ActiveDocumentIndex": 4
            }
            """);

        // The one pair in the file that can contradict itself, and nothing this editor writes can
        // produce it — so there is no honest way to guess what it meant.
        AssertRejected(nameof(SessionState.ActiveDocumentIndex), "position of one of the open documents");
    }

    [Fact]
    public void NoActiveTabAtAll_IsAccepted()
    {
        _folder.WriteSessionFile("""
            {
              "Version": 1,
              "OpenDocuments": ["/tmp/one.md"],
              "ActiveDocumentIndex": -1
            }
            """);

        Assert.Null(SessionLoader.Load(_folder.SessionFile).Failure);
    }

    [Fact]
    public void AWindowWithNonsenseBounds_IsAcceptedAndLeftToTheClamp()
    {
        _folder.WriteSessionFile("""
            {
              "Version": 1,
              "Window": { "X": -99999, "Y": -99999, "Width": 0, "Height": 0 }
            }
            """);

        // Not a broken file: the world moved, and WindowPlacements.Clamp is what absorbs it. Throwing
        // the whole session away over a window that has to be nudged would be the wrong trade.
        ConfigLoadResult<SessionState> result = SessionLoader.Load(_folder.SessionFile);

        Assert.Null(result.Failure);
        Assert.NotNull(result.Config.Value.Window);
    }

    [Fact]
    public void ABrokenFile_IsQuarantinedAndTheEditorStartsAsAFirstRunWould()
    {
        _folder.WriteSessionFile("{ not json at all");

        ConfigLoadResult<SessionState> result = SessionLoader.Load(_folder.SessionFile);

        Assert.NotNull(result.Failure);
        Assert.Equal(_folder.QuarantinedSessionFile, result.QuarantinedFilePath);
        Assert.False(result.PersistenceDisabled);
        Assert.Empty(result.Config.Value.OpenDocuments);
        Assert.Equal(ViewMode.Split, result.Config.Value.ViewMode);
        Assert.Equal("{ not json at all", _folder.ReadQuarantinedFile());
    }

    [Fact]
    public async Task AfterQuarantine_TheNextWriteLaysDownACleanFileAtTheRealPath()
    {
        _folder.WriteSessionFile("{ broken");

        ConfigLoadResult<SessionState> result = SessionLoader.Load(_folder.SessionFile);
        result.Config.Value.SidebarTab = SidebarTab.Search;
        await result.Config.WriteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(_folder.SessionFile, result.Config.FilePath);
        Assert.Equal(SidebarTab.Search, SessionLoader.Load(_folder.SessionFile).Config.Value.SidebarTab);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TheLoader_RejectsAPathItCannotUse(string? path)
        => Assert.ThrowsAny<ArgumentException>(() => SessionLoader.Load(path!));

    public void Dispose() => _folder.Dispose();

    private void AssertRejected(string memberPath, string? messagePart = null)
    {
        ConfigLoadResult<SessionState> result = SessionLoader.Load(_folder.SessionFile);

        ConfigurationValidationException failure =
            Assert.IsType<ConfigurationValidationException>(result.Failure);

        Assert.Contains(failure.Errors, error => error.MemberPath == memberPath);

        if (messagePart is not null)
        {
            Assert.Contains(failure.Errors, error => error.Message.Contains(messagePart, StringComparison.Ordinal));
        }

        // Whatever was wrong, the editor still has a session object to start from.
        Assert.Equal(ViewMode.Split, result.Config.Value.ViewMode);
    }
}
