# FEATURE-C8F1 — Interactive preview checkboxes

**Status: DONE** · Branch: `feature/feature-c8f1-preview-tasks` · Plan: `docs/plan/FEATURE-C8F1.md`

## Summary

A task list can now be ticked off from the pane it is read in. The glyph that stood for a task item's
box — `☐` / `☑`, drawn as the item's marker and reachable by nothing — is a real `CheckBox`, and
ticking one rewrites the single character between that task's brackets in the note being edited.

**The box remembers where it came from.** Markdig spans the whole `[x]`, so `Span.Start` is the `[`
and the character a toggle rewrites is the one after it. That offset is recorded on the checkbox as
it is built, through an attached property (`MarkdownTaskBox.SourceOffset`) rather than a `CheckBox`
subclass of our own — the same choice `MarkdownLinkRegions` makes, and for the same reason: the
control stays a plain framework `CheckBox` and keeps the theme's template with no style-key
override. The offsets are absolute because the pipeline already has `UsePreciseSourceLocation`, so a
task nested in a sub-list or written inside a block quote records where it is in the *document*
rather than where it is in the block it sits in.

**The click goes out the same seam a link does.** A new `MarkdownPreview.TaskToggled`, forwarded by
`MainWindow.axaml.cs` to `MarkdownPreviewViewModel`, which writes into the active tab's live
`TextDocument` — one `Replace(offset, 1, "x" | " ")`. Into the buffer and not the file, so the tab
goes dirty on its own, `Ctrl+Z` takes the tick back in a single step, and autosave persists it
through the path every keystroke already goes through. Nothing new saves anything.

**The answer travels back on the event.** This is the one place the toggle departs in shape from
`LinkActivated`, which is fire-and-forget: the renderer has *already* flipped the box, because the
reader clicked it and expects it to move, and only the side holding the buffer can say whether the
character was actually written. So the args carry a settable `Applied`, and a box nobody applied —
no handler attached, a handler that threw, or a document that refused — is put back the way the
document has it. The default is `false`, so every one of those three cases is the safe one.

**The stale guard is what makes the write safe.** The preview is rebuilt 250 ms after the last
keystroke, so a box on screen can describe text that has since been typed over. Before replacing
anything, `ToggleTask` reads the buffer back: `[` at `offset - 1`, `]` at `offset + 1`, and a space,
`x` or `X` between them. A position that does not still read as a task box is one the ViewModel no
longer understands, so it writes nothing, logs at Debug, and lets the imminent re-render show the
reader what the document really says.

**Keyboard came free, and the layout did not.** `IsCheckedChanged` drives the toggle rather than
`Click`, so the pointer and `Space` on a focused box are literally the same path — and the glyph
this replaced could not be reached by a keyboard at all. The theme's checkbox, though, is built for
a form: it pads a 20×20 box out to a 32-tall touch target, which made every task row 32 px against
a bullet row's 21 and stepped a mixed list at the first task. The height is overridden on this one
control by putting `CheckBoxMinHeight` in the checkbox's own `Resources` — the key the Fluent
template sizes the box's cell from, and the only reachable handle on it, since the grid holding it
is unnamed and private to the template.

## Files/modules touched

**Created — Desktop**

- `Controls/MarkdownTaskBox.cs` — the `SourceOffset` attached property and `Attach`, which records
  the offset and wires the toggle, including the re-entrancy guard around putting a refused box back.
- `Controls/MarkdownTaskToggledEventArgs.cs` — `SourceOffset`, `IsChecked`, and the `Applied` flag
  the handler answers on.

**Modified — Desktop**

- `Controls/MarkdownVisualBuilder.cs` — the `onTaskToggled` constructor callback; `Marker` split so
  a task item's marker is `TaskBox` (a real `CheckBox`) and every other item's is the old text
  marker; the `TaskBoxSize` / `TaskBoxHeightKey` constants; the two comments in `Append` and
  `Convert` that pointed at `Marker` for the task box.
- `Controls/MarkdownPreview.cs` — the `TaskToggled` event, `OnTaskToggled` (which reports a throwing
  handler as a render failure and answers `false`), and the extra builder argument.
- `Views/MainWindow.axaml.cs` — subscribing `OnPreviewTaskToggled` and forwarding it to the pane's
  ViewModel; the `Enigma.MarkdownEditor.Desktop.Controls` using.
- `ViewModels/MarkdownPreviewViewModel.cs` — `ToggleTask`, the private `HoldsTaskBox` guard, and the
  `AvaloniaEdit.Document` using.

**Created — tests**

- `Desktop.UnitTests/Controls/MarkdownTaskBoxTests.cs` — the twelve cases below.

**Modified — tests**

- `Desktop.UnitTests/ViewModels/MarkdownPreviewViewModelTests.cs` — eight cases for `ToggleTask`, and
  the `StateOffsets` helper that reads a document's task-box positions off the source.
- `Desktop.UnitTests/Controls/MarkdownPreviewTests.cs` — `ATaskList_ShowsABoxInsteadOfABulletAndSays
  NothingTwice` asserted the two glyphs; it now asserts that each task item's marker is a `CheckBox`,
  and what the box itself carries and reports moved to `MarkdownTaskBoxTests`.
- `Desktop.UnitTests/Fixtures/04-tasks.md` — one quoted task added, so the manual visual pass covers
  the block-quote case this dev's acceptance criteria name. The corpus is still ten files.

**Modified — docs**

- `README.md` — a *Preview* bullet for the tickable task list. Folded in from the documentation
  freshness sweep, at your choice.
- `docs/RELEASE.md` — a manual smoke check for ticking a task from the preview, including the nested
  and quoted cases. Same sweep.

## Deviations & follow-ups

