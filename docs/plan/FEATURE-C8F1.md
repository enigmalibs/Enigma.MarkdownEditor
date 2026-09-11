# FEATURE-C8F1 — Interactive preview checkboxes

**Status: DONE** · Single phase · Roadmap: `docs/roadmap.md`
Branch at build time: `feature/feature-c8f1-preview-tasks`.

## Objective

Let a task list be ticked off from the preview pane. Clicking a checkbox writes the single character back into the document being edited, so it behaves exactly as if it had been typed.

## Context & constraints

- Task items render today as read-only glyphs: `MarkdownVisualBuilder.Marker` returns `☐` / `☑` as the list item's marker, and the `TaskList` inline itself is skipped in `Append` (it also swallows the space that follows the box). The current code comment states the glyphs are read-only "because the checkbox is in the document" — this item is what changes that.
- The click-to-ViewModel seam already exists and should be copied rather than reinvented: `MarkdownPreview.LinkActivated` is raised by the control, forwarded by `MainWindow.axaml.cs`, and acted on by `MarkdownPreviewViewModel`. The control decides nothing about what a click means.
- The preview is **debounced**: `MarkdownPreviewViewModel.LiveUpdateDelay` is 250ms, so the rendered tree can be up to a quarter second behind the editor buffer — and the buffer may have been edited by hand in between. Any offset captured at render time is therefore a *stale* offset by the time a click arrives.
- Markdig's `TaskList` inline carries a `Span`; `Span.Start` is the offset of `[`, so the state character sits at `Span.Start + 1`. The pipeline already has `.UseTaskLists()`.
- The write target is the active tab's live `TextDocument` (AvaloniaEdit), not the file: that puts the change on the undo stack, marks the tab dirty and lets autosave handle persistence — no second save path to keep consistent.

## Design

**Rendering.** `MarkdownVisualBuilder.Marker` returns a small real `CheckBox` for a task item instead of the glyph: focusable, no content, `IsChecked` from `TaskList.Checked`, sized and aligned to sit in the existing marker column so list indentation does not move. The source offset of the state character is recorded on the visual.

**Event.** A new `MarkdownPreview.TaskToggled` event mirroring `LinkActivated`, carrying the source offset and the requested state. Raised from the checkbox's toggle. `MainWindow.axaml.cs` forwards it to `MarkdownPreviewViewModel`, which asks `DocumentManagerViewModel.ActiveDocument` to apply a **one-character** `TextDocument.Replace(offset, 1, "x" | " ")`.

**Stale guard — not optional.** Before replacing, verify the buffer actually holds a task box at the recorded position: `[` at `offset - 1`, a space / `x` / `X` at `offset`, `]` at `offset + 1`. On any mismatch the click is a no-op — the checkbox visual reverts and a Debug-level line is logged. Without this guard, a click on a render that is 250ms stale (or on a buffer edited since) writes a character into the middle of unrelated text.

**Optimistic visual.** The checkbox flips immediately on click; the debounced re-render 250ms later confirms it, so the lag is invisible.

**Keyboard.** The checkbox is focusable and Space toggles it — a small accessibility gain over the current glyph, which nothing can reach.

## Acceptance criteria

- Clicking a checkbox in the preview flips it and changes exactly one character in the editor buffer; the tab becomes dirty; Ctrl+Z in the editor undoes it in one step.
- Space on a focused checkbox does the same thing as a click.
- A toggle whose recorded offset no longer holds a task box changes nothing, and the checkbox returns to the state the document says.
- Task items nested in sub-lists and inside block quotes toggle the correct character.
- With autosave on, the toggle is persisted by the existing autosave path — no new save call.
- `Desktop.UnitTests` (headless): the builder emits a `CheckBox` with the right `IsChecked` and the right recorded offset for checked/unchecked/nested/quoted items; the toggle applies the expected single-character edit; a mismatched offset is a no-op; undo restores the original text.
- Build clean with zero warnings; the whole suite passes.

## Dependencies

None. Independent of the other three items in this batch, though it and `FEATURE-E340` PHASE02 both touch `MarkdownVisualBuilder` — whichever is built second rebases onto the other.

## Out of scope

Editing any other markdown from the preview (text, links, tables) · toggling a whole sub-tree of tasks at once · a "check all" affordance · a read-only preview mode toggle.
