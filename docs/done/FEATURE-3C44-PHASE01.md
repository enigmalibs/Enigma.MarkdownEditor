# FEATURE-3C44-PHASE01 — AvaloniaEdit & tab lifecycle

**Status: DONE** · Branch: `feature/feature-3c44-phase01-editor-tabs` · Plan: `docs/plan/FEATURE-3C44.md`

## Summary

The middle pane stops being a read-only preview and becomes the editor. Files open in tabs, the text
can be changed, and the changes can be written back — explicitly with `Ctrl+S`, or on their own after
a pause once autosave is switched on. A tab with unsaved work shows a dot instead of its close button
and asks before it goes away, and that question can be answered with *Cancel* all the way up to the
window refusing to close.

A file the editor opened is written back the way it was found. The encoding, the byte-order mark and
the line endings are all read off the bytes and re-applied on save, so editing one line of a CRLF file
produces a one-line diff rather than a whole-file one. The buffer itself holds line feeds alone, which
is what stops a file acquiring mixed terminators the moment a line is typed into it.

Each open document watches its own file. When that file changes underneath, a buffer with nothing to
lose is silently brought up to date, and one with unsaved work in it gets a bar over the editor
offering *Reload* or *Keep mine*. A file deleted from outside flags its tab `(deleted)` and keeps the
buffer, because saving it puts the file back.

One decision was taken with you before building (the recommended option): **external changes are
detected by a new per-document watcher in Core**, not by the tree's. The plan assumed the tree's
watcher could report them; it cannot, and deliberately so — see *Deviations* 1.

## Files/modules touched

### Created — `src/Enigma.MarkdownEditor.Core/Documents`

| File | Notes |
|---|---|
| `LineEndingStyle.cs` | `Lf` and `CrLf` — the two that are still written. A lone `\r` is read as a break and never written back as one. |
| `LineEndings.cs` | `Detect` (dominant style, LF on a tie), `Normalize`, `Apply`. `Normalize` returns the very same string when there is no `\r`, which is the case every autosave hits. |
| `TextFileFormat.cs` | Encoding + byte-order mark + line endings, as one thing to carry across a save. `DetectEncoding` tests UTF-32 LE **before** UTF-16 LE — the 32-bit mark opens with the whole of the 16-bit one. The encoding it returns already carries the right preamble policy, so writing is one framework call. |
| `DocumentContent.cs` | What a read produced: normalized text plus its format. |
| `DocumentFile.cs` | `ReadAsync` / `WriteAsync`, deliberately one class so the detection and its mirror cannot drift. Reads bytes rather than through a `StreamReader`, which would swallow the mark without saying what it was. 32 MB ceiling, refused with a sentence for the user. |
| `FileStamp.cs` | Exists / last-write / length, taken after every read and write. Comparing it is what tells the editor's own save apart from somebody else's edit. |
| `IDocumentFileWatcher.cs`, `DocumentFileWatcher.cs`, `IDocumentFileWatcherFactory.cs`, `DocumentFileWatcherFactory.cs` | One `FileSystemWatcher` over the document's folder, filtered to its name, on `FileName｜LastWrite｜Size｜CreationTime`, debounced to 250 ms. The event carries nothing — what actually happened is settled by a fresh stamp. |

### Created — `src/Enigma.MarkdownEditor.Desktop`

| File | Notes |
|---|---|
| `ViewModels/DocumentViewModel.cs` | One open document: its `TextDocument`, its format, dirty, missing, and the caret/scroll it was left at. Dirty comes from `UndoStack.IsOriginalFile`, so undoing back to the saved state clears the dot. Owns its watcher; the stamp comparison runs on the watcher's own thread and only a real change is marshalled. |
| `ViewModels/DocumentManagerViewModel.cs` | The pane: the tabs, the active one, and every open/save/save-as/save-all/close/reload path, each reported through the info bar rather than thrown. Named `…ViewModel` rather than the plan's `DocumentManager` — see *Deviations* 2. |
| `Services/IDocumentPrompts.cs`, `DocumentPrompts.cs`, `UnsavedChangesChoice.cs` | The two questions a close or a Save As has to ask, behind one seam — which is what lets the whole close matrix be driven in a test with no Avalonia application. |
| `Services/IAutosaveScheduler.cs`, `AutosaveScheduler.cs` | One restartable idle timer per document, marshalled back through `IUiDispatcher`. A seam for the same reason: a test fires an autosave instead of sleeping through it. |
| `Views/EditorPaneView.axaml(.cs)` | The tab strip, the external-change bar and the one editor they share. Code-behind carries what a binding cannot: caret/scroll carried across tab switches, middle-click close, and the four editing commands, which are methods on the control. |

