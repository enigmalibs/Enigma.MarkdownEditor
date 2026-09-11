# FEATURE-0DE1-PHASE03 — File operations

**Status: DONE** · Branch: `feature/feature-0de1-phase03-file-ops` · Plan: `docs/plan/FEATURE-0DE1.md`

## Summary

The tree stops being read-only. A right-click on any row offers **New file…**, **New folder…**,
**Rename…** (F2) and **Delete** (Del); a file or folder can be dragged onto another folder, within a
root or across roots; and a delete goes to the operating system's own trash rather than being
destroyed. Every name is checked as it is typed, and a move onto a name that is already taken asks
before it does anything — Overwrite, Keep both, or Cancel.

Nothing here decides anything on its own. What a name may be, what a move means, and what to do about
a clash are all pure decisions in `Enigma.MarkdownEditor.Core.Files`, which is why they are tested
without a file system under them. What is left in the ViewModel is the sequence: ask, plan, execute,
re-read, report.

Three decisions were taken with you before building (all three were the recommended option):

1. **The trash lives entirely in Core**, not split across Core and Desktop as the plan's wording had
   it. `SHFileOperation` and `gio trash` need no Avalonia, and Core already owns every other
   disk-touching class — `DirectoryLister`, `DirectoryWatcher`. Only the ContentDialog that confirms a
   *permanent* delete stayed in Desktop, where the dialog is.
2. **Names are typed into a ContentDialog**, not edited inline in the row. Inline editing is nicer to
   use, but its focus/commit/cancel logic would live in code-behind where nothing can test it; the
   dialog is the pattern the manage-workspaces view already uses, and it puts the validation message
   where the user is looking.
3. **A trash delete does not ask.** It is recoverable, and a prompt on every delete is the one users
   learn to click through. The permanent-delete fallback — the only delete that cannot be undone — is
   the one that confirms.

## Files/modules touched

### Created — `src/Enigma.MarkdownEditor.Core/Files` (the decisions)

| File | Notes |
|---|---|
| `FileNameValidation.cs` | What a name may be, as the sentence to show the user or `null` — the same contract `WorkspaceValidation` uses. `Validate` is pure syntax; `ValidateIn` adds the clash check against a folder. Windows' reserved device names and trailing dots/spaces are rejected on **every** platform, so a vault behaves the same wherever it is opened. |
| `UniqueName.cs` | The "Keep both" naming: `readme.md` → `readme (2).md`, the number before the extension for a file and at the end for a folder or a dotfile. Bounded, so it throws rather than looping. |
| `MoveResolution.cs` | `Cancel`, `Overwrite`, `KeepBoth`. Cancel is first, so it is the `default`. |
| `FileMovePlanStatus.cs`, `FileMovePlan.cs` | What a proposed move amounts to: ready, a clash, or refused with a reason. Static factories, like `DirectoryListing` in the same folder. |
| `FileMovePlanner.cs` | The one place a move is judged: source missing, target missing, into itself, into its own descendant, already in that folder, clash, ready. A malformed path comes back refused rather than thrown. |
| `FileOperations.cs` | Create / rename / move / delete, each offloaded for the same reason `DirectoryLister` is. Includes the two-step dance a **case-only rename** needs on Windows, with a best-effort restore if the second half fails. |
| `FileOperationException.cs` | Mirrors `WorkspaceValidationException`: the message is written for the user, not for a log. |

### Created — `src/Enigma.MarkdownEditor.Core/Files` (the trash)

| File | Notes |
|---|---|
| `ITrashService.cs` | `IsAvailable`, and `MoveToTrashAsync` returning `bool`. `false` means precisely "not trashed, and the entry is still exactly where it was" — that promise is what the whole delete flow is built on. |
| `WindowsTrashService.cs` | `SHFileOperationW` with `FOF_ALLOWUNDO` — the one flag that makes it the recycle bin rather than a delete. Blittable struct, double-NUL `pFrom`, `fAnyOperationsAborted` checked. See *Deviations* 1. |
| `LinuxTrashService.cs` | `gio trash` first; failing that, a minimal XDG home-trash implementation (`.trashinfo` written before the move, and removed again if the move fails). Cross-device is deliberately out of scope — see *Deviations* 4. |
| `UnsupportedTrashService.cs` | Null object for macOS and anything else: `IsAvailable` is `false`, so the caller offers a confirmed permanent delete. |
| `TrashService.cs` | `ForCurrentPlatform()`. |

