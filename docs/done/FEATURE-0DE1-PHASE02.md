# FEATURE-0DE1-PHASE02 — File tree, filter & watcher

**Status: DONE** · Branch: `feature/feature-0de1-phase02-tree` · Plan: `docs/plan/FEATURE-0DE1.md`

## Summary

The sidebar stops listing folders and becomes the real file tree: one branch per workspace root,
directories first and names ordered numerically (`chapter-2` before `chapter-10`), expanded lazily,
and kept in step with the file system by one debounced `FileSystemWatcher` per root. Markdown is what
it shows by default; an eye toggle in the pane's caption widens that to every file and writes the
choice to the settings file. Hidden and dot-prefixed entries never show either way.

Opening a file from the tree now does something: markdown lands in the editor pane, read-only, with
line numbers; anything the editor does not take goes to the operating system's default application.

Three decisions were taken with you before building (all three were the recommended option):

1. **The editor pane is one read-only AvaloniaEdit, not a tab strip.** FEATURE-3C44 PHASE01 owns
   `DocumentManager`, tabs, dirty tracking and saving; building them here would have been writing
   3C44's plan a dev early. The pane shows the file and nothing else — there is no way to type into
   it, so there is nothing here that could lose a user's work.
2. **The default filter is folders + `.md`/`.markdown`.** That is what this plan's acceptance
   criterion says ("md always"); the contradicting `AppSettings.ShowAllFiles` doc comment — "markdown
   and images only", written in FEATURE-0F9A PHASE03 — was the stale half and has been corrected.
3. **The ShowAllFiles toggle lives in the sidebar's caption row.** The plan called it a "View-menu
   toggle", but this shell has no menu bar at all. It changes what that pane shows, so it belongs to
   the pane; it writes the same live `AppSettings` object as the settings window's checkbox, so the
   two cannot disagree.

## Files/modules touched

### Created — `src/Enigma.MarkdownEditor.Core/Files`

| File | Notes |
|---|---|
| `FileTreeFilter.cs` | Every rule about what the tree shows and what the editor takes, as pure decisions over a name. `Includes`, `IsMarkdown`, `OpensInEditor`, plus the markdown and text extension sets. |
| `NaturalNameComparer.cs` | Case-insensitive, digit-runs-as-numbers. Compares digit runs without parsing them, so a name can hold more digits than any integer type. Falls through to ordinal so the order stays total. |
| `FileSystemEntry.cs` | Path, name, is-directory. A snapshot, so two listings can be diffed. |
| `FileSystemEntryComparer.cs` | Directories first, then files, each group by the comparer above. |
| `DirectoryListingStatus.cs`, `DirectoryListing.cs` | What a directory answered: read (possibly empty), missing, access denied, unavailable. |
| `DirectoryLister.cs` | The only code in the tree that touches disk. One level, never recursive. Offloaded with `Task.Run` — see *Deviations* 6. |
| `FileChangeKind.cs`, `FileSystemChange.cs` | One raw watcher event. |
| `DirectoryChangeSet.cs`, `FileChangeCoalescer.cs` | The reduction from a burst of events to the directories to re-read, or "read the whole root again". Pure, so twenty files copied into a folder can be asserted without copying twenty files. |
| `IDirectoryWatcher.cs`, `DirectoryWatcher.cs` | One `FileSystemWatcher` per root, recursive, 300 ms debounce capped at 1 s, 64 KB buffer, overflow → full re-read. See *Deviations* 3 and 4. |
| `IDirectoryWatcherFactory.cs`, `DirectoryWatcherFactory.cs` | The seam that lets the tree's reaction to a change be tested by raising one. |

### Created — `src/Enigma.MarkdownEditor.Desktop`

