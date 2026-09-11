# FEATURE-6054 — Markdown preview

**Status: DONE** · Multi-phase (3) · Roadmap: `docs/roadmap.md`
Branches at build time: `feature/feature-6054-phase01-renderer`, `feature/feature-6054-phase02-tables-code`, `feature/feature-6054-phase03-view-modes`.

## Objective

A native, Enigma-themed live preview of the active document — **custom renderer** over the Markdig AST (decided: no Markdown.Avalonia — its Avalonia 12 support is alpha-only; no WebView) — with GFM core, YAML front matter, syntax-highlighted code blocks, and editor/split/preview view modes with one-way scroll sync.

## Context & constraints

- Depends on FEATURE-3C44 PHASE01 (active document); code highlighting reuses the TextMateSharp stack from FEATURE-3C44 PHASE02.
- Markdig 1.3.2 (already pinned in CPM). Renderer is Desktop-side; the pipeline and AST-level logic are Core-side and unit-tested.
- Every brush is an `{DynamicResource Enigma…}` key so both theme variants work without renderer changes.
- **Error isolation is a hard requirement**: a renderer failure shows an inline error block and logs — it never crashes the app.

## Design

- **Core** `Markdown/`: `MarkdownPipelineFactory` building the single shared `MarkdownPipeline`:
  `UsePipeTables() · UseTaskLists() · UseEmphasisExtras(Strikethrough) · UseAutoLinks() · UseYamlFrontMatter() · UsePreciseSourceLocation()` — the last one powers scroll sync. Exposes `Parse(string)` → `MarkdownDocument`.
- **Desktop** `Controls/MarkdownPreview`: a control with `Markdown` (string) and `BasePath` (string) properties; on change, walks the AST and rebuilds its visual tree (a `StackPanel` of block visuals inside a `ScrollViewer` — adequate at notes scale; per-block virtualization noted as the future fallback if large documents ever lag). Debouncing (~250 ms after last keystroke) is owned by the hosting ViewModel, not the control.
- **Block rendering** (PHASE01): headings h1–h6 (`SelectableTextBlock`, scaled size/weight), paragraphs with inline runs (bold / italic / strikethrough / `code span` with monospace + surface background), hyperlinks (accent brush, hand cursor), bulleted/numbered lists with nesting, task lists (read-only check glyphs), block quotes (left accent border + subtle background), thematic breaks, images (resolved against `BasePath`, async bitmap load, constrained to available width, missing file → bordered placeholder showing the path). All text selectable.
- **PHASE02 additions**: pipe tables (`Grid`: emphasized header row, `EnigmaBorderSubtleBrush` cell borders, column alignment from the AST); fenced code blocks (bordered monospace block, token-colored via the TextMateSharp tokenizer for the fence language, theme-matched; unknown language → plain monospace); YAML front matter hidden from output (optional subtle "metadata" chip); **link activation**: `http(s)` → `TopLevel.Launcher.LaunchUriAsync`, relative `.md` → `OpenDocumentRequest` (opens a tab), other relative paths → OS default app.
- **PHASE03 — view modes & sync**: app-level `ViewMode` enum `EditorOnly | Split | PreviewOnly`; toolbar buttons + one shortcut cycling the modes (`Ctrl+Shift+V`); mode drives pane visibility in the 3-pane shell. **One-way scroll sync** (editor → preview) in Split: on editor scroll, take the first visible source line, find the deepest rendered block whose Markdig source span contains it (binary search over a line→block index built at render time), scroll the preview to that block's offset with linear interpolation inside the span; re-entrancy guarded. Live update: re-render debounced ~250 ms after typing stops; caret/scroll position of the preview preserved across re-renders when the content around the viewport is unchanged (best effort).

## Testing strategy

- Core.UnitTests: pipeline configuration (each extension active), front-matter detection, source spans present, link classification (external/internal-md/other) as pure logic.
- Desktop.UnitTests: renderer structural tests using **Avalonia.Headless** (12.1.1, added to the CPM Avalonia coupled set) — given markdown, assert the produced control tree (types, counts, text) for each block kind; error-isolation test (malicious/broken input renders the error block).
- A small fixtures corpus under `tests/…/Fixtures/*.md` (headings, emphasis, lists, tasks, quotes, tables, fences, images, front matter) doubles as the manual visual checklist in dark + light.

## Phases

### PHASE01 — Markdig pipeline & renderer v1 — **Status: DONE**

Steps: Core pipeline factory + tests → preview control skeleton + AST walker → all PHASE01 block/inline kinds → image resolution via `BasePath` → error isolation → hosted in the shell's preview pane rendering the active document (no modes yet — pane toggle from FEATURE-0F9A suffices).

Acceptance: fixtures corpus renders correctly in dark + light (headless structural tests + manual pass recorded); images resolve relative to the note; a parse/render exception yields the inline error block and a log entry, app alive.

### PHASE02 — Tables, code highlight & links — **Status: DONE**

Steps: table rendering incl. alignment → TextMateSharp-based fence colorizer (theme-matched, live variant switch) → front matter hidden (+ chip) → link activation matrix.

Acceptance: GFM table fixture matches expected structure incl. per-column alignment; C#/JSON/bash fences colored, unknown languages plain; front matter never renders as content; external links open the browser, relative `.md` opens a tab, others hit the OS default (manual verify + unit tests for the classification and `OpenDocumentRequest` raising).

### PHASE03 — View modes & scroll sync — **Status: DONE**

Steps: `ViewMode` state + toolbar buttons + `Ctrl+Shift+V` cycle → pane visibility wiring → debounced live re-render → line→block index + one-way scroll sync → re-entrancy/feedback guards.

Acceptance: three modes cycle via shortcut and buttons and survive pane-collapse interplay; typing updates the preview ≤ ~300 ms after the last keystroke without stealing focus; scrolling the editor tracks the preview to within the correct block on the fixtures corpus; no oscillation.

## Dependencies

FEATURE-3C44 PHASE01 (active document + tabs); PHASE02 reuses the TextMate infrastructure from FEATURE-3C44 PHASE02. FEATURE-40F5 PHASE02 persists the view mode.

## Out of scope

Mermaid/math (excluded with the WebView decision — future work item) · HTML passthrough rendering (raw HTML shows as literal text in v1) · preview→editor reverse sync · export to HTML (backlog).