### Created — `src/Enigma.MarkdownEditor.Desktop`

| File | Notes |
|---|---|
| `Services/IFileOperationPrompts.cs`, `FileOperationPrompts.cs` | Every question an operation has to ask, behind one seam — which is what lets the tree's operations be driven end to end in a test with no Avalonia application. Three name prompts, the conflict prompt, and the permanent-delete confirmation. |
| `ViewModels/FileNamePromptViewModel.cs` | The name box: the value, what is wrong with it, and whether it can be used. Checked on every keystroke. |
| `Views/FileNamePromptView.axaml(.cs)` | One box and the message under it. Two handlers, both unavoidable: Enter has to reach the ViewModel because nothing can press a `ContentDialog`'s button, and the opening focus has to be posted — see *Deviations* 6 and 7. |

### Modified

| File | Change |
|---|---|
| `Desktop/ViewModels/FileTreeViewModel.cs` | Four commands, the public `MoveAsync` a drop calls, `CanAcceptDrop` for the drag cursor, `CanActOnSelection`, the target-folder resolution, and the reveal/re-read helpers. `ReportAsync` gained an overload so a `FileOperationException` is shown as advice rather than as an incident. |
| `Desktop/Views/FileTreeView.axaml` | The context menu, and `DragDrop.AllowDrop`. The three pointer handlers moved out of XAML — see *Deviations* 5. |
| `Desktop/Views/FileTreeView.axaml.cs` | Context-menu selection, F2/Delete, and the whole drag gesture. |
| `Desktop/Extensions/ServiceCollectionExtensions.cs` | `IFileOperationPrompts` and `ITrashService` (resolved once from the running platform). |

### Created / modified — tests (482 total, was 330)

| File | Notes |
|---|---|
| `Core.UnitTests/Files/FileNameValidationTests.cs`, `UniqueNameTests.cs`, `FileMovePlannerTests.cs`, `FileOperationsTests.cs` | 112 tests over the decisions: every rejection rule, both clash outcomes, own-name and case-only renames, every planner branch (including a malformed path refused rather than thrown), and all of create/rename/move/overwrite-file/overwrite-folder/keep-both/cancel/delete. |
| `Core.UnitTests/Files/TrashServiceTests.cs` | 5 tests, including one real round trip through the platform's trash — see *Deviations* 2. |
| `Desktop.UnitTests/ViewModels/FileNamePromptViewModelTests.cs` | 10 tests: every validation outcome, the own-name exemption, trimming, and that Enter cannot commit what the button refuses. |
| `Desktop.UnitTests/ViewModels/FileTreeViewModelTests.cs` | 25 tests added: each operation end to end over real directories, the folder a new entry lands in (selection, a file's own folder, an empty folder's own row), a root refusing to be renamed or deleted, all three conflict resolutions, the trash declining, the trash breaking, and the drops `CanAcceptDrop` refuses up front. |
| `Desktop.UnitTests/Fakes/` | `FakeFileOperationPrompts` (the answers a user would have given) and `FakeTrashService` (a trash that takes things, one that declines, one that is not there — and it really removes the entry, because the tree re-reads the folder afterwards). |
| Existing suites | `ServiceCollectionExtensionsTests` extended for the two registrations; `MainWindowViewModelTests` and `FileTreeViewModelTests` updated for the two new constructor arguments. |

## Deviations & follow-ups

1. **`[DllImport]` with a scoped `SYSLIB1054` suppression, not `[LibraryImport]`.** The source generator
   emits `unsafe` unconditionally, so `LibraryImportAttribute` fails this build outright
   (`SYSLIB1062` → `CS0227`); `AllowUnsafeBlocks` is set nowhere in the solution. Given the choice
   between two lines of suppression and turning unsafe code on for the whole of Core — a library that
   is otherwise entirely safe managed code, and which will hold the vim engine and the markdown
   pipeline — the suppression is the smaller door to leave open. `SYSLIB1054` is a modernization
   suggestion, not a correctness one, and the struct is already blittable, so flipping it later is a
   two-line change. **Worth your review**: if you would rather have `AllowUnsafeBlocks` in
   `Enigma.MarkdownEditor.Core.csproj`, say so and it flips.
