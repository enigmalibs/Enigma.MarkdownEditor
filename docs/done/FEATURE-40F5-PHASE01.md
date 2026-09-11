# FEATURE-40F5-PHASE01 — Workspace-wide search

**Status: DONE** · Branch: `feature/feature-40f5-phase01-search` · Plan: `docs/plan/FEATURE-40F5.md`

## Summary

The sidebar stops being only a file tree. It now carries two tabs — **Explorer | Search** — and the
second one answers "where did I write that" across every markdown file in the workspace, then takes
you to the exact characters that matched.

**The scan (Core).** `WorkspaceSearchService` is a static, stateless reader in the shape the rest of
the assembly already uses (`DirectoryLister`, `DocumentFile`, `FileOperations`), offloaded once with
`Task.Run` because directory enumeration is synchronous all the way down. It walks the active
workspace's roots iteratively, pruning by the same rule the tree uses with ShowAllFiles off, which is
what makes the search's reach predictable from what the sidebar shows: markdown only, hidden and
dot-prefixed entries never, whatever the display toggle says. Overlapping roots and junctions that
point back up their own tree are walked once; a reparse-point folder is not descended into; a folder
that cannot be enumerated contributes nothing rather than failing the search. The candidate list is
sorted, so the same query over the same workspace produces the same answer twice running.

Then two passes, and the order is the ranking the plan asked for. The first matches **file names**
alone and reports them before a single byte of content has been read — a note whose name matches is
nearly always the one being looked for. The second reads the files, up to eight at a time, and reports
each file's hits **as one contiguous run**, which is what lets a list grouped by file never reopen a
group it has closed. Matching is a plain substring, `OrdinalIgnoreCase` by default and `Ordinal` under
Match Case — ordinal on purpose, because a culture-aware comparison can match a different number of
characters than it was given and the highlight would then be wrong. Occurrences on a line are
non-overlapping: "aa" is in "aaa" once, which is what a reader counting them would say.

Two limits keep a search over an unknown folder bounded. Files over 10 MB are not read — a markdown
file that size is a log or a paste accident — but are still matched on their name, which costs nothing
and is the only way to find one. And the whole search stops at 500 hits. Both counts come back in the
summary and both reach the status line. The 500-cap is enforced in the one place hits leave the search
— a small locked sink — because several readers finish at once and "is there room for one more" has to
be asked and answered in the same breath as taking that room. Reaching the cap *exactly*, with nothing
left to report, is a complete answer rather than a truncated one.

`WorkspaceSearchHit` carries two coordinate systems on purpose. `Line`/`Column` address the file as
the editor sees it and are what put the caret on the match; `LineText`/`MatchStart` are the excerpt the
result list draws. They differ because a line has no length limit and a hit list does: a minified
file's half-megabyte line, held five hundred times over, is the difference between a search and an
out-of-memory. Long lines are windowed around the match, and the whitespace indenting a line is dropped
— but never into the match, so a query made of spaces still highlights.

**The pane (Desktop).** `SearchViewModel` debounces typing on the existing `IIdleScheduler` (300 ms,
longer than the preview's 250 because this reads the workspace rather than re-rendering one note in
memory). Enter and the Match Case toggle go through the *same* timer with a zero delay rather than
calling the scan directly — one road into `RunAsync`, and the timer is a restart rather than a queue,
so an immediate run displaces whatever the typing had pending. Emptying the box clears the results at
once rather than after another wait: the pane must never show answers to a question that is no longer
on screen.

Every run is stamped with an integer, and hits marshalled back through `IUiDispatcher` are dropped
unless the stamp still matches. That is the whole of the "no stale results" race, and it is why the
check is an integer compared on the UI thread rather than a token read from a background one. A new
query also cancels the previous scan outright, so a superseded run stops reading rather than finishing
into a list nobody will see.

Results are grouped per file — the file's name, its workspace-relative folder beside it, its hit count
— with each hit a row showing the context line cut into prefix / match / suffix, the match drawn
highlighted. Rows are `Button`s rather than list items: the keyboard already knows what to do with one,
and a hit is a place to go rather than a thing to keep selected.

**Opening a hit.** The editor had no way to be told "open this file *there*", so this dev added one,
and deliberately not by reaching into the control. `DocumentSelection(Line, Column, Length)` is left
*on the document* by `DocumentManagerViewModel.RevealAsync`, and the editor pane takes it the next time
it puts that document on screen. Two entry points can get there — a document being swapped in, and a
request against the document already showing — so the request is **taken** rather than read: whichever
arrives first honours it and the other finds nothing to do, in either order. `DocumentSelection.Resolve`
turns the line and column into a clamped offset against the text the editor is actually holding, which
is where "lands exactly on the match" is asserted; the file may have been shortened between the scan
reading it and the editor opening it, and landing on the last line beats throwing at someone who only
clicked a result. The view then selects the range and **centres** it rather than merely scrolling it
into view — a hit on the last visible line is a hit with no context under it.