### Modified

| File | Change |
|---|---|
| `Desktop/Services/DocumentOpener.cs` | Opens into the tabbed pane instead of the single read-only document. |
| `Desktop/ViewModels/MainWindowViewModel.cs` | Hosts `Documents`; the two status-bar slots it owns now mirror the pane's. |
| `Desktop/Views/MainWindow.axaml` | The editor pane replaced by `EditorPaneView`; `Ctrl+S` / `Ctrl+Shift+S` / `Ctrl+W` key bindings. |
| `Desktop/Views/MainWindow.axaml.cs` | The closing handshake (cancel, ask, close again) and the tunnelled `Ctrl+Tab` / `Ctrl+Shift+Tab`. |
| `Desktop/Views/SettingsWindow.axaml` | Font family, font size, wrap and autosave enabled — see *Deviations* 4. |
| `Desktop/Extensions/ServiceCollectionExtensions.cs` | `DocumentManagerViewModel`, `IDocumentFileWatcherFactory`, `IDocumentPrompts`, `IAutosaveScheduler`. |

### Deleted

| File | Why |
|---|---|
| `Desktop/ViewModels/OpenDocumentViewModel.cs` | The read-only stand-in FEATURE-0DE1 PHASE02 put in so the tree had somewhere to open into. Its own XML docs said PHASE01 would replace it. |
| `Desktop.UnitTests/ViewModels/OpenDocumentViewModelTests.cs` | Its 9 tests went with it; what they covered is covered by `DocumentManagerViewModelTests`. |

### Created / modified — tests (570 total, was 482)

| File | Notes |
|---|---|
| `Core.UnitTests/Documents/LineEndingsTests.cs` (10) | Detection per style, mixed and tie-break, the lone `\r`, and the trap `Apply` has to avoid: CRLF applied to text that already has it must not double the carriage returns. |
| `Core.UnitTests/Documents/TextFileFormatTests.cs` (6) | Every mark, its preamble length, the UTF-32-before-UTF-16 ordering, and that an unmarked file gets an encoding that writes **no** mark. |
| `Core.UnitTests/Documents/DocumentFileTests.cs` (10) | Round trips asserted on **bytes**: CRLF stays CRLF including lines typed afterwards, LF stays LF, a mark survives, a file without one does not gain one, UTF-16 survives, an empty file reads as the default so a new file gets LF, and a write recreates a file that is gone. |
| `Core.UnitTests/Documents/FileStampTests.cs` (7) | Missing is `default`; two stamps of an untouched file are equal — the comparison the whole external-change path rests on. |
| `Core.UnitTests/Documents/DocumentFileWatcherTests.cs` (9) | Against the real file system: a **content** edit is reported (which the tree's watcher cannot see), a delete and a create are, a neighbouring file is not, a burst is one report, and a disposed watcher goes quiet. |
| `Desktop.UnitTests/ViewModels/DocumentManagerViewModelTests.cs` (35) | The whole lifecycle over real files: open, de-dup including a different spelling of the same path, dirty and undo-to-saved, save/save-as/save-as-cancelled/save-all, all three close answers, which tab is activated after a close, `ConfirmCloseAll` stopping at the first cancel, tab wrap-around, clean-reload / dirty-bar / reload / keep-mine / deleted-flag-then-save, the editor's own save not looking external, autosave armed-fired-and-held-back, and live settings. |
| `Desktop.UnitTests/UiThread.cs` | One thread and a pumping synchronization context. The ViewModels are thread-affine because a `TextDocument` is; without this the continuation after an `await` lands on an arbitrary pool thread and the next assertion throws. |
| `Desktop.UnitTests/Fakes/` | `EditorHarness` (a manager wired to fakes over real files), `FakeDocumentPrompts`, `FakeAutosaveScheduler`, `FakeDocumentFileWatcher(Factory)`. |
| Existing suites | `DocumentOpenerTests` and `MainWindowViewModelTests` rebuilt around the harness; `ServiceCollectionExtensionsTests` extended for the four registrations. |

## Deviations & follow-ups

1. **External changes come from a new per-document watcher, not from the tree's** (agreed with you
   before building). The plan said "watcher events from FEATURE-0DE1". That watcher subscribes to
   `NotifyFilters.FileName | DirectoryName` only, and its own comment says why: content changes do not
   alter what the tree draws, and subscribing to them would wake it on every keystroke the editor
   saves. It therefore cannot see the case this phase needs. `FileTreeViewModel` also keeps its
   watchers private, and they only exist for workspace roots. `DocumentFileWatcher` watches one file,
   covers any path — including a Save As target outside every root — and leaves the tree untouched.
