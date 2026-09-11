# FEATURE-6054-PHASE03 — View modes & scroll sync

**Status: DONE** · Branch: `feature/feature-6054-phase03-view-modes` · Plan: `docs/plan/FEATURE-6054.md`

## Summary

The preview stops being a thing you switch to and becomes a thing you work beside. Three changes, and
one rename that made the third one honest.

**View modes.** A `ViewMode` of `EditorOnly | Split | PreviewOnly` is now the single source of both
panes' visibility, replacing the standalone "show the preview" toggle. One value rather than a flag
each, because the two panes are not independent: there is no arrangement in which neither is shown,
and two toggles could ask for one. The toolbar carries them as a segmented group of three toggles
behaving as radio buttons — square and borderless inside one rounded, clipped border, so they read as
one control — and `Ctrl+Shift+V` walks the same three in the same order, wrapping round. Switching the
*active* segment off is refused, and refusing means saying so: the property re-notifies, or the button
stays down in the ViewModel and up on screen. The sidebar toggle is deliberately untouched by all of
it — the sidebar is where files are found, and reading a note full-width is not a reason to lose the
tree.

The window mirrors the two flags onto its grid columns exactly as it already did for the sidebar,
because hiding a pane is not enough on its own: a `Grid` column keeps whatever width it was given, so
a collapsed pane would leave a gap. The editor pane gained a `Border` wrapper to carry the flag and
the column — its own `DataContext` is the document manager, and the flag belongs to the window — and
the splitter between the two panes now appears only in Split, where there is actually a boundary to
drag.

**Live update.** Typing reaches the renderer a quarter of a second after the last keystroke rather
than never. The timer lives in `MarkdownPreviewViewModel`, not in the control: only that side knows
what a keystroke is, and a control that debounced itself would have no way to say so. It is keyed on
the pane rather than on the document, so a keystroke in the active tab pushes back whatever was
pending regardless of which tab scheduled it, and switching tabs cancels it — fired afterwards it
would publish the outgoing document's text over the incoming one's. A fired refresh publishes the text
and only the text: the folder and the path have not moved, and each assignment is a render.

**Scroll sync, editor → preview.** In Split, the source line at the top of the editor drives the
preview. The chain is three pieces, each of which can be tested where it lives:

- `MarkdownLineMap` (Core) is the arithmetic: built from the first source line of every rendered block
  and the document's last line, it answers "which block owns line *n*, and how far into it" by binary
  search, and inverts that answer back to a line. The blocks are treated as a gapless partition — each
  owns the lines up to the one before the next block's start — so the blank space under a paragraph
  belongs to that paragraph, which is what the reader sees. A fraction rather than a line number
  because the caller is about to interpolate across the block's height: scrolling to a block's top
  alone makes a forty-line fence stick while the editor moves through it.
- `MarkdownPreview` builds the map from the walk rather than from the AST. The walk is the only place
  the pairing is known — it draws nothing for some block kinds, so the *n*th visual is not the *n*th
  block of the document — so the source line now travels out with the visual as a
  `MarkdownBlockVisual`. Building the index is isolated from the render in its own `try`: an index
  failure costs the sync, never the note.
- `EditorPaneView` reports the top visible line, filtered on the line rather than the offset, because
  a wheel notch is a dozen scroll events across a few pixels and the preview only has somewhere new to
  go when a different line reaches the top. `MainWindow` wires the two together — a line number out of
  one control and an offset into another, with nothing in between a ViewModel could own.

**The view's position survives a re-render**, which is what makes the live update usable at all:
before the tree is thrown away the top source line is read off the old visuals, and the new ones are
scrolled back to it. A line survives an edit three paragraphs above it where a pixel offset does not.
Telling "the same note, retyped" from "a different note" needs identity the control did not have —
two notes in one folder share a `BasePath` — so it gained a `SourcePath` property. It is never
rendered and never triggers a render; all it does is decide whether the next render may keep the
position, and reset the view when the document changes.

**The rename.** The debounce needed a restartable idle timer, and `IAutosaveScheduler` already was
exactly one — per-key, UI-thread callback, with a fake in the suite. Rather than a second
implementation of the same twelve lines, it is now `IIdleScheduler` / `IdleScheduler`: the same
service, one instance in the container, with autosave and the preview keyed on different objects so
neither can displace the other. Confirmed with you before building.

## Files/modules touched

**Created — Core**

- `Settings/ViewMode.cs` — `EditorOnly | Split | PreviewOnly`. Core-side and persisted by name,
  because FEATURE-40F5 PHASE02 saves which mode the window was left in.
