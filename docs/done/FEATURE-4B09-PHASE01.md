# FEATURE-4B09-PHASE01 — Multi-selection in the tree

**Status: DONE** · Phase 01 of 2 · Branch: `feature/feature-4b09-phase01-multi-selection`
Plan: `docs/plan/FEATURE-4B09.md`

## Summary

The file tree now selects several rows: Ctrl+click adds or removes one, Shift+click (and
Shift+arrows) selects a range. Every existing operation stays correct over a selection of several,
which in this phase means standing down where "this one" has no meaning. Deleting the whole
selection is PHASE02.

- **One selection, not two.** `FileTreeViewModel.SelectedNodes` is the tree's selection list itself:
  the view binds `TreeView.SelectedItems` to it and nothing else, and the control adds and removes in
  place. `SelectedNode` is now the primary entry — the first one selected — and setting it replaces the
  whole selection, which is exactly what each of its callers meant already (a reveal after create or
  rename, a refresh, a right-click outside the selection). Binding `SelectedItem` as well would have
  meant two properties over one selection, kept in step by whoever wrote last.
- **What stands down with more than one row selected:** Rename, Delete (until PHASE02) and Colour.
  New file / New folder still work, next to the primary entry.
- **Right-click keeps a selection it lands in.** On a row outside the selection it selects that row
  alone, as before. Without this the context menu could never act on more than one row.
- **A selection never outlives its rows.** When a re-listing removes a row, or replaces a folder's rows
  with its *Nothing here* / *No access* row, the removed nodes and everything loaded under them leave
  the selection, by identity. A selected file deleted outside the editor, or the folder it was in,
  no longer lingers as something the next Delete would act on.

## Files/modules touched

**Modified — `src/Enigma.MarkdownEditor.Desktop`**

- `ViewModels/FileTreeViewModel.cs` — `SelectedNodes`; `SelectedNode` derived from it, with a
  replacing setter; `IsSingleSelection` behind `CanActOnSelection`, `ColorTarget` and the
  `SetColorAsync` guard; `OnSelectionChanged` re-raising what depends on the selection; `Deselect`
  walking a removed node's loaded subtree, called from both removal paths of `Merge`; remarks updated
  for the primary entry and for the several-rows case.
- `Views/FileTreeView.axaml` — `SelectionMode="Multiple"`, `SelectedItems="{Binding SelectedNodes}"`,
  the `SelectedItem` binding removed, and a comment saying why it is one binding.
- `Views/FileTreeView.axaml.cs` — `OnNodeContextRequested` leaves a selection containing the clicked
  row alone.

**Modified — `tests/Enigma.MarkdownEditor.Desktop.UnitTests`**

- `ViewModels/FileTreeViewModelTests.cs` — nine tests: setting `SelectedNode` replaces the set and
  `null` clears it; the primary entry is the first selected; with two selected, Rename and Colour stand
  down and their guards hold when executed anyway; Delete waits for a single selection; New file goes
  next to the primary entry; a selected file deleted outside leaves the selection; a folder deleted
  outside takes a selected row inside it out of the selection although only the root was re-read; a
  folder emptied outside does the same through the *Nothing here* path; a refresh clears the
  selection. A `Child(tree, name)` helper.

**Created — `tests/Enigma.MarkdownEditor.Desktop.UnitTests`**

- `Views/FileTreeSelectionTests.cs` — six headless tests over the real `FileTreeView` and real pointer
  input: the tree is `Multiple`; the control's `SelectedItems` *is* the ViewModel's collection, both
  ways; Ctrl+click adds a row and a plain click starts over; Shift+click selects the range between;
  right-clicking inside a two-row selection keeps both; right-clicking outside it selects that row
  alone.

## Decisions taken at build time

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| How the view tests make a selection | Real `MouseDown`/`MouseUp` through the headless platform, over the row's name | Exercises the control's own selection rules and the right-click path end to end; raising `ContextRequested` by hand would skip the control's own right-press selection, which is half of what the test is about | Writing into `SelectedItems` directly (proves the binding, not the gestures) |
| Delete with several selected, in this phase | Disabled, guard included | The plan's interim state: a Delete that removed only the primary row would be a surprise, and PHASE02 lifts it | Deleting the primary row only |
| `FileTreeNodeViewModel.IsSelected` | Left untouched | Found during the build: it is declared, never bound and never read — but deleting it is out of this phase's scope | Binding `TreeViewItem.IsSelected` to it as the selection mechanism (two sources of truth again) |

## Deviations & follow-ups

- **One addition to the plan's tests:** `WithSeveralSelected_DeleteWaitsForASingleSelection`, which
  pins the interim Delete behaviour that PHASE02 replaces.
- **Mutation-checked:** with the right-click handler put back to always selecting the clicked row,
  `RightClickingInsideTheSelection_KeepsIt` fails; with this phase's handler it passes.
- **Follow-up:** `FileTreeNodeViewModel.IsSelected` is dead code (declared, never bound or read).
- **Follow-up, unchanged by design:** a drag still carries the row it started on, not the selection.
- **Line endings:** nothing to recommend; LF throughout, no churn.
- **Documentation freshness sweep:** nothing edited. The README's file-tree bullet and the `F2` /
  `Delete` shortcut row are still accurate for this phase; the multi-delete clause is a planned step
  of PHASE02, which is what makes the selection useful to a reader.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release` — **0 warnings, 0 errors**; no `AVLN` line (the
  new `SelectedItems` binding compiled as a compiled binding without a diagnostic).
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` — **3601 passed, 0 failed, 0 skipped**
  (3586 before; 15 added). Green on the first run; no fix cycle used.
- Acceptance criteria: Ctrl/Shift+click select several rows into `SelectedNodes` (view tests); Rename,
  Delete and Colour disabled with several selected while New file works; right-click inside keeps the
  selection and outside replaces it; vanished rows leave `SelectedNodes`; every pre-existing
  `FileTreeViewModelTests` / `FileTreeColourMenuTests` test passes unchanged.
