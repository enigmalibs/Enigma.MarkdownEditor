# BUG-18AD-PHASE02 — Code-span chip padding

**Status: DONE** · Phase 02 of 2 · Branch: `bugfix/bug-18ad-phase02-code-span-padding`
Plan: `docs/plan/BUG-18AD.md`

## Summary

A code chip now has room between its text and its edge, and a copy out of the pane still yields
exactly what the note said.

- **The padding is a no-break space either side, inside the run's own text.** An Avalonia inline has
  no padding, no margin and no corner radius to set — `Run` carries a background, a foreground and a
  font, and that is the list. Putting the space in the run is what makes the chip one rectangle: the
  fill is the run's own background, so padding and glyphs are drawn as a single box with nothing to
  align. `U+00A0` because it is non-breaking (a line can never be broken between a chip and its own
  padding) and because it is the one fixed-width space every font carries — a thin or figure space
  would come out as a missing-glyph box on whichever monospace family the reader chose.
- **The clipboard is paid back by index.** The two characters are the renderer's, not the note's, and
  this is an editor whose readers paste commands into shells. `MarkdownCodePads` records where each
  pad landed — measured off `InlineCollection.Text` before and after the inline, exactly as
  `MarkdownLinkRegions` measures a link range — and `MarkdownPreview.BuildCopyRequest` takes those
  indices back out of a drag selection. By index and never by searching for the character, because
  `&nbsp;` in a note renders as a real `U+00A0`, and deleting the reader's own text to tidy up after
  the renderer is not a trade worth making.
- **The record threads through the walk the way link ranges already do**, so a chip nested in
  emphasis or inside a link records where it actually landed rather than where its own span begins.
  It is attached to a block only when that block holds a chip, so a paragraph of prose carries
  nothing and `Strip` hands its selection straight back.
- **The whole-note copy is untouched**: it is projected from the markdown by `MarkdownPlainText`, not
  read off the visuals, so it never saw a pad in the first place.

## Files/modules touched

**Created — `src/Enigma.MarkdownEditor.Desktop`**

- `Controls/MarkdownCodePads.cs` — the per-block record: `Pad` (the `U+00A0` constant, written as an
  escape so it is visible in source), a `Pads` attached property on `TextBlock` with the house
  `GetPads`/`SetPads` pair, `IsPad`, and `Strip` in an instance and a static form. Indices are held in
  a `HashSet<int>` rather than a sorted list — they arrive in order today, because the renderer walks
  the inlines in order, but a copy should not depend on that staying true.

**Modified — `src/Enigma.MarkdownEditor.Desktop`**

- `Controls/MarkdownVisualBuilder.cs` — `Code()` wraps the content in `MarkdownCodePads.Pad` either
  side and marks the run with `MarkdownPreviewTheme.CodeSpanClass`; `Prose()` builds a
  `MarkdownCodePads` beside its `MarkdownLinkRegions` and attaches it only when there is one to
  attach; `Append()` records both pad indices off the same two measurements a link range is read
  from; `Append`, `Convert`, `Emphasis` and `Link` each carry the collector, exactly as they already
  carry the links.
- `Controls/MarkdownPreview.cs` — `BuildCopyRequest()` strips the selection through
  `MarkdownCodePads` before handing it to `MarkdownCopyRequest.ForSelection`, using
  `Math.Min(SelectionStart, SelectionEnd)` because a selection can be dragged backwards. A selection
  that was nothing but padding is treated as no selection at all, which means the note — the
  behaviour that path already had for an empty selection.
- `Controls/MarkdownPreviewTheme.cs` — `CodeSpanClass`, beside the other class names.

**Modified — `tests/Enigma.MarkdownEditor.Desktop.UnitTests`**

- `Controls/PreviewTree.cs` — `CodeSpans(TextBlock)` finds chips by their class rather than by their
  text (a chip's text is no longer the code alone), `Unpadded(Run)` gives a chip's text as the
  document wrote it, and `Text(TextBlock)` goes through `Unpadded`, so a test asserting what a block
  "reads as" sees what the reader sees.
- `Controls/MarkdownPreviewTests.cs`, `Controls/MarkdownPreviewTypographyTests.cs` — the four places
  that located the chip with `run.Text == "code"` / `"family"` now use `PreviewTree.CodeSpans`; the
  table test also pins that the chip's unpadded text is still `family`.
- `Controls/MarkdownCodePadsTests.cs` **(new)** — the chip carries one pad either side; the block
  records both where they landed, including a chip that opens the paragraph (index 0) and one nested
  in emphasis; a paragraph with no chip carries no record; `Strip` over a whole-block selection, over
  selections starting at and past the opening pad, and over one that missed every pad; a link wrapped
  around a chip still answers over every character of it; and the argument guards. Every record is
  built by rendering, never by hand — the collector's `Add` is internal to the renderer, and a test
  that assembled one itself would only be asserting against indices it had just invented.
- `Controls/MarkdownPreviewCopyTests.cs` — a copy spanning a chip leaves the padding behind; a copy of
  prose holding an authored `&nbsp;` keeps it; a paragraph with no chip is handed straight back; and a
  selection of nothing but padding asks for the note rather than writing an empty clipboard.

## Deviations & follow-ups

- **One deviation, in the tests rather than in the plan's design.** The plan's step 7 listed the new
  tests; it did not anticipate that four *existing* tests locate the code run by its text, or that
  `PreviewTree.Text` would start reporting the padding. Both were fixed at the helper rather than by
  restating the padded string in each test: `PreviewTree.Text` strips through `Unpadded`, and the
  lookups moved to the new class-based `CodeSpans`. Baking the padded string into tests about
  font families would have spread a rendering detail across files that have nothing to do with it.
- **Follow-up — the pad is one space, not a fraction of an em.** A monospace family renders `U+00A0`
  as one full cell, which is generous next to the ~0.4em a web renderer would use, and a proportional
  code family renders it narrower. Making it proportional would mean measuring the font at build
  time, which needs a second box beside the run — the very thing this phase avoids. Worth revisiting
  only together with the corner radius, and both want a renderer that draws the paragraph itself.
- **Follow-up — the chip still has no corner radius**, for the reason PHASE01 recorded.
- **`InternalsVisibleTo` is deliberately still absent.** `MarkdownCodePads.Add` and `Attach` stay
  internal and the tests build every record by rendering; nothing here was made public to be tested.
- **Line endings:** nothing to recommend. `.gitattributes` pins `* text=auto eol=lf` with explicit
  `*.cs` and `*.md` rules, the working tree is LF throughout, and this phase's diff carries no
  line-ending churn.
- **Documentation freshness sweep:** nothing edited. `README.md:92` says "a selection inside one
  paragraph copies just that range", which this phase makes more true rather than less;
  `README.md:76` lists code spans among the supported elements without describing how they are drawn;
  and the `Ctrl+A` / `Ctrl+C` row of the shortcut table is unchanged. No `CLAUDE.md`, `AGENTS.md`,
  `CHANGELOG.md` or `CONTRIBUTING.md` exists.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release` — **0 warnings, 0 errors**.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` — **3582 passed, 0 failed,
  0 skipped** (3568 before this item; 14 added across the two phases).
- One fix cycle used, and it was the four pre-existing tests described under *Deviations* — found by
  the phase's first test run, green on the next.
- Acceptance criteria: a rendered `` a `code` span `` carries one pad either side inside the
  background-bearing run; a selection across a chip copies the code with no padding while an authored
  `&nbsp;` in the same sentence survives; a link containing a chip still hit-tests over its whole
  range.
