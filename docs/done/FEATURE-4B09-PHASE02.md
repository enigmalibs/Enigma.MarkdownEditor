# FEATURE-4B09-PHASE02 — Delete the whole selection

**Status: DONE** · Phase 02 of 2 · Branch: `feature/feature-4b09-phase02-multi-delete`
Plan: `docs/plan/FEATURE-4B09.md`

## Summary

Selecting several entries in the tree and pressing `Delete` (or the menu item) now deletes all of
them, under the same rules a single delete has always followed.

- **Everything goes to the trash first, silently.** Entries are trashed one by one, in the order they
  were selected, with no confirmation, because all of it can be restored. Their rows leave the tree,
  and so the selection empties.
- **One question for whatever the trash would not take.** When there is no trash, or it declines some
  entries (a different device, typically), the user is asked once for all of them. One entry gets the
  existing single-entry dialog. Several get a new one that names the first five and says how many more
  there are. Refusing leaves every one of them where it was, and whatever was already trashed stays
  trashed.
- **Nothing is deleted twice.** A folder and something inside it both selected are reduced to the
  folder before anything happens, by the new Core helper `FileSelection.Outermost`, so the file is not
  deleted a second time and then reported as missing.
- **A root in the selection refuses the whole command.** A root is never deleted from the tree, so a
  selection that includes one is refused rather than half-applied. Informational rows (*Nothing here*,
  which a Shift+click range across an empty folder picks up) are not entries, so they are ignored
  rather than disabling Delete.
- **A failure stops the batch and is reported once**, through the existing Warning/Error path.
  Whatever was trashed before the failure stays trashed, and the entries after it are untouched.
- **Each affected folder is re-read once**, however many of its entries went.

## Files/modules touched

**Created**

- `src/Enigma.MarkdownEditor.Core/Files/FileSelection.cs` — `Outermost(IEnumerable<string>)`:
  duplicates removed (trailing separator ignored, first spelling kept), every path beneath another path
  of the set dropped, input order kept. It walks the folder chain with `WorkspaceValidation.PathComparer`,
  like `FileMovePlanner.IsSameOrBeneath`, so a shared prefix is never mistaken for containment. It
  never reads the disk.
- `tests/Enigma.MarkdownEditor.Core.UnitTests/Files/FileSelectionTests.cs` — 11 cases: empty input;
  separate entries kept in order; duplicates; a direct and a deep child dropped (child listed first);
  a prefix-sharing sibling kept; case folded only on Windows; argument guards.

**Modified — `src/Enigma.MarkdownEditor.Desktop`**

- `ViewModels/FileTreeViewModel.cs` — `DeleteAsync` rewritten for a set (trash each → one
  confirmation for the rest → delete those outright → re-read each touched folder once);
  `DeletionTargets`, `ConfirmPermanentDeleteAsync(List<…>)` choosing the single- or multi-entry
  question, `Touch`; `DeleteCommand` now enabled by the new private `CanDeleteSelection`;
  `CanActOnSelection` is documented as the rename condition it now is.
- `Services/IFileOperationPrompts.cs` — `ConfirmPermanentDeleteAsync(IReadOnlyList<string> names)`.
- `Services/FileOperationPrompts.cs` — its implementation. The title is "Delete N items
  permanently?", or the single-name title for one. The message is followed by the first five names
  and "…and N more.". Buttons are *Delete permanently* / *Cancel*. It throws `ArgumentNullException`
  / `ArgumentException` on a null, empty or nameless list.

**Modified — tests**

- `Fakes/FakeFileOperationPrompts.cs` — records what every permanent-delete question named
  (`AskedToDeletePermanently`). `PermanentDeletesAsked` is now derived from that list, so the
  existing tests read it unchanged.
- `Fakes/FakeTrashService.cs` — `Declines` and `BreaksOn`, by entry name, for a trash that declines
  or breaks on one entry of a selection.
