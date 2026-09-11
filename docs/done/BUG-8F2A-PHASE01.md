# BUG-8F2A-PHASE01 — Thin scrollbar on the tab strip

**Status: DONE** · Phase 01 of 2 · Branch: `bugfix/bug-8f2a-phase01-tab-scrollbar`
Plan: `docs/plan/BUG-8F2A.md`

## Summary

A strip with more tabs than room said nothing about it and could only be scrolled by the wheel.
`FEATURE-F3FA` had hidden the bar because Fluent's `ScrollViewer` floated it across the bottom of the
very tabs it was there to reach. The strip now draws a real 6px scrollbar in a band of its own under
the tabs, present only while the tabs overflow, and the same thickness whatever the pointer is doing.

**The premise the plan was built on no longer holds, and that made the fix much smaller than
planned.** The plan assumed Fluent still spans its `ScrollContentPresenter` across both rows of the
`ScrollViewer` template, and therefore designed a padding band on the `ListBox` for the bar to float
into. Measured against Avalonia **12.1.1** through the harness, that is not what happens:
**`ScrollViewer.AllowAutoHide="False"` alone makes the template stop overlaying the bar** and give it
a row of its own. With `AllowAutoHide` off the presenter arranges to `0,0,320,30` and the bar to
`0,30,320,6` — already disjoint. So no padding band was added; setting one would have reserved the
space **twice** and left a 12px gap under the tabs.

What the plan's step 1 was actually protecting against — the strip changing height as the last tab
that fits is opened — is still real, because the bar's row collapses to nothing when the bar is away.
That is handled by pinning the strip `Border` to `MinHeight="37"` (a 30px tab, the 6px band, the 1px
bottom border) and by giving the `ListBox` `VerticalAlignment="Top"`. Left to stretch, the `ListBox`
would have filled the reserved height and made the tabs themselves 6px taller whenever the bar was
away — the same wobble by another route.

**Attempt (a) of the plan's step 2 worked, and neither (b) nor (c) was needed.** The Fluent template's
parts turned out to be reachable after all — they were enumerated off a live control in the harness
rather than read out of the `.dll`, which is what the plan had tried. Four styles scoped to
`ListBox#Tabs ScrollBar:horizontal` do the whole job: `MinHeight` **and** `Height` at 6 on the bar and
again on its `Thumb` (the theme sets both, so setting only `MinHeight` left a 16px thumb overhanging a
6px track at `y = -5`), and `IsVisible="False"` on `PART_LineUpButton` / `PART_LineDownButton` — two
16px squares around a `PathIcon` that at 6px would be a squashed glyph taking a third of the track.
`PART_PageUpButton` / `PART_PageDownButton` are the invisible click-to-page halves of the track and
are left alone.

No `ControlTheme` was introduced and no code-behind was written: `OnTabsPointerWheelChanged` is
unchanged, as the plan required.

## Files/modules touched

**Modified — `src/Enigma.MarkdownEditor.Desktop`**

- `Views/EditorPaneView.axaml`
  - `ScrollViewer.HorizontalScrollBarVisibility` `Hidden` → `Auto`; `ScrollViewer.AllowAutoHide="False"`
    added. The `ListBox` keeps `Padding="0"` and gains `VerticalAlignment="Top"`.
  - The strip `Border` gains `MinHeight="37"`.
  - Four new styles under `UserControl.Styles`, all scoped to `ListBox#Tabs ScrollBar:horizontal` so
    nothing else in the pane — the editor's own scrollbars, the preview's — is re-themed.
  - The comment above the strip is rewritten: it explained why there was no bar, and says instead what
    `AllowAutoHide` buys and where the 37 comes from.
- `Views/EditorPaneView.axaml.cs` — comment only. The one above `OnTabsPointerWheelChanged` claimed the
  bar was hidden and gave that as the reason the wheel is translated by hand. The reason is now only
  that a mouse reports notches as a vertical delta; the code itself is unchanged.

**Modified — `tests/Enigma.MarkdownEditor.Desktop.UnitTests`**