## Files/modules touched

**Created — Core**

- `Search/WorkspaceSearchService.cs` — the walk, the two passes, the parallel read, the locked sink.
- `Search/WorkspaceSearchRequest.cs` — query, roots, `MatchCase`, and the two limits (`MaxHits` 500,
  `MaxFileBytes` 10 MB) as `init` properties so a test can exercise a cap without writing 500 hits.
- `Search/WorkspaceSearchHit.cs` — the hit, its two coordinate systems, and the excerpt logic.
- `Search/WorkspaceSearchHitKind.cs` — `FileName | Content`, which is the ranking.
- `Search/WorkspaceSearchSummary.cs` — the counts behind the status line.
- `Settings/SidebarTab.cs` — `Explorer | Search`. In Core beside `ViewMode` and `AppTheme`, and by
  name rather than by number, because PHASE02 persists which tab the sidebar was left on.

**Created — Desktop**

- `ViewModels/SearchViewModel.cs` — the debounce, the run stamp, the grouping, the status line.
- `ViewModels/SearchResultGroup.cs` — one file's hits; observable, because a group grows as they
  arrive.
- `ViewModels/SearchHitItem.cs` — one row, cut into prefix / match / suffix once rather than by three
  bindings.
- `ViewModels/DocumentSelection.cs` — a caret request in lines and columns, and `Resolve`.
- `ViewModels/DocumentSelectionRange.cs` — the same request measured into offsets.
- `Views/SearchPaneView.axaml` / `.axaml.cs` — the pane. The code-behind is a constructor and nothing
  else: Enter and Escape are key bindings, each hit is a `Button`, and everything else is bound.

**Modified — Desktop**

- `ViewModels/DocumentManagerViewModel.cs` — `RevealAsync`, the `SelectionRequested` event, and
  `OpenAsync` refactored onto one private core that both paths share.
- `ViewModels/DocumentViewModel.cs` — `PendingSelection`, `RequestSelection`, `TakePendingSelection`.
- `ViewModels/MainWindowViewModel.cs` — the `Search` child, `SidebarTab` and its two segment flags.
- `Views/EditorPaneView.axaml.cs` — honouring a pending selection from either entry point, and
  `CentreOn`.
- `Views/MainWindow.axaml` — the segmented Explorer|Search row, the tab styles, the Explorer content
  moved under its flag, the search pane and its wrapper.
- `Extensions/ServiceCollectionExtensions.cs` — `SearchViewModel` as a singleton.

**Modified — tests**

- New: `Core.UnitTests/Search/WorkspaceSearchServiceTests.cs`,
  `Desktop.UnitTests/ViewModels/SearchViewModelTests.cs`,
  `Desktop.UnitTests/ViewModels/DocumentSelectionTests.cs`.
- `Fakes/FakeUiDispatcher.cs` — an opt-in `Defer` mode that holds posted work until `DrainAsync`,
  which is the only way to write the superseded-scan race down: the late hits have to be able to
  actually arrive late.
- `ViewModels/MainWindowViewModelTests.cs` — the new constructor argument, a `CreateSearch` helper,
  and the sidebar-tab tests.
- `Extensions/ServiceCollectionExtensionsTests.cs` — the `SearchViewModel` lifetime row.

## Deviations & follow-ups

- **The search API's shape was your call during the pre-flight** (static class + hit callback +
  awaited summary), and one detail of the option text did not survive contact: there is no
  `WasCancelled` on the summary. Cancellation throws `OperationCanceledException` instead, which is
  the .NET convention and what the ViewModel's `catch` is written around — a flag that could never be
  `true` would have been dead API.
- **`WorkspaceSearchHit` carries a `MatchStart` the plan's field list does not name.** The plan says
  `{ file, line, column, lineText, matchLength }`; without a separate index into the *excerpt*, a
  windowed long line cannot be highlighted at all, because `Column` addresses the whole line and
  `LineText` is not the whole line. `Kind` is likewise beyond the list, and is what makes "filename
  hits ranked first" checkable rather than implied.
- **Filename hits are ranked first globally *and* sit first inside their file's group.** In a list
  grouped by file the plan's "ranked before content matches" has two readings; this is the one that
  keeps a file's hits together, which the grouping needs anyway.
- **An oversize file is still matched on its name.** The plan says files over 10 MB are "skipped and
  counted"; skipping its *contents* while still matching its name costs nothing and is the more useful
  reading. Both are surfaced: the group appears, and the status line says how many files were too big
  to search.
- **Changing workspace re-runs the query rather than clearing it.** The plan does not say; leaving the
  box holding a query with an empty list under it reads as broken.
- **The results list is not virtualized.** The scan stops at 500 hits, which a `StackPanel` carries
  without noticing. If the cap is ever raised, this is the first thing to revisit.
