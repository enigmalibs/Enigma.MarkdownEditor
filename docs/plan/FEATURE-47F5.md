# FEATURE-47F5 — Confirm every file tree delete

**Status: DONE** · single phase · Roadmap: `docs/roadmap.md`
**Type:** FEATURE · **Branch:** `feature/feature-47f5-confirm-delete`, cut from the run branch at build time.
**Run:** feature/2026-10-14-confirm-delete-release

## Objective

Always ask the user to confirm before a file or a folder is removed from disk. Today a delete that
goes to the trash happens silently; only a delete that cannot go to the trash asks. After this item,
every delete from the file tree — the `Delete` key and the context menu, one entry or a whole
selection — asks first, and nothing is touched unless the user explicitly confirms.

## Context & constraints

Established by reading the code, not assumed.

- **Every user-initiated removal from disk goes through `FileTreeViewModel.DeleteAsync`**
  (`src/Enigma.MarkdownEditor.Desktop/ViewModels/FileTreeViewModel.cs:568`), reached from
  `DeleteCommand`, which both the `Delete` key (`FileTreeView.axaml.cs:119`) and the *Delete* menu
  item (`FileTreeView.axaml:112`) execute.
- **The flow today:** `DeletionTargets()` (the deletable entries of the selection, informational rows
  dropped, reduced to the outermost by `FileSelection.Outermost`); each goes to
  `ITrashService.MoveToTrashAsync` **without a question**; entries the trash declines — or all of them
  when `_trash.IsAvailable` is false — are asked about once through
  `IFileOperationPrompts.ConfirmPermanentDeleteAsync` (single-entry overload, or the list overload for
  several) and deleted outright on a yes. Each touched folder is re-read once.
- **The silence was deliberate**: `IFileOperationPrompts.ConfirmPermanentDeleteAsync`'s remarks say
  "A trashed entry can be fetched back out of the recycle bin, so confirming it would be a click the
  user learns to ignore", and `FileTreeViewModelTests.Deleting_SendsItToTheTrashAndTakesTheRowWithIt`
  asserts no prompt. This item reverses that decision at the user's request; those remarks and
  assertions change with it.
- **The other removals already ask or touch nothing on disk:** a move onto a taken name asks
  *Overwrite / Keep both / Cancel* (`AskHowToResolveAsync`); removing a workspace asks in an inline bar
  (`WorkspaceManagerViewModel`); removing a folder from a workspace's list leaves the folder on disk;
  `ConfigRecovery`'s `File.Move(..., overwrite: true)` is internal recovery, not a user removal.
- **The dialog host:** `FileOperationPrompts` asks through `IContentDialogService.ShowAsync`; only
  `DialogResult.Primary` is a confirmation — Escape and a scrim click return `None`
  (enigma-avalonia-desktop). The permanent prompts set no `DefaultButton`.
- **Naming the trash:** FEATURE-425B removed a bar that said "recycle bin" on Windows and "trash"
  elsewhere; the permanent prompt says "trash" everywhere.
- **Tests:** `FileTreeViewModelTests` drives the ViewModel over real temp folders with
  `FakeFileOperationPrompts` and `FakeTrashService`. `FileOperationPrompts` itself has no tests today;
  headless tests run under `[AvaloniaFact]` (`HeadlessTestApp`).
- Build bar: Release, 0 warnings, no `AVLN`; `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`.

## Design

1. **New prompt pair** on `IFileOperationPrompts`, mirroring the permanent pair:
   - `Task<bool> ConfirmMoveToTrashAsync(string name, bool isDirectory)`;
   - `Task<bool> ConfirmMoveToTrashAsync(IReadOnlyList<string> names)`.

   `true` only when the user explicitly confirmed. Remarks on the interface and on the permanent pair
   are rewritten: every delete asks; the trash question is the one asked first, the permanent one is
   asked instead of it when there is no trash, and after it for whatever the trash then declines.
2. **`FileOperationPrompts` implementation.**
   - Single: title `Delete '<name>'?`; message — the file (or "folder and everything in it") will be
     moved to the recycle bin (Windows) / trash (elsewhere), where it can be restored from.
   - Several: title `Delete <N> items?` (one name → the single title); message saying they will be
     moved to the recycle bin / trash and can be restored, folders with everything in them, then the
     first five names as `• name` and `…and <N> more.` beyond five — the same listing the permanent
     prompt uses, shared rather than duplicated.
   - Buttons: *Delete* (primary) / *Cancel* (close); no `DefaultButton`, as the other destructive
     prompts. Argument guards as the permanent pair.
