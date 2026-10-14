# FEATURE-47F5 — Confirm every file tree delete

**Status: DONE** · single phase · Branch: `feature/feature-47f5-confirm-delete` (cut from
`feature/2026-10-14-confirm-delete-release`)
Plan: `docs/plan/FEATURE-47F5.md` · Roadmap: `docs/roadmap.md`

## Summary

Every delete from the file tree now asks before anything leaves the disk. Until now a delete that
went to the trash was silent, and only a delete that could not go there asked.

- **With a trash** (the normal case on Windows and Linux), pressing `Delete` or choosing *Delete*
  asks one question first:
  - *Delete 'name'?*, or *Delete N items?* for a selection, naming up to five entries and then
    *…and N more*;
  - it says where they go (the recycle bin on Windows, the trash elsewhere) and that they can be
    restored from there.

  *Cancel*, `Escape` and a click outside the dialog leave everything where it was, with no InfoBar.
  On *Delete*, the entries go to the trash as before.
- **Whatever the trash then declines is still asked about separately**, through the existing
  permanent-delete question (a cross-device move on Linux is the usual case). Agreeing to a delete
  that can be undone is not agreeing to one that cannot.
- **Without a trash**, the permanent question is the **only** one: one delete, one question.
- The targets are captured **before** asking, so the answer covers exactly the entries the dialog
  named, whatever the watcher does to the selection while it is open.
- A selection holding a workspace root is still refused outright, before any question.

## Files/modules touched

**Modified**

- `src/Enigma.MarkdownEditor.Desktop/Services/IFileOperationPrompts.cs`:
  - new `ConfirmMoveToTrashAsync(string name, bool isDirectory)` and
    `ConfirmMoveToTrashAsync(IReadOnlyList<string> names)`;
  - the remarks that called the permanent question "the one delete that asks" are rewritten to say
    which question is asked when.
- `src/Enigma.MarkdownEditor.Desktop/Services/FileOperationPrompts.cs`:
  - the two new prompts;
  - the four delete confirmations now share one `ConfirmAsync` (no default button, only `Primary`
    confirms), a `Listing` (five names and *…and N more*) and a `ThrowIfNoNames` guard;
  - `TrashName` names the bin as the platform does.
- `src/Enigma.MarkdownEditor.Desktop/ViewModels/FileTreeViewModel.cs`:
  - `DeleteAsync` asks before the trash loop, or skips straight to the permanent question when
    there is no trash;
  - new private `ConfirmMoveToTrashAsync(List<…>)` picks the single or list overload;
  - the `DeleteCommand` doc says it confirms first.
- `tests/…/Fakes/FakeFileOperationPrompts.cs`:
  - `ConfirmsMoveToTrash` (default `true`), `AskedToMoveToTrash` and `TrashConfirmationsAsked`;
  - the two new methods.
- `tests/…/ViewModels/FileTreeViewModelTests.cs`:
  - every delete test now also asserts which questions were asked and what they named;
  - `Deleting_SendsItToTheTrashAndTakesTheRowWithIt` no longer asserts "no confirmation";
  - three new tests: refusing for a folder, refusing for a selection, and a trash that declines one
    entry whose second question is refused.
- `README.md` — *Workspaces and the file tree*: a delete always asks first, with one question for a
  selection.
- `docs/RELEASE.md`:
  - §1, a new standing manual check (*Every delete asks first*);
  - §2, the *Tree operations* step now includes answering the confirmation and checking it says
    *trash*.

**Created**

- `tests/…/Fakes/FakeContentDialogService.cs` — an `IContentDialogService` that applies each
  `configure` delegate to a real `ContentDialog`, keeps it, and returns a chosen `DialogResult`.
- `tests/…/Services/FileOperationPromptsTests.cs` — 12 headless tests:
  - the file and folder wording;
  - the several-entries title and listing;
  - the five-name cut-off;
  - the single title for a list of one;
  - the permanent wording;
  - only `Primary` confirms, across all four prompts (theory, 4 cases);
  - argument guards;
  - the constructor guard.
- `docs/done/FEATURE-47F5.md` — this document.

**Workflow artifacts** — `docs/roadmap.md` (`TODO` → `IN PROGRESS` → `DONE`) and the plan file's
status.

## Deviations & follow-ups

- **The single-entry permanent prompt's wording changed**, beyond the plan's letter:
  - It used to say "There is no trash on this system to move it to", which was already wrong when
    the trash merely *declined* the entry.
  - Now that the question can directly follow *"…will be moved to the recycle bin"*, that sentence
    would contradict the dialog the user just answered.
  - It now reads *"It cannot be moved to the {recycle bin|trash}, so the file would be deleted
    outright. This cannot be undone."*, and both permanent prompts name the bin as the platform
    does, like the new ones.
- **The permanent prompts were refactored onto the shared helpers.** Their titles, buttons and
  guards are unchanged.
- **Manual check not performed here.** No test can show that the real dialog opens on the key and
  the menu, takes focus and closes on `Escape`, because nothing in the suite builds `MainWindow`.
  That is the new §1 item in `docs/RELEASE.md`.
- **Line endings:** every touched file is LF in the index and the working tree; `git diff --check`
  is clean.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release -v normal`:
  - **0 Warning(s), 0 Error(s)**;
  - `grep -i AVLN` over the log: **no output**.
- The first build failed on one `CS8600` in the new test file (`string message =` a `string?`).
  Fixed in **one fix cycle** (of the budget of 3).
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3634 passed, 0 failed, 0
  skipped**.
  - That is 3619 (FEATURE-4B09-PHASE02's count) + 15 new: 12 in `FileOperationPromptsTests`
    (confirmed alone with `--filter-class`: 12 of 12) and 3 in `FileTreeViewModelTests`.
  - No existing assertion was weakened. The one that asserted *no* confirmation now asserts exactly
    one, naming the entry.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Share code between the four confirmations? | One `ConfirmAsync`, one `Listing`, one guard | Four near-identical dialog blocks would drift; the permanent prompts' behaviour is unchanged |
| Permanent single-entry wording | "It cannot be moved to the …" instead of "There is no trash on this system" | Correct both with no trash and with a declining trash, and no longer contradicts the trash question it can follow |
| Fake dialog host: keep or apply the delegates? | Apply them to a real `ContentDialog` under `[AvaloniaFact]` | The wording is what these tests are about; `FakeInfoBarService` keeps its delegates because its tests are about *whether* a bar was shown |
| Fake default for the trash answer | `ConfirmsMoveToTrash = true` | Existing tests describe what a delete *does*; a user who confirmed is the precondition, and refusal gets tests of its own |
| Where the new manual check goes | `docs/RELEASE.md` §1, plus one clause in §2's tree step | Every release since 1.4.1 turns what an item leaves unverified into a standing check; the trash's name differs per platform, so Linux gets its own look |
