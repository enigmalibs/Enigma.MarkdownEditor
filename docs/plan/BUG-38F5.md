# BUG-38F5 — Save leaves the dirty dot in vim

**Status: DONE** · Single phase · Roadmap: `docs/roadmap.md`
Planned on `main`; branches from `HEAD` at build time as `bugfix/bug-38f5-save-dirty-dot`.

## Objective

A save made while vim is in insert mode — `Ctrl+S`, an autosave that fires mid-session, or `:w` from
the normal mode that follows — writes the file but leaves the tab's dot showing, and closing the tab
then asks about "unsaved changes" that are already on disk. Make the dot and the close prompt tell the
truth after every save, in every vim mode, without changing what vim's `u` undoes.

Reported against **1.4.0** as "Ctrl+S is not working in vim modes other than normal, sometimes even in
normal mode; autosave works but the dot stays and closing the tab asks whether to save". The user
judged "not working" by the dot and the prompt, not by the file — and the code confirms the file is
written every time. This is one defect in the dirty flag, not two.

## Symptom → root cause

Established by reading the code during the interview and checked against AvaloniaEdit 12.0.0's
`UndoStack` source. The build still opens with **failing tests that reproduce it** before anything is
changed.

**The save itself is fine.** `MainWindow.axaml:168` binds `Ctrl+S` to `Documents.SaveCommand`;
`VimEngine.Dispatch` (`VimEngine.cs:329`) returns `false` for every `Ctrl`/`Alt` key it does not name,
in normal, visual and command-line mode alike, and insert mode returns `false` for everything but
`Esc` (`VimEngine.cs:305`). `DocumentManagerViewModel.CanSave` (`:663`) only requires an open
document. `:w` goes through the same `SaveAsync` (`:535`). So in every mode `WriteAsync` (`:705`) runs
and the bytes land.

**The flag is what is wrong.** `DocumentViewModel.MarkSaved` (`DocumentViewModel.cs:256`) never sets
`IsDirty` itself: it calls `Document.UndoStack.MarkAsOriginalFile()` and relies on AvaloniaEdit raising
`PropertyChanged("IsOriginalFile")`, which `OnUndoStackPropertyChanged` (`:438`) turns into
`IsDirty = false`. Two facts break that chain under vim:

1. **Vim's insert session holds one undo group open** for the whole session (`VimEngine.BeginInsertGroup`
   → `AvaloniaEditVimBuffer.BeginUndoGroup`, closed on `Esc` in `LeaveInsert`). While the depth is
   above zero, AvaloniaEdit's `Push` increments its `_elementsOnUndoUntilOriginalFile` counter but
   **defers `RecalcIsOriginalFile()` until the group closes**. So `IsOriginalFile` still reads the
   `true` it held at the last save. `MarkAsOriginalFile()` resets the counter to `0`, recalculates,
   finds no change — and **raises nothing**. `IsDirty` — set `true` by `OnTextChanged` (`:446`), the
   BUG-7AB2-PHASE02 fix — stays `true`. Dot stays, close prompt asks.
2. **Closing the group over a save point mis-reports.** `EndUndoGroup` collapses an `n`-operation group
   into one entry and calls `FileModified(1 - n)`. After a mid-group `MarkAsOriginalFile()` the counter
   is `0`, so it becomes `1 - n < 0` for `n ≥ 2`, and `IsOriginalFile` flips to **false** although the
   text equals the disk. For `n = 1` nothing flips at all, so the stack goes on believing the file is
   original while `IsDirty` is `true` — and the *next* `Ctrl+S` in **normal** mode marks an
   already-"original" stack, raises nothing, and leaves the dot. That is the "sometimes even in normal
   mode".

Both paths end the same way: `IsDirty` disagrees with the disk and nothing corrects it until an edit
happens to flip `IsOriginalFile` false and a further save flips it true again.

**A third hole, found in the sweep and fixed here by decision.** `WriteAsync` reads
`document.Document.Text` (`:713`), awaits the disk write, then calls `MarkSaved`. A keystroke landing
in that await — an autosave firing as the user resumes typing after the 2 s idle — is followed by a
`MarkSaved` that clears the flag over a buffer that differs from what was written.

## Context & constraints

- **Fix lives in `Enigma.MarkdownEditor.Desktop/ViewModels` only.** `VimEngine`, `IVimBuffer`,
  `AvaloniaEditVimBuffer`, `VimEditorBehavior` and every view are untouched. **One `u` still undoes a
  whole insert session** — the `FEATURE-11AA` and `FEATURE-724A` phases rest on that group, and
  BUG-7AB2-PHASE02 already chose to keep it. Decided in the interview over the alternative of making
  every save an undo boundary.
- **`Core` never references Avalonia**; nothing here touches `Core`.
- **`TreatWarningsAsErrors` is on solution-wide.** 0-warning build. No XAML is touched, so no `AVLN`
  sweep is needed beyond the normal build.
