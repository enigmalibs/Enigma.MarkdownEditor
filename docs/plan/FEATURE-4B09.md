# FEATURE-4B09 — File tree multi-selection

**Status: TODO** · 2 phases · Roadmap: `docs/roadmap.md`
**Type:** FEATURE · **Branch:** one per phase, cut from the run branch at build time.
**Run:** feature/2026-10-14-infobars-tree-multi-select

## Objective

Let the user select several entries in the workspace file tree — Ctrl+click to add or remove one,
Shift+click for a range — and delete them all at once. PHASE01 makes the tree's selection a set and
keeps every existing operation correct over it; PHASE02 makes Delete act on the whole set.

## Context & constraints

Established by reading the code and the Avalonia 12.1.2 API, not assumed.

- **The tree binds one item today**: `FileTreeView.axaml` has `SelectedItem="{Binding SelectedNode}"`
  on the `TreeView`. Everything the context menu and the keys do reads `FileTreeViewModel.SelectedNode`.
- **Avalonia 12's `TreeView`** has `SelectionMode` (`Single` / `Multiple` / `Toggle` / `AlwaysSelected`)
  and a settable `SelectedItems : IList`, which it subscribes to (`SubscribeToSelectedItems`) and keeps
  in step with `SelectedItem` (the first selected item). With `Multiple`, Ctrl+click toggles an item
  and Shift+click / Shift+arrows select a range — the platform gestures, at no cost.
- **Operations and what they act on** (`FileTreeViewModel`):
  - `NewFileCommand` / `NewFolderCommand` — `TargetDirectory`, derived from `SelectedNode`.
  - `RenameCommand` / `DeleteCommand` — `CanActOnSelection`: a non-root file or directory.
  - `SetColorCommand` — `ColorTarget`: the selected directory, roots included.
  - Drag & drop — the pressed row only (`FileTreeView.OnPointerPressed`), not the selection.
- **The right-click handler** (`FileTreeView.OnNodeContextRequested`) sets `SelectedNode = node` so the
  menu acts on the clicked row. Unchanged, it would collapse a multi-selection on every right-click.
- **Nodes leave the tree** in `Merge` (removed children, and `Children.Clear()` when a folder becomes
  empty, unreadable or missing) and in `RefreshAsync` (`Roots.Clear()`). Nothing prunes the selection
  today; a stale `SelectedNode` is harmless for one item but a stale *set* would be acted on.
- **The delete flow** (`DeleteAsync`): trash it if the platform has one; if the trash is absent or
  declines (`false` means "untouched", `ITrashService` remarks), ask
  `IFileOperationPrompts.ConfirmPermanentDeleteAsync(name, isDirectory)` and delete outright; then
  re-read the parent. Failures surface through `RunAsync` → `ReportAsync` (Warning for a
  `FileOperationException`, Error otherwise). FEATURE-425B removes the Info bar after a trash.
- **Decisions live in Core** (`Enigma.MarkdownEditor.Core.Files` — `FileMovePlanner`, `FileNameValidation`,
  `UniqueName`…); the ViewModel is glue. Path equality is `WorkspaceValidation.PathComparer`
  (case-insensitive on Windows).
- **Tests**: `FileTreeViewModelTests` drives the ViewModel over real temp directories with
  `FakeTrashService`, `FakeFileOperationPrompts`, `FakeInfoBarService` and a fake watcher; view tests
  (`FileTreeColourMenuTests`) show a real `FileTreeView` in a headless `Window` under `[AvaloniaFact]`.
  Core tests live in `tests/…Core.UnitTests/Files`. No `InternalsVisibleTo`.
- Build bar: Release, 0 warnings, no `AVLN`; `dotnet test --solution Enigma.MarkdownEditor.slnx`.

## Out of scope

- Moving several entries by drag and drop — a drag still carries the row it started on.
- Colouring or renaming several folders at once.
- Opening several files at once with Enter.
- A "select all" command or shortcut in the tree.

---

## PHASE01 — Multi-selection in the tree

**Status: TODO** · Branch: `feature/feature-4b09-phase01-multi-selection`

### Steps