2. **The manager is `DocumentManagerViewModel`, not `DocumentManager`.** The plan calls it "a Desktop
   service + VM layer"; it is a ViewModel either way — the pane binds to its collection, its active
   document and its three appearance properties — and it sits next to `FileTreeViewModel` and
   `WorkspacesViewModel`. Name only; nothing about the design changed.
3. **The dirty-document bar is in the pane, not the shell's info bar.** The plan says "InfoBar with
   Reload / Keep mine". `Enigma.Avalonia.Desktop`'s `InfoBar` exposes `Title`, `Message`, `Severity`
   and a close button — it carries no action buttons, so it cannot ask that question. The bar over
   the editor can, and it is the better home anyway: the question belongs to one tab, and switching
   tabs has to leave it behind, which a window-wide bar would not.
4. **The settings window's Editor rows were enabled, and Autosave with them.** Not in the plan's file
   list, but its step list says "settings (font/size/wrap) applied live", and FEATURE-0F9A PHASE03 had
   left those rows built, bound and `IsEnabled="False"` with the comment *"Drop the `IsEnabled="False"`
   as each consumer arrives."* Until this commit the section read *"arrives with the editor
   (FEATURE-3C44)"* and there was no way to change the font, its size or wrapping at all. Vim, Show
   every file and Image folder are left disabled — they belong to FEATURE-11AA, FEATURE-0DE1 and
   PHASE03. **Worth your review**: *Show every file* is in fact already live through the sidebar's own
   toggle, so that row could be enabled too; it is FEATURE-0DE1's to claim, so I left it.
5. **Save As, Save and Save All were added to the editor's context menu.** The plan gives gestures for
   Save and Save All but none for Save As, and this window has no menu bar — so `SaveAsCommand` had no
   way in at all. Rather than invent a gesture (the conventional `Ctrl+Shift+S` is already Save All
   here), all three sit in the context menu with their gestures shown, which also makes the two
   shortcuts discoverable.
6. **Focus lands in the editor one dispatcher pass after the document opens.** Found by driving the
   app, not by a test: opening a file starts as a double-click in the tree, and the tree takes the
   focus for the row that was clicked *after* the open completes. `Focus()` called inline, and even
   posted at `Loaded`, was simply lost — the user's first keystrokes went into the file list. It is
   posted at `Background`, and it focuses `Editor.TextArea` rather than the `TextEditor` around it.
7. **Autosave is armed from the dirty flag, not only from the text changing.** AvaloniaEdit raises
   `TextChanged` from inside its update group and closes the undo group *after* it, so at that moment
   a first edit has not made the document dirty yet. Caught by a failing test, not by reading.
8. **A document is written in place, not through a temporary file.** A swap would survive a crash
   mid-write, but it also drops a second file into the folder the user is looking at — which the tree,
   its watcher and any sync client all see — and it replaces the file's identity, taking hard links
   and access rules with it. **Follow-up if you want the durability**: the cheap version is writing to
   the same name via a handle opened with `FileShare.None` plus `FlushToDisk`.
9. **Encoding detection trusts the byte-order mark and nothing else.** A file without one is read as
   UTF-8, which also reads plain ASCII correctly. Guessing a legacy code page from byte statistics is
   the one way an editor corrupts a file it merely opened, so it is not attempted; a Windows-1252 file
   with accented characters will come back with replacement characters rather than silently mangled
   bytes.
10. **A file that cannot be stamped reads as missing.** A permission the user lost, or a share that
    went away, flags the tab `(deleted)`. That is the honest answer to "is what I read still there",
    but it does mean a network blip can briefly mislabel a tab.
11. **The current-line highlight uses AvaloniaEdit's own default colours.** `HighlightCurrentLine` is
    on; matching it and the selection to the Enigma palette is PHASE02's appearance pass, which is
    where the plan puts it.
12. **The window-closing handshake and the tab gestures have no unit tests.** They are view code with
    no seam — the same class of code as `MainWindow`'s existing pane-width logic, which is also
    untested. The decision underneath, `ConfirmCloseAllAsync`, is tested; the 20 lines that cancel the
    event and close again were verified by driving the real application (below).
