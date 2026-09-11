# FEATURE-4797-PHASE01 — Select-all & plain-text copy

**Status: DONE** · Branch: `feature/feature-4797-phase01-preview-copy` (cut from `develop`)
Plan: `docs/plan/FEATURE-4797.md` · Roadmap: `docs/roadmap.md`

## Summary

The rendered preview pane can now be copied. `Ctrl+A` in the pane selects every text block in it and
`Ctrl+C` puts the whole rendered note on the clipboard as plain text; a selection inside one paragraph
still copies just that range through the block's own handler, untouched. The same copy is on a new
icon button in the `PREVIEW` header and on a context menu offering **Copy** and **Select all**. All
three surfaces obey one rule — the selection when there is one, the whole note otherwise — because all
three go through one method on the control.

The text is a projection of the markdown, not of the visuals: `MarkdownPlainText` walks the same AST
the renderer was handed and mirrors its block-level drop rules, so what is copied is what is on
screen. Markup goes; what the reader can see stays — list markers and nesting, `[ ]` / `[x]` task
boxes, fenced code verbatim without its fences, tab-separated table rows, `[alt text]` where an image
sat, and nothing at all for a thematic rule or the front-matter chip.

The clipboard write stays out of the control: the renderer resolves *which* copy was asked for and
raises `CopyRequested`, and the pane's ViewModel reaches `IClipboardWriter` — the same seam
`RenderFailed`, `LinkActivated` and `TaskToggled` already use.

## Files/modules touched

**Created**

- `src/Enigma.MarkdownEditor.Core/Markdown/MarkdownPlainText.cs` — `Render` (the clipboard's text) and
  `Blocks` (one entry per drawn block, empty for the chip and a rule).
- `src/Enigma.MarkdownEditor.Desktop/Controls/MarkdownCopyRequest.cs`
- `src/Enigma.MarkdownEditor.Desktop/Services/IClipboardWriter.cs` + `ClipboardWriter.cs`
- `tests/Enigma.MarkdownEditor.Core.UnitTests/Markdown/MarkdownPlainTextTests.cs` — 25 tests, rule by
  rule over the smallest document that shows each.
- `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Controls/MarkdownPreviewCopyTests.cs` — 41 tests:
  the gestures through the headless window, the request the renderer builds, the drift guard and the
  copied shape over the fixture corpus.
- `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Fakes/FakeClipboardWriter.cs`
- `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Fakes/RecordingLogger.cs` — see *Deviations*.

**Modified**

- `Controls/MarkdownPreview.cs` — `SelectAll`, `ClearSelection`, `RequestCopy`, `HasContent`,
  `CopyRequested`, the `_allSelected` flag, `Focusable`, a tunnelling key handler, and an
  `OnPointerPressed` override.
- `ViewModels/MarkdownPreviewViewModel.cs` — `IClipboardWriter`, `CanCopy`, `CopyAsync`.
- `Views/MainWindow.axaml` — the header row with its copy button, and the renderer's context menu.
- `Views/MainWindow.axaml.cs` — the `CopyRequested` subscription and the two `Click` handlers.
- `Extensions/ServiceCollectionExtensions.cs` — `IClipboardWriter` registered beside `IFileLauncher`.
- `README.md` — a Preview bullet for the copy, and a `Ctrl+A` / `Ctrl+C` row in the shortcut table
  (accepted from the documentation freshness sweep).
- `tests/…/ViewModels/MarkdownPreviewViewModelTests.cs` — 9 copy tests, plus the new constructor
  argument and two existing notification assertions (see *Deviations*).
- `tests/…/ViewModels/MainWindowViewModelTests.cs` — the new constructor argument.

## Deviations & follow-ups

**Two plan facts were wrong and were reconciled before building.**

1. **`SelectableTextBlock.HasSelection()` is not public.** The plan confirmed it against
   `Avalonia.Controls.xml`, but that file documents non-public members too (its own summary points at
   an undocumented `GetSelection`). Used `SelectionStart != SelectionEnd` instead — which is already
   how `MarkdownLinkRegions` tells a drag from a click, so the two now agree by construction.
2. **`IClipboard.SetTextAsync` no longer exists in Avalonia 12.1.2.** Replaced by the typed
   data-transfer API; the writer calls `clipboard.SetValueAsync(DataFormat.Text, text)`.
   **This affects PHASE02**: its design says "one `DataObject` carrying `text/plain` and the HTML
   flavour", which is the pre-12 API. The 12.x equivalent is an `IAsyncDataTransfer` carrying two
   formats, and PHASE02 should be re-read against `Avalonia.Base.xml` before it is built.

**The drift test could not compare counts as the plan wrote it.** The front-matter chip and a thematic
rule *are* drawn blocks that carry no text, so `Render`'s output has fewer parts than the pane has
blocks. `MarkdownPlainText` therefore exposes `Blocks` as well — one entry per drawn block, empty for
those two — and `Render` joins only the non-empty ones. The drift test reads `Blocks`, which keeps the
plan's guarantee (a block kind added to one side and not the other fails the test) and its rule (a
chip and a rule contribute nothing to the clipboard).

