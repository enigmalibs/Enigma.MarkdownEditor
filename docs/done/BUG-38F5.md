# BUG-38F5 — Save leaves the dirty dot in vim

**Status: DONE** · Single phase · Plan: `docs/plan/BUG-38F5.md` · Branch: `bugfix/bug-38f5-save-dirty-dot`

## Summary

A save made while vim held an insert session open wrote the file and left the tab's dot showing, so
closing the tab asked about "unsaved changes" that were already on disk. The save was never broken —
the dirty flag was. `DocumentViewModel.MarkSaved` cleared `IsDirty` only *inferentially*, by marking
the undo stack's save point and waiting for AvaloniaEdit to raise `IsOriginalFile`. AvaloniaEdit does
not recalculate that property while an undo group is open, and vim holds one open for the whole
insert session, so the notification never came.

Three holes are closed, all inside `Desktop/ViewModels`:

1. **The save is now stated, not inferred.** `MarkSaved` sets `IsDirty = false` itself after marking
   the save point, so an open undo group can no longer swallow the news.
2. **The group close is corrected.** `EndUndoGroup` collapses an `n`-operation group into one entry
   and reports `FileModified(1 - n)` against a counter the mid-group save already zeroed, so for
   `n ≥ 2` the stack calls the just-written text "modified"; for `n = 1` it reports nothing at all.
   `OnUndoStackPropertyChanged` now recognises the one report that can be wrong — `IsOriginalFile`
   going false with nothing typed since the last save — and answers it by marking the save point
   again, which both puts the stack's counter where the buffer actually is and raises the
   notification that clears the flag.
3. **A write no longer clears the flag over a buffer that moved under it.** `WriteAsync` captures
   `Document.Version` with the text snapshot and hands it to `MarkSaved`, which leaves the document
   dirty (and re-armed for autosave) when a keystroke landed during the awaited disk write.

The new `bool _editedSinceSave` is the one fact AvaloniaEdit cannot supply while a group is open:
whether anything at all has happened since the last save. Raised by `OnTextChanged`, cleared by
`MarkSaved` and `ApplyReload`.

Vim is untouched: `u` after an insert session still undoes the whole session, because the insert
group is still one group. Nothing under `Core/`, `AvaloniaEditVimBuffer` or `VimEditorBehavior`
changed.

## Files / modules touched

**Modified**

- `src/Enigma.MarkdownEditor.Desktop/ViewModels/DocumentViewModel.cs` — `_editedSinceSave` field;
  `MarkSaved` takes a required `ITextSourceVersion written`, keeps the flag when the buffer moved
  during the write, and states `IsDirty = false` when it did not; `OnUndoStackPropertyChanged` gains
  the group-close correction; `OnTextChanged` and `ApplyReload` maintain the new field; class
  `<remarks>` rewritten around the three rules that move the flag.
- `src/Enigma.MarkdownEditor.Desktop/ViewModels/DocumentManagerViewModel.cs` — `WriteAsync` captures
  `document.Document.Version` alongside the text snapshot and passes it to `MarkSaved`; one added
  `using AvaloniaEdit.Document;`.
- `tests/…Desktop.UnitTests/ViewModels/DocumentManagerViewModelTests.cs` — eight new tests (below).
- `tests/…Desktop.UnitTests/ViewModels/MarkdownPreviewViewModelTests.cs` — the Save-As call site
  updated for the new `MarkSaved` signature.
- `tests/…Desktop.UnitTests/Views/EditorTabStripTests.cs` — the real-control case.
- `tests/…Desktop.UnitTests/Views/TabStrip.cs` — a `SaveAsync()` helper.

**Created** — `docs/done/BUG-38F5.md` (this file).

## Deviations & follow-ups

- **`TabStrip.SaveAsync()` instead of exposing `Documents`.** The plan allowed "a small accessor for
  the harness's `Documents` if it does not expose one already". A raw accessor would have broken the
  harness's own invariant that every mutating helper ends in a layout pass, so the helper awaits
  `SaveCommand.ExecuteAsync(null)` — the very command the window's `Ctrl+S` binding names, exactly as
  the plan specified — and then settles.
- **`SaveAll_SkipsDocumentsThatAreClean` asserts through the file, not a stamp.** The plan's wording
  ("only the dirty one's stamp changes") assumed a readable stamp; `DocumentViewModel._stamp` is
  private and widening the API for a test was not worth it. The test instead changes the clean file
  underneath without telling its watcher and asserts that content survives Save All — a stricter
  check of the same property.
