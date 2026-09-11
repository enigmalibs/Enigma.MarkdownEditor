# BUG-8F2A — Tab scrollbar & checkbox alignment

**Status: DONE** · 2 phases · Roadmap: `docs/roadmap.md`
Planned on `develop`; each phase cuts its own branch from `HEAD` at build time.

## Objective

Two visual defects reported against **1.2.0**, one per phase. Both are layout arithmetic in the
Desktop project — no ViewModel, no setting, no new package, no `Core` change.

Root causes were established by reading the code during the interview and are recorded below. Each
phase still opens with a test that reproduces the defect before anything is changed, and any
deviation from what is written here goes in that phase's completion doc.

## Context & constraints

- **`TreatWarningsAsErrors` is on solution-wide** and `EnforceCodeStyleInBuild` with it. The bar for
  every phase is a **0-warning** build.
- **`AVLN*` diagnostics are emitted by an MSBuild task** and are **not** promoted by
  `TreatWarningsAsErrors`, so `dotnet build -v normal | grep -i AVLN` is part of the check whenever
  XAML is touched.
- **Theme brushes are always `{DynamicResource Enigma…}`** — never a hard-coded colour, so both
  variants follow.
- **The Fluent theme's private template parts are not enumerable offline.** The `.dll`'s compiled
  XAML exposes resource keys (`CheckBoxMinHeight`, `ScrollBarThumb`) but not element names, so both
  phases below name a **primary approach that does not depend on a template part** and keep any
  part-level styling as an optimisation, not as the plan.
- **The real-view harness already exists** and is the right tool for PHASE01:
  `tests/…Desktop.UnitTests/Views/TabStrip.cs` shows a real `EditorPaneView` in a real window over
  real files, with headless pointer input. Extend it rather than building a second harness.
- **Test bar for both phases:** measured assertions where the headless layout pass can report the
  geometry; anything only an eye can confirm (exact thickness, colour, weight) is stated plainly in
  the completion doc as verified by inspection.

---

## PHASE01 — Thin scrollbar on the tab strip

**Status: DONE**

### Symptom

With more tabs open than fit the pane, nothing on screen says so and nothing can be dragged: the
strip scrolls by the mouse wheel alone. There is no scrollbar at all.

### Root cause

Deliberate, and now wrong. `FEATURE-F3FA` hid the bar because Avalonia's Fluent `ScrollViewer`
template floats its scrollbars **over** the content — the `ScrollContentPresenter` spans both grid
rows, so a horizontal bar is drawn across the bottom of the very tabs it exists to reach. The fix
taken then was to remove the bar entirely (`Views/EditorPaneView.axaml:107`,
`ScrollViewer.HorizontalScrollBarVisibility="Hidden"`) and to translate wheel deltas into a
horizontal offset by hand (`Views/EditorPaneView.axaml.cs:241-268`).

Hiding it was one of two ways to stop it covering the tabs. The other — never taken — is to leave
the bar where it is and give it somewhere to float that is not a tab.

### Design

1. **Reserve a band under the tabs.** `ScrollViewer.HorizontalScrollBarVisibility` goes back to
   `Auto`, and the `Tabs` `ListBox` gets a bottom padding wide enough for the bar (start at **6**,
   tuned by eye). Fluent's `ListBox` template passes `Padding` to its `ScrollViewer`, which insets
   the presenter's content while the bar keeps drawing at the control's bottom edge — so the bar
   lands in the padding band and the tab titles move up out of its way. The strip's `Border` sits in
   an `Auto` row, so it simply grows by the band.
2. **Pin the bar thin, and stop it expanding.** `ScrollViewer.AllowAutoHide="False"` on the
   `ListBox` so it never fades to a hairline, plus styles scoped to `ListBox#Tabs` fixing the track
   and thumb at **~4px** in every state — at rest, under the pointer, and while dragging. It stays a
   real `ScrollBar`: draggable, and keyboard-reachable as before.
   Attempt order, first that works wins, and the completion doc records which:
   a. styles on `ScrollBar` / `Thumb` reached through `/template/` inside the strip's scope;
   b. a `ControlTheme` for `ScrollBar` supplied as a **local** resource on the strip, so only this
      one is re-themed;
   c. **fallback** — the bar goes back to `Hidden` and an own `ScrollBar` is placed in its own
      `Grid` row under the strip, wired in the code-behind to `Tabs.Scroll`
      (`Offset`/`Extent`/`Viewport`). Deterministic, at the cost of the sync code.
3. **Visible only while it is needed.** `Auto` already means that: the bar appears when
   `Extent.Width > Viewport.Width` and is gone when every tab fits. The reserved band stays either
   way, so the strip does not change height as tabs are opened and closed.
4. **Nothing else about the strip changes.** `OnTabsPointerWheelChanged` stays exactly as it is
   (including its "nothing to scroll, swallow nothing" guard), selection still scrolls the selected
   tab into view, and the hover / dirty-dot rules from `FEATURE-F3FA` are untouched.
5. **The XAML comment above the strip is rewritten.** It currently explains why there is no bar; it
   will be wrong the moment there is one.

### Acceptance criteria

- With more tabs than fit, a horizontal scrollbar is drawn under the tabs, covering no part of any
  tab title, and dragging it scrolls the strip.
- The bar is the same thickness at rest, under the pointer and while being dragged.
- With every tab fitting, no bar is drawn; the strip's height is the same in both cases.
- The mouse wheel still scrolls the strip sideways, and selecting an off-screen tab still brings it
  into view.
