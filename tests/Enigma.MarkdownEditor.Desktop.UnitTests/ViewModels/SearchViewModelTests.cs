using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Core.Search;
using Enigma.MarkdownEditor.Core.Workspaces;
using Enigma.MarkdownEditor.Desktop.UnitTests.Fakes;
using Enigma.MarkdownEditor.Desktop.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Enigma.MarkdownEditor.Desktop.UnitTests.ViewModels;

/// <summary>
/// Tests for the sidebar's Search tab: when it decides to search, what it makes of the answer, and
/// what opening a hit asks the editor for.
/// </summary>
/// <remarks>
/// Over the real <see cref="WorkspaceSearchService"/> and real files, like the rest of the editor
/// suite: the scan is not what is under test here, but the grouping, the debounce and the status line
/// are all shaped by what a real scan reports, and a fake of it would only be this class's own
/// assumptions repeated back.
/// </remarks>
public sealed class SearchViewModelTests
{
    [Fact]
    public void WithNothingTyped_ThereIsNothingToShow()
    {
        using SearchHarness harness = new();

        Assert.False(harness.Search.HasQuery);
        Assert.False(harness.Search.HasResults);
        Assert.False(harness.Search.HasStatus);
        Assert.False(harness.Search.IsSearching);
    }

    [Fact]
    public void Typing_DoesNotSearchUntilTheBoxHasBeenIdle()
    {
        using SearchHarness harness = new();
        harness.Write("note.md", "the needle\n");

        harness.Search.Query = "needle";

        // A scan per keystroke would read the whole workspace once for every letter of a word, for
        // answers nobody would see.
        Assert.Empty(harness.Search.Groups);
        Assert.True(harness.Idle.IsScheduled(harness.Search));
        Assert.Equal(SearchViewModel.TypingDelay, harness.Idle.DelayFor(harness.Search));
    }

    [Fact]
    public void SeveralKeystrokes_LeaveOneScanPending()
    {
        using SearchHarness harness = new();

        harness.Search.Query = "n";
        harness.Search.Query = "ne";
        harness.Search.Query = "nee";

        Assert.Equal(1, harness.Idle.ScheduledCount);
    }

    [Fact]
    public async Task OnceTheBoxIsIdle_TheResultsArrive()
    {
        using SearchHarness harness = new();
        harness.Write("note.md", "the needle is here\n");

        await harness.SearchAsync("needle");

        SearchResultGroup group = Assert.Single(harness.Search.Groups);
        SearchHitItem hit = Assert.Single(group.Hits);

        Assert.Equal("note.md", group.Name);
        Assert.Equal("1", hit.LineLabel);
        Assert.Equal("needle", hit.Match);
        Assert.False(harness.Search.IsSearching);
    }

    [Fact]
    public void Enter_SearchesWithoutWaitingOutTheDebounce()
    {
        using SearchHarness harness = new();

        harness.Search.Query = "needle";
        harness.Search.SearchCommand.Execute(null);

        // Still one pending scan, but with nothing left to wait for.
        Assert.Equal(1, harness.Idle.ScheduledCount);
        Assert.Equal(TimeSpan.Zero, harness.Idle.DelayFor(harness.Search));
    }

    [Fact]
    public async Task TheCaseToggle_AsksTheQuestionAgainAtOnce()
    {
        using SearchHarness harness = new();
        harness.Write("note.md", "Needle\nneedle\n");

        await harness.SearchAsync("needle");
        Assert.Equal(2, harness.Search.Groups[0].Hits.Count);

        harness.Search.MatchCase = true;
        Assert.Equal(TimeSpan.Zero, harness.Idle.DelayFor(harness.Search));

        await harness.FireAsync();

        SearchHitItem hit = Assert.Single(harness.Search.Groups[0].Hits);
        Assert.Equal("2", hit.LineLabel);
    }

    [Fact]
    public async Task EmptyingTheBox_TakesTheResultsWithIt()
    {
        using SearchHarness harness = new();
        harness.Write("note.md", "the needle\n");

        await harness.SearchAsync("needle");
        Assert.True(harness.Search.HasResults);

        harness.Search.Query = string.Empty;

        // At once, rather than after another debounce: the pane must never show answers to a question
        // that is no longer on screen.
        Assert.False(harness.Search.HasResults);
        Assert.False(harness.Search.HasStatus);
        Assert.False(harness.Idle.IsScheduled(harness.Search));
    }

