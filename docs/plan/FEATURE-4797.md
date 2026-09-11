# FEATURE-4797 — Preview select-all & copy

**Status: DONE** · 2 phases · Roadmap: `docs/roadmap.md`
Planned on `develop`; each phase cuts its own branch from `HEAD` at build time.

## Objective

Make the rendered preview pane copyable. Today a selection stops at the paragraph it started in, so
there is no way to take the whole rendered note into another application. PHASE01 adds document-wide
select-all with a plain-text copy on three surfaces; PHASE02 adds a rich-text (HTML) flavour beside
the plain text, so a paste into Word or Outlook keeps its headings, bold, links and tables.

Cross-block **drag** selection — dragging from the middle of one paragraph to the middle of another —
is deliberately **out of scope** (see *Out of scope*). What ships here is all-or-nothing selection
plus the per-paragraph selection that already exists.

## Context & constraints

Established by reading the code during the interview.

- **The renderer rebuilds its whole visual tree on every render**, by documented design
  (`Controls/MarkdownPreview.cs:36`). No selection can survive a render, and this plan does not try
  to make one — see *A live selection is lost on re-render* below.
- **Every text visual is a `SelectableTextBlock`**, one per block, and Avalonia keeps selection state
  **per instance** — which is the whole cause of the reported problem. Instances are created in
  `Controls/MarkdownVisualBuilder.cs` (prose, headings, list markers, table cells, front-matter chip,
  image placeholders, the inline error block) and in `Controls/MarkdownCodeBlock.cs:55`.
- **Avalonia 12.1.2 already gives a block what it needs**: `SelectAll()`, `Copy()`, `SelectedText`,
  `CanCopy`, `HasSelection()`, `ClearSelection()`, and a `CopyingToClipboardEvent`. Confirmed against
  `Avalonia.Controls.xml` in the 12.1.2 package. **Copying a selection inside one paragraph already
  works today** — only the document-level layer is missing, and the native path must keep working.
- **Link clicks and task ticks ride the same pointer events as selection.**
  `MarkdownLinkRegions.Attach` subscribes with `handledEventsToo: true` precisely because
  `SelectableTextBlock` handles those events itself, and it already suppresses a link activation when
  `SelectionStart != SelectionEnd` (`Controls/MarkdownLinkRegions.cs:167`). Neither behaviour may
  regress.
- **`TextBlock.Text` is never set by the renderer** — a paragraph is a sequence of runs and spans and
  its text lives in `InlineCollection.Text` (recorded in `tests/…/Controls/PreviewTree.cs`). Any
  route that reads `.Text` off a block reads `null`.
- **Controls raise events; ViewModels reach services.** `RenderFailed`, `LinkActivated` and
  `TaskToggled` are all this seam, and the preview control injects nothing. The clipboard write must
  follow it rather than being done inside the control.
- **A service that needs a window takes `Func<MainWindow>` from DI.** `Services/FileLauncher.cs:14`
  is the precedent, registered at `Extensions/ServiceCollectionExtensions.cs:271`. This is how the
  clipboard is reached without a control owning it.
- **`Core` never references Avalonia** (stated in `Enigma.MarkdownEditor.Core.csproj`) but does
  reference Markdig, and `Core/Markdown/MarkdownPipelineFactory.cs` already owns the pipeline. Both
  the plain-text and the HTML projection therefore belong in `Core`, where they are testable with no
  window at all.
- **`TreatWarningsAsErrors` is on solution-wide.** The bar for each phase is a **0-warning** release
  build, and because XAML is touched, `dotnet build -v normal | grep -i AVLN` is part of the check —
  `AVLN*` diagnostics come from an MSBuild task and are not promoted by `TreatWarningsAsErrors`.
- **Headless layout caveat**: naming a font family makes headless layout measure nonsense
  (`PreviewTree.cs` documents why). Any test that depends on layout must leave families inherited.
- **No new package.** Markdig and Avalonia cover everything, and `Enigma.Icons.Avalonia` is already
  referenced for the header button's icon.