1. **ViewModel — the selection becomes a collection.** `FileTreeViewModel.SelectedNodes`:
   `ObservableCollection<FileTreeNodeViewModel>`, get-only, the single source of truth.
   - `SelectedNode` becomes the *primary* entry — `SelectedNodes[0]`, or `null` when empty. Its setter
     replaces the whole selection with that one node (or clears it), and is a no-op when the selection
     is already exactly that node. Every existing caller (`RevealAsync`, `RefreshAsync`, the
     right-click handler, the tests) keeps its meaning.
   - One `CollectionChanged` handler raises `SelectedNode`, `CanActOnSelection`, `CanColorSelection`
     and re-asks every command (`NotifyOperationsChanged`).
2. **Commands over a set.** Rename, Delete and Colour require **exactly one** selected entry in this
   phase (Delete is lifted to the whole set in PHASE02). New file / New folder keep creating next to
   the primary entry — the folder it is in, or the folder it is.
3. **Prune what leaves the tree.** When `Merge` removes a child, or clears a folder's children, the
   removed nodes *and their loaded descendants* are taken out of `SelectedNodes`, by node identity
   (a path that flipped between file and directory is a different node).
4. **View.** `FileTreeView.axaml`: `SelectionMode="Multiple"`, `SelectedItems="{Binding SelectedNodes}"`,
   and the `SelectedItem` binding removed — one binding, one source of truth; the control derives its
   own `SelectedItem` from the list. Update the menu comment that says the menu acts on "the selection".
5. **Right-click.** `OnNodeContextRequested` keeps the selection when the clicked row is already part
   of it, and otherwise selects that row alone — what Explorer and every editor tree do.
6. **Tests.**
   - ViewModel: setting `SelectedNode` replaces the set and `null` clears it; two entries selected →
     Rename, Delete and Colour disabled while New file still targets the primary entry's folder; a
     selected file deleted outside and re-listed leaves the selection; a selected folder's child
     leaves the selection when the folder is emptied; a refresh clears the selection.
   - View (headless, new `Views/FileTreeSelectionTests.cs`): the tree is `Multiple`; the control's
     `SelectedItems` is the ViewModel's collection (same instance), so an item the control adds is seen
     by the ViewModel and `SelectedNode` set by the ViewModel is the control's `SelectedItem`; a right-click
     on a row inside a two-row selection keeps both, and one outside replaces them.

### Acceptance criteria

- Ctrl+click and Shift+click select several rows; the ViewModel's `SelectedNodes` holds exactly them.
- With more than one entry selected, Rename, Delete and Colour are disabled; New file / New folder work.
- Right-clicking inside the selection keeps it; outside it, selects the clicked row alone.
- A selected entry that disappears from the tree disappears from `SelectedNodes`.
- Every existing `FileTreeViewModelTests` / `FileTreeColourMenuTests` test passes unchanged.
- Release build: 0 warnings, 0 errors, no `AVLN`. Whole suite green.

---

## PHASE02 — Delete the whole selection

**Status: TODO** · Branch: `feature/feature-4b09-phase02-multi-delete`

### Steps

1. **Core — `FileSelection.Outermost(IEnumerable<string> paths)`** in `Enigma.MarkdownEditor.Core.Files`:
   the given paths with duplicates removed and every path that sits under another one in the set
   dropped, in input order. Compared with `WorkspaceValidation.PathComparer`, separator-aware so that
   `/notes-old` is not under `/notes`. Reuse an existing Core "is under" helper if one exists.
2. **What Delete acts on.** The deletable entries of the selection — non-root files and directories;
   informational rows ignored — reduced by `Outermost`. `DeleteCommand` is enabled when there is at
   least one, **and no workspace root is selected**: a root is never deleted from the tree, and a
   selection that includes one is refused as a whole rather than half-applied.
3. **The delete flow, for N entries.**
   1. Each entry goes to the trash, in order, when the platform has one; entries the trash declines,
      or all of them when there is none, are kept aside.
   2. If any were kept aside, ask **once**: one entry → the existing
      `ConfirmPermanentDeleteAsync(name, isDirectory)`; several → a new
      `ConfirmPermanentDeleteAsync(IReadOnlyList<string> names)`. Confirmed → each is deleted
      outright; refused → they stay exactly where they are, and what was trashed stays trashed.
   3. Each affected folder is re-read once.
   4. A failure stops the batch there and is reported once through the existing `ReportAsync` path;
      what was already trashed stays trashed, and the watcher re-reads the folders.