| File | Notes |
|---|---|
| `Services/IUiDispatcher.cs`, `UiDispatcher.cs` | Marshals a watcher's thread-pool callback onto the UI thread. A seam, and the reason the tree is testable at all — see *Deviations* 1. |
| `Services/IFileLauncher.cs`, `FileLauncher.cs` | `TopLevel.Launcher` behind one method. Takes `Func<MainWindow>`, not the window — see *Deviations* 2. |
| `Services/IDocumentOpener.cs`, `DocumentOpener.cs` | The one decision made when a file is activated: this editor, or the operating system. Reports a file nothing can open through the InfoBar rather than doing nothing. |
| `ViewModels/FileTreeNodeKind.cs` | Directory, file, missing root, and the four rows that describe a folder rather than list it. |
| `ViewModels/FileTreeNodeViewModel.cs` | One row: what it is, its icon, its tooltip, and the one thing it does — ask for its children the first time it is opened. |
| `ViewModels/FileTreeViewModel.cs` | The tree. Owns the roots, the watchers, the listing and the merge; every reaction to a change is posted through `IUiDispatcher` first. |
| `ViewModels/OpenDocumentViewModel.cs` | The editor pane's single read-only document, capped at 8 MB. |
| `Views/FileTreeView.axaml(.cs)` | The `TreeView`. Two handlers — double-click and Enter — because neither is a command a `TreeView` exposes; everything else is bound. |

### Modified

| File | Change |
|---|---|
| `Core/Settings/AppSettings.cs` | `ShowAllFiles`'s doc comment corrected (decision 2 above). No behaviour change. |
| `Desktop/Enigma.MarkdownEditor.Desktop.csproj` | `Avalonia.AvaloniaEdit` — the pin already existed in CPM for FEATURE-3C44 and is pulled forward one dev. |
| `Desktop/App.axaml` | AvaloniaEdit's Fluent control theme, without which the `TextEditor` renders as an untemplated blank — see *Deviations* 5. |
| `Desktop/Extensions/ServiceCollectionExtensions.cs` | Six new shell registrations, all singleton. |
| `Desktop/ViewModels/MainWindowViewModel.cs` | Holds the tree and the document; re-reads what the settings window changed once it closes; the status bar's file slot follows the open document. |
| `Desktop/ViewModels/WorkspacesViewModel.cs` | `ActiveRoots` removed — PHASE01's *Deviations* 8 said the tree replaces it, and it has. `WorkspaceRootItem` stays; the manage dialog still uses it. |
| `Desktop/Views/MainWindow.axaml` | Sidebar caption row (filter toggle + refresh), the tree in place of the folder list, and the editor pane. The missing-folder warning bar above the tree is unchanged, as PHASE01 asked. |

### Created / modified — tests (330 total, was 193)

| File | Notes |
|---|---|
| `Core.UnitTests/Files/FileTreeFilterTests.cs` | 30 tests. The filter is the whole of what the tree does and does not show, so the rules are pinned exactly: markdown always, everything else only with the setting, dot and hidden entries never. |
| `Core.UnitTests/Files/FileTreeOrderTests.cs` | 9 tests: digit runs as numbers, leading zeros, a digit run longer than any integer type, case, total order, directories first. |
| `Core.UnitTests/Files/DirectoryListerTests.cs` | 13 tests over real directories: filter and order applied, dot and hidden entries excluded, empty vs missing, one level only. |
| `Core.UnitTests/Files/FileChangeCoalescerTests.cs` | 10 tests: twenty changes to one directory, a change three levels down naming its own parent, a rename across directories naming both, reset winning over the batch, and a sibling directory whose name merely starts with the root's being dropped. |
| `Core.UnitTests/Files/DirectoryWatcherTests.cs` | 8 tests against a real temp directory — the acceptance criterion's "watcher-coalescing logic against a temp directory". Every wait is bounded, so a broken watcher fails rather than hangs. |
| `Core.UnitTests/Files/TempTreeFolder.cs` | Throw-away directory tree, with a hidden-attribute helper. |
| `Desktop.UnitTests/ViewModels/FileTreeViewModelTests.cs` | 22 tests over real directories and a fake watcher: auto-expanding a lone root, lazy expansion read once, an external create/delete/reset applied, **the very node object surviving a refresh** (which is what carries the expansion), a change in an unopened folder ignored, the filter toggle widening the tree and reaching the settings file, and both failure paths. |
| `Desktop.UnitTests/ViewModels/FileTreeNodeViewModelTests.cs` | 15 tests: icons, tooltips, the informational rows, and that expanding asks for children exactly once. |
| `Desktop.UnitTests/ViewModels/OpenDocumentViewModelTests.cs` | 9 tests over real files, including the size cap and a failure reported in the pane rather than thrown. |
| `Desktop.UnitTests/Services/DocumentOpenerTests.cs` | 6 tests: markdown to the editor, an image to the OS, a text file crossing over when the filter widens, and a platform that declines or throws. |
| `Desktop.UnitTests/Fakes/` | `FakeUiDispatcher` (runs posted work inline and hands back a way to await it, including what that work posts in turn), `FakeDirectoryWatcher(+Factory)`, `FakeDocumentOpener`, `FakeFileLauncher`. |
| Existing suites | `MainWindowViewModelTests` extended for the two new children and the status-bar slot; `WorkspacesViewModelTests` de-scoped with `ActiveRoots`; `ServiceCollectionExtensionsTests` extended for the six registrations. |