**The three surfaces reach the control through the code-behind, not through ViewModel commands.** The
plan bound the button and the menu to `Preview.CopyCommand` / `SelectAllCommand`, but gave the
ViewModel no way to learn what is selected — which only the control knows. Decided with the user
during pre-flight: `Click` handlers call methods on `PreviewRenderer`, which raises `CopyRequested`
for the ViewModel to write. This is the answer `EditorPaneView.axaml` already documents for the same
problem ("the four editing commands are methods on the control rather than anything bindable, so they
go through the code-behind"). Consequently there is no `SelectAllCommand` and no `CopyCommand`; both
surfaces gate on `Preview.CanCopy` via `IsEnabled`. Note also that this codebase uses **no**
`[RelayCommand]` source generation anywhere — every command is an explicit `AsyncRelayCommand`
property — so the plan's `[RelayCommand]` would have been a new pattern regardless.

**`Ctrl+A` / `Ctrl+C` are handled on a tunnelling handler, not in `OnKeyDown`.** A focused
`SelectableTextBlock` handles both gestures itself, so a bubbling handler would only ever see `Ctrl+A`
after the focused paragraph had already selected itself alone. Same reasoning, and same mechanism, as
the window's `Ctrl+Tab` handler. `Ctrl+C` is still passed through untouched unless the whole document
is selected, which is what keeps the native per-paragraph copy working.

**`SelectAll()` also focuses the pane**, so that *Select all* from the context menu can be followed by
`Ctrl+C`. Without it the gesture would arrive wherever the focus happened to be left.

**`CanCopy` is announced once per refresh**, from the end of `Refresh`/`PublishText`, rather than from
the `Markdown` and `HasDocument` setters it derives from. Announcing it from both made
`EveryChangeIsAnnouncedOnce` fail with two `CanCopy` entries for one document being opened — the test
was right, so the code changed rather than the assertion. Both existing notification assertions were
then extended by one trailing `CanCopy` entry; neither was weakened, and both still fail if a path or
folder is republished or if the text is published twice.

**`RecordingLogger<T>` is a new pattern in the test suite** — everything else passes `NullLogger`. It
earns its place for one acceptance criterion: "a clipboard write that fails is logged at Warning and
never surfaces to the reader" leaves the log entry as the only evidence the failure was noticed, and a
test without it would pass over a silently swallowed exception.

**Images-only paragraphs are not stacked in the copied text.** The renderer draws
`![one](a) ![two](b)` as two stacked image visuals; the projection writes `[one] [two]` on one line.
Mirroring the stack would mean duplicating the renderer's `ImagesOnly` layout rule inside `Core`,
which is the structural knowledge the plan deliberately kept out of it. It is one block either way,
so the drift guard is unaffected.

**Follow-ups (not acted on)**

- `FixtureCorpusTests` and `MarkdownPreviewCopyTests` each hold a private six-line copy of the
  fixture-folder listing. Worth hoisting into one internal helper if a third class needs it.
- The header button and the context menu are **compile-verified, not clicked**: compiled bindings,
  the `x:DataType` on the menu, the `Click` handler names and the `Kind="Copy"` icon are all resolved
  at build time and `AVLN` output is clean, but nothing in the suite builds `MainWindow`, so no test
  exercises the two surfaces end to end. A thirty-second manual check of the button, the menu and
  their disabled state with no tab open is worth doing before the release item.
- Line endings: nothing to report. `.gitattributes` already normalizes to LF and every file touched
  here is LF.

## Build/test evidence

- **Release build, clean**: `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental
  -v normal` → **0 Warning(s), 0 Error(s)**, and no `AVLN` diagnostics in the normal-verbosity log
  (`TreatWarningsAsErrors` is on solution-wide, and `AVLN*` is not promoted by it, so the log is
  checked separately).
- **Full suite green**: `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` →
  **3379 passed, 0 failed, 0 skipped**.
- **New tests**: 25 in `MarkdownPlainTextTests`, 41 in `MarkdownPreviewCopyTests` (2 of them theories
  over all 10 fixtures), 9 in `MarkdownPreviewViewModelTests`.
- **Every acceptance criterion met**, with these notes: the three surfaces' *behaviour* is tested at
  the control and the ViewModel; their XAML hookup is compile-verified only (see *Follow-ups*). The
  link-click, drag-into-a-link and task-tick criteria are covered by the pre-existing
  `MarkdownLinkRegionsTests` and `MarkdownTaskBoxTests`, which pass unchanged.