- `Markdown/MarkdownLinePosition.cs` — a block index and a fraction into it.
- `Markdown/MarkdownLineMap.cs` — the line-to-block index, its binary search, and the inverse.

**Created — Desktop**

- `Controls/MarkdownBlockVisual.cs` — one rendered block and the source line it came from.

**Renamed — Desktop**

- `Services/IAutosaveScheduler.cs` → `Services/IIdleScheduler.cs`, `Services/AutosaveScheduler.cs` →
  `Services/IdleScheduler.cs` (git mv, so the history follows). Doc comments rewritten from "per
  document" to "per key"; the field and parameter in `DocumentManagerViewModel` stay named `autosave`,
  which is still what that class uses it for.

**Modified — Desktop**

- `Controls/MarkdownPreview.cs` — the `SourcePath` property, `LineMap`, `ScrollToSourceLine`, the
  position kept across a render, and the re-entrancy guard.
- `Controls/MarkdownVisualBuilder.cs` — `Build` returns `MarkdownBlockVisual`s.
- `ViewModels/MarkdownPreviewViewModel.cs` — the debounce, `LiveUpdateDelay`, `SourcePath`.
- `ViewModels/MainWindowViewModel.cs` — `ViewMode`, the derived `IsEditorVisible` /
  `IsPreviewVisible`, the three segment properties, `CycleViewModeCommand`.
- `Views/MainWindow.axaml` — the segmented group and its style, the `Ctrl+Shift+V` binding, the editor
  pane's wrapper, the splitter's condition, `SourcePath` on the renderer.
- `Views/MainWindow.axaml.cs` — the editor column's collapse, the sync wiring, and the push on
  entering Split.
- `Views/EditorPaneView.axaml.cs` — `TopVisibleLine` and `TopVisibleLineChanged`.
- `Extensions/ServiceCollectionExtensions.cs` — the renamed registration.

**Modified — tests, docs**

- New: `Markdown/MarkdownLineMapTests.cs` (Core), `Controls/MarkdownPreviewSyncTests.cs` (Desktop).
- Renamed: `Fakes/FakeAutosaveScheduler.cs` → `Fakes/FakeIdleScheduler.cs`; `EditorHarness.Autosave`
  → `EditorHarness.Idle`.