13. **Linux was not verified** — no Linux machine was available in this session. Nothing here is
    platform-specific beyond path comparison (case-insensitive on Windows only, by
    `OperatingSystem.IsWindows()`) and `FileSystemWatcher`, which the Core suite exercises on whichever
    platform it runs on.
14. **Line endings (recommendation only, no action taken).** Every new file is LF in the working tree
    and `.gitattributes` (`* text=auto eol=lf`) keeps it that way; there is no CRLF noise in this
    diff and nothing to do.

## Build/test evidence

```
> dotnet build Enigma.MarkdownEditor.slnx -c Release -t:Rebuild
Build succeeded.
    0 Warning(s)
    0 Error(s)

> dotnet test Enigma.MarkdownEditor.slnx
Test run summary: Passed!
  total: 570   failed: 0   succeeded: 570   skipped: 0
```

570 tests = 482 before, minus the 9 that went with `OpenDocumentViewModel`, plus 97 added here
(42 Core, 55 Desktop).

### How the run-time criteria were verified

The real executable was driven against a temp vault holding an LF file, a CRLF file, a UTF-8-BOM file
and a 300-line file, with the workspace seeded into `%APPDATA%\Enigma.MarkdownEditor\workspaces.json`.
Input was injected with `SetCursorPos`/`mouse_event`/`SendKeys` and read back through **UI Automation**
plus screen captures; bytes on disk were checked in hex. **The pre-existing
`%APPDATA%\Enigma.MarkdownEditor` folder was moved aside before the run and restored afterwards**, and
the temp vault was deleted, so the profile is back to its pre-run state.

| Criterion | Result |
|---|---|
| Open from the tree | **Met.** Double-click opens a tab, the status bar leaves *No file open*, and the caret slot tracks the caret live (`Ln 3, Col 6` after typing). |
| De-duplication | **Met.** Three tabs open; double-clicking an already-open file kept the count at three and activated its tab. |
| Dirty indicator | **Met.** The tab's close button is replaced by the dot the moment the buffer differs, and comes back when it is saved. |
| CRLF round trip | **Met, in hex.** `alpha\r\nbeta\r\n` → typed a line → saved → `616c706861 0d0a 62657461 0d0a 67616d6d61`. The line typed in the editor was written with the file's own terminator. |
| LF round trip | **Met, in hex.** No `0d` anywhere after an edit and save. |
| Byte-order mark | **Met, in hex.** `efbbbf` still leads the file after an edit and save. |
| `Ctrl+S` / `Ctrl+W` / `Ctrl+Tab` / `Ctrl+Shift+Tab` | **Met.** Tab switching wraps in both directions. |
| Close: Cancel | **Met.** Tab stayed, still dirty, file untouched. |
| Close: Save / Don't save | **Met** on the app-exit path below. |
| Middle-click close, X button | **Met.** Both removed the tab. |
| App exit with two dirty tabs | **Met.** `Alt+F4` asked about the first; *Cancel* left the application running with both tabs and the disk untouched. A second `Alt+F4`, answered *Save* then *Don't save*, wrote the first file, left the second alone, and exited. |
| External change, clean buffer | **Met.** The file was rewritten from outside; the tab reloaded silently, stayed clean, and no prompt appeared. |
| External change, dirty buffer | **Met.** The bar appeared reading *"This file was changed by something else while you had unsaved changes here."* with *Reload* and *Keep mine*. *Reload* took the file and cleared the dot; *Keep mine* kept the buffer, dismissed the bar, and the next save overwrote the outside version. |
| Deleted externally | **Met.** The tab and the status bar both read `third.md (deleted)`, the buffer stayed, and `Ctrl+S` recreated the file with the buffer's contents and cleared the flag. |
| Save As | **Met.** The platform's own save dialog opened pre-filled with the document's name in its folder; saving under a new name wrote the new file, moved the tab and the status bar onto it, and left the original untouched. |
| Autosave | **Met.** With autosave on at a 1-second delay, typing and then stopping wrote the file with no `Ctrl+S`, and the dirty dot cleared. |
| Settings applied live | **Met.** With a document open, font size 14 → 26 and wrap on → off, then *Close*: the editor re-rendered at the new size and grew a horizontal scrollbar, with no restart. |
| Caret restored per tab | **Met.** `Ln 301, Col 23` in one tab and `Ln 2, Col 3` in another survived switching back and forth. |
| Line numbers, current-line highlight | **Met**, visible in the captures. |

The log recorded no `WARN`, `ERROR` or `FATAL` entry across the whole session.