## Design decisions taken in the interview

- **Copied text reads as the pane reads, markup-free.** Headings bare; list items keep their drawn
  marker and indent (`• item`, `1. item`); quotes plain; fenced code verbatim without the fence
  lines; table rows tab-separated, so a paste into Excel lands one cell per column; task items
  prefixed `[ ] ` / `[x] `; images as `[alt text]`; thematic rules and the front-matter chip omitted;
  one blank line between blocks. Line separator is `Environment.NewLine`.
- **One copy rule on all three surfaces**: the selection when there is one, the whole note otherwise.
- **Both flavours only for a whole-pane copy.** A partial in-paragraph copy stays plain text, because
  producing HTML for an arbitrary character range means mapping that range back to the markdown that
  produced it, which the renderer cannot do.
- **A live selection is lost on re-render.** Typing in the editor rebuilds the preview and the
  highlight goes. Not restored and not suppressed: select-then-copy is a momentary act, and holding
  the refresh back would silently stop the pane being live.
- **Focus follows the click.** `Ctrl+A` / `Ctrl+C` are handled inside the preview and need focus in
  the pane; the header button and the context menu work regardless of focus. Switching view mode
  never steals focus, and the editor's own `Ctrl+A` is untouched.

---

## PHASE01 — Select-all & plain-text copy

**Status: DONE**

### Scope

Document-wide select-all, plain-text extraction, and the three surfaces. No HTML.

### Design

**1 — `Core/Markdown/MarkdownPlainText.cs`** (new)

`public static string Render(string markdown)` — parses with `MarkdownPipelineFactory.Shared` and
walks the AST to the shape in *Design decisions* above. A Markdig `TextRendererBase`-derived renderer
or a hand-rolled walk, whichever comes out simpler; either way it lives in `Core` and is tested with
no Avalonia in the room.

It **mirrors the renderer's own drop rules**, so what is copied matches what is on screen: a
`YamlFrontMatterBlock` contributes nothing, a `ThematicBreakBlock` contributes nothing, an
`HtmlBlock` contributes its literal lines, and a block kind the preview does not draw contributes
nothing — the same set as the `Visual(Block)` switch at `MarkdownVisualBuilder.cs:191`. A drift test
guards this (acceptance criteria).

Re-parsing rather than walking the control tree is deliberate: the visual tree carries tables as
nested `Grid`s, code as token runs and tasks as custom controls, so reading text back out of it means
encoding structural knowledge of the renderer in a second place. The AST is the same input the
renderer itself was given.

**2 — `Controls/MarkdownPreview.cs`**

- `public void SelectAll()` — walks `Blocks` recursively, calls `SelectAll()` on every
  `SelectableTextBlock` it finds, and sets a private `_allSelected` flag.
- `public void ClearSelection()` — the inverse, and `_allSelected = false`.
- `_allSelected` is cleared by any pointer press in the pane and by every render. It is what
  distinguishes the two copy cases: after a select-all *every* block reports a selection, so the
  native per-block `Ctrl+C` would otherwise copy only the focused paragraph.
- `public event EventHandler<MarkdownCopyRequest>? CopyRequested` — raised when the reader asks for a
  copy. The payload says which case it is: the whole document, or a selection carrying the selected
  text already resolved. The control knows nothing about the clipboard, matching the `RenderFailed` /
  `LinkActivated` / `TaskToggled` seam.
- `OnKeyDown`: `Ctrl+A` selects all and marks the event handled. `Ctrl+C` is handled **only** when
  `_allSelected`; otherwise it is left to the native `SelectableTextBlock` path, so a per-paragraph
  copy keeps working exactly as it does today.
- `Focusable = true` on the control, so a click in the pane gives it focus and both gestures arrive.
- `public bool HasContent` — whether there is anything to copy, for the button's enabled state.

**3 — `Services/IClipboardWriter.cs` + `Services/ClipboardWriter.cs`** (new)