- `Views/TabStrip.cs` — `HorizontalScrollBar`'s doc no longer calls it "the control that must never be
  seen". New readings: `ScrollBarBand` and `TabBand(int)`, both rectangles in the **strip's** own
  coordinates so they can simply be intersected (`Bounds` alone are relative to whatever each hangs
  off — the bar off the `ScrollViewer`'s grid, a tab off the items panel); `Height`; and the input
  helpers `PointerOverScrollBar()` and `DragScrollBar(distance, whileDragging)`. The callback is there
  because the dragged state cannot be observed any other way — it is gone by the time the drag returns.
- `Views/EditorTabStripTests.cs` — `AStripWithMoreTabsThanRoom_DrawsNoScrollbarOverThem` asserted the
  behaviour this phase reverses and is replaced by
  `AStripWithMoreTabsThanRoom_DrawsAScrollbarBesideTheTabsRatherThanOverThem`. Three tests added:
  `AStripWhoseTabsAllFit_DrawsNoBarAndStandsExactlyAsTall`,
  `TheScrollbar_IsTheSameThicknessAtRestUnderThePointerAndUnderADrag`, and
  `DraggingTheScrollbar_ScrollsTheStrip`.

## Deviations & follow-ups

- **The padding band of design step 1 was not added** — see the Summary. It would have reserved the
  bar's space a second time. The height stability it was there to protect is instead pinned by the
  `Border`'s `MinHeight` and asserted by `AStripWhoseTabsAllFit_DrawsNoBarAndStandsExactlyAsTall`.
- **`MinHeight="37"` is arithmetic on a number declared elsewhere** — the `ListBoxItem` style's
  `MinHeight` of 30. If the tab height is ever changed, 37 has to move with it. The height-equality
  test fails if it does not, which is why the constant was left readable in the markup with a comment
  rather than hidden behind a resource key.
- **The plan's claim that the theme's template parts are not enumerable is worth retiring for
  PHASE02.** It is true of reading the compiled XAML offline and false of a live control: dumping
  `GetVisualDescendants()` off a shown `ScrollBar` in the harness gave every part name, bound and
  visibility in one run. PHASE02's step 3 hedges the same way about `CheckBox`; the same probe will
  settle it in a minute rather than sending that phase to its `LayoutTransformControl` fallback
  unnecessarily.
- **The bar's colours are Fluent's, not Enigma's — deliberate, but worth an eye.** Nothing in this
  phase sets a brush, so track and thumb follow the variant through Fluent's own keys: track `#1f1f1f`
  dark / `#e6e6e6` light, thumb `#33ffffff` / `#33000000`. The track sits close to
  `EnigmaSurfaceBrush` and reads as a band; the **thumb is a 20%-alpha fill on a 6px bar**, which may
  be fainter than wanted. Giving it a `{DynamicResource Enigma…}` brush is a one-line follow-up if it
  is — not taken here because no acceptance criterion asks for it and the plan's "nothing else about
  the strip changes" argues against it.
- **Verified by measurement, not by eye.** The suite is headless with `UseHeadlessDrawing` on, so
  there are no pixels to look at and nothing was rendered. Every geometric claim above (6px band,
  disjoint from every tab, 37px strip in both states, thumb tracking a drag) is asserted; the *look*
  of the bar — whether 6px is the right weight and the thumb is visible enough — still wants a pass in
  the running application.
- **Line endings:** no CRLF/LF churn was observed in the touched files; no action taken and none
  recommended.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx` — **succeeded, 0 warnings, 0 errors.**
- `dotnet build -v normal | grep -ci AVLN` — **0**. XAML was touched, so this was run explicitly.
- `dotnet test Enigma.MarkdownEditor.slnx` — **1961 passed, 0 failed, 0 skipped.**
- **The four new/rewritten tests were run against the pre-fix XAML and all four failed** (the file
  restored from `HEAD`, the class run, the file put back): 4 failed, 15 passed of the 19 in
  `EditorTabStripTests`. They pin the fix rather than describing it.
