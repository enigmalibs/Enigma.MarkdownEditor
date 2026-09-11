# BUG-8F2A-PHASE02 — Preview checkbox alignment & size

**Status: DONE** · Phase 02 of 2 · Branch: `bugfix/bug-8f2a-phase02-checkbox-alignment`
Plan: `docs/plan/BUG-8F2A.md`

## Summary

A task item's checkbox in the preview sat visibly above the text beside it, and got worse as the
preview's font size or line height was raised. Both halves of the plan's root cause were confirmed
against the code before anything was changed, and both are fixed.

**Alignment.** `MarkdownVisualBuilder.TaskBox` built a box that was `VerticalAlignment.Top` in
column 0, while column 1 held prose whose text is centred inside its own line box rather than sat at
the top of it. The box was therefore out by `(lineHeight − boxSize) / 2` — an error that grows with
both settings, which is exactly why the report named them. At the shipped 18pt × 1.5 default the
line box is 27 tall and its text centres 13.5 down; the 20px box centred at 10. The box is now
pushed down by that same half-difference through a top margin (`TaskBoxOffset`), clamped at zero so
a box held at its floor under a very tight line height is never pulled above its row.

Deliberately a margin and not `VerticalAlignment.Center` with the line's height: centring would put
the box wherever Fluent's template centres it inside its own bounds, which is a private detail this
fix must not be hostage to.

**Size.** The box was a fixed 20px while the prose beside it is a 6–72pt setting, so a task and a
bullet in the same list were drawn to two different scales. `MarkdownPreviewTypography` gains a
derived `TaskBoxSize` — `Math.Clamp(Math.Round(BodyFontSize * 1.1), 12, 36)` — beside `AsideFontSize`
and `LineHeightAt`. The 1.1 makes the shipped 18pt default come out at exactly the 20 the renderer
already drew, so the default is continuous and nobody's preview changes. Derived rather than a new
setting: nobody asked to size a checkbox, and one number that follows the prose is what "aligned with
the text" means.

The marker column follows: `MarkerColumnWidth` stops being the constant `22` and becomes a floor,
`max(22, TaskBoxSize + 2)`, read by `Marker` as well as by `TaskBox` so a grown box cannot indent
tasks one step past the bullets under them. The grid column is still `Auto`, so a wide ordered
marker such as `10.` grows past it as before.

**How the box is scaled — attempt (a) was tried and disproven; the plan's fallback (b) is what
shipped.** The plan allowed a style on the template part "if that part turns out to be reachable in
Avalonia 12.1.1". The parts *are* named and reachable — `Border#NormalRectangle` inside
`Grid#RootGrid`, with the tick as `Path#CheckGlyph` in a `Viewbox` — but styling them does nothing:
the Fluent template assigns `Width="20"` / `Height="20"` **as local values**, and in Avalonia a local
value outranks every style, so a `/template/` selector reaching `NormalRectangle` is simply ignored.
`RootGrid`'s first column is pinned at a literal `20` on top of that, and `ColumnDefinitions` is not
a styled property, so it cannot be moved by a setter either. Both were verified empirically against
a real headless `CheckBox`, not inferred. So the box is scaled by wrapping it in a
`LayoutTransformControl` with a uniform `ScaleTransform` of `TaskBoxSize / 20` — which takes the box,
its tick and its hit area together and asks nothing of the template's internals.
`MarkdownTaskBox.Attach` and the `SourceOffset` attached property are untouched, and the wrapper is
transparent to hit testing: the existing click and `Space` tests pass unchanged, because
`TranslatePoint` maps through the transform.

## Files/modules touched

**Modified — `src/Enigma.MarkdownEditor.Desktop`**

- `Controls/MarkdownPreviewTypography.cs` — new derived `TaskBoxSize`, placed between
  `AsideFontSize` and `LineHeightAt`, with an XML doc stating why it is derived rather than a
  setting, why the multiplier is 1.1, and what each end of the clamp is for. `using System;` added
  (the file had no using directives and `ImplicitUsings` is disabled solution-wide).