- **The plan's test 3 does not reproduce as written; one assertion was added so that it does.**
  `ASingleEditSavedInsideItsGroup_ThenSavedAgainInNormalMode_IsClean` inserts *outside* the group
  before the second save, and that insert flips `IsOriginalFile` to false all by itself, so the
  normal-mode save that follows raises the notification the mid-group save could not — it passed
  against the unfixed code. The `n = 1` hole the plan describes exists only *before* that intervening
  edit, so an `Assert.False(document.IsDirty)` was added immediately after `EndUndoGroup()`. With it
  the test reproduces; the plan's normal-mode leg is kept as the regression guard it was written to
  be.
- **Manual verification was not performed** — see *Build/test evidence*.
- **Line endings:** the diff is clean, every touched file is LF throughout, and no CRLF churn was
  observed. No action taken or recommended.
- **Follow-ups from the plan stand, untouched by this work:** the BUG-7AB2 limitation (editing back
  to the saved text *inside* an open insert group cannot clear the dot until `Esc`, because
  AvaloniaEdit's `UndoStack` cannot express a save point inside a group), and `Ctrl+Z` reaching
  AvaloniaEdit while vim's insert group is open.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release` — **succeeded, 0 warnings, 0 errors**
  (`TreatWarningsAsErrors` is on solution-wide).
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` — **3304 passed, 0 failed,
  0 skipped**. 3295 before this item; nine added.

**Nine tests added, eight of them seen failing first.** The repro run was taken twice: once with the
tests written against the unmodified code (six failures), and once with the fix deliberately made
inert — the version guard, `IsDirty = false` and `_editedSinceSave = false` stripped out of
`MarkSaved`, which disables all three holes' fixes at their common root — so that the two tests
needing the new signature could reproduce too. That run: **8 failed, 1 passed of 9.**

| # | Test | Repro'd first |
|---|------------------------------------------------------------------|-----|
| 1 | `SavingInsideAnOpenUndoGroup_ClearsTheDirtyFlag`                  | yes |
| 2 | `ClosingTheGroupAfterASaveInsideIt_KeepsTheDocumentClean`         | yes |
| 3 | `ASingleEditSavedInsideItsGroup_ThenSavedAgainInNormalMode_IsClean` | yes, after the added assertion (see *Deviations*) |
| 4 | `TypingAfterASaveInsideTheGroup_StaysDirtyWhenTheGroupCloses`     | yes |
| 5 | `UndoingAnInsertSessionSavedMidWay_MarksDirtyAndRedoMarksClean`   | yes |
| 6 | `AutosaveFiringInInsertMode_ClearsTheDotAndCloseDoesNotAsk`       | yes |
| 7 | `AnEditDuringTheWrite_LeavesTheDocumentDirty`                     | yes |
| 8 | `SaveAll_SkipsDocumentsThatAreClean`                              | no — guard only; passed before and after |
| 9 | `SavingInInsertMode_HidesTheDotAndEscDoesNotBringItBack`          | yes |

Test 9 drives the real `EditorPaneView` in a shown window with vim on: `i`, typed text, the dot
visible, `SaveCommand`, the dot gone with the mode still `Insert`, `Esc` back to `Normal` with the
dot still gone, then a close that asks nothing. That is the reported scenario end to end, through the
real vim engine and the real tab template.

**Manual verification: not performed.** The plan asks for a pass in the running app (`Ctrl+S`, `:w`
and autosave, checking the dot and the file after each). Driving keystrokes into a live desktop
window and reading a rendered dot back is not something this environment can do; test 9 exercises the
same path through the same control, and the acceptance criteria are met by the suite. The manual pass
remains available to run before the 1.4.1 release (`FEATURE-6A2F`).

## Acceptance criteria

| Criterion | Met |
|---|---|
| The nine tests exist; the repro ones were seen failing first | yes (one qualified — see *Deviations*) |
| Dot hidden after a save in insert mode, and stays hidden across `Esc`; close does not ask — `Ctrl+S`, `:w`, autosave alike | yes (tests 1, 2, 6, 9; `:w` goes through the same `SaveAsync`/`WriteAsync`) |
| A normal-mode save after an insert session saved mid-way clears the dot | yes (test 3) |
| An edit landing during a write leaves the document dirty and re-armed | yes (test 7) |
| `u` still undoes a whole insert session; nothing under `Core/Vim`, `AvaloniaEditVimBuffer`, `VimEditorBehavior` changed | yes (test 5; the diff touches two `ViewModels` files) |
| Build 0 warnings; full suite green | yes |
| Completion doc written, roadmap and plan flipped to `DONE` | yes |