- **`vim` is on by default** (`AppSettings.VimEnabled = true`); manual verification happens under it.
- **The ViewModel-level tests can model the insert session** with `UndoStack.StartUndoGroup()` /
  `EndUndoGroup()` — `DocumentManagerViewModelTests.cs:112–160` already do exactly that. The
  real-control harness `tests/…Desktop.UnitTests/Views/TabStrip.cs` (`Type`, `Press`, `Mode`,
  `Prompts`, `Dot`) drives the actual engine and is extended, not duplicated. Its window is **not**
  `MainWindow`, so the `Ctrl+S` `KeyBinding` is not under test there; the save is invoked through
  `SaveCommand.ExecuteAsync(null)`, which is precisely what that binding calls.
- **Autosave in tests** fires through `FakeIdleScheduler.FireAsync(document)`.
- **The known limitation from BUG-7AB2-PHASE02 stays and is not this item's to remove:** while an
  insert session is open, editing back to the saved text cannot clear the dot until the group closes.
- **No dependency moves.**

## Design

### 1 — `DocumentViewModel` (`src/Enigma.MarkdownEditor.Desktop/ViewModels/DocumentViewModel.cs`)

New private state: `bool _editedSinceSave` — "the buffer has changed at least once since the last
`MarkSaved` / `ApplyReload`". It is the one fact AvaloniaEdit cannot tell us while a group is open.

- **`OnTextChanged`** (`:446`) — alongside the existing `IsDirty = true` (still skipped while
  `_replacing`), set `_editedSinceSave = true` unconditionally. Keep the existing comment about the
  asymmetry; extend it with why the flag exists.
- **`MarkSaved(string filePath, FileStamp stamp, ITextSourceVersion written)`** (`:256`) — a third,
  **required** parameter: the `Document.Version` captured when the text that was written was read.
  After the stamp/path/watcher bookkeeping already there:
  - if `written.CompareAge(Document.Version) == 0` — nothing changed since the snapshot:
    `Document.UndoStack.MarkAsOriginalFile(); IsDirty = false; _editedSinceSave = false;`.
    Stating `IsDirty = false` is the fix for cause 1; the same "said rather than inferred" rule
    `ApplyReload` already follows at `:337`.
  - else — the buffer moved while the write was in flight: **leave `IsDirty` and the undo marker
    alone**. The document is still dirty, `_editedSinceSave` is still `true`, and the autosave that
    the in-flight keystroke re-armed (via `OnDocumentTextChanged`) writes it again. Document this
    branch: it is the third hole.
  Replace the existing "Not IsDirty = false" comment (`:279–280`) — it describes the behaviour this
  item removes.
- **`OnUndoStackPropertyChanged`** (`:438`) — when `IsOriginalFile` becomes **false** and
  `!_editedSinceSave`, this is cause 2: a group closing over a save point, with no edit since that
  save, so the text still equals the disk. Answer by calling `Document.UndoStack.MarkAsOriginalFile()`
  again. That puts the counter back at `0` — the top of the stack *is* the saved state now — and raises
  the `true` notification, which re-enters this handler and clears `IsDirty`. In every other case keep
  today's `IsDirty = !Document.UndoStack.IsOriginalFile`. Re-entrancy is safe: AvaloniaEdit raises the
  event as the last statement of `EndUndoGroup`, and the nested call toggles the flag once.
  Why this is exact: `IsOriginalFile` can only turn false through `FileModified`, which is reached
  from `Push`, `Undo`, `Redo` and `EndUndoGroup`. The first three always change the text first, so
  `_editedSinceSave` is `true` and the normal path runs; only the group close can arrive with the flag
  clear, and then the text is provably what was saved.
- **`ApplyReload`** (`:295`) — reset `_editedSinceSave = false` next to the existing `IsDirty = false`.
- Class-level `<remarks>`: update the paragraph about the flag's two sources to name the three rules
  (an edit raises it; a save with an unchanged buffer clears it; the stack clears it on undo back to
  the saved state, with the group-close correction).

### 2 — `DocumentManagerViewModel` (`…/ViewModels/DocumentManagerViewModel.cs`)

- **`WriteAsync`** (`:705`) — capture `ITextSourceVersion written = document.Document.Version;`
  on the same line as the text snapshot (`:713`), and pass it to `MarkSaved` (`:717`). Comment: the
  version is what lets `MarkSaved` tell a buffer that sat still from one that moved during the write.
- Nothing else changes. `SaveAllAsync` (`:694`) already filters on `HasUnsavedChanges`, so it stops
  rewriting clean documents as a consequence of the flag being right.

### 3 — Test call site

- `tests/…Desktop.UnitTests/ViewModels/MarkdownPreviewViewModelTests.cs:217` passes the document's
  current `Document.Version` to the new `MarkSaved` signature.

### Out of scope

Making a save an undo boundary · any change to the vim engine, buffer or behaviour · a text-hash
comparison against the saved content · the BUG-7AB2 known limitation (editing back to the saved text
inside an open group) · release notes and version (→ `FEATURE-6A2F`).

## Tests

**Reproduce first.** Each test below marked *(repro)* must fail against the unfixed code and pass
against the fix; record both runs in the completion doc.