`Task<bool> WriteTextAsync(string text)`. The implementation takes `Func<MainWindow>` and goes
through `TopLevel.GetTopLevel(window)?.Clipboard`, mirroring `FileLauncher` exactly, and is
registered as a singleton in `Extensions/ServiceCollectionExtensions.cs` beside it. It returns
`false` when the platform has no clipboard rather than throwing at the caller.

**4 — `ViewModels/MarkdownPreviewViewModel.cs`**

- Takes `IClipboardWriter`.
- `CopyCommand` (`[RelayCommand]`, async) serves the header button, the context menu, and the
  control's `CopyRequested`. All three go through one method: a whole-note copy calls
  `MarkdownPlainText.Render(Markdown)`, a selection copy writes the text the request carried.
- `SelectAllCommand` for the context menu, calling through to the control.
- `CanCopy` / `CanSelectAll` gate both commands on there being a document with content.
- A write that returns `false` or throws is logged at **Warning** through the existing `_logger` and
  nothing is shown to the reader — the same treatment as a survived render failure.

**5 — `Views/MainWindow.axaml`**

- The `PREVIEW` header (`:448`) becomes a two-column `Grid`: the existing `paneHeader` `TextBlock` on
  the left, an icon-only `Button` on the right bound to `Preview.CopyCommand`, with
  `ToolTip.Tip="Copy the selection, or the whole note"`. Icon from `Enigma.Icons.Avalonia`.
- A `ContextMenu` on `PreviewRenderer` with **Copy** → `Preview.CopyCommand` and **Select all** →
  `Preview.SelectAllCommand`.
- Both surfaces are disabled, not hidden, when there is nothing to copy.

### Acceptance criteria

- `Ctrl+A` in the preview leaves every text block in the pane selected; `Ctrl+C` then writes the
  whole rendered note as plain text.
- The context menu offers **Copy** and **Select all**; the PREVIEW header carries a tooltipped icon
  button; all three obey *selection if any, else the whole note*.
- For every file in `tests/…Desktop.UnitTests/Fixtures/`, `MarkdownPlainText.Render` produces the
  shape in *Design decisions* — headings bare, list markers kept, code verbatim without fences,
  table rows tab-separated, `[ ]` / `[x]` task prefixes, `[alt]` images, no rules, no front matter.
- **Drift test**: for every fixture, the blocks `MarkdownPlainText` emits and the blocks
  `MarkdownPreview` renders agree in count and order, so a future renderer change that adds or drops
  a block kind fails here rather than silently copying something the reader cannot see.
- Selecting inside one paragraph and pressing `Ctrl+C` still copies just that range (native path
  untouched); clicking a link still opens it; a drag that ends inside a link still does not; ticking
  a task box still works.
- No document open, or an empty note: button and menu entries disabled, nothing written.
- A clipboard write that fails is logged at Warning and never surfaces to the reader.
- Release build **0 warnings**, no `AVLN` output, full suite green, with the new tests in it.
- `docs/done/FEATURE-4797-PHASE01.md` written; roadmap and this file flipped.

### Files

- **New** — `src/Enigma.MarkdownEditor.Core/Markdown/MarkdownPlainText.cs`;
  `src/Enigma.MarkdownEditor.Desktop/Services/IClipboardWriter.cs` + `ClipboardWriter.cs`;
  `src/Enigma.MarkdownEditor.Desktop/Controls/MarkdownCopyRequest.cs`;
  `tests/Enigma.MarkdownEditor.Core.UnitTests/Markdown/MarkdownPlainTextTests.cs`;
  `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Controls/MarkdownPreviewCopyTests.cs`;
  a fake clipboard writer under `tests/…Desktop.UnitTests/Fakes/`.
- **Modified** — `Controls/MarkdownPreview.cs`; `ViewModels/MarkdownPreviewViewModel.cs`;
  `Views/MainWindow.axaml`; `Extensions/ServiceCollectionExtensions.cs`; `README.md` (the Preview
  bullets and the `Ctrl+` reference table).

---

## PHASE02 — Rich-text (HTML) flavour

**Status: DONE**