## Deviations & follow-ups

1. **`IUiDispatcher` is a seam the plan does not mention.** A watcher raises its events on a
   thread-pool thread, so every reaction to one has to be marshalled — and a unit test has no Avalonia
   application, so work posted to the real dispatcher would simply never run. Without this seam the
   tree's whole reason for existing (reacting to a change) could not be tested at all. It is one
   method.
2. **`IFileLauncher` takes `Func<MainWindow>`, not `MainWindow`.** The window depends on the shell
   ViewModel, which depends on the tree, which depends on the opener, which needs the window's
   launcher — taking the instance would close that cycle. This is the pattern
   `SettingsWindowService` already uses, and the composition-root test now asserts the graph resolves.
3. **The watcher subscribes to `FileName` and `DirectoryName` only.** Content and timestamp changes do
   not alter what the tree draws, and subscribing to them would wake the tree on every save the editor
   makes. A consequence worth knowing for FEATURE-3C44, whose plan wants external-change reload: that
   feature needs its own watch on the *open document*, not this one.
4. **A root that is not on disk gets no watcher, so it cannot notice itself coming back.** A directory
   that does not exist cannot be watched. The **refresh button** in the sidebar caption — not in the
   plan — exists for exactly that: it is the way back for a root that was unplugged while the editor
   was running. The same gap applies to a root deleted at run time: its own watcher dies with it, and
   refresh is the recovery.
5. **`App.axaml` needed AvaloniaEdit's control theme.** `avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml`
   ships separately from the assembly's default styles; without the `StyleInclude` the editor renders
   as an untemplated blank.
6. **`DirectoryLister` offloads with `Task.Run`, which is usually a smell.** Here it is not decoration:
   directory enumeration has no asynchronous API anywhere in the BCL, and a root on a sleeping network
   share blocks for seconds. The offload is what keeps that off the UI thread.
7. **`TextEditor.Text` cannot be bound**, so `OpenDocumentViewModel` exposes an AvaloniaEdit
   `TextDocument`. In AvaloniaEdit 12 `Text` is a plain CLR property, not an Avalonia one — the XAML
   compiler refuses it outright (`AVLN3000`). `Document` is what the control exposes for binding. A
   `TextDocument` verifies the thread it was built on, so it is built on the UI thread rather than
   beside the file read.
8. **The status bar's file slot now shows the open file's name.** Small, and beyond the plan, but a
   status bar reading "No file open" while a file is plainly on screen is worse than no status bar.
   FEATURE-3C44 takes the slot over properly.
9. **`FontFamily` from settings does not resolve — confirmed, and it is FEATURE-3C44's to fix.** The
   pane binds `AppSettings.EditorFontFamily`, whose value is the comma-separated fallback list
   `"Cascadia Mono, Consolas, DejaVu Sans Mono, Liberation Mono, monospace"`. Driving the real app,
   the text rendered **proportional**, not monospace, with both Cascadia Mono and Consolas installed on
   the machine — so Avalonia's `FontFamily` is not treating that string as a first-available-family
   list. FEATURE-3C44 PHASE01's plan says "font family/size from settings applied live"; it will have
   to either bind only the first family or change the setting's shape. Flagged rather than fixed here:
   the setting belongs to that item, and changing its shape reaches the settings window and its
   validator.
10. **One acceptance sub-criterion could not be driven in the real app.** "Unknown types go to the OS
    default app" is covered at the decision level by `DocumentOpenerTests`
    (`AnImage_GoesToTheOperatingSystem`), but the real `FileLauncher` over `TopLevel.Launcher` was
    **not** exercised, because the workstation locked partway through the verification session and a
    locked session refuses injected input. The same applies to the Enter-key path in `FileTreeView`
    (a design detail, not an acceptance criterion). Everything else was driven end to end — see below.