2. **Running the suite on Windows leaves one file in your recycle bin per run.** It is named
   `enigma-markdowneditor-trash-test-<guid>.md` and says so in its own content. This is deliberate: a
   real `SHFileOperationW` call is the only thing that proves the marshalling is right, and wrong
   marshalling fails silently. On Linux the equivalent lands in `~/.local/share/Trash`. **My session
   left 4 such files in your bin** (3 from the sub-agent's verification runs, 1 from mine), plus the
   `chapter-20.md` and `empty-folder` entries from driving the real app.
3. **`ITrashService` is registered as an instance, not a type.** Which implementation is right cannot
   change while the process is alive, and the Windows one holds no state.
4. **The Linux trash is home-trash only.** The XDG spec's per-volume `.Trash-$uid` directories are out
   of scope, so a delete across filesystems answers `false` and falls through to the confirmed
   permanent delete. Honest, and the safe direction to be wrong in.
5. **The three pointer handlers are attached in code, in the *tunnel* phase.** This is the difference
   between a drag that works and one that never starts: a `TreeViewItem` marks `PointerPressed` as
   handled in order to select itself, so a bubble-phase handler — which is all the XAML attribute form
   can give you — never sees the press. None of the three marks the event handled, so selection and
   expansion are unaffected. **Found by driving the real app, not by a test**; the gesture is view code
   and has no unit-testable seam.
6. **The name dialog's opening focus has to be posted.** `ContentDialog` takes focus for itself as it
   opens, and it does so *after* its content is attached — so `NameBox.Focus()` from
   `OnAttachedToVisualTree` is simply undone, and the dialog opens with a pre-filled name you cannot
   type into. Posting at `DispatcherPriority.Background` fixes it. Also found by driving the app;
   UI Automation reported focus sitting on the Window.
7. **With no stem, the caret goes to the start and nothing is selected.** The New file dialog opens on
   a bare `.md`; selecting the whole box would mean the first keystroke threw the extension away,
   undoing the point of pre-filling it. Same for renaming a dotfile. Also found by driving the app.
8. **A new file is only opened if this editor would be the one to show it.** The plan says "opens the
   new file"; creating an empty `photo.png` and having the operating system launch something for it is
   not what that means here, so the open is gated on `FileTreeFilter.OpensInEditor`.
9. **A workspace root cannot be renamed or deleted from the tree.** Either would leave the workspaces
   file pointing at a folder that is no longer there or no longer called that. The manage-workspaces
   dialog is the only place that keeps the two in step. Not in the plan; the alternative is a
   silently-broken workspace.
10. **Nothing reconciles operations with the watcher, because nothing needs to.** The plan asks for
    "no double-apply, no flicker". PHASE02's tree never applies events — it re-lists the affected
    directory and merges by path, keeping node identity — so applying the same change twice is
    applying it once. An operation re-reads its folders immediately rather than waiting out the 300 ms
    debounce, and the watcher's own event a moment later is a no-op. Verified: 12 consecutive
    operations left the tree exactly matching disk.
11. **The trash InfoBar is intrusive by design, and worth a second look.** It reports where a deleted
    entry went, which is the thing a vanishing row cannot say. It also covers the toolbar until
    dismissed, and its overlay swallows clicks meant for the buttons underneath. It has a close
    button. If it grates in daily use, the fix is to drop it for the trash case and keep it only for
    the permanent delete.
12. **A name that the tree then hides is still accepted.** `.md` is a legal file name, and the tree's
    filter excludes dot-prefixed entries — so a user who clears the box and types exactly `.md`
    creates a file they will not see. The caret fix means the natural flow never produces it. Left
    alone rather than special-cased: a dotfile is a legitimate thing to want, and the filter rule is
    PHASE02's.
13. **The editor pane keeps showing a file after it is deleted or renamed.** The document has no
    lifecycle of its own yet; FEATURE-3C44 PHASE01 owns `DocumentManager`, and this is its to handle.
14. **Linux was not verified** — no Linux machine was available in this session, and the plan
    anticipates this ("Linux verified manually — record in the completion doc"). What is untested
    there: `gio trash`, the XDG fallback, and `LinuxTrashService.IsAvailable`. The Core suite's trash
    round-trip test runs on whichever platform it is executed on, so a single `dotnet test` on Linux
    exercises the real path.
15. **Documentation freshness sweep: offered, declined.** The candidates were the README's status block
    — it still says "the panes are still placeholders: no file tree, no editor, no preview, no vim",
    now wrong in three ways with FEATURE-0DE1 complete — and the README's *Layout* section, whose
    one-line description of Core still does not mention the `Files` namespace, by now the largest
    thing in that project. You chose to skip both, so no prose doc was touched in this commit. This is
    the third dev running that the status block has understated what works; FEATURE-5B28 is the last
    point at which it has to be fixed.
16. **Line endings (recommendation only, no action taken).** New files are LF in the working tree and
    `.gitattributes` (`* text=auto eol=lf`) keeps them that way; nothing to do.

## Build/test evidence

```
> dotnet build Enigma.MarkdownEditor.slnx -c Release -t:Rebuild
Build succeeded.
    0 Warning(s)
    0 Error(s)

> dotnet test --solution Enigma.MarkdownEditor.slnx
Test run summary: Passed!
  total: 482   failed: 0   succeeded: 482   skipped: 0
```

482 tests = PHASE02's 330 + 152 added here (117 Core, 35 Desktop).

### How the run-time criteria were verified

The real Release executable was driven against a temp vault (markdown, nested folders, an empty
folder, and two files deliberately sharing the name `readme.md` in different folders) with the
workspace seeded into `%APPDATA%\Enigma.MarkdownEditor\workspaces.json`. Input was injected with
`SetCursorPos`/`mouse_event`/`SendKeys` and the result read back through **UI Automation**, which
reports the tree's actual contents rather than pixels; screenshots via `PrintWindow` are in the session
scratchpad, not the repo. **The whole `%APPDATA%\Enigma.MarkdownEditor` folder and the temp vault were
deleted afterwards**, so the profile is back to its pre-run state.

| Criterion | Result |
|---|---|
| New file, from the context menu | **Met.** Created in the selected folder, empty (0 bytes), revealed and selected in the tree, and opened in the editor pane with the status bar following. Both commit paths driven: Enter and the Create button. |
| New folder | **Met.** Created, revealed, and sorted above the files. |
| Name validation, live | **Met.** Empty box → Create disabled, no message. `a/b` → disabled, *"A name cannot contain \ or /."*. A name a file in that folder already uses → disabled, *"There is already a file called 'idea.md' here."* — correctly naming it a *file* while a folder was being created. |
| Rename (F2) | **Met.** F2 on the selected row opened the dialog pre-filled with `chapter-2.md`, **stem selected and `.md` not**. Renaming to `chapter-20.md` reordered the row correctly: `chapter-1.md`, `chapter-10.md`, `chapter-20.md`. |
| Delete → recycle bin (file) | **Met, against the real shell.** `chapter-20.md` left disk and appeared in the Windows recycle bin, confirmed by enumerating `Shell.Application.Namespace(0xA)` before and after. No confirmation was asked. An Info bar read *"'chapter-20.md' was moved to the recycle bin — It can be put back from there."* |
| Delete → recycle bin (folder with contents) | **Met.** `empty-folder`, holding a file, went to the bin as a folder. |
| Move by drag & drop | **Met.** `chapter-1.md` dragged into `empty-folder`, and a **folder** (`empty-folder`, with its file inside) dragged into `notes`. `DoDragDropAsync` returned `Move` both times. |
| Drop refused up front | **Met.** Dragging over the file's own parent folder set the effect to `None` before the button was released — the drop never reached the ViewModel. |
| Conflict prompt: Cancel | **Met.** Both files untouched, contents unchanged. |
| Conflict prompt: Keep both | **Met.** The moved file became `readme (2).md` carrying its own content; the existing `readme.md` kept its own. |
| Conflict prompt: Overwrite | **Met.** The destination's content was replaced by the moved file's, and the source was gone. |
| No tree desync after any operation | **Met.** Twelve consecutive operations — create, rename, delete, three moves, three conflict resolutions — left the tree matching disk exactly, with expansion preserved throughout. PHASE02's watcher also still picked up an *external* file creation on its own during the session, so nothing here regressed it. |
| Fallback confirm-permanent path | **Met, by test double** — which is what the criterion asks for. `FakeTrashService { IsAvailable = false }` and `{ Accepts = false }` both route to the confirmation; confirming deletes outright, refusing leaves the file exactly where it was. |
| Linux trash | **Not verified.** See *Deviations* 14. |

The log file recorded no `WARN`, `ERROR` or `FATAL` entry across the whole session.