4. **The new prompt.** `IFileOperationPrompts.ConfirmPermanentDeleteAsync(IReadOnlyList<string> names)`
   and its `FileOperationPrompts` implementation: title "Delete N items permanently?", a message that
   lists the first five names and "…and N more", says they cannot go to the trash, that folders go
   with everything in them, and that it cannot be undone; buttons "Delete permanently" / "Cancel". The
   fake records each call's names.
5. **README** — *Workspaces and the file tree*: one clause in the create/rename/move/delete bullet
   saying several entries can be selected with Ctrl/Shift+click and deleted at once.
6. **Tests.**
   - Core: `FileSelectionTests` — duplicates; a child under a selected folder dropped, deep and direct;
     a sibling sharing a prefix kept; order preserved; case-insensitive on Windows; argument guards.
   - ViewModel: three selected files → all trashed, no prompt, no InfoBar; a folder and a file inside it
     → the trash is asked once, for the folder; no trash → one confirmation naming every entry, then all
     deleted; refusing it → nothing deleted; a trash that declines one of three → two trashed and the
     single-entry confirmation for the third; a root in the selection → Delete disabled and the guard
     holds when executed anyway; an informational row in the selection is ignored; a trash that breaks
     on the second entry → one bar, the first entry trashed, the third untouched; the selection is empty
     afterwards.

### Acceptance criteria

- Selecting several entries and pressing Delete (or the menu item) sends all of them to the trash,
  silently, and takes their rows with them.
- Without a usable trash, one confirmation covers the whole selection.
- Nothing inside a selected folder is deleted twice or reported as a failure.
- A selection containing a workspace root cannot be deleted.
- A failure is reported once and stops the batch.
- README mentions multi-selection delete.
- Release build: 0 warnings, 0 errors, no `AVLN`. Whole suite green.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Item type and shape | One `FEATURE`, two phases | The selection model and the batch delete are each one reviewable commit, and the first is useful (and safe) on its own | One commit across Core, prompts, ViewModel, view and four test files |
| Gesture | `SelectionMode="Multiple"`: Ctrl+click, Shift+click, Shift+arrows | The platform convention, built into the control | `Toggle` mode (a plain click would add to the selection — surprising in a file tree); checkboxes per row |
| Source of truth | `SelectedNodes` alone; `SelectedNode` derived as its first entry, its setter replacing the set | Two bound properties over one selection invite a sync loop between control and ViewModel; the existing callers of `SelectedNode` keep their meaning | Binding both `SelectedItem` and `SelectedItems` |
| Which commands go multi | Delete only (PHASE02); Rename and Colour need one entry; New file/folder use the primary entry | The ask is "remove multiple files at once"; renaming several at once has no single meaning; colouring several is not asked | Multi-colour; multi-drag; disabling New file/folder |
| A workspace root in the selection | Delete disabled | Roots are never deleted from the tree; refusing the whole command is honest, silently skipping the root is not | Delete the rest and skip the root |
| Informational rows in a Shift-range | Ignored | They are not entries; a range across an expanded empty folder would otherwise disable Delete | Disabling Delete |
| A folder and something inside it both selected | Delete the outermost only, via a Core helper | Deleting the child after its folder would fail and be reported as an error | Deleting in order and swallowing "not found" |
| Confirmation | None for the trash; one for everything the trash would not take | Matches the single-entry rule (only an irreversible delete asks), without one dialog per file | A confirmation for every multi-delete; one dialog per declined entry |
| Failure mid-batch | Stop, report once, keep what was trashed | One bar per failure would bury the user; what was trashed is recoverable | Continue and aggregate every failure |
| Right-click inside a selection | Keep the selection | Explorer and editor-tree convention; otherwise the context menu could never act on a set | Always select the clicked row |
| Stale entries after a re-list | Pruned from `SelectedNodes` by node identity | A set acted on after its rows vanished would act on things the user can no longer see | Leaving it to the control (not documented to prune nested items) |
| README | One clause in the existing file-tree bullet, as a planned step | Precedent `FEATURE-E64A` / `FEATURE-A225`: user-visible tree features are named there | Leaving it for the release item |
