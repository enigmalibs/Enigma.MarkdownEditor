using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Enigma.MarkdownEditor.Core.Search;
using Enigma.MarkdownEditor.Core.UnitTests.Files;
using Xunit;

namespace Enigma.MarkdownEditor.Core.UnitTests.Search;

/// <summary>
/// Tests for workspace-wide search, over real directory trees.
/// </summary>
/// <remarks>
/// Real files rather than a stand-in file system, like the rest of this assembly's file tests: what is
/// being asked is which files the walk reaches and what a read of them returns, and both are exactly
/// what a fake would get wrong in the same way the code under test does.
/// </remarks>
public sealed class WorkspaceSearchServiceTests
{
    [Fact]
    public async Task ASubstring_IsFoundWithItsFileLineAndColumn()
    {
        using TempTreeFolder folder = new();
        string path = folder.AddFile("note.md", "first line\nthe needle is here\nlast line\n");

        (IReadOnlyList<WorkspaceSearchHit> hits, _) = await SearchAsync(folder, "needle");

        WorkspaceSearchHit hit = Assert.Single(hits);
        Assert.Equal(path, hit.FilePath);
        Assert.Equal(WorkspaceSearchHitKind.Content, hit.Kind);
        Assert.Equal(2, hit.Line);
        Assert.Equal(5, hit.Column);
        Assert.Equal("the needle is here", hit.LineText);
        Assert.Equal(4, hit.MatchStart);
        Assert.Equal(6, hit.MatchLength);
    }

    [Fact]
    public async Task EveryOccurrenceOnALine_IsItsOwnHit()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("note.md", "one two one two one\n");

        (IReadOnlyList<WorkspaceSearchHit> hits, _) = await SearchAsync(folder, "one");