11. **`WorkspacesViewModel.ActiveRoots` was deleted**, per PHASE01's *Deviations* 8. Its two tests went
    with it; the rest of that suite is untouched.
12. **Documentation freshness sweep: offered, declined.** The candidates were the README's status block
    — it still says "the panes are still placeholders: no file tree, no editor, no preview, no vim",
    which is now wrong in two more ways than it was after PHASE01 — and the README's *Layout* section,
    whose one-line description of Core does not mention the new `Files` namespace. You chose to skip
    both, so no prose doc was touched in this commit. The status block is the second dev running that
    it understates what works; FEATURE-5B28 is the last point at which it has to be fixed.
13. **Line endings (recommendation only, no action taken).** New files are LF in the working tree and
    `.gitattributes` (`* text=auto eol=lf`) keeps them that way; nothing to do. If you ever want it
    applied explicitly, the git-repo-hygiene command set — **not run here** — is
    `git add --renormalize .`, `git status`, `git commit -m "Normalize all line endings to LF"`.

## Build/test evidence

```
> dotnet build Enigma.MarkdownEditor.slnx -c Release -t:Rebuild
Build succeeded.
    0 Warning(s)
    0 Error(s)

> dotnet test --solution Enigma.MarkdownEditor.slnx
Test run summary: Passed!
  total: 330   failed: 0   succeeded: 330   skipped: 0
```

330 tests = PHASE01's 193 + 137 added here (70 Core files, 52 Desktop, 15 in the existing suites).

### How the run-time criteria were verified

The real Release executable was driven against a temp vault holding markdown, a `.txt`, a `.png`
under `assets/`, an empty folder, a `.git/` folder, and `chapter-1/2/10.md` for the ordering. The
workspace was seeded into `%APPDATA%\Enigma.MarkdownEditor\workspaces.json` so the tree itself was
what got exercised rather than PHASE01's manage dialog. Screenshots are in the session scratchpad,
not the repo. **The whole `%APPDATA%\Enigma.MarkdownEditor` folder was deleted afterwards**, so the
profile is back to its pre-run state.

| Criterion | Result |
|---|---|
| External create / rename / delete appears within ~1 s | **Met.** With two folders expanded, a file created in the root, a file created in an expanded subfolder, a delete and a rename were all applied **1.2 s** after the last of them: `aaa-new.md` appeared first among the files, `chapter-3.md` slotted between `chapter-2` and `chapter-10`, `zebra.md` left, and `alpha.markdown` re-sorted to last under its new name. |
| Expansion preserved | **Met.** Both `notes` and `empty-folder` were still open after that batch, and their contents unchanged where nothing had changed. `FileTreeViewModelTests` pins the mechanism — the surviving node is asserted to be the very same object, which is what carries the expansion. |
| Filter rules exact | **Met.** Default: `assets`, `empty-folder`, `notes`, `alpha.markdown`, `readme.md`, `zebra.md` — `notes.txt`, `assets/diagram.png` and `.git` all absent. Toggled: `notes.txt` and `diagram.png` appear; `.git` still absent. |
| Toggle persists | **Met.** `settings.json` read `"ShowAllFiles": true` immediately after the toggle, and the restarted editor came up with the toggle lit and `notes.txt` listed. |
| Directories first, natural order | **Met.** Folders above files, and `chapter-1.md`, `chapter-2.md`, `chapter-10.md` in that order. |
| Empty and unreadable folders render gracefully | **Met.** `empty-folder` expanded to a single greyed italic *Nothing here* row with its own icon. (Access-denied is covered by `FileTreeNodeViewModelTests` and `DirectoryListerTests`; no denied folder was staged in the vault.) |
| Double-click opens a `.md` into the editor | **Met.** Double-clicking `readme.md` filled the pane — caption `README.MD`, the note *read-only until FEATURE-3C44*, line numbers, the file's text — and the status bar changed from *No file open* to `readme.md`. |
| Unknown types go to the OS default app | **Not driven.** See *Deviations* 10: covered by `DocumentOpenerTests` at the decision level, but the platform call itself was not exercised — the workstation locked mid-session. |

The log file recorded no `WARN`, `ERROR` or `FATAL` entry across the whole session.
