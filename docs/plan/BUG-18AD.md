# BUG-18AD — Preview code spans barely visible

**Status: IN PROGRESS** · 2 phases · Roadmap: `docs/roadmap.md`
**Type:** BUG · **Branch:** one per phase, cut from the run branch at build time.
**Run:** bugfix/2026-09-11-preview-code-spans

## Objective

A backtick code span in the preview pane is supposed to read as a chip inside the sentence. Today it
reads as prose: the fill behind it is one step off the fill behind the pane, and there is no space
between the text and the edge of that fill. Two phases put both back — PHASE01 the contrast, PHASE02
the breathing room — and neither changes what a copy out of the pane produces.

## Context & constraints

Established by reading the code, not assumed.

- **The span is a bare `Run`.** `MarkdownVisualBuilder.Code()` (`src/…/Controls/MarkdownVisualBuilder.cs:852`)
  builds a `Run`, sets the code family and size on it, and binds `TextElement.Background` to
  `MarkdownPreviewTheme.CodeBackgroundKey`. Nothing else.
- **That key is `"EnigmaSurfaceHighBrush"`**, and `CodeBlockBackgroundKey` / `TableHeaderBackgroundKey`
  are documented aliases of it (`MarkdownPreviewTheme.cs:53-72`). The preview pane itself is painted
  `EnigmaSurfaceBrush` (`Views/MainWindow.axaml:443`). `EnigmaSurface` and `EnigmaSurfaceHigh` are
  adjacent steps of one surface ramp, so the chip sits one ramp step off its own background — which
  is the whole defect. A fenced block survives the same fill because it carries a 1 px
  `EnigmaBorderSubtleBrush` border and `Padding = 10,8`; a span carries neither.
- **Avalonia has no padding on an inline.** `Run : Inline : TextElement` exposes background,
  foreground and font properties only. There is no `Padding`, no `Margin` and no `CornerRadius` to
  set, so padding has to come from either an `InlineUIContainer` or from glyphs inside the run.
- **An `InlineUIContainer` contributes zero characters** to `InlineCollection.Text` — the renderer
  already says so about images (`MarkdownVisualBuilder.cs:743`, *"none for an image"*). Making the
  chip, or its padding, a container therefore drops it out of
  `SelectableTextBlock.SelectedText`, which is what `MarkdownPreview.BuildCopyRequest()`
  (`Controls/MarkdownPreview.cs:615`) puts on the clipboard for a drag selection. A code span that
  disappears from a copied sentence is a worse defect than the one being fixed.
- **Character ranges are measured off the collection, never counted off the markdown.**
  `MarkdownVisualBuilder.Append()` reads `target.Text.Length` before and after each inline
  (`:721-746`) and `MarkdownLinkRegions` records link ranges from those two numbers. Padding
  characters therefore cost link hit-testing nothing, *and* the same two numbers give the exact index
  of each pad character for free.
- **Every piece of prose goes through one funnel.** `Prose()` (`:690`) is the only place that creates
  a `MarkdownLinkRegions`, attaches it and returns the block; headings (`:214`), paragraphs (`:251`)
  and therefore list items and table cells all reach it. A second per-block collector attaches in
  exactly the same place.
- **The project owns no resource dictionary.** `App.axaml` merges
  `avares://Enigma.Avalonia.Desktop/Themes/Fluent.axaml` and nothing else, and
  `HeadlessTestApp.Initialize()` (`tests/…Desktop.UnitTests/HeadlessTestApp.cs`) mirrors that merge by
  hand so the suite resolves the same brushes. A dictionary of our own has to be merged **after** the
  Enigma one, in **both** places, or the tests assert against a palette the app does not have.
- **Theme brushes are always `{DynamicResource …Brush}`**, never `StaticResource` and never a literal
  colour in C#: the renderer binds keys precisely so a variant switch repaints without re-rendering
  (`MarkdownPreviewTheme.BindBrush`, and the test
  `SwitchingVariant_RepaintsWithoutRebuildingTheTree`).
- **`MarkdownPreviewTheme.BrushKeys` is asserted** to resolve to an `IBrush` in **both** variants and
  to hold no duplicates (`MarkdownPreviewThemeTests`). A new key joins that list and must therefore be
  defined under both `Dark` and `Light`.