    [Fact]
    public async Task TheClearCommand_EmptiesTheBox()
    {
        using SearchHarness harness = new();
        harness.Write("note.md", "the needle\n");

        await harness.SearchAsync("needle");
        harness.Search.ClearCommand.Execute(null);

        Assert.Equal(string.Empty, harness.Search.Query);
        Assert.False(harness.Search.HasResults);
    }

    [Fact]
    public async Task OneFilesHits_AreOneGroup()
    {
        using SearchHarness harness = new();
        harness.Write("note.md", "needle\nfiller\nneedle\n");

        await harness.SearchAsync("needle");

        SearchResultGroup group = Assert.Single(harness.Search.Groups);

        Assert.Equal(2, group.Hits.Count);
        Assert.Equal("2", group.HitCountText);
        Assert.Equal(["1", "3"], group.Hits.Select(hit => hit.LineLabel));
    }

    [Fact]
    public async Task EachFile_IsAGroupOfItsOwn()
    {
        using SearchHarness harness = new();
        harness.Write("one.md", "needle\n");
        harness.Write("two.md", "needle\n");

        await harness.SearchAsync("needle");

        Assert.Equal(2, harness.Search.Groups.Count);
        Assert.Equal(["one.md", "two.md"], harness.Search.Groups.Select(group => group.Name).Order());
    }

    [Fact]
    public async Task AFileInASubfolder_ShowsTheFolderItIsIn()
    {
        using SearchHarness harness = new();
        harness.Write(Path.Combine("journal", "2026"), "day.md", "needle\n");

        await harness.SearchAsync("needle");

        SearchResultGroup group = Assert.Single(harness.Search.Groups);

        Assert.Equal("day.md", group.Name);
        Assert.Equal(Path.Combine("journal", "2026"), group.Location);
        Assert.True(group.HasLocation);
    }

    [Fact]
    public async Task AFileInTheRootItself_ShowsNoFolder()
    {
        using SearchHarness harness = new();
        harness.Write("note.md", "needle\n");

        await harness.SearchAsync("needle");

        Assert.Equal(string.Empty, Assert.Single(harness.Search.Groups).Location);
    }

    [Fact]
    public async Task TheStatusLine_CountsWhatWasFoundAndWhereItWas()
    {
        using SearchHarness harness = new();
        harness.Write("one.md", "needle\nneedle\n");
        harness.Write("two.md", "needle\n");

        await harness.SearchAsync("needle");

        Assert.Equal("3 results in 2 files", harness.Search.StatusText);
    }

    [Fact]
    public async Task OneOfEach_IsSaidInTheSingular()
    {
        using SearchHarness harness = new();
        harness.Write("one.md", "needle\n");

        await harness.SearchAsync("needle");

        Assert.Equal("1 result in 1 file", harness.Search.StatusText);
    }

    [Fact]
    public async Task NothingFound_SaysSoRatherThanShowingAnEmptyList()
    {
        using SearchHarness harness = new();
        harness.Write("note.md", "nothing of the sort\n");

        await harness.SearchAsync("needle");

        Assert.Equal("No results", harness.Search.StatusText);
        Assert.False(harness.Search.HasResults);
    }

    [Fact]
    public async Task WithNoWorkspaceOpen_TheStatusSaysWhatIsMissing()
    {
        using SearchHarness harness = new(withWorkspace: false);

        await harness.SearchAsync("needle");

        Assert.Equal("Open a workspace to search in it.", harness.Search.StatusText);
        Assert.False(harness.Search.HasResults);
    }

