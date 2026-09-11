# BUG-7AB2-PHASE02 — Dirty flag missed in insert mode

**Status: DONE** · Phase 02 of 5 · Branch: `bugfix/bug-7ab2-phase02-insert-mode-dirty-flag`
Plan: `docs/plan/BUG-7AB2.md`

## Summary

Typing in the editor raised no dot on the tab, and closing that tab discarded the work without
asking. The cause is the one the plan named, and it was reproduced before anything was changed:
`DocumentViewModel.IsDirty` came from exactly one source, `UndoStack.IsOriginalFile`, which AvalonEdit
only recalculates when the undo group depth returns to zero — and vim's insert mode holds one group
open for the whole insert session. For as long as the user was typing, the flag was stale `false`: no
dot, and `CloseAsync`'s `if (target.HasUnsavedChanges)` never asked the question.

`OnTextChanged` now raises the flag, and `OnUndoStackPropertyChanged` keeps its sole right to clear
it. The asymmetry is the point and is documented on both sites: an edit is proof there is something
to save, while nothing but the undo stack can prove there is not — so undoing back to the saved state
still clears the dot the moment the group closes. The insert-session undo group itself is untouched:
one `u` undoing a whole insert session is what vim does, and four phases of `FEATURE-11AA` rest on it.

`ApplyReload` is the one write to the buffer that is not the user's work, so it is guarded by a
`_replacing` flag around the `Document.Text` assignment and states `IsDirty = false` at the end rather
than relying on `ClearAll()` / `MarkAsOriginalFile()` to have raised a notification. The `TextChanged`
event itself is still raised during a reload — only the flag is suppressed — so nothing downstream of
it changes.

## Files/modules touched

**Modified — `src/Enigma.MarkdownEditor.Desktop`**

- `ViewModels/DocumentViewModel.cs` — `OnTextChanged` sets `IsDirty` unless `_replacing`; new
  `_replacing` field; `ApplyReload` guards the text assignment (`try`/`finally`) and ends with
  `IsDirty = false`; a class-level `<para>` and comments at both handlers explaining the asymmetry.
- `ViewModels/DocumentManagerViewModel.cs` — comment only. The one above `ArmAutosave` in
  `OnDocumentPropertyChanged` said the document "is not dirty yet" when its text changes, which this
  phase makes false; it now says what that arming is actually for.

**Modified — `tests/Enigma.MarkdownEditor.Desktop.UnitTests`**

- `HeadlessTestApp.cs` — AvaloniaEdit's Fluent control theme added to `Styles`, after `FluentTheme`
  and for the reason `App.axaml` gives. See the deviation below: without it the `TextEditor` has no
  template, so no test could type into it.
- `Views/TabStrip.cs` — `Mode` and `Prompts` readings; `FocusEditor()` and `Type(string)`, which
  drives the platform's text input one character per event.
- `Views/EditorTabStripTests.cs` — **2 new tests** through the real control with vim on: typing `i`
  then a character raises the dot without an `Esc`, and closing that tab asks before taking the work.
- `ViewModels/DocumentManagerViewModelTests.cs` — **3 new tests**: an edit inside an explicitly opened
  undo group is dirty while the group is still open, closing such a document asks about unsaved
  changes, and a reload leaves the document clean with no autosave armed.

Nothing was added to `Enigma.MarkdownEditor.Core` or its test project. No new dependency, no settings
change, no public API change, and nothing in `Core/Vim` or `VimEditorBehavior` was touched.

## Deviations & follow-ups

- **The plan's fallback for the vim test was not needed, because one line in the test app fixed the
  harness.** The `TextEditor`'s `TextArea` was never in the visual tree under the headless app — no
  bounds, no visual parent, `Focus()` returning `false` — because `HeadlessTestApp` merged the Fluent
  theme and the Enigma dictionary but not AvaloniaEdit's own control theme, which `App.axaml` includes
  separately. Adding that include gives the tests a real, templated, focusable editor, and the two
  new view tests then drive the whole real path: platform text input → `VimEditorBehavior` → the
  engine → AvaloniaEdit's insert. Both fail against the unfixed code and pass against the fix. The
  rest of the suite is unaffected (1935 → 1940, none changed).
- **This also explains PHASE01's note that the reported stack trace could not be reproduced.** Pointer
  input could not reach `SelectionMouseHandler` because there was no `TextArea` in the tree to reach.
  Now that the theme is included, that regression could plausibly be written as a real click test —
  worth a look, but out of this phase's scope and not required: PHASE01's fix is covered by the state
  assertions it already makes.
- **The close test hovers the tab before clicking.** A dirty tab shows the dot in place of the close
  button until the pointer is over it, so `PointerOver(0)` comes first — which is what a user does,
  and what makes the button's bounds real.
- **Known limitation, as planned and now documented in the code:** while a vim insert session is open,
  editing back to the saved text cannot clear the dot until the group closes on `Esc` or a tab switch.
  The alternative was breaking insert-session undo.
- **Autosave now arms from two places on the first edit** — the text changing and the flag turning
  true — where before only the second fired. Both call the same idempotent `ArmAutosave`, which
  reschedules the same timer, so the behaviour is unchanged; the autosave tests confirm it.
- **No line-ending noise seen.** Every file touched here is LF; no CRLF recommendation to make.
- **Not verified by hand in the running application.** The plan asks for no manual pass in PHASE02 and
  the vim path is now covered through the real control, but the next launch is worth a glance: type in
  insert mode and watch the dot appear on the first keystroke.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -v normal` (Debug): **succeeded, 0 errors, 0 warnings**, no
  `AVLN*` diagnostic in the log.
- `dotnet test Enigma.MarkdownEditor.slnx`: **1940 passed, 0 failed, 0 skipped** across
  `Enigma.MarkdownEditor.Core.UnitTests` and `Enigma.MarkdownEditor.Desktop.UnitTests`. **5 of them
  are new** — `--filter-class "*EditorTabStripTests"`: 16 total, 16 succeeded (14 before this phase);
  `--filter-class "*DocumentManagerViewModelTests"`: 63 total, 63 succeeded (60 before).
- **The defect was reproduced first, at both levels.** Against the unfixed code,
  `EditingInsideAnOpenUndoGroup_…` and `ClosingADocumentEditedInsideAnOpenUndoGroup_…` failed on the
  flag and on a prompt that was never put; with the fix stashed again after the harness was fixed,
  `TypingInVimInsertMode_…` and `ClosingATabTypedIntoInInsertMode_…` failed the same way through the
  real control. All four pass against the fix.
- `EditingADocument_MarksItDirtyAndUndoingBackToTheSavedStateClearsIt` passes unchanged, as do the
  four autosave tests and the reload/external-change set.
