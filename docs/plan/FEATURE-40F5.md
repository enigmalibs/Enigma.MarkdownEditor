# FEATURE-40F5 — Search & session restore

**Status: DONE** · Multi-phase (2) · Roadmap: `docs/roadmap.md`
Branches at build time: `feature/feature-40f5-phase01-search`, `feature/feature-40f5-phase02-session`.

## Objective

Find anything across the workspace fast, and reopen the app exactly where it was left — the two features that make a multi-directory workspace app a daily driver.

## Context & constraints

- Depends on FEATURE-0DE1 (workspaces/tree), FEATURE-3C44 (tabs/open-at-line); view-mode persistence hooks into FEATURE-6054 PHASE03.
- Persistence via Enigma.Configuration (`IConfig<SessionState>`, `session.json`), same app-data location as settings/workspaces.
- All scanning async with `CancellationToken` (dotnet-async); UI updates dispatched.

## Design

### PHASE01 — Workspace-wide search — **Status: DONE**

- **Core `Search/WorkspaceSearchService`**: enumerates all workspace roots for `*.md` files (markdown only, regardless of the ShowAllFiles tree toggle — predictable scope; hidden/dot directories always skipped; files > 10 MB skipped and counted), reads and scans line-by-line with bounded parallelism, honors cancellation (a new query cancels the previous one). Matching: plain substring, case-insensitive by default with a Match-Case toggle. **Filename matches** are included and ranked before content matches. Results: `{ file, line, column, lineText, matchLength }`, streamed as produced, capped at 500 with an explicit truncation flag.
- **UI**: the sidebar becomes two segmented tabs — **Explorer | Search** (`ei:Icon` icons). Search tab: query box (debounced-on-type + Enter to force), Match-Case toggle, result list grouped by file (workspace-relative path, hit count), each hit showing the context line with the match highlighted; Enter/click opens the document, moves the caret to the match, selects it, and centers the view. Status line under the box: "N results in M files" (+ "truncated" when capped).
- No persistent index in v1 — a cold scan of a notes-sized workspace is fast enough; an index is the documented fallback if it ever isn't.

Acceptance criteria:
- Correct hits including multiple per line; filename hits ranked first; case toggle honored.
- Retyping cancels the in-flight scan (no stale results, no race); big-file skip and the 500 cap surfaced in the UI.
- Opening a result lands selection exactly on the match.
- Core.UnitTests on a temp directory tree: matching, ranking, cancellation, cap, hidden/oversize skipping. Desktop.UnitTests: search VM behavior (debounce, grouping).

### PHASE02 — Session restore — **Status: DONE**

- **Core `SessionState`** (`IConfig<SessionState>`): `Version = 1`, `ActiveWorkspaceId`, ordered open documents (absolute paths), active document index, `ViewMode`, window placement (position, size, maximized), pane widths, sidebar tab, sidebar/preview collapsed flags.
- **Save**: debounced on any tracked change + a final write on exit. **Restore**: on startup after the workspace loads — missing files are skipped silently (logged), a corrupt/incompatible session file is discarded for a fresh start (logged + InfoBar notice), never a crash.
- Window placement sanity: restored bounds clamped to the current screen arrangement (monitor removed since last run).

Acceptance criteria:
- Full restart round-trip restores workspace, tabs (order + active), view mode, window bounds/state, pane widths, sidebar tab.
- Deleted/moved files are skipped without error; corrupt session file → fresh start with the InfoBar notice.
- Core.UnitTests: state round-trip + tolerance paths (missing files, corrupt JSON, unknown future version field).

## Dependencies

FEATURE-0DE1, FEATURE-3C44, FEATURE-6054 PHASE03 (view-mode value exists). Build order: after those items.

## Out of scope

Regex / whole-word search, search-and-replace across files (backlog) · persistent content index (documented fallback) · per-document caret/scroll restore in the session (backlog — cheap to add later).