- **The toggle is driven by `IsCheckedChanged`, not by `Click`.** The plan says "raised from the
  checkbox's toggle" without choosing. `Click` fires only from real interaction and would have needed
  no re-entrancy guard; `IsCheckedChanged` is the single point every path — pointer, `Space`, and
  anything added later — passes through, and it is what a test can drive without simulating how a
  button decides it was pressed. The guard it costs is four lines, and the pointer and keyboard are
  still asserted through real headless input rather than through the property.
- **The event args carry the answer back.** The plan asks for "a new `MarkdownPreview.TaskToggled`
  event mirroring `LinkActivated`" *and* for the checkbox to revert on a mismatch. Those two pull
  apart: `LinkActivated` is `EventHandler<string>` and tells the raiser nothing. `Applied` on the
  args is the smallest thing that satisfies both — see *Summary*.
- **The checkbox height needed a local resource override, which the plan does not anticipate.** The
  plan says only "sized and aligned to sit in the existing marker column". The column was never the
  problem — 22 + 4 either way, and the content column starts at the same x for a task and a bullet
  without any intervention. The *height* was: 32 against 21. `MinHeight = 0` on the control does
  nothing, because the template's box cell carries a hard `Height`. This was found by a layout test
  rather than by eye, and that test is now the guard on it.
- **`RELEASENOTES.md` was deliberately left alone** and was not offered in the sweep: it records what
  shipped in 1.0.0, and the next release's notes belong to the release skill rather than to a dev.
- **Both guards were mutation-checked, and one of them is more load-bearing than expected.**
  Bypassing `HoldsTaskBox` fails the two stale-offset cases, as designed. Removing the `reverting`
  guard does not fail a test — it **crashes the test host with a stack overflow** (exit code
  `0xC00000FD`), because a refused toggle reverts, the revert reports as a toggle, and it is refused
  again, forever. In the running application that is the window going down under a single click on a
  stale checkbox. Both the implementation and the tests were restored before the final build, and the
  full suite was re-run green afterwards.
- **What it looks like is still a manual pass.** The tests assert the marker column, the row height
  and every behaviour; whether a 20 px box reads well beside prose at an unusual preview font size is
  a judgement no headless test makes. Worth a look: open `04-tasks.md`, tick the plain, the wrapped,
  the nested and the quoted task in both theme variants, and check the box sits on the text's line
  rather than above or below it at a large and a small *Preview font size*.
- **No sub-agents were used.** One rendering change, one event, one ViewModel method and their tests
  — tightly coupled through a single offset, and nothing here splits cleanly enough to be worth it.
- **Line endings:** no CRLF/LF churn was observed in this dev's diff, and nothing was done about line
  endings either way, per the workflow's recommendation-only rule.
- **Follow-up, not done:** everything the plan lists as out of scope stays out — editing any other
  markdown from the preview, toggling a sub-tree of tasks at once, a "check all" affordance, and a
  read-only preview mode. One thing worth a thought later: the box does not scale with the *Preview
  font size*, so at a large reading size it stays 20 px beside taller text. That is what most
  renderers do and it is not a regression, but it is now a visible choice rather than an accident.

## Build/test evidence

- `dotnet build -c Release -p:ArtifactsPath=<temp>` — succeeded, **zero warnings**
  (`TreatWarningsAsErrors` is on solution-wide, so a warning would have failed it). The output was
  redirected because a copy of the app built earlier is running from
  `src/Enigma.MarkdownEditor.Desktop/bin/Release` and holds a lock on its own `.dll`; the redirect
  builds the same sources to a different folder rather than closing your editor. `dotnet build`
  (Debug, in-repo output) — also succeeded with zero warnings.
- `dotnet test -c Release -p:ArtifactsPath=<temp>` — **1836 passed, 0 failed, 0 skipped**. The same
  1836 pass in Debug. This dev adds **20 cases**.
- `MarkdownTaskBoxTests` — **12 cases**, every render inside a shown window:
  - a task item marked with a real box showing what the document says, checked and unchecked;
  - each box recording the offset of the character between its own brackets;
  - a nested task and a quoted one recording where they are in the whole document, not in their
    block, with the right state for each;
  - a checkbox this renderer did not build carrying `NoOffset`;
  - ticking a box reporting that offset and `IsChecked = true`, once;
  - unticking one asking for the opposite state;
  - a toggle nobody applied putting the box back **and not asking a second time** — the guard whose
    absence is a stack overflow;
  - a preview with no handler at all leaving its box showing the document's state;
  - a handler that throws putting the box back and being reported through `RenderFailed`;
  - a real left click through the headless window ticking the box and reporting offset 3;
  - `Space` on a focused box doing exactly what the click did — same offset, same result;
  - a task and a bullet in one list keeping the same marker column (identical content-column x) and
    the same row height to within a pixel.
- `MarkdownPreviewViewModelTests` — **8 new cases**, through a real `DocumentManagerViewModel` over
  real files:
  - ticking a task rewriting exactly the one character between its brackets, and the tab going dirty;
  - unticking one writing the space back;
  - one `Undo` restoring the original text and the clean flag — the tick is one undo entry, not two;
  - a nested task and a quoted one each toggling their own character and no other;
  - an offset that no longer holds a task box changing nothing and returning `false`;
  - seven offsets that are outside the buffer or beside the wrong characters, all refused rather than
    clamped, with the buffer untouched;
  - a toggle with nothing open refused rather than thrown;
  - with autosave on, the tick arming the document manager's own timer and that timer writing the
    file — no save call of this feature's own.
- `FixtureCorpusTests` — still green over the ten fixtures, including `04-tasks.md` with the quoted
  task added.
- **Not covered:** how the box looks beside prose at unusual preview font sizes — see *Deviations &
  follow-ups*.