- `Controls/MarkdownVisualBuilder.cs`
  - `TaskBox` returns a `LayoutTransformControl` wrapping the `CheckBox`. `MinWidth`, `Margin` and
    `VerticalAlignment` move from the box to the wrapper — `MinWidth` **has** to, or the column
    minimum would come back out of the transform multiplied by it. The remarks gained two paragraphs:
    why the scale is a transform rather than a restyled template part, and why the vertical placement
    is arithmetic rather than an alignment.
  - New `TaskBoxOffset(double size)` — the half-difference, floored at zero, with the reason in its
    remarks.
  - New `MarkerColumnWidth` derived property (`max(MarkerColumnFloor, _type.TaskBoxSize + 2)`),
    replacing the `MarkerColumnWidth = 22` constant, which becomes `MarkerColumnFloor`. Read by both
    `Marker` and `TaskBox`.
  - The `TaskBoxSize = 20` constant is renamed `ThemeTaskBoxSize` and its comment says what it now
    is: the theme's own number, and the denominator the reader's size is scaled against — not a size
    this renderer chooses.
  - New `MarkerGap = 4` constant for the marker's right margin, which was a bare literal in `Marker`
    and is now needed in `TaskBox` too.

**Modified — `README.md`**

- The Preview group's type bullet enumerates what the reading size drives; the checkbox now joins
  that list, so the bullet says so. (Accepted in the documentation freshness sweep; the
  `(unreleased)` `RELEASENOTES.md` section was offered and declined as release-time work.)

**Modified — `tests/Enigma.MarkdownEditor.Desktop.UnitTests`**

- `Controls/MarkdownTaskBoxTests.cs` — **6 new test cases**:
  - `ATaskBox_IsCentredOnTheFirstLineOfItsOwnText` (3 cases: 14 × 1.5, the shipped 18 × 1.5, and
    32 × 1.9) — the box's laid-out centre and the first line's centre agree within 1px.
  - `TheBox_IsDrawnToTheScaleOfTheProseBesideIt` — `size(8pt) < size(18pt) < size(32pt)`, and 18pt is
    still 20.
  - `ATaskABulletAndANumber_StartAtTheSameX` (2 cases: 14pt, and 32pt where the box does outgrow the
    old 22 floor) — the marker column and the prose column start at one x for a task, a bullet and a
    number.
  - Plus a second `Hosted` overload that takes a typography and lays the preview out, and a `Rows` /
    `BoxSize` pair, for the measuring tests. The original `Hosted` is unchanged, so the pointer and
    keyboard tests are untouched.
- `Controls/MarkdownPreviewTypographyTests.cs` — **1 new test**,
  `TheTaskBoxSize_FollowsTheProseAndIsHeldBetweenTwoLimits`: 18pt → 20 (the continuity that keeps the
  default unchanged), 12pt < 24pt, and both ends of the clamp at the 6–72pt the setting allows.
- `Controls/MarkdownPreviewTests.cs` — `ATaskList_ShowsABoxInsteadOfABulletAndSaysNothingTwice` now
  finds the box by walking the marker cell instead of asserting `IsType<CheckBox>(row.Children[0])`.
  See *Deviations*.

## Deviations & follow-ups

- **Plan step 3(a) does not work, and the completion doc is where the plan said to record which
  attempt was taken: (b).** The finding is worth keeping because the plan's premise was wrong in an
  interesting direction — the Fluent template parts *are* enumerable and *are* named, so the
  constraint is not visibility but precedence: the sizes are local values, and no style can beat a
  local value. Any future attempt to restyle a themed control's internals in this codebase should
  check that first.
- **One existing test had to be changed**,
  `MarkdownPreviewTests.ATaskList_ShowsABoxInsteadOfABulletAndSaysNothingTwice`. The plan required
  that a wrapper "must not break the tests that find the box by walking the tree", and it does not —
  every such test passed untouched. This one did not walk: it indexed `row.Children[0]` and asserted
  the exact type, which is now the wrapper. It has been changed to assert that the marker cell holds
  exactly one real `CheckBox`, which is what the test is actually about; the comment says why. No
  assertion was weakened — `Assert.Single` over the whole cell is strictly more than the old
  `IsType` on one child.
