# BUG-18AD-PHASE01 — Code-span chip gets its own colour

**Status: DONE** · Phase 01 of 2 · Branch: `bugfix/bug-18ad-phase01-code-span-colour`
Plan: `docs/plan/BUG-18AD.md`

## Summary

A code span in the preview is painted from a colour of the application's own rather than from a step
of the shipped surface ramp, so it stops being one shade off the pane it is drawn on.

- **The alias is broken, in the direction the defect pointed.** `CodeBackgroundKey` was literally
  `"EnigmaSurfaceHighBrush"`, and `CodeBlockBackgroundKey` / `TableHeaderBackgroundKey` were C#
  aliases of *it*. The preview pane is `EnigmaSurfaceBrush` and `EnigmaSurfaceHigh` is the step
  immediately above it, so every chip in every paragraph was one ramp step from its own background.
  `CodeBackgroundKey` now names a new key; the fenced block and the table header keep
  `"EnigmaSurfaceHighBrush"`, written out in `CodeBlockBackgroundKey` with `TableHeaderBackgroundKey`
  aliasing *that* — the one alias that is still true, since a fence and a header row really are one
  surface under two names.
- **The new colour is translucent, not a fourth surface.** The same chip lands on the pane
  (`EnigmaSurface`), inside a block quote (`EnigmaSurfaceLow`) and inside a table header
  (`EnigmaSurfaceHigh`). White at 20 % in Dark and black at 12 % in Light reads correctly over all
  three; an opaque colour would have needed one variant per surface a chip can land on, and would
  have gone wrong again the first time the palette moved.
- **"Lighter" was read as "more contrast".** In Dark that is lighter, as asked. In Light the chip is
  *darker* than the pane, because a lighter chip on a light pane is less visible, not more — which is
  the opposite of the request.
- **The colour lives where a colour belongs.** `Themes/PreviewBrushes.axaml` is the project's first
  resource dictionary of its own, defined per theme variant like the palette itself, so the chip
  follows a variant switch through the same `DynamicResource` as everything else the renderer paints.
  No literal colour reached C#.

The renderer itself is unchanged: `MarkdownVisualBuilder.Code()` still binds
`MarkdownPreviewTheme.CodeBackgroundKey` and does not know the key moved.

## Files/modules touched

**Created — `src/Enigma.MarkdownEditor.Desktop`**

- `Themes/PreviewBrushes.axaml` — a `ResourceDictionary` whose `ThemeDictionaries` define
  `MarkdownPreviewCodeSpanBackgroundBrush` under `Dark` (`#33FFFFFF`) and `Light` (`#1F000000`). The
  file's own comments carry why the key exists at all, why it is translucent, and why the light
  variant is darker rather than lighter. Picked up by Avalonia's default `**/*.axaml` glob — no
  `.csproj` change was needed, and none was made.

**Modified — `src/Enigma.MarkdownEditor.Desktop`**

- `App.axaml` — the new dictionary merged after the Enigma `ResourceInclude`, with a comment saying
  that it adds a key rather than overriding one, and that the order is nonetheless where an override
  would have to go.
- `Controls/MarkdownPreviewTheme.cs` — `CodeBackgroundKey` is
  `"MarkdownPreviewCodeSpanBackgroundBrush"` and gains a `<remarks>` explaining why one colour in the
  preview is not the palette's; `CodeBlockBackgroundKey` is `"EnigmaSurfaceHighBrush"` in its own
  right and its `<remarks>` — which used to justify the alias to the code span — now records why the
  two parted and why the *other* alias stays; `TableHeaderBackgroundKey` aliases
  `CodeBlockBackgroundKey`; `BrushKeys` gains `CodeBlockBackgroundKey`, and its `<remarks>` records
  that `CodeBackgroundKey` is in the list for a second reason — being this project's own key, the
  resolution test is also what proves the dictionary was merged, for both variants.

**Modified — `tests/Enigma.MarkdownEditor.Desktop.UnitTests`**

- `HeadlessTestApp.cs` — merges `Themes/PreviewBrushes.axaml` after the Enigma dictionary, exactly as
  `App.axaml` does. Without it every assertion about the chip would be made against a palette the
  application does not have.
- `Controls/MarkdownPreviewThemeTests.cs` — `ACodeSpan_IsPaintedWithTheThemesSurfaceInEitherVariant`
  renamed to `…IsPaintedWithTheChipColourInEitherVariant` (it no longer is a surface);
  `ACodeSpan_IsNoLongerPaintedLikeTheBlockAroundIt` pins the regression itself, both at the key level
  and on a rendered chip against a rendered fence, in both variants;
  `AFencedBlockAndATableHeader_KeepTheSurfaceTheyAlwaysHad` pins that the un-aliasing moved the span
  alone.

## Deviations & follow-ups

- **No deviation from the plan.** All five steps landed as written.
- **Follow-up — the chip has no corner radius.** An Avalonia text run cannot carry one, and the only
  way to fake it (an `InlineUIContainer`) would drop the code out of every drag-selected copy, which
  is why PHASE02 pads with glyphs instead. A rounded chip needs a renderer that draws the paragraph
  itself, which is a different item entirely.
- **Follow-up — the exact alphas are a manual pass.** The headless suite runs without Skia by
  deliberate choice, so no test here can say what the chip *looks* like — only that it resolves, that
  it follows the variant, and that it is not the fence's colour. The existing theme tests already say
  as much about colour generally.
- **Line endings:** nothing to recommend. `.gitattributes` already pins `* text=auto eol=lf` with an
  explicit `*.cs`/`*.md` rule, the working tree is LF throughout, and this phase's diff carries no
  line-ending churn.
- **Documentation freshness sweep:** nothing edited. `README.md:74` says the preview renders "in the
  application's own palette" — truer after this phase than before it — and `README.md:76` lists code
  spans among the supported elements without describing how they are painted. The repository-layout
  block lists top-level folders only, so the new `Themes/` folder does not belong in it. No
  `CLAUDE.md`, `AGENTS.md`, `CHANGELOG.md` or `CONTRIBUTING.md` exists.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release` — **0 warnings, 0 errors**, and no `AVLN`
  diagnostic in the log (checked explicitly, because this phase adds a XAML file and those are
  emitted by an MSBuild task that `TreatWarningsAsErrors` does not promote).
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` — **3568 passed, 0 failed,
  0 skipped**, first run, no fix cycle used.
- Acceptance criteria: `BrushKeys` still resolves in both variants and still holds no duplicates (the
  two pre-existing tests pass untouched, now over one more key); the chip resolves to the new key and
  to a different brush from a fence's, in both variants; the fence and the table header still resolve
  to `EnigmaSurfaceHighBrush`; `SwitchingVariant_RepaintsWithoutRebuildingTheTree` still passes.