- `ViewModels/FileTreeViewModelTests.cs` — PHASE01's interim `WithSeveralSelected_DeleteWaitsForASingleSelection`
  replaced by eight tests:
  - two selected → both trashed in selection order, no prompt, no bar, selection empty;
  - a folder plus a file inside it → the trash is given the folder once;
  - no trash → one confirmation naming both, then both deleted;
  - refusing that confirmation → both kept;
  - a trash declining one of three → two trashed, and the single-entry question for the third;
  - a root in the selection → disabled, and the guard holds when the command runs anyway;
  - an informational row in the selection → ignored;
  - a trash breaking on the second of three → one bar, the first trashed, the rest untouched.

**Modified — docs**

- `README.md` — *Workspaces and the file tree*: one sentence on selecting several entries with
  `Ctrl`/`Shift`+click and deleting them at once (a planned step, plan §5).

## Decisions taken at build time

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Reuse an existing Core "is under" helper (plan step 1) | A private walk in `FileSelection`, modelled on `FileMovePlanner.IsSameOrBeneath` | Both existing helpers (`FileMovePlanner.IsSameOrBeneath`, `FileChangeCoalescer.IsWithin`) are private; making one shared is a refactor of two unrelated files | Promoting one of them to a public helper in this phase |
| Is `CanDeleteSelection` public? | Private | Nothing binds it — the menu item and the key both read `DeleteCommand.CanExecute`, which is what the tests assert too | A public property mirroring the command |
| Order the touched folders are re-read in | First-touched first, deduplicated with the path comparer | Deterministic; a `HashSet`'s order is an implementation detail | Iterating a `HashSet` |
| The multi-entry dialog's wording on a declined (rather than absent) trash | "These cannot be moved to the trash…" | True in both cases; the single-entry dialog's "There is no trash on this system" is not quite true for a decline, and was left as it is | Copying the single-entry sentence |
| PHASE01's interim test | Replaced, not weakened | It pinned "Delete waits for a single selection", which this phase replaces by design; the new tests pin the new behaviour | Keeping it with an inverted assertion |

## Deviations & follow-ups

- **Fix cycle 1 of 3 used, on a test.** `AnEntryDeepInsideASelectedFolder_IsDropped` listed its
  "unrelated" entry *inside* the selected folder, so `Outermost` correctly dropped it. The fix moved
  the entry outside the folder; the helper was right and was not changed.
- **Not unit-tested: the multi-entry dialog's text.** `FileOperationPrompts` needs a registered
  `ContentDialog` host, and none of its dialogs has a test (the seam is `IFileOperationPrompts`, faked in
  the ViewModel tests). The composition is one private method; verified by reading.
- **Follow-up:** the single-entry permanent-delete dialog says "There is no trash on this system" also
  when the trash exists but declined that entry — pre-existing wording, left alone.
- **Follow-up:** Core now has three private "is beneath" walks (`FileMovePlanner`, `FileChangeCoalescer`,
  `FileSelection`); worth one shared helper in a refactor of its own.
- **Follow-up, unchanged by design:** dragging moves the row the drag started on, not the selection.
- **Follow-up for the next release item:** a *New Features* line for multi-selection delete.
- **Line endings:** nothing to recommend; LF throughout, no churn.
- **Documentation freshness sweep:** the README sentence above is the plan's own step. Nothing else
  was made wrong: the `F2` / `Delete` shortcut row still reads "Rename / delete in the file tree".

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release` — **0 warnings, 0 errors**; no `AVLN` line.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` — **3619 passed, 0 failed, 0 skipped**
  (3601 before; +11 Core, +8 ViewModel, −1 replaced interim test).
- Acceptance criteria:
  - several entries are all trashed silently and their rows go;
  - without a usable trash, one confirmation covers the selection;
  - a folder and its contents are deleted once, with no failure bar;
  - a selection holding a root cannot be deleted;
  - a failure is reported once and stops the batch;
  - the README mentions multi-selection delete.