- Updated: `Controls/FixtureCorpusTests.cs` (a theory that walks every line of every fixture and
  asserts the sync leaves that line's block on screen), `ViewModels/MainWindowViewModelTests.cs`,
  `ViewModels/MarkdownPreviewViewModelTests.cs`, `ViewModels/DocumentManagerViewModelTests.cs`,
  `Extensions/ServiceCollectionExtensionsTests.cs`, `Controls/MarkdownPreviewTests.cs` (the
  `BuildBlocks` override's signature).
- `README.md` — the status paragraph's "still to come in the preview" sentence, which this phase made
  false (documentation sweep).

## Deviations & follow-ups

- **`ViewMode` is in `Core/Settings/`**, not in the Desktop project. The plan says "app-level"; Core
  is where the app's other persisted UI enum (`AppTheme`) lives, and FEATURE-40F5 PHASE02 is going to
  save this one. Easy to move if you would rather it sat beside the ViewModels.
- **The preview toggle button is gone**, replaced by the three-segment group (your choice during the
  pre-flight). `IsPreviewVisible` is now read-only and derived, so the test that asserted assigning to
  it notified once was replaced by the view-mode tests.
- **`MarkdownPreview.SourcePath` is a third property** beyond the plan's `Markdown` and `BasePath`.
  Without it the control cannot tell a note being retyped from a different note, and keeping the
  scroll position across a re-render would land a freshly opened tab half way down. It resets the view
  the moment it changes rather than at the next render, because two notes can hold the same text and an
  assignment of unchanged markdown renders nothing at all.
- **`BuildBlocks` changed shape.** The protected seam PHASE01 left now returns
  `IReadOnlyList<MarkdownBlockVisual>` rather than `IReadOnlyList<Control>`, because the index has to
  be built by the walk that decides which blocks are drawn. One test subclass was updated with it.
- **"Deepest rendered block" is the top-level block.** The plan's wording implies descending into
  containers; the index is over the blocks the preview actually stacks, which is what an offset can be
  measured against — a paragraph inside a quote has no offset of its own in the block panel. Line
  granularity inside a quote or a list is therefore the whole quote or list. If that ever reads as
  coarse, the fix is to index the nested visuals too rather than to change the arithmetic.
- **A first guard was wrong and is worth recording.** "Has this been laid out yet" cannot be answered
  from the block panel's own height: straight after a render the panel still reports the height it had
  *before* the rebuild while every child sits at zero, so the sync scrolled to nowhere and stayed
  there. It is now an explicit flag cleared by each render and set by the panel's next
  `LayoutUpdated`, with a pending line held until then. Found by probing the headless tree, not by
  reasoning.
- **The editor→preview wiring is not covered by an automated test.** It needs a live `TextEditor`, and
  no test in this suite instantiates one — `MarkdownHighlightingTests` deliberately drives the grammar
  instead. `EditorPaneView.TopVisibleLine` and the `MainWindow` handler are therefore verified by
  inspection only; everything either side of them (the map, the control's scroll, the mode flags, the
  debounce) is tested. Adding a headless editor test would be a worthwhile piece of work on its own
  and would pay off again for FEATURE-40F5.
- **The manual visual pass was not performed** — this session has no way to look at the running
  window. The XAML is compile-verified (compiled bindings are on, so every new binding path resolved
  at build time) but not eye-verified. What wants a human, in dark and in light: that the segmented
  group reads as one control and its checked segment is obvious; that `Ctrl+Shift+V` cycles from
  inside the editor with vim on as well as off; that collapsing to `PreviewOnly` and back to `Split`
  restores the splitter's dragged width; and that scrolling a fixture in Split tracks smoothly rather
  than in visible jumps.
- **Line endings:** no CRLF/LF churn was observed in this dev's diff; nothing was done about line
  endings either way, per the workflow's recommendation-only rule.
- **Follow-up, not done:** PHASE02's note still stands and now matters more — a debounced render
  re-tokenizes every fence in the note. On a large document with many fences the 250 ms refresh will
  be where it shows first; caching a fence's tokens by (code, language, variant) is the cheapest fix,
  and virtualizing the block panel is the one after that.
- **Follow-up, not done:** preview→editor reverse sync is out of scope by the plan, but the map
  already inverts, so it is now a small piece of work rather than a design one.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release` — succeeded, **zero warnings**
  (`TreatWarningsAsErrors` is on solution-wide, so a warning would have failed it).
- `dotnet test Enigma.MarkdownEditor.slnx` — **1577 passed, 0 failed, 0 skipped** (1516 before this
  dev, so **+61 cases**).
- Core: `MarkdownLineMapTests` — **32 cases**. The empty map, the gapless partition, a document
  shorter than its last block claims, the fraction at a block's start / part way through / in a
  single-line block, two blocks sharing a line, a line above the first block and one far past the end,
  the binary search checked against a linear walk over five blocks, the line→position→line round trip,
  clamping, and the four rejections (null, a line below one, out-of-order starts, an index outside the
  map).
- Desktop: `MarkdownPreviewSyncTests` — **14 cases**. That the index has one entry per *rendered*
  block and carries its one-based source line; that a link reference definition, which the walk draws
  nothing for, shifts the index rather than the AST's numbering; that the front-matter chip does hold a
  place, on its own line; that the index is rebuilt per render; that a scroll lands inside the block
  that owns the line; that walking sixty source lines downwards never takes the view back up (the
  reader's definition of "no oscillation"); that line 1 returns to the top; that an empty preview and a
  line nobody wrote are answered rather than throwing; that retyping the same document keeps the
  reader's position and opening a different one does not; and that a scroll asked for before the tree
  is laid out is answered once it is.
- Desktop: `FixtureCorpusTests` — **+10 cases**, one per fixture: every source line of the file is
  pushed through the sync and the block the index attributes it to must be on screen afterwards. This
  is the plan's "tracks the preview to within the correct block on the fixtures corpus", asserted
  rather than eyeballed.
- Desktop: `MarkdownPreviewViewModelTests` — the debounce. Typing does not reach the renderer until the
  timer fires; the delay is `LiveUpdateDelay`; three keystrokes leave one pending refresh, not three; a
  fired refresh notifies `Markdown` and nothing else; switching tabs cancels what was pending; a
  disposed pane stops following typing.
- Desktop: `MainWindowViewModelTests` — every pane visible by default in Split; each mode's pane flags,
  with "never both hidden" asserted directly; that a mode change notifies all six flags the view reads;
  that setting the same mode stays silent; that the shortcut cycles and wraps; that a segment selects
  its mode; and that switching the active segment off leaves the mode alone and re-notifies.
- **Not covered:** the editor→preview wiring and the manual visual pass — see
  *Deviations & follow-ups*.