- Asserted through the real-view harness: the bar is present iff `Extent.Width > Viewport.Width`,
  and the tabs' laid-out bounds do not intersect the band the bar occupies.
- Build clean with zero warnings; no `AVLN` output; the whole suite passes.

### Files

- `src/Enigma.MarkdownEditor.Desktop/Views/EditorPaneView.axaml` — visibility, padding, bar styles,
  comment.
- `src/Enigma.MarkdownEditor.Desktop/Views/EditorPaneView.axaml.cs` — only if fallback (c) is taken.
- `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Views/EditorTabStripTests.cs` (+ the `TabStrip.cs`
  harness, if it needs a narrower strip to force overflow).

---

## PHASE02 — Preview checkbox alignment & size

**Status: DONE**

### Symptom

In the preview pane, a task item's checkbox sits visibly higher than the text beside it. It gets
worse as the preview's font size or line height is raised.

### Root cause

`MarkdownVisualBuilder.TaskBox`
(`src/Enigma.MarkdownEditor.Desktop/Controls/MarkdownVisualBuilder.cs:340-361`) builds a box of a
**fixed 20px** (`TaskBoxSize`), `VerticalAlignment = Top`, in column 0 of the item's grid. Column 1
holds prose set at `_type.BodyFontSize` with `LineHeight = _type.LineHeightAt(BodyFontSize)` — and
text is centred inside its own line box, not sat at the top of it.

At the shipped default (18pt × 1.5) the first line box is 27px tall and its text centres 13.5px from
the top of the row; the box centres at 10px. The box is 3.5px high, and the error is
`(lineHeight − boxSize) / 2` — it grows with both settings, which is why the report names them.

The second half of the same defect: the box is fixed at 20px while the prose beside it is a 6–72pt
setting. `Marker` (the `•` / `1.` of an ordinary item) already tracks `BodyFontSize`; the box does
not, so a task and a bullet in the same list are drawn to two different scales.

### Design

1. **The box size joins the type scale.** `MarkdownPreviewTypography` gains a derived member beside
   `AsideFontSize` and `LineHeightAt`:
   `TaskBoxSize => Math.Clamp(Math.Round(BodyFontSize * 1.1), 12, 36)`.
   The 1.1 is chosen so the shipped default is continuous: 18pt → 20, exactly what is drawn today.
   The clamp keeps the box legible at 6pt and stops it dominating the line at 72pt. Derived rather
   than a new setting — nobody asked to size a checkbox, and one number that follows the prose is
   what "aligned with the text" means.
2. **Centre it on the first line, by arithmetic rather than by alignment.** Keep
   `VerticalAlignment = Top` and set the top margin to
   `Math.Max(0, (LineHeightAt(BodyFontSize) − TaskBoxSize) / 2)`, leaving the existing 4px right
   margin. Deliberately *not* "give the box the line's height and let the template centre it": that
   would depend on how Fluent's `CheckBox` aligns its box inside its own bounds, which is a private
   detail this phase must not be hostage to.
3. **Scale what is drawn.** The theme exposes `CheckBoxMinHeight` (already overridden here) but no
   key for the drawn box, so:
   a. a style scoped to this box setting the size on the template part, if that part turns out to be
      reachable in Avalonia 12.1.1;
   b. **fallback** — wrap the `CheckBox` in a `LayoutTransformControl` with a uniform
      `ScaleTransform` of `TaskBoxSize / 20`, which scales the box, its tick and its hit area
      together and is immune to the template's internals.
   Whichever is taken is recorded in the completion doc. `MarkdownTaskBox.Attach` and the
   `SourceOffset` attached property are unchanged either way — and a wrapper must not break the
   tests that find the box by walking the tree.
4. **The marker column follows.** `MarkerColumnWidth` stops being the constant `22` and becomes a
   floor of `max(22, TaskBoxSize + 2)`, applied to both `TaskBox` and `Marker`. The grid column is
   `Auto`, so a wide ordered marker (`10.`) still grows past it as it does today.
5. **The horizontal indent does not change.** Tasks, bullets and numbers already start at the same
   x; the criteria below pin that, so the fix cannot move it.

### Acceptance criteria

- A task item's checkbox is vertically centred on the first line of its own text: the box's
  laid-out `Bounds` centre and the first text line's centre agree within 1px, asserted at the
  shipped default and at two other size / line-height combinations.
- The box grows with the preview font size — `size(8pt) < size(18pt) < size(32pt)` — and at 18pt it
  is the 20px it is today.
- A list mixing task items with bullets and ordered items has one left edge: the marker column's x
  is identical for all three.
- Ticking a box still rewrites the one character between the brackets, and a refused toggle still
  puts the box back — the `FEATURE-C8F1` behaviour is unchanged.
- Build clean with zero warnings; no `AVLN` output; the whole suite passes.

### Files

- `src/Enigma.MarkdownEditor.Desktop/Controls/MarkdownPreviewTypography.cs` — `TaskBoxSize`.
- `src/Enigma.MarkdownEditor.Desktop/Controls/MarkdownVisualBuilder.cs` — `TaskBox`, `Marker`,
  `MarkerColumnWidth`, and the retired `TaskBoxSize` constant.
- `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Controls/MarkdownPreviewTests.cs` and
  `MarkdownTaskBoxTests.cs`.

## Dependencies

None. The two phases are independent of each other and of `FEATURE-414B`.

## Out of scope

Tab reordering, an overflow dropdown or chevron buttons, middle-click close, pinned tabs · any new
setting for the checkbox size · restyling checkboxes anywhere but the preview.