- **`TreatWarningsAsErrors` is on solution-wide** (`Directory.Build.props`). The bar is a 0-warning
  build. `AVLN*` XAML diagnostics come from an MSBuild task and are **not** promoted by it, so a
  phase that touches XAML greps the build log for `AVLN` as well.
- **`dotnet test --solution Enigma.MarkdownEditor.slnx`** — the `--solution` flag is required under the
  Microsoft Testing Platform runner pinned in `global.json`.
- **The headless suite has no Skia**, by deliberate choice (`HeadlessTestApp`: *"nothing here needs
  pixels"*). Nothing below may depend on capturing a frame; what the chip *looks* like stays a manual
  pass, exactly as the existing theme tests already say of colour.
- Baseline before either phase: `dotnet build Enigma.MarkdownEditor.slnx -c Release` → 0 warnings,
  0 errors.

## Out of scope

- The fenced code block and the table header keep `EnigmaSurfaceHighBrush`. Both already read
  correctly, and re-colouring them is a visual change nobody asked for.
- No corner radius on the chip: a text run cannot have one, and faking it with an `InlineUIContainer`
  costs the copy path (above). Recorded as a follow-up instead.
- No change to the code span's **foreground**, font family or size — all three are already settings.
- No change to `MarkdownPlainText`, to the HTML copy flavour, or to anything in `Core`.

---

## PHASE01 — Code-span chip gets its own colour

**Status: DONE**

### Symptom

In either theme variant, a `` `code span` `` in a paragraph is almost indistinguishable from the
prose around it: the fill behind it differs from the pane's fill by one step of the surface ramp.

### Root cause

`MarkdownPreviewTheme.CodeBackgroundKey` is `"EnigmaSurfaceHighBrush"`, the step immediately above
`EnigmaSurfaceBrush`, which is what the preview pane is painted with. The palette that ships in
`Enigma.Avalonia.Desktop` has no step meant to sit *inside* a paragraph, so no existing key is the
right answer.

### Steps

1. Add `src/Enigma.MarkdownEditor.Desktop/Themes/PreviewBrushes.axaml`: a `ResourceDictionary` whose
   `ResourceDictionary.ThemeDictionaries` defines `MarkdownPreviewCodeSpanBackgroundBrush` under both
   `Dark` and `Light`.
   - **Translucent, not opaque**: the chip lands on the pane (`EnigmaSurface`), inside a blockquote
     (`EnigmaSurfaceLow`) and inside a table header (`EnigmaSurfaceHigh`). One alpha reads correctly
     over all three; three opaque colours would not.
   - Dark `#33FFFFFF` (white 20 %), Light `#1F000000` (black ≈ 12 %).
2. Merge it in `App.axaml`, **after** the Enigma `ResourceInclude`, with a comment saying why the
   order matters.
3. Merge it the same way in `HeadlessTestApp.Initialize()`, so the suite resolves what the app does.
4. In `MarkdownPreviewTheme`, stop aliasing: `CodeBackgroundKey` becomes
   `"MarkdownPreviewCodeSpanBackgroundBrush"`, and `CodeBlockBackgroundKey` /
   `TableHeaderBackgroundKey` are pinned to `"EnigmaSurfaceHighBrush"` in their own right. Rewrite the
   `<remarks>` that justified the alias — it is no longer true, and a stale rationale is worse than
   none. Add the new key to `BrushKeys`.
5. Extend `MarkdownPreviewThemeTests`: the code-span test keeps asserting the bound key, and a new
   test pins that a code span and a fenced block no longer resolve to the *same* brush.

### Acceptance criteria

- `MarkdownPreviewTheme.BrushKeys` still resolves to an `IBrush` under both `Dark` and `Light`, and
  still holds no duplicates — the two existing tests pass unchanged.
- A rendered `` a `code` span `` has its `Run.Background` bound to the new key and resolving to a
  different brush from a fenced block's background, in both variants.
- The fenced code block and the table header still resolve to `EnigmaSurfaceHighBrush`.
- A variant switch still repaints without rebuilding the tree.
- Release build: 0 warnings, 0 errors, and no `AVLN` diagnostic in the log.
- Whole suite green.

---

## PHASE02 — Code-span chip padding

**Status: TODO**

### Symptom

The chip's fill stops exactly at the first and last glyph, so the text touches its own background on
both sides and the chip reads as a colour change rather than as a chip.

### Root cause

`Code()` returns a `Run` holding exactly `CodeInline.Content`, and an Avalonia inline has no padding
property (see *Context & constraints*).

### Steps

1. In `MarkdownVisualBuilder.Code()`, wrap the content in one ` ` on each side, declared as a
   named constant. No-break rather than an ordinary space, so the chip can never be broken from its
   own padding at a line end, and `U+00A0` rather than a thin or figure space because it is the one
   fixed space every font carries — a missing glyph would put a tofu box inside the chip.
   Keeping the pad in the **same run** is what makes its height right by construction: the fill is
   the run's own background, so padding and text are one rectangle with no box to align.
2. Mark the run with a `previewCodeSpan` class, declared on `MarkdownPreviewTheme` beside the other
   class names, so a chip is identifiable in the tree rather than only by having a background.
3. Add `MarkdownCodePads`, modelled on `MarkdownLinkRegions`: an attached property on `TextBlock`
   carrying the indices of the pad characters in that block's text, plus a `Strip` that removes those
   indices from a selected substring.
4. Record the indices in `Append()`, where a `CodeInline` is already measured before and after — the
   leading pad is at `origin + before`, the trailing one at `origin + after - 1`. The pads collector
   threads through `Append` / `Convert` / `Emphasis` / `Link` exactly as `links` already does, so a
   chip nested in emphasis or in a link records absolute indices like everything else.
5. Attach the collector in `Prose()`, beside the links, and only when there is at least one pad.
6. In `MarkdownPreview.BuildCopyRequest()`, run the block's selected text through `MarkdownCodePads`
   before handing it to `MarkdownCopyRequest.ForSelection`, so a drag selection over a chip copies
   the code and not the padding. The whole-document copy is unaffected: it is rendered from the
   markdown, not read off the visuals.
7. Tests: the chip's text carries a pad either side; a selection across a chip comes back unpadded;
   a literal `&nbsp;` in prose still survives a copy (the strip is index-based, not a character
   replace); link ranges over a chip are still right.

### Acceptance criteria

- A rendered `` a `code` span `` has one ` ` before and after the content inside the same
  background-bearing run.
- Selecting a whole paragraph containing a chip and copying yields the code with no padding
  characters, and a paragraph containing an authored `&nbsp;` keeps it.
- A link containing a code span still hit-tests over its whole range.
- Release build: 0 warnings, 0 errors.
- Whole suite green.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Item type | `BUG` | A legibility defect in shipped rendering, not a new capability | `FEATURE` — nothing new is being offered to the reader |
| Where the chip colour comes from | A project-owned `Themes/PreviewBrushes.axaml`, merged after the Enigma dictionary | The shipped palette has no step meant to sit inside a paragraph; borrowing `EnigmaHoverBrush` would lie about what hover means and would break the day the palette redefines it | Re-point the key at another `Enigma*` key; a literal colour in C# (the house rule is brushes, always `DynamicResource`) |
| Opaque or translucent fill | Translucent — white 20 % Dark, black 12 % Light | The chip also lands inside blockquotes and table headers; one alpha reads correctly over all three surfaces | Two opaque colours, which would need a third for every surface a chip can land on |
| What "a lighter background" means | More contrast: lighter in Dark, darker in Light | A lighter chip on a white pane is *less* visible, which is the opposite of what was asked for | Literally lighter in both variants |
| Whether fenced blocks and table headers move too | No — un-alias the span alone | Both already read correctly; they carry a border and real padding | Moving all three, an unrequested visual change to two other elements |
| How the padding is made | ` ` inside the same run | The fill is the run's own background, so padding and text are one rectangle — no second box whose height has to be guessed, and none can be guessed here because the suite has no Skia to check it with | An `InlineUIContainer` chip (drops the code out of every drag-selected copy); `InlineUIContainer` spacers either side (unverifiable height match against the glyph run) |
| Clipboard fidelity | Record each pad's index at build time and drop it on copy | This is a markdown editor for developers: copying `git status` out of the preview has to yield exactly that | Leaving the pads in (corrupts a pasted command); stripping every `U+00A0` from the selection (an authored `&nbsp;` is a real `U+00A0` run, so that is lossy) |
| Corner radius | Not done, recorded as a follow-up | Impossible on a text run, and the only way to fake it costs the copy path | Faking it with an `InlineUIContainer` |
| Shape | One item, two phases | Colour and padding are independently reviewable and independently valuable | One commit across theme, XAML, builder, copy path and four test files |