**`ViewModels/DocumentManagerViewModelTests.cs`** — same `EditorHarness` + `UiThread.RunAsync` shape
as the existing undo-group tests:

1. *(repro)* `SavingInsideAnOpenUndoGroup_ClearsTheDirtyFlag` — `StartUndoGroup`, insert, `SaveCommand`
   → `IsDirty` false and `HasUnsavedChanges` false while the group is still open.
2. *(repro)* `ClosingTheGroupAfterASaveInsideIt_KeepsTheDocumentClean` — as 1 with **two** inserts
   before the save (so the collapse path `n ≥ 2` runs), then `EndUndoGroup` → still clean; then
   `CloseCommand` → `Prompts.AskedAbout` empty and the document gone.
3. *(repro)* `ASingleEditSavedInsideItsGroup_ThenSavedAgainInNormalMode_IsClean` — one insert, save
   inside the group, `EndUndoGroup` (the `n = 1`, no-flip path), one more insert outside any group,
   save → clean. This is the "sometimes even in normal mode" case.
4. `TypingAfterASaveInsideTheGroup_StaysDirtyWhenTheGroupCloses` — insert, save, insert again,
   `EndUndoGroup` → `IsDirty` true; save → false.
5. `UndoingAnInsertSessionSavedMidWay_MarksDirtyAndRedoMarksClean` — two inserts, save, `EndUndoGroup`,
   `UndoStack.Undo()` → dirty; `Redo()` → clean.
6. *(repro)* `AutosaveFiringInInsertMode_ClearsTheDotAndCloseDoesNotAsk` — `AutosaveEnabled`,
   `StartUndoGroup`, insert, `Idle.FireAsync(document)` → clean; `EndUndoGroup` → clean; close → no
   prompt, file content on disk equals the buffer.
7. *(repro)* `AnEditDuringTheWrite_LeavesTheDocumentDirty` — the third hole, tested at the
   `DocumentViewModel` seam because nothing in the harness can pause the disk write: capture
   `Document.Version`, insert text, call `MarkSaved(path, stamp, staleVersion)` → `IsDirty` stays
   true; then `MarkSaved` with the current version → false.
8. `SaveAll_SkipsDocumentsThatAreClean` — two documents, one edited, `SaveAllCommand` → only the dirty
   one's stamp changes.

**`Views/EditorTabStripTests.cs`** — through the real control, vim on:

9. *(repro)* `SavingInInsertMode_HidesTheDotAndEscDoesNotBringItBack` — `FocusEditor`, `Type("i")`,
   `Type("xy")`, dot visible; `await Documents.SaveCommand.ExecuteAsync(null)`; settle → dot hidden,
   `Mode` still `Insert`; `Press(PhysicalKey.Escape)` → `Mode` `Normal`, dot **still hidden**;
   `PointerOver(0)` + `CloseAsync(0)` → `Prompts.AskedAbout` empty, tab gone. (`TabStrip` may need a
   small accessor for the harness's `Documents` if it does not expose one already.)

Existing tests that must stay green unchanged: `EditingADocument_MarksItDirtyAndUndoingBackToTheSavedStateClearsIt`,
the three BUG-7AB2-PHASE02 tests, the autosave set, the reload/external-change set, and
`WithAutosaveOn_ATickIsWrittenByThePathEveryOtherEditGoesThrough`.

## Manual verification (recorded in the completion doc)

Vim on. In a workspace file: `i`, type a sentence, `Ctrl+S` → dot disappears at once; `Esc` → dot
stays off; close the tab → no prompt. Repeat with autosave on: type, wait 2 s → dot off; keep typing
→ dot on; wait → off; `Esc` → off. Repeat with `:w` from normal mode after an insert session. Check
the file on disk after each save.

## Acceptance criteria

- The nine tests above exist; the *(repro)* ones were seen failing before the fix.
- After a save in insert mode the tab's dot is hidden and stays hidden across `Esc`, and closing the
  tab does not ask — under `Ctrl+S`, `:w` and autosave alike.
- A save in normal mode after an insert session that was saved mid-way clears the dot.
- An edit that lands while a write is in flight leaves the document dirty and re-armed for autosave.
- `u` after an insert session still undoes the whole session; nothing under `Core/Vim`,
  `AvaloniaEditVimBuffer` or `VimEditorBehavior` changes.
- `dotnet build Enigma.MarkdownEditor.slnx`: 0 warnings. `dotnet test --solution Enigma.MarkdownEditor.slnx`:
  all green.
- Completion doc `docs/done/BUG-38F5.md` written, roadmap and this file flipped to `DONE`.

## Follow-ups (not this item)

- The BUG-7AB2 known limitation (editing back to the saved text inside an open insert group cannot
  clear the dot until `Esc`) remains; a fix would need the save point inside the group, which
  AvaloniaEdit's `UndoStack` cannot express.
- `Ctrl+Z` inside vim insert mode reaches AvaloniaEdit while the insert group is open; AvaloniaEdit
  throws on `Undo()` with a group open. Not observed by the user and not verified here — worth a look
  in a later vim item.