- **Three renames inside `MarkdownVisualBuilder`** that the plan did not spell out but its design
  implies: `TaskBoxSize` → `ThemeTaskBoxSize` (the plan calls the old constant "retired"; it is still
  needed as the scale's denominator, so it is renamed rather than deleted), `MarkerColumnWidth` const
  → `MarkerColumnFloor` const plus a derived property of the old name, and the extraction of
  `MarkerGap`.
- **Not verified by eye.** Every acceptance criterion in the plan is measured by a test — there is no
  "thickness / colour / weight" left over that only an eye can confirm. What has *not* been done is a
  human look at whether 1.1 reads well at the extremes of the 6–72pt range; the arithmetic is pinned,
  the aesthetic judgement is the user's. Running the real app to make it means backing up and
  restoring the real `%APPDATA%\Enigma.MarkdownEditor`, as recorded for `BUG-7AB2-PHASE05`.
- **No line-ending noise.** The repository is normalized to LF by `.gitattributes`
  (`* text=auto eol=lf`), all eight touched files are LF, and the diff carries no whole-file churn.
  Nothing to recommend.
- **Follow-up:** none surfaced. The plan's own *Out of scope* list already excludes a setting for the
  checkbox size, and nothing here suggests one is wanted.

## Build/test evidence

- `dotnet build -c Release -v normal --no-incremental`: **succeeded, 0 errors, 0 warnings**, and no
  `AVLN*` diagnostic anywhere in the normal-verbosity log. (No XAML was touched this phase — the whole
  change is two C# files — so the `AVLN` sweep was a formality, but it was run.)
- `dotnet test` across the solution, Debug and Release: **1968 passed, 0 failed, 0 skipped**
  (`Enigma.MarkdownEditor.Core.UnitTests` + `Enigma.MarkdownEditor.Desktop.UnitTests`). **7 test cases
  are new**; 1961 before this phase.
- **The new tests fail against the unfixed code**, which is what makes them regression tests rather
  than descriptions. Verified by stashing `MarkdownVisualBuilder.cs` alone and re-running:
  `ATaskBox_IsCentredOnTheFirstLineOfItsOwnText` fails at 18 × 1.5 and at 32 × 1.9, and
  `TheBox_IsDrawnToTheScaleOfTheProseBesideIt` fails outright. The 14 × 1.5 case *passes* against the
  old code — the error there is only 0.5px — which is the plan's root cause reproduced exactly: the
  defect is a function of the two settings, invisible at the small end and plain at the shipped
  default.
- **Acceptance criteria, one by one.**
  - Box centred on the first line of its own text, within 1px, at the shipped default and two other
    size / line-height combinations ✓ — `ATaskBox_IsCentredOnTheFirstLineOfItsOwnText`, three cases.
  - The box grows with the preview font size, and at 18pt is the 20 it is today ✓ —
    `TheBox_IsDrawnToTheScaleOfTheProseBesideIt` asserts the measured heights strictly increase and
    pins the 18pt one at 20. By the formula the three are 12 (8pt, at the clamp), 20 and 35.
  - A list mixing tasks with bullets and ordered items has one left edge ✓ —
    `ATaskABulletAndANumber_StartAtTheSameX`, at 14pt and at 32pt, asserting one distinct x for the
    marker column *and* one for the prose column across all three rows.
  - Ticking still rewrites the one character between the brackets and a refused toggle still puts the
    box back — the `FEATURE-C8F1` behaviour unchanged ✓ — the whole of `MarkdownTaskBoxTests`'
    existing suite passes untouched, including the pointer click and the `Space` key driven through
    the headless window, which are what prove the wrapper is transparent to input.
  - Build clean with zero warnings, no `AVLN`, whole suite passes ✓ — as above.