- **The editor-side wiring is not covered by an automated test**, for the same reason PHASE03 of
  FEATURE-6054 recorded: no test in this suite instantiates a live `TextEditor`. What *is* tested is
  everything either side of it — that `RevealAsync` opens the file and leaves the request on the
  document, that a request against the already-showing tab raises `SelectionRequested`, and that
  `DocumentSelection.Resolve` produces the exact offsets. `EditorPaneView.ApplyPendingSelection` and
  `CentreOn` are verified by inspection only. A headless editor test would pay for itself twice over
  now; it is the same follow-up that phase left.
- **The manual visual pass was not performed** — this session cannot look at the running window. The
  XAML is compile-verified (compiled bindings are on, so every new binding path resolved at build
  time) but not eye-verified. What wants a human, in dark and in light: that the Explorer|Search
  segments read as one control and the chosen one is obvious; that the highlighted match is legible
  against `EnigmaWarningBackgroundBrush` in both themes; that Enter on a focused hit row opens it and
  the editor really does end up centred on the match with the text selected; that a long context line
  ellipsises rather than widening the pane; and that Escape in the query box clears it.
- **`Run`-based highlighting is the one binding I would look at first.** The three `<Run>`s inside the
  row's `TextBlock` bind `Prefix` / `Match` / `Suffix` and rely on inlines inheriting the row's
  DataContext. The compiled-binding pass resolved all three against `SearchHitItem` at build time,
  which is strong evidence it works, but it has not been seen on screen.
- **Line endings:** no CRLF/LF churn was observed in this dev's diff; nothing was done about line
  endings either way, per the workflow's recommendation-only rule.
- **Follow-up, not done:** search-and-replace across files, regex and whole-word matching are out of
  scope by the plan and remain so. The service's shape does not change to add them — a matcher
  abstraction in place of `IndexOf` is the whole of it.
- **Follow-up, not done:** `Ctrl+Shift+F` has no binding. Reaching search means clicking its segment;
  a window-level shortcut that shows the sidebar, selects the Search tab and focuses the query box is
  a small piece of work and an obvious one.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release` — succeeded, **zero warnings**
  (`TreatWarningsAsErrors` is on solution-wide, so a warning would have failed it).
- `dotnet test Enigma.MarkdownEditor.slnx -c Release` — **1647 passed, 0 failed, 0 skipped** (1577
  before this dev, so **+70 cases**).
- Core: `WorkspaceSearchServiceTests` — **29 cases**. A hit's file, line, column, excerpt and match
  offsets; several occurrences on one line and their non-overlap; case-insensitive by default and the
  Match Case toggle; a filename hit and its once-per-file rule; filename hits ranked ahead of content
  hits even when path order would say otherwise; that one file's hits arrive as a single run (asserted
  by counting runs against distinct files over twelve files read in parallel); markdown-only scope;
  hidden and dot-prefixed entries skipped; recursion to the bottom of a tree; a file reachable from two
  roots searched once; a missing root tolerated; an oversize file counted, its contents unread and its
  name still matched; the cap truncating and the cap reached exactly not truncating; the summary's
  counts; an empty query and an empty workspace; cancellation before the scan and cancellation from
  inside the callback part way through; a long line excerpted around the match with the column still
  addressing the whole line; indentation dropped from the excerpt but not from the column; a whitespace
  match surviving that trim; a UTF-8 BOM not counting as a character; and the argument rejections.
- Desktop: `SearchViewModelTests` — **27 cases**. That typing schedules rather than searches and that
  the delay is `TypingDelay`; that three keystrokes leave one pending scan; that firing the timer
  produces the results; that Enter re-schedules at zero delay; that the case toggle re-asks at once and
  narrows the answer; that emptying the box drops the results, the status and the timer; grouping —
  one file's hits in one group with the right count and line numbers, two files as two groups; the
  workspace-relative folder shown, and none for a file in a root; the status line's plural and singular
  forms, "No results", and the no-workspace message; the 500-cap surfaced as "truncated"; a file over
  10 MB surfaced as "1 file too big to search"; a row cut into prefix/match/suffix; a filename hit
  first with no line number; a hit's `DocumentSelection`; opening a hit opening the file and leaving
  the request on it; opening a hit in the tab already showing still raising `SelectionRequested`;
  **that a superseded scan's hits are dropped** (both scans run with the dispatcher deferring, then
  both are drained — only the second query's group survives); re-running on a workspace change; a
  disposed pane letting go; and the argument rejections.
- Desktop: `DocumentSelectionTests` — **9 cases**. A line and column becoming the right offset; the
  first character; a line past the end clamped to the last; a column past the end of its line landing
  one past the last character; a length running past the end cut to what is there; a line or column
  below one clamped up; a zero length placing a caret and selecting nothing; an empty document
  answered rather than thrown at; and a null document rejected.
- Desktop: `MainWindowViewModelTests` — the sidebar starting on Explorer, a segment selecting its tab
  and notifying both flags, and switching the active tab off being refused the way a view-mode segment
  is.
- **Not covered:** the editor-side application of a pending selection, and the manual visual pass —
  see *Deviations & follow-ups*.