### Scope

A second clipboard flavour on a whole-pane copy, so a paste into Word or Outlook keeps its
formatting. A partial in-paragraph copy stays plain text.

### Design

**1 — `Core/Markdown/MarkdownHtml.cs`** (new)

`public static string Render(string markdown, string? basePath)` — `Markdown.ToHtml` over
`MarkdownPipelineFactory.Shared`, then relative `<img src>` values rewritten to absolute `file://`
URIs against `basePath`, so images survive the paste. `MarkdownImageSource.Resolve` already holds the
resolution rules the renderer uses and is reused rather than reimplemented.

**Deliberately unstyled**: no fonts, no sizes, no colours. The receiving application applies its own,
which is the only correct answer when the pane may be in dark theme and the document being pasted
into is white.

**2 — `Services/ClipboardWriter`**

Add `Task<bool> WriteTextAndHtmlAsync(string text, string html)`: one `DataObject` carrying
`text/plain` and the HTML flavour, written once, so the receiving application picks. On Windows the
HTML flavour needs the **CF_HTML** wrapper — the `Version:0.9` / `StartHTML:` / `EndHTML:` /
`StartFragment:` / `EndFragment:` header with byte offsets — which is written here, in the one place
that knows about platforms. On Linux the flavour is `text/html`, unwrapped.

**3 — `ViewModels/MarkdownPreviewViewModel`**

The whole-note path calls the two-flavour write; the selection path keeps calling the text-only one.
One branch, no new command, no new UI.

### Acceptance criteria

- A whole-pane copy writes `text/plain` **and** an HTML flavour in a single clipboard write; a
  partial copy writes plain text only.
- The HTML carries no font, size or colour styling, and every relative image link comes out as an
  absolute `file://` URI.
- The CF_HTML wrapper's byte offsets are correct — asserted in a unit test against the produced
  string, because a wrong offset is exactly what makes a paste arrive empty.
- ~~**Verified by hand, on both platforms, and recorded in the completion doc**: pasting into Word and
  Outlook on Windows keeps headings, bold, links and tables; pasting into Notepad gets the plain
  text; pasting into a Linux editor and into LibreOffice behaves. A headless test cannot prove what
  Word receives, and this plan says so rather than pretending otherwise.~~
  **Superseded during the build, by decision.** The premise turned out to be too pessimistic: the
  Windows path *can* be proved without a paste target. The write was driven through a real Avalonia
  desktop window and the clipboard read back through Win32 `GetClipboardData` — `HTML Format`
  registered and sitting beside `UnicodeText`, a 380-byte `HGLOBAL` ending in `NUL`, genuine UTF-8
  (`Café — 日本語` round-tripped), and the declared `StartFragment`/`EndFragment` slicing back to the
  exact fragment. That covers everything the manual check was there to catch except how Word chooses
  to *render* valid markup. Recorded in the completion doc under *Build/test evidence*.
- Release build **0 warnings**, no `AVLN` output, full suite green.
- `docs/done/FEATURE-4797-PHASE02.md` written; roadmap and this file flipped.

### Files

- **New** — `src/Enigma.MarkdownEditor.Core/Markdown/MarkdownHtml.cs`;
  `tests/Enigma.MarkdownEditor.Core.UnitTests/Markdown/MarkdownHtmlTests.cs`.
- **Modified** — `Services/IClipboardWriter.cs` + `ClipboardWriter.cs`;
  `ViewModels/MarkdownPreviewViewModel.cs`; the Desktop copy tests; `README.md`, if the Preview
  bullets need the rich-text sentence.

---

## Dependencies

PHASE02 builds on PHASE01's clipboard service and copy path. Nothing outside this item.

## Out of scope

Cross-block **drag** selection · mapping a partial selection back to its markdown so it could be
copied as HTML · a separate "copy as markdown" command (the editor pane already does that with
`Ctrl+A` / `Ctrl+C`) · styling the pasted HTML to match the pane · restoring a selection across a
re-render · any new setting, and therefore any settings migration · any new package.