3. **`DeleteAsync` flow.**
   1. Guard (`CanDeleteSelection`) and snapshot `DeletionTargets()` **before** asking.
   2. **No trash** (`!_trash.IsAvailable`): the permanent-delete confirmation over every target is the
      one question; refused → nothing touched; confirmed → each deleted outright.
   3. **Trash available:** ask `ConfirmMoveToTrashAsync` over every target first; refused (or
      dismissed) → nothing touched, no InfoBar. Confirmed → each to the trash; whatever the trash
      declines is then asked about through the permanent confirmation, exactly as today.
   4. Re-read touched folders, failures through `RunAsync` — unchanged.
4. **Fakes & tests.**
   - `FakeFileOperationPrompts`: `ConfirmsMoveToTrash` (default `true`, so the existing trash tests keep
     describing a user who confirms), `AskedToMoveToTrash` (names per question), `TrashConfirmationsAsked`.
   - New `Fakes/FakeContentDialogService.cs`: records the `ContentDialog` each `ShowAsync` configured
     and returns a result the test chose.
   - ViewModel tests (`FileTreeViewModelTests`): one entry → asked once, naming it, then trashed;
     several → one question naming every entry in selection order; a folder with its own child
     selected → the question names the folder alone; refusing → nothing trashed, every row kept, no
     InfoBar, no permanent question; no trash → only the permanent question (no trash question); a
     trash that declines one of three → the trash question for all three, then the permanent one for
     the declined entry; a root in the selection → no question; an informational row is left out of
     the names; a trash that breaks part-way → asked once, then one bar. Existing tests that assert
     "no confirmation" are updated to assert the trash confirmation instead.
   - New `Services/FileOperationPromptsTests.cs` (headless, `[AvaloniaFact]`, over the fake dialog
     service): single file vs folder wording and title; the several-entries title, the five-name
     listing and "…and N more"; the button texts; only `Primary` confirms (`Close` and `None` do not);
     argument guards.
5. **Docs (sweep):** README *Workspaces and the file tree* — the delete bullet says a delete asks
   first. `docs/RELEASE.md` §1 — a standing manual check for the confirmation (Delete key and menu,
   one and several entries, Cancel and Escape keep everything) and §2's *Tree operations* step
   confirms the prompt on Linux.

## Acceptance criteria

- Pressing `Delete` or choosing *Delete* in the tree with one or several entries selected opens one
  confirmation naming what will be removed; nothing on disk changes until the user presses *Delete*.
- *Cancel*, Escape and a click outside the dialog leave every entry exactly where it was.
- Without a trash, exactly one question is asked (the permanent one); with a trash that declines some
  entries, the permanent question still follows for those.
- A selection holding a workspace root asks nothing and deletes nothing.
- New and updated tests cover the above; the whole suite is green.
- Release build: 0 warnings, 0 errors, no `AVLN`.

## Out of scope

- A "don't ask again" option or a setting to turn the confirmation off — the request is *always*.
- Confirming removal of a folder from a workspace's list (touches nothing on disk) and changing the
  move-overwrite prompt (already asks).
- Deleting a file from anywhere other than the tree (there is no such command).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| What counts as "removing files and directories"? | Deletes from disk through the tree — trash and permanent | The only user-initiated removals from disk; move-overwrite already asks, workspace-list removals touch nothing on disk | Also prompting on removing a root from a workspace list (no file is removed); re-wording the move-overwrite prompt (already a confirmation) |
| How many questions for a multi-selection? | One for the whole selection, naming up to five entries | Consistent with the existing permanent prompt; a dialog per file is the kind users click through | One per entry |
| No trash at all — ask twice? | No: the permanent question is the only one | It is already the stricter confirmation; two questions for one delete would teach clicking through | Trash question then permanent question |
| Trash declines an entry after the user confirmed | Still ask the permanent question for those | The user agreed to a recoverable delete, not an irreversible one | Deleting them outright on the first yes |
| Wording and buttons | "Delete '<name>'?" / "Delete N items?", *Delete* / *Cancel*, recycle bin on Windows and trash elsewhere | Matches the menu item the user chose and the platform's own name for the bin, as the bar 425B removed did | "Move to trash" button; "trash" on every platform |
| Default button | None | Matches the existing destructive prompts; the library's `DefaultButton` only declares intent | `Primary` |
| An opt-out setting | None | The request says *always*; a setting is scope nobody asked for | "Don't ask again" checkbox; a settings toggle |
| Interface shape | A `ConfirmMoveToTrashAsync` pair mirroring `ConfirmPermanentDeleteAsync` | Keeps the seam's existing shape; the single overload words a folder differently | One list-only method; a method taking tree nodes (would leak ViewModel types into the seam) |
| Testing the dialog text | Headless tests over a new `FakeContentDialogService` | The prompts carry the wording the user confirms against, and have had no tests; a fake keeps it to the existing headless harness | Leaving `FileOperationPrompts` untested, as before |