        Assert.Equal([1, 9, 17], hits.Select(hit => hit.Column));
        Assert.All(hits, hit => Assert.Equal(1, hit.Line));
    }

    [Fact]
    public async Task Occurrences_DoNotOverlap()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("note.md", "aaa\n");

        // "aa" is in "aaa" once, which is what a reader counting them would say. Counting the
        // overlapping pair as a second hit would put two rows on one three-character line.
        (IReadOnlyList<WorkspaceSearchHit> hits, _) = await SearchAsync(folder, "aa");

        WorkspaceSearchHit hit = Assert.Single(hits);
        Assert.Equal(1, hit.Column);
    }

    [Fact]
    public async Task Matching_IsCaseInsensitiveByDefault()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("note.md", "The Needle\n");

        (IReadOnlyList<WorkspaceSearchHit> hits, _) = await SearchAsync(folder, "needle");

        Assert.Single(hits);
    }

    [Fact]
    public async Task WithMatchCase_ADifferentCaseIsNotAMatch()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("note.md", "The Needle\nthe needle\n");

        (IReadOnlyList<WorkspaceSearchHit> hits, _) = await SearchAsync(folder, "needle", matchCase: true);

        WorkspaceSearchHit hit = Assert.Single(hits);
        Assert.Equal(2, hit.Line);
    }

    [Fact]
    public async Task AFileWhoseNameMatches_IsAHitOfItsOwn()
    {
        using TempTreeFolder folder = new();
        string path = folder.AddFile("meeting-notes.md", "nothing in here\n");

        (IReadOnlyList<WorkspaceSearchHit> hits, _) = await SearchAsync(folder, "meeting");

        WorkspaceSearchHit hit = Assert.Single(hits);
        Assert.Equal(WorkspaceSearchHitKind.FileName, hit.Kind);
        Assert.Equal(path, hit.FilePath);
        Assert.Equal("meeting-notes.md", hit.LineText);
        Assert.Equal(0, hit.MatchStart);
        Assert.Equal(7, hit.MatchLength);

        // The top of the file: what matched is the name, and the file still has to open somewhere.
        Assert.Equal(1, hit.Line);
        Assert.Equal(1, hit.Column);
    }

    [Fact]
    public async Task AFileNameMatchesOnlyOnce_HoweverOftenTheNameRepeatsIt()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("note-note-note.md", "empty\n");

        (IReadOnlyList<WorkspaceSearchHit> hits, _) = await SearchAsync(folder, "note");

        Assert.Single(hits);
    }

    [Fact]
    public async Task FileNameHits_ComeBeforeEveryContentHit()
    {
        using TempTreeFolder folder = new();

        // Named so that path order would put the content-only file first: the ranking has to come
        // from the kind of hit, not from where the walk happened to reach it.
        folder.AddFile("aaa-content.md", "the needle again\n");
        folder.AddFile("zzz-needle.md", "the needle is in here too\n");

        (IReadOnlyList<WorkspaceSearchHit> hits, _) = await SearchAsync(folder, "needle");

        Assert.Equal(WorkspaceSearchHitKind.FileName, hits[0].Kind);
        Assert.Equal("zzz-needle.md", hits[0].LineText);
        Assert.All(hits.Skip(1), hit => Assert.Equal(WorkspaceSearchHitKind.Content, hit.Kind));
    }

    [Fact]
    public async Task OneFilesHits_AreReportedTogether()
    {
        using TempTreeFolder folder = new();

        for (int index = 0; index < 12; index++)
        {
            folder.AddFile($"note{index:00}.md", "needle\nfiller\nneedle\nfiller\nneedle\n");
        }

        (IReadOnlyList<WorkspaceSearchHit> hits, _) = await SearchAsync(folder, "needle");

        // The files are read several at a time, so which file comes first is the scheduler's business.
        // That a file's hits arrive as one run is not: a result list grouped by file would otherwise
        // have to reopen a group it had already closed.
        List<string> order = [.. hits.Select(hit => hit.FilePath)];

        Assert.Equal(36, order.Count);
        Assert.Equal(order.Distinct().Count(), CountRuns(order));
    }

    [Fact]
    public async Task NonMarkdownFiles_AreNotSearched()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("notes.txt", "needle\n");
        folder.AddFile("data.json", "needle\n");
        folder.AddFile("kept.markdown", "needle\n");

        (IReadOnlyList<WorkspaceSearchHit> hits, WorkspaceSearchSummary summary) = await SearchAsync(folder, "needle");

        // The scope is markdown whatever the tree's ShowAllFiles toggle says: a search whose reach
        // changed with a display setting is a search nobody can trust.
        Assert.Equal("kept.markdown", Path.GetFileName(Assert.Single(hits).FilePath));
        Assert.Equal(1, summary.ScannedFiles);
    }

    [Fact]
    public async Task HiddenAndDotEntries_AreSkipped()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("visible.md", "needle\n");
        folder.AddFile(".hidden.md", "needle\n");
        folder.AddFile(".git/config.md", "needle\n");

        string hiddenDirectory = folder.AddDirectory("secret");
        folder.AddFile("secret/inside.md", "needle\n");
        TempTreeFolder.Hide(hiddenDirectory);

        (IReadOnlyList<WorkspaceSearchHit> hits, _) = await SearchAsync(folder, "needle");

        // On Linux the hidden attribute does not exist, so the "secret" folder is walked there; the
        // dot-prefixed entries are excluded on every platform.
        Assert.DoesNotContain(hits, hit => hit.FilePath.Contains(".git", StringComparison.Ordinal));
        Assert.DoesNotContain(hits, hit => hit.FilePath.EndsWith(".hidden.md", StringComparison.Ordinal));
        Assert.Contains(hits, hit => hit.FilePath.EndsWith("visible.md", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Subdirectories_AreSearchedToTheBottom()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("one/two/three/deep.md", "the needle at the bottom\n");

        (IReadOnlyList<WorkspaceSearchHit> hits, _) = await SearchAsync(folder, "needle");

        Assert.Single(hits);
    }

    [Fact]
    public async Task AFileReachableFromTwoRoots_IsSearchedOnce()
    {
        using TempTreeFolder folder = new();
        string nested = folder.AddDirectory("nested");
        folder.AddFile("nested/note.md", "needle\n");

        WorkspaceSearchRequest request = new("needle", [folder.Root, nested]);
        (IReadOnlyList<WorkspaceSearchHit> hits, WorkspaceSearchSummary summary) = await RunAsync(request);

        Assert.Single(hits);
        Assert.Equal(1, summary.ScannedFiles);
    }

    [Fact]
    public async Task AMissingRoot_IsNotAFailedSearch()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("note.md", "needle\n");

        WorkspaceSearchRequest request = new("needle", [folder.Root, folder.Combine("gone")]);
        (IReadOnlyList<WorkspaceSearchHit> hits, _) = await RunAsync(request);

        Assert.Single(hits);
    }

    [Fact]
    public async Task AnOversizeFile_IsCountedAndItsContentsLeftUnread()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("huge.md", new string('x', 4096) + "\nneedle\n");
        folder.AddFile("small.md", "needle\n");

        WorkspaceSearchRequest request = new("needle", [folder.Root]) { MaxFileBytes = 1024 };
        (IReadOnlyList<WorkspaceSearchHit> hits, WorkspaceSearchSummary summary) = await RunAsync(request);

        Assert.Equal("small.md", Path.GetFileName(Assert.Single(hits).FilePath));
        Assert.Equal(1, summary.SkippedOversizeFiles);
    }

    [Fact]
    public async Task AnOversizeFile_IsStillMatchedOnItsName()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("needle-log.md", new string('x', 4096));

        WorkspaceSearchRequest request = new("needle", [folder.Root]) { MaxFileBytes = 1024 };
        (IReadOnlyList<WorkspaceSearchHit> hits, WorkspaceSearchSummary summary) = await RunAsync(request);

        // Costs nothing, and a file too big to read is exactly the one whose name is the only way to
        // find it.
        Assert.Equal(WorkspaceSearchHitKind.FileName, Assert.Single(hits).Kind);
        Assert.Equal(1, summary.SkippedOversizeFiles);
    }

    [Fact]
    public async Task PastTheCap_TheSearchStopsAndSaysSo()
    {
        using TempTreeFolder folder = new();

        for (int index = 0; index < 10; index++)
        {
            folder.AddFile($"note{index:00}.md", "needle\nneedle\nneedle\n");
        }

        WorkspaceSearchRequest request = new("needle", [folder.Root]) { MaxHits = 7 };
        (IReadOnlyList<WorkspaceSearchHit> hits, WorkspaceSearchSummary summary) = await RunAsync(request);

        Assert.Equal(7, hits.Count);
        Assert.Equal(7, summary.HitCount);
        Assert.True(summary.IsTruncated);
    }

    [Fact]
    public async Task ExactlyTheCap_IsACompleteAnswer()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("note.md", "needle\nneedle\nneedle\n");

        WorkspaceSearchRequest request = new("needle", [folder.Root]) { MaxHits = 3 };
        (IReadOnlyList<WorkspaceSearchHit> hits, WorkspaceSearchSummary summary) = await RunAsync(request);

        Assert.Equal(3, hits.Count);
        Assert.False(summary.IsTruncated);
    }

    [Fact]
    public async Task TheSummary_CountsHitsFilesAndFilesLookedAt()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("one.md", "needle\nneedle\n");
        folder.AddFile("two.md", "needle\n");
        folder.AddFile("three.md", "nothing here\n");

        (_, WorkspaceSearchSummary summary) = await SearchAsync(folder, "needle");

        Assert.Equal(3, summary.HitCount);
        Assert.Equal(2, summary.FileCount);
        Assert.Equal(3, summary.ScannedFiles);
        Assert.False(summary.IsTruncated);
    }

    [Fact]
    public async Task AnEmptyQuery_IsNoQuestionHavingBeenAsked()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("note.md", "anything at all\n");

        (IReadOnlyList<WorkspaceSearchHit> hits, WorkspaceSearchSummary summary) = await SearchAsync(folder, string.Empty);

        Assert.Empty(hits);
        Assert.Equal(0, summary.ScannedFiles);
    }

    [Fact]
    public async Task AWorkspaceWithNoFolders_FindsNothingAndDoesNotThrow()
    {
        WorkspaceSearchRequest request = new("needle", []);
        (IReadOnlyList<WorkspaceSearchHit> hits, WorkspaceSearchSummary summary) = await RunAsync(request);

        Assert.Empty(hits);
        Assert.Equal(0, summary.ScannedFiles);
    }

    [Fact]
    public async Task ACancelledSearch_Throws()
    {
        using TempTreeFolder folder = new();

        for (int index = 0; index < 200; index++)
        {
            folder.AddFile($"note{index:000}.md", "needle\n");
        }

        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => WorkspaceSearchService.SearchAsync(
                new WorkspaceSearchRequest("needle", [folder.Root]),
                static _ => { },
                cancellation.Token));
    }

    [Fact]
    public async Task ASearchCancelledPartWayThrough_StopsReporting()
    {
        using TempTreeFolder folder = new();

        for (int index = 0; index < 400; index++)
        {
            folder.AddFile($"note{index:000}.md", "needle\n");
        }

        using CancellationTokenSource cancellation = new();
        int reported = 0;

        // Cancelled from inside the callback, which is the shape the ViewModel has: a new query
        // arrives while the previous scan is still handing over hits.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => WorkspaceSearchService.SearchAsync(
                new WorkspaceSearchRequest("needle", [folder.Root]),
                _ =>
                {
                    if (Interlocked.Increment(ref reported) == 5)
                    {
                        cancellation.Cancel();
                    }
                },
                cancellation.Token));

        // Not an exact count: the readers already in flight finish the file they are on. What matters
        // is that the scan stopped rather than running the other few hundred files to the end.
        Assert.InRange(reported, 5, 200);
    }

    [Fact]
    public async Task ALongLine_IsExcerptedAroundTheMatch()
    {
        using TempTreeFolder folder = new();
        string line = new string('a', 400) + "needle" + new string('b', 400);
        folder.AddFile("note.md", line + "\n");

        (IReadOnlyList<WorkspaceSearchHit> hits, _) = await SearchAsync(folder, "needle");

        WorkspaceSearchHit hit = Assert.Single(hits);

        // The line is 806 characters; five hundred of those held at once is what the cap prevents.
        Assert.Equal(WorkspaceSearchHit.MaxLineTextLength, hit.LineText.Length);
        Assert.Equal("needle", hit.LineText.Substring(hit.MatchStart, hit.MatchLength));

        // The column still addresses the whole line, because that is what puts the caret on the match.
        Assert.Equal(401, hit.Column);
    }

    [Fact]
    public async Task IndentationIsDroppedFromTheExcerpt_ButNotTheColumn()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("note.md", "        - a needle in a list\n");

        (IReadOnlyList<WorkspaceSearchHit> hits, _) = await SearchAsync(folder, "needle");

        WorkspaceSearchHit hit = Assert.Single(hits);

        Assert.Equal("- a needle in a list", hit.LineText);
        Assert.Equal("needle", hit.LineText.Substring(hit.MatchStart, hit.MatchLength));
        Assert.Equal(13, hit.Column);
    }

    [Fact]
    public async Task AMatchMadeOfWhitespace_SurvivesTheIndentTrim()
    {
        using TempTreeFolder folder = new();
        folder.AddFile("note.md", "    indented\n");

        (IReadOnlyList<WorkspaceSearchHit> hits, _) = await SearchAsync(folder, "  ");

        // Four spaces hold two non-overlapping pairs, and the indent trim must not eat either of
        // them: whichever spaces the excerpt drops, the ones that matched are still in it.
        Assert.Equal([1, 3], hits.Select(hit => hit.Column));
        Assert.All(hits, hit => Assert.Equal("  ", hit.LineText.Substring(hit.MatchStart, hit.MatchLength)));
    }

    [Fact]
    public async Task AUtf8FileWithABom_ReadsAsItsText()
    {
        using TempTreeFolder folder = new();
        string path = folder.Combine("bom.md");
        File.WriteAllText(path, "héllo needle\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        (IReadOnlyList<WorkspaceSearchHit> hits, _) = await SearchAsync(folder, "héllo");

        // The byte-order mark is not part of the first line, so the match starts where it looks like
        // it should rather than one character in.
        Assert.Equal(1, Assert.Single(hits).Column);
    }

    [Fact]
    public async Task ANullArgument_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => WorkspaceSearchService.SearchAsync(null!, static _ => { }, TestContext.Current.CancellationToken));

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => WorkspaceSearchService.SearchAsync(
                new WorkspaceSearchRequest("x", []),
                null!,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ACapBelowOne_IsRejected()
    {
        WorkspaceSearchRequest request = new("needle", []) { MaxHits = 0 };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => WorkspaceSearchService.SearchAsync(request, static _ => { }, TestContext.Current.CancellationToken));
    }

    private static Task<(IReadOnlyList<WorkspaceSearchHit> Hits, WorkspaceSearchSummary Summary)> SearchAsync(
        TempTreeFolder folder,
        string query,
        bool matchCase = false)
        => RunAsync(new WorkspaceSearchRequest(query, [folder.Root]) { MatchCase = matchCase });

    private static async Task<(IReadOnlyList<WorkspaceSearchHit> Hits, WorkspaceSearchSummary Summary)> RunAsync(
        WorkspaceSearchRequest request)
    {
        List<WorkspaceSearchHit> hits = [];

        // The callback is serialized by the search itself, so a plain list is safe here — which is
        // exactly the contract the ViewModel relies on to append to an ObservableCollection.
        WorkspaceSearchSummary summary = await WorkspaceSearchService.SearchAsync(
            request,
            hits.Add,
            TestContext.Current.CancellationToken);

        return (hits, summary);
    }

    // How many times the path changes from one hit to the next, plus one: the number of runs the
    // report is made of.
    private static int CountRuns(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return 0;
        }

        int runs = 1;

        for (int index = 1; index < paths.Count; index++)
        {
            if (!string.Equals(paths[index], paths[index - 1], StringComparison.Ordinal))
            {
                runs++;
            }
        }

        return runs;
    }
}
