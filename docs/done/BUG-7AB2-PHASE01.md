# BUG-7AB2-PHASE01 — Closing a tab leaves a live editor

**Status: DONE** · Phase 01 of 5 · Branch: `bugfix/bug-7ab2-phase01-tab-close-live-editor`
Plan: `docs/plan/BUG-7AB2.md`

## Summary

Closing the active tab left `ActiveDocument` at `null` while other tabs were still open, and the pane
went on drawing an editor over it — a visible, hit-testable text area with no document, which is what
AvaloniaEdit threw `Document is null` about the moment the pointer reached it.

The cause is the one the plan named, and it was reproduced before anything was changed. The tab strip
binds `SelectedItem="{Binding ActiveDocument}"` two-way, so taking the selected tab out of `Documents`
makes the `ListBox` clear its own selection and write that `null` straight back into the ViewModel —
**before** `Remove`'s `if (ActiveDocument == document)` guard ran. The guard was therefore always
false when it mattered, and the neighbour was never activated. `Remove` now reads
`bool wasActive = ReferenceEquals(ActiveDocument, document)` *before* touching the collection and
branches on that; the neighbour rule itself is unchanged.

The view is hardened separately, so this class of fault cannot render even if something else clears
the selection. `DocumentManagerViewModel` gained `HasActiveDocument`, notified from both
`OnActiveDocumentChanged` and `OnDocumentsChanged` (the second because that write arrives from the
view, not from here), and `EditorPaneView` keys the `TextEditor` on it and the placeholder on its
negation. The tab strip's `Border` keeps `HasDocuments`: tabs with no selection are still tabs.

Placeholder text is unchanged. Nothing about the closing sequence itself moved — the unsaved-changes
prompt, `Cancel` refusing the close, middle-click, the context menu and `:q` all still go through the
same `CloseAsync`/`Remove` pair.

## Files/modules touched

**Modified — `src/Enigma.MarkdownEditor.Desktop`**

- `ViewModels/DocumentManagerViewModel.cs` — new `HasActiveDocument` property; notified from
  `OnActiveDocumentChanged` and `OnDocumentsChanged`; `Remove` captures `wasActive` before
  `Documents.Remove` and branches on it.
- `Views/EditorPaneView.axaml` — `TextEditor.IsVisible` re-keyed from `HasDocuments` to
  `HasActiveDocument`, the placeholder `TextBlock` to `!HasActiveDocument`; comments at both sites
  saying why the two questions are not the same one.

**Modified — `tests/Enigma.MarkdownEditor.Desktop.UnitTests`**

- `Views/TabStrip.cs` — `Strip`, `Placeholder`, `ActiveTitle` and `Count` readings; `CloseAsync(index)`
  (clicks the real close button and awaits the command's `ExecutionTask`), `Deactivate()`,
  `ClickPane()`, and a private `Click(Visual)` press/release helper.
- `Views/EditorTabStripTests.cs` — **6 new tests**: the active middle tab, the active last tab, the
  only tab, a pane with tabs but nothing active, and the two pane-click regressions.
- `ViewModels/DocumentManagerViewModelTests.cs` — **2 new tests**: the `HasActiveDocument`
  notifications, and a VM-level regression that simulates the strip's write-back by clearing
  `ActiveDocument` from a `CollectionChanged` handler.

Nothing was added to `Enigma.MarkdownEditor.Core` or its test project. No new dependency, no settings
change, no public API on `ISessionService` or anywhere else.

## Deviations & follow-ups

- **The reported stack trace itself is not reproducible under the headless platform, and the test says
  so instead of pretending otherwise.** With two tabs open and `ActiveDocument` cleared, the pre-fix
  pane really did hold a **visible** `TextEditor` whose `Document` was **`null`** — verified directly
  before the fix — but `window.MouseDown`/`MouseUp` over it does not reach
  `SelectionMouseHandler.TextArea_MouseLeftButtonUp`, so nothing threw. The two click tests therefore
  assert the *precondition* the crash needed (`Editor.Document is null` and `Editor.IsVisible` false
  after the fix) and then click, rather than claiming to catch the exception. That state assertion is
  the real automated proof: with the control hidden there is nothing for a pointer to land on.
- **Closing goes through the real close button, not the command.** `TabStrip.CloseAsync` clicks it with
  headless input and then awaits `CloseCommand.ExecutionTask`, because the `Button` starts the async
  command and does not wait for it. A clean tab never awaits anything, but a tab with a prompt to
  collect does, so the await is not redundant.
- **`Deactivate()` is an artificial state, deliberately.** With `Remove` fixed, no user path leaves
  tabs open and nothing active. The helper reaches that state directly so the view hardening has
  something to be tested against — it is defence in depth, per the plan's step 2, not a live path.
- **The editor's visibility now flips on activation rather than on the collection changing.** Opening a
  file previously made the editor visible at `Documents.Add` and bound its document one line later at
  `ActiveDocument = document`; both now happen in the same property change. Tighter, not looser, and
  the caret/scroll-restore and session-restore tests are unaffected.
- **`docs/roadmap.md` shows a large diff for a two-cell change.** `IN PROGRESS` is wider than the
  previous widest Status cell, so the whole table was re-padded, as the workflow requires.
- **No line-ending noise seen.** Every file touched here is LF; no CRLF recommendation to make.
- **Not verified by hand in the running application.** All of this phase's acceptance criteria are
  covered through the real control in a real window, and the plan asks for no manual pass in PHASE01
  (unlike 03–05). Worth a glance at the next launch all the same: close the middle of three tabs and
  confirm the successor comes up.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -v normal` (Debug): **succeeded, 0 errors, 0 warnings**, and
  no `AVLN*` diagnostic in the log — checked explicitly because XAML was touched.
- `dotnet test Enigma.MarkdownEditor.slnx`: **1935 passed, 0 failed, 0 skipped** across
  `Enigma.MarkdownEditor.Core.UnitTests` and `Enigma.MarkdownEditor.Desktop.UnitTests`. **8 of them are
  new** — `--filter-class "*EditorTabStripTests"`: 14 total, 14 succeeded (8 before this phase);
  `--filter-class "*DocumentManagerViewModelTests"`: 60 total, 60 succeeded (58 before).
- **The defect was reproduced first.** Against the unfixed code the four new defect tests failed
  exactly as diagnosed: `ClosingTheActiveMiddleTab_…` and `ClosingTheActiveLastTab_…` reported
  `ActiveDocument` as `null` where a neighbour was expected, and both visibility tests found the
  editor still `IsVisible` with nothing active. All four pass against the fix.