    [Fact]
    public async Task PastTheCap_TheStatusSaysTheAnswerIsNotTheWholeAnswer()
    {
        using SearchHarness harness = new();

        StringBuilder text = new();

        for (int line = 0; line < WorkspaceSearchRequest.DefaultMaxHits + 20; line++)
        {
            text.Append("needle\n");
        }

        harness.Write("many.md", text.ToString());

        await harness.SearchAsync("needle");

        Assert.Equal(WorkspaceSearchRequest.DefaultMaxHits, harness.Search.Groups[0].Hits.Count);
        Assert.Contains("truncated", harness.Search.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFileTooBigToSearch_IsSaidInTheStatus()
    {
        using SearchHarness harness = new();
        harness.Write("small.md", "needle\n");

        // Made big by its length rather than by its contents: the search skips it on the size it reads
        // off the directory entry, without opening it.
        using (FileStream huge = File.Create(Path.Combine(harness.Root, "huge.md")))
        {
            huge.SetLength(WorkspaceSearchRequest.DefaultMaxFileBytes + 1);
        }

        await harness.SearchAsync("needle");

        Assert.Contains("1 file too big to search", harness.Search.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AHitRow_CutsTheLineIntoWhatCameBeforeTheMatchAndWhatCameAfter()
    {
        using SearchHarness harness = new();
        harness.Write("note.md", "before needle after\n");

        await harness.SearchAsync("needle");

        SearchHitItem hit = Assert.Single(harness.Search.Groups[0].Hits);

        Assert.Equal("before ", hit.Prefix);
        Assert.Equal("needle", hit.Match);
        Assert.Equal(" after", hit.Suffix);
    }

    [Fact]
    public async Task AHitOnTheFilesName_ComesFirstAndHasNoLineNumber()
    {
        using SearchHarness harness = new();
        harness.Write("needle.md", "the needle again\n");

        await harness.SearchAsync("needle");

        SearchResultGroup group = Assert.Single(harness.Search.Groups);

        Assert.True(group.Hits[0].IsFileName);
        Assert.Equal(string.Empty, group.Hits[0].LineLabel);
        Assert.Equal("needle", group.Hits[0].Match);

        Assert.False(group.Hits[1].IsFileName);
        Assert.Equal("1", group.Hits[1].LineLabel);
    }

    [Fact]
    public async Task AHitsSelection_IsWhereTheMatchIs()
    {
        using SearchHarness harness = new();
        harness.Write("note.md", "first\nthe needle here\n");

        await harness.SearchAsync("needle");

        SearchHitItem hit = Assert.Single(harness.Search.Groups[0].Hits);

        Assert.Equal(new DocumentSelection(2, 5, 6), hit.Selection);
    }

    [Fact]
    public Task OpeningAHit_OpensTheFileAndLeavesTheCaretRequestOnIt()
        => UiThread.RunAsync(async () =>
        {
            using SearchHarness harness = new();
            string path = harness.Write("note.md", "first\nthe needle here\n");

            await harness.SearchAsync("needle");
            await harness.Search.OpenHitCommand.ExecuteAsync(harness.Search.Groups[0].Hits[0]);

            DocumentViewModel document = Assert.Single(harness.Editor.Documents.Documents);

            Assert.Equal(path, document.FilePath);
            Assert.Same(document, harness.Editor.Documents.ActiveDocument);

            // Left on the document rather than applied: the editor control is what puts the caret
            // there, and in this test there is none.
            Assert.Equal(new DocumentSelection(2, 5, 6), document.PendingSelection);
        });

    [Fact]
    public Task OpeningAHitInTheFileAlreadyShowing_StillAsksForTheCaretToMove()
        => UiThread.RunAsync(async () =>
        {
            using SearchHarness harness = new();
            string path = harness.Write("note.md", "first\nthe needle here\n");

            await harness.Editor.Documents.OpenAsync(path);

            int requests = 0;
            harness.Editor.Documents.SelectionRequested += (_, _) => requests++;

            await harness.SearchAsync("needle");
            await harness.Search.OpenHitCommand.ExecuteAsync(harness.Search.Groups[0].Hits[0]);

            // Nothing about the editor changed — the tab was already the one on screen — so the event
            // is the only thing that would tell the view to look at the request.
            Assert.Equal(1, requests);
            Assert.Single(harness.Editor.Documents.Documents);
            Assert.Equal(new DocumentSelection(2, 5, 6), harness.Editor.Documents.ActiveDocument!.PendingSelection);
        });

    [Fact]
    public async Task HitsFromASupersededScan_AreDropped()
    {
        using SearchHarness harness = new();
        harness.Write("alpha.md", "alpha\n");
        harness.Write("beta.md", "beta\n");

        // Held back so both scans finish before either one's hits are delivered — which is the race
        // itself, written down: the slow answer to the old question must not join the new one's list.
        harness.Dispatcher.Defer = true;

        harness.Search.Query = "alpha";
        await harness.FireAsync();

        harness.Search.Query = "beta";
        await harness.FireAsync();

        await harness.Dispatcher.DrainAsync();

        SearchResultGroup group = Assert.Single(harness.Search.Groups);
        Assert.Equal("beta.md", group.Name);
    }

    [Fact]
    public async Task ChangingWorkspace_AsksTheSameQuestionOfTheNewOne()
    {
        using SearchHarness harness = new();
        harness.Write("here.md", "needle\n");

        await harness.SearchAsync("needle");
        Assert.True(harness.Search.HasResults);

        using TempFolder elsewhere = new();
        Workspace other = harness.Workspaces.Seed("Elsewhere", active: false, elsewhere.Root);
        await harness.Workspaces.ActivateAsync(other.Id, TestContext.Current.CancellationToken);

        await harness.FireAsync();

        // The query stands; the answer is the other workspace's, which holds nothing.
        Assert.Equal("needle", harness.Search.Query);
        Assert.False(harness.Search.HasResults);
        Assert.Equal("No results", harness.Search.StatusText);
    }

    [Fact]
    public async Task ADisposedPane_StopsFollowingTheWorkspace()
    {
        using SearchHarness harness = new();
        harness.Search.Query = "needle";

        harness.Search.Dispose();

        Assert.False(harness.Idle.IsScheduled(harness.Search));

        Workspace other = harness.Workspaces.Seed("Elsewhere", active: false, harness.Root);
        await harness.Workspaces.ActivateAsync(other.Id, TestContext.Current.CancellationToken);

        Assert.False(harness.Idle.IsScheduled(harness.Search));
    }

    [Fact]
    public void EveryDependency_IsRequired()
    {
        using EditorHarness editor = new();
        FakeWorkspaceService workspaces = new();

        Assert.Throws<ArgumentNullException>(
            () => new SearchViewModel(null!, editor.Documents, new FakeIdleScheduler(), new FakeUiDispatcher(), NullLogger<SearchViewModel>.Instance));
        Assert.Throws<ArgumentNullException>(
            () => new SearchViewModel(workspaces, null!, new FakeIdleScheduler(), new FakeUiDispatcher(), NullLogger<SearchViewModel>.Instance));
        Assert.Throws<ArgumentNullException>(
            () => new SearchViewModel(workspaces, editor.Documents, null!, new FakeUiDispatcher(), NullLogger<SearchViewModel>.Instance));
        Assert.Throws<ArgumentNullException>(
            () => new SearchViewModel(workspaces, editor.Documents, new FakeIdleScheduler(), null!, NullLogger<SearchViewModel>.Instance));
        Assert.Throws<ArgumentNullException>(
            () => new SearchViewModel(workspaces, editor.Documents, new FakeIdleScheduler(), new FakeUiDispatcher(), null!));
    }

    /// <summary>
    /// A search pane over a real folder, a real editor pane, and the fakes for the two things a unit
    /// test cannot have: the UI thread and a clock.
    /// </summary>
    private sealed class SearchHarness : IDisposable
    {
        public SearchHarness(bool withWorkspace = true)
        {
            Editor = new EditorHarness();

            if (withWorkspace)
            {
                Workspaces.Seed("Notes", active: true, Editor.Root);
            }

            Search = new SearchViewModel(
                Workspaces,
                Editor.Documents,
                Idle,
                Dispatcher,
                NullLogger<SearchViewModel>.Instance);
        }

        public EditorHarness Editor { get; }

        public FakeWorkspaceService Workspaces { get; } = new();

        public FakeIdleScheduler Idle { get; } = new();

        public FakeUiDispatcher Dispatcher { get; } = new();

        public SearchViewModel Search { get; }

        public string Root => Editor.Root;

        public string Write(string name, string content) => Editor.Write(name, content);

        public string Write(string relativeFolder, string name, string content)
        {
            string folder = Path.Combine(Editor.Root, relativeFolder);
            Directory.CreateDirectory(folder);

            string path = Path.Combine(folder, name);
            File.WriteAllBytes(path, Encoding.UTF8.GetBytes(content));

            return path;
        }

        /// <summary>
        /// Types a query and fires the debounce, which is what a pause in the typing amounts to.
        /// </summary>
        public Task SearchAsync(string query)
        {
            Search.Query = query;

            return FireAsync();
        }

        /// <summary>
        /// Runs whatever the pane has pending, as the real timer would once its delay passed.
        /// </summary>
        public Task FireAsync() => Idle.FireAsync(Search);

        public void Dispose()
        {
            Search.Dispose();
            Idle.Dispose();
            Editor.Dispose();
        }
    }

    /// <summary>
    /// A second throw-away folder, for the workspace a test switches to.
    /// </summary>
    private sealed class TempFolder : IDisposable
    {
        public TempFolder()
        {
            Root = Path.Combine(Path.GetTempPath(), "Enigma.MarkdownEditor.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
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
}
