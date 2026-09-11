# FEATURE-6054-PHASE01 — Markdig pipeline & renderer v1

**Status: DONE** · Branch: `feature/feature-6054-phase01-renderer` · Plan: `docs/plan/FEATURE-6054.md`

## Summary

The third pane is real. The note you are in is parsed with one shared Markdig pipeline and drawn as
Avalonia controls in the Enigma palette — no WebView, no third-party markdown control, no HTML
anywhere.

**What renders.** Headings h1–h6 on their own scale (h1 at 24pt down to h6, which stays bold at body
size rather than shrinking below the paragraph under it). Paragraphs with **bold**, *italic*,
***both*** as one nested span rather than only the outer one, ~~strikethrough~~, and `code spans` set
in the editor's own monospace stack on a surface chip. Links in the theme's accent, underlined; a link
with no label — `<https://…>` or a bare address the autolink extension picked up — shows the address
itself. Bulleted and numbered lists, nested to any depth, numbering from wherever the document starts
it (`7.` → `7. 8.`) and keeping the delimiter it was written with (`1)` stays a bracket). Task items
whose box **is** the marker, so `- [x] done` reads as one checked line rather than a bullet followed by
a box — and its text starts exactly where an ordinary item's does in the same list. Block quotes ruled
down the left in the accent over a subtle background, with whatever blocks are inside them. Thematic
breaks as a hairline. Images resolved against the note's own folder, decoded off the UI thread and held
to the pane's width without ever being blown up past their own pixels; an image that is not there is a
bordered placeholder showing **the link as the document wrote it**, and a remote address says it is not
fetched rather than reaching for the network. Everything is a `SelectableTextBlock`, so all of it can be
selected and copied.

**Every colour is a key, not a brush.** Each visual binds its brushes as `{DynamicResource Enigma…}`,
so switching the window between dark and light re-resolves the tree in place — the document is not
re-walked, and there is no renderer code that knows which variant it is in. That is asserted rather
than asserted-to: `MarkdownPreviewThemeTests` proves every key in `MarkdownPreviewTheme.BrushKeys`
resolves to a brush in **both** variants, that a link's colour is the accent in either, and that a
variant switch repaints the very same visual object.

**Error isolation was the hard requirement, and it is layered.** Each block is built inside its own
`try`: one that throws becomes a bordered sentence in its own place and the rest of the note keeps
rendering. A failure in the parse or in the walk over the top-level blocks replaces the whole content
with that block. Even a `RenderFailed` handler that itself throws cannot get past the guard around the
report. Nothing reaches the UI thread unhandled, and every failure is logged by the pane's ViewModel
with the file it happened in.

**What the pane deliberately does not do yet** is follow your typing: the preview is what the tab held
when it became active, and it refreshes on a tab switch, an external reload, and a Save As that moves
the file. The ~250 ms debounce is PHASE03's, and `MarkdownPreviewViewModel` is where that timer will
live — there is a test that documents the boundary rather than leaving it to be discovered.

## Files/modules touched

### Created — `src/Enigma.MarkdownEditor.Core/Markdown`

| File | Notes |
|---|---|
| `MarkdownPipelineFactory.cs` | The one pipeline, built once and reused: `UsePipeTables · UseTaskLists · UseEmphasisExtras(Strikethrough) · UseAutoLinks · UseYamlFrontMatter · UsePreciseSourceLocation`. `Create()` is public so a test can assert a pipeline it built itself; `Parse(string)` is what the renderer calls. Strikethrough alone out of EmphasisExtras — subscript would claim `~` in a document that meant it literally. |
| `MarkdownImageSource.cs` | Where an `![…](url)` actually points: `Resolve(basePath, url)` → `Local` (with the absolute path and whether it is there), `Remote` (`http`/`https`), or `Unresolvable`. Handles a rooted link, a `file://` link, a link out of the folder (`../shared/logo.png`), a relative link with **no** folder (unresolvable rather than resolved against the working directory), a path the OS rejects, and a percent-encoded name (`my%20shot.png`) — tried raw first, so a file whose name really contains a `%` still wins. |
| `MarkdownImageKind.cs` | The three answers above. |

### Created — `src/Enigma.MarkdownEditor.Desktop/Controls` (new folder)

| File | Notes |
|---|---|
| `MarkdownPreview.cs` | The control: `Markdown` and `BasePath` styled properties, a `Blocks` list of what it produced, a `RenderFailed` event, and `protected virtual BuildBlocks(MarkdownDocument)`. Every assignment to either property is a render; the tree is rebuilt wholesale into a `StackPanel` inside a `ScrollViewer`. An event rather than an injected logger because the XAML builds this control, never the container — the same seam the editor pane reports a highlighting failure through. |
| `MarkdownVisualBuilder.cs` | The AST walk, one instance per render because it is scoped to one document's folder. Block dispatch, inline dispatch, the image visuals and their off-thread decode, the per-block `try`, and the inline error block. |
| `MarkdownPreviewTheme.cs` | The palette keys, the type scale, and `BindBrush` — the one place a dynamic resource is attached, so no renderer method resolves a colour itself. `BrushKeys` exists for the test that proves the palette still defines all ten. |

### Created — `src/Enigma.MarkdownEditor.Desktop/ViewModels`

| File | Notes |
|---|---|
| `MarkdownPreviewViewModel.cs` | Reads two strings off the active tab — the text and the folder — and owns nothing about how either is drawn. Follows `ActiveDocument`, `DocumentReloaded` and the tracked document's `FilePath`; `HasDocument` is what swaps the pane between the renderer and its empty state; `OnRenderFailed` logs at warning with the file path. `IDisposable`, idempotent, and it unsubscribes. |

### Modified

| File | Change |
|---|---|
| `Directory.Packages.props` | `Avalonia.Headless.XUnit` 12.1.1 pinned into the version-coupled Avalonia group (see *Deviations* 1). |
| `Core/Enigma.MarkdownEditor.Core.csproj` | `Markdig` referenced — the pin was already in CPM, unreferenced, waiting for this phase. |
| `Desktop.UnitTests/*.csproj` | `Avalonia.Headless` + `Avalonia.Headless.XUnit` referenced, and the fixture copy-glob added back (markdown only) — the comment saying this suite loads no files by relative path is no longer true. |
| `Desktop/ViewModels/MainWindowViewModel.cs` | One new dependency and one new property, `Preview`, held as a child ViewModel like the other three panes. |
| `Desktop/Views/MainWindow.axaml` | The `FEATURE-6054` placeholder replaced by the real control, plus an empty state for "nothing open". New `preview:` xmlns — `controls:` was already taken by `Enigma.Avalonia.Desktop.Controls`. |
| `Desktop/Views/MainWindow.axaml.cs` | Wires `PreviewRenderer.RenderFailed` to `ViewModel.Preview.OnRenderFailed`. |
| `Desktop/Extensions/ServiceCollectionExtensions.cs` | `MarkdownPreviewViewModel` as a singleton — it subscribes to the editor pane for the life of the window. |
| `README.md` | The status blockquote said "Two of the three panes are real" and ended "Still to come: the markdown preview". Rewritten, including what the preview does **not** do yet. Documentation freshness sweep. |

### Created / modified — tests (1430 total, was 1337)

| File | Notes |
|---|---|
| `Desktop.UnitTests/HeadlessTestApp.cs` | The `[assembly: AvaloniaTestApplication]` the headless harness runs on: the real application's **resource surface** and nothing else of it. Deliberately not the real `App`, whose `OnFrameworkInitializationCompleted` would read the developer's own `%AppData%` settings and workspaces, build the host and start NLog against the real log file. |
| `Desktop.UnitTests/Controls/PreviewTree.cs` (helper) | Renders a preview **inside a shown window** and reads the tree back: descendants by type, text collected from the inlines (a paragraph's `Text` property is never set), inlines by type at any depth. |
| `Desktop.UnitTests/Controls/MarkdownPreviewTests.cs` (30) | One per block and inline kind, plus: a second render replacing the first, front matter never appearing as content, a fence and a table skipped without taking their neighbours with them, the four image outcomes, an image inside a sentence staying inline, a change of folder re-resolving the images, and three error-isolation cases. |
| `Desktop.UnitTests/Controls/MarkdownPreviewThemeTests.cs` (12) | The light-and-dark criterion: every key resolves as a brush in both variants, a link is the accent in either, a code span is the surface in either, and a variant switch repaints without rebuilding. Plus the type-scale guards. |
| `Desktop.UnitTests/Controls/FixtureCorpusTests.cs` (12) | Every fixture rendered: blocks produced, no error block, no reported failure. Plus a guard on the corpus itself, so a fixture quietly deleted cannot take its coverage with it. |
| `Desktop.UnitTests/Fixtures/*.md` (10) | Headings · emphasis & code · lists · tasks · quotes & rules · tables · fences · images · front matter · links & HTML. Also the manual visual checklist (below). |
| `Desktop.UnitTests/ViewModels/MarkdownPreviewViewModelTests.cs` (11) | What the pane follows and what it does not, over real files: opening, switching tabs, closing the last tab, an external reload, a Save As that moves the folder, one notification per change, disposal, and the typing boundary. |
| `Core.UnitTests/Markdown/MarkdownPipelineFactoryTests.cs` (13) | Each extension asserted through the AST it produces rather than by reading the extension list back — including the one that is **off** (`H~2~O` is not subscript) and the precise source locations PHASE03's sync needs. |
| `Core.UnitTests/Markdown/MarkdownImageSourceTests.cs` (14) | Resolution over real files in a throw-away folder: relative, out-of-folder, rooted, missing, percent-encoded both ways, remote, and four kinds of link nothing can open. |
| `Desktop.UnitTests/ViewModels/MainWindowViewModelTests.cs` | The eight-argument guard test grew a position; `Create` builds a preview ViewModel. |
| `Desktop.UnitTests/Extensions/ServiceCollectionExtensionsTests.cs` | One more case on the composition-root theory. |

## Deviations & follow-ups

1. **One package beyond the plan: `Avalonia.Headless.XUnit` 12.1.1 — your call, asked before
   building.** The plan named `Avalonia.Headless` but not how xunit.v3 gets a UI thread and an
   application per test. Its 12.1.1 build depends on `xunit.v3.extensibility.core` **3.2.2**, exactly
   the version the test group already pins, so the two move together. It joins the version-coupled
   Avalonia set: **bump it with the rest of that set, never alone.** `Avalonia.Skia` was *not* added —
   headless drawing is enough for a structural assertion, which keeps a second copy of the graphics
   stack out of the test project.
2. **The headless harness gives each test its own `Application`.** Found the hard way: a `Window`
   cached in a static field belongs to the *previous* test's application by the time the next one looks
   at it, and every dynamic resource under it silently resolves to nothing — a green structural test
   over a colourless preview. `PreviewTree` therefore shows a fresh window per render, and the comment
   there says why. Worth knowing before writing the next headless test.
3. **A preview must be inside a shown window for its brushes to resolve at all.** Detached, a dynamic
   resource has no host chain and leaves every property unset. This is the intended failure mode (one
   wrong colour rather than invisible text) but it does mean the renderer cannot be unit-tested without
   a window.
4. **`protected virtual BuildBlocks` exists partly so error isolation is testable on every platform.**
   Real input *can* reach the isolation — a 33 000-character image link makes `Path.GetFullPath` raise
   `PathTooLongException`, which is not one of the `ArgumentException`s the resolver answers for, and
   that test is in the suite — but only on Windows; a Unix file system just has no such file. The
   virtual seam gives a deterministic test everywhere, and is a genuine extension point for a subclass
   that renders block kinds this version does not.
5. **Front matter is hidden now, which was listed under PHASE02.** One `switch` arm. Left visible it
   would make the preview *wrong* rather than merely unfinished — metadata rendering as the note's first
   paragraph. **PHASE02 still owns the chip that announces it.**
6. **Raw HTML renders as literal monospace text**, inline and block. That is the plan's own *Out of
   scope* note ("raw HTML shows as literal text in v1") made concrete; the monospace and the quieter
   foreground are mine, so it reads as "not rendered" rather than as broken prose.
7. **Fenced code blocks and pipe tables render as nothing at all** — not a gap, not an approximation.
   The blocks around them are unaffected, which two fixtures and a test exist to prove. PHASE02.
8. **A link has no hand cursor.** `Cursor` is a control property and these links are text inlines;
   giving each one an `InlineUIContainer` to carry a cursor would cost text selection and wrapping
   across the link. It belongs with **PHASE02's link activation**, which needs pointer handling anyway.
9. **Remote images are not fetched.** The plan says images are resolved against `BasePath`; nothing says
   the preview should reach the network, and a preview that made HTTP requests while you type is a
   decision worth taking deliberately. Today it says so in a placeholder. Follow-up if you want it.
10. **A percent-encoded image name is decoded as a fallback** — not named in the plan. `my%20shot.png`
    is what other tools write for a name with a space, and it is three lines and one test. The raw name
    is always tried first.
11. **Switching tabs costs two renders**, because `BasePath` and `Markdown` are two assignments and each
    is a render. The order is the one where the second — and therefore final — render is the correct
    pairing. Coalescing them would mean making the control's render asynchronous; PHASE03's debounce
    sits above this anyway.
12. **Bitmaps are not explicitly disposed on re-render.** Nothing leaks permanently — Skia's bitmaps are
    finalizable — but a re-render drops decoded images for the GC to reclaim on its own schedule. It
    does not matter at a tab switch. **It is worth revisiting in PHASE03**, where a debounced re-render
    happens while you type, and the fix is a generation list on the control.
13. **Per-block virtualization was not built.** The plan names it as the future fallback if a large
    document ever lags; the pane rebuilds wholesale today, which is what keeps the renderer a function
    of its input.
14. **`docs/done/FEATURE-3C44-PHASE03.md` deviation 10 is now closed**: `Avalonia.Headless` is no longer
    pinned-but-unreferenced, and the Desktop suite has a real headless harness.
15. **Linux was not verified** — no Linux machine this session. Nothing here is platform-specific except
    image decoding (Skia on both) and path handling (through `Path`, asserted with `Path.Combine`). The
    one test with a platform-dependent outcome is written to assert the right thing on either.
16. **Line endings (recommendation only, no action taken).** Every file in this diff is LF and
    `.gitattributes` (`* text=auto eol=lf`) keeps it that way; there is no CRLF noise here and nothing
    to do.

## Build/test evidence

```
> dotnet build Enigma.MarkdownEditor.slnx --no-incremental          (Debug)
Build succeeded.
    0 Warning(s)
    0 Error(s)
    grep -icE "AVLN[0-9]+" over the full normal-verbosity log → 0

> dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental
Build succeeded.
    0 Warning(s)
    0 Error(s)

> dotnet test --solution Enigma.MarkdownEditor.slnx
Test run summary: Passed!
  total: 1430   failed: 0   succeeded: 1430   skipped: 0
```

1430 tests = 1337 before, plus 93 added here: 27 in `Core.UnitTests/Markdown` (13 + 14), 65 in
`Desktop.UnitTests` (30 renderer + 12 theme + 12 fixture corpus + 11 preview ViewModel), and one more
case on the composition-root theory.

### The manual visual pass

The fixture corpus was rendered through the **real `MarkdownPreview` control** with real Skia drawing
(`Avalonia.Headless` with `UseHeadlessDrawing = false` + `Avalonia.Skia`, in a throwaway probe outside
the repository, the same way earlier phases verified their behavioural criteria), in a 440 × 1500 window
against `EnigmaBackgroundBrush`, once per theme variant — **20 frames, captured with
`CaptureRenderedFrame` and looked at.** Two real PNGs (900 × 300 and 40 × 24) were written beside the
working copy so the image fixture exercised a decode rather than only the placeholder.

| Fixture | Dark | Light | What was checked |
|---|---|---|---|
| `01-headings` | ✔ | ✔ | Six levels visibly distinct and descending; h6 still reads as a heading against the paragraph under it. |
| `02-emphasis-and-code` | ✔ | ✔ | Bold, italic, bold-italic, strikethrough all distinct; `code span` chips legible on the surface colour in both variants; the hard break breaks and the soft one does not. |
| `03-lists` | ✔ | ✔ | Bullets and numbers aligned in their own column; wrapped item text lines up under itself, not under the marker; three levels of nesting step in; `7. 8.` and `1) 2)` as written. |
| `04-tasks` | ✔ | ✔ | ☐/☑ in the marker column; **a task's text starts exactly where the ordinary bullet's does** — it did not before this pass, which is what found it. |
| `05-quotes-and-rules` | ✔ | ✔ | Accent rule down the left, background a shade off the page rather than a panel; a heading, a paragraph and a list inside one quote; both thematic breaks visible hairlines. |
| `06-tables` | ✔ | ✔ | The table is absent; the heading and both paragraphs around it render (PHASE02). |
| `07-fences` | ✔ | ✔ | All three fences absent; the text around them renders (PHASE02). |
| `08-images` | ✔ | ✔ | The 900 px PNG **shrunk to the pane's 408 px content width**, aspect kept, not upscaled; the 40 px one stayed its own size inside the sentence; "Image not found: assets/missing.png", "Remote image not shown: …", "Image with no link" each in a bordered placeholder. |
| `09-front-matter` | ✔ | ✔ | The YAML block does not appear; the heading and paragraph under it do. |
| `10-links-and-html` | ✔ | ✔ | Links in the accent and underlined in both variants; a labelled link, an angle autolink and a bare address all correct; `<div …>` and an inline `<b>` shown as the text they were written as. |

### How the acceptance criteria were verified

| Criterion | Result |
|---|---|
| Fixtures corpus renders correctly in dark + light — headless structural tests | **Met.** 30 renderer tests, 12 theme tests (every key in both variants, a link's accent in both, a variant switch repainting in place) and 12 corpus tests (every fixture renders, no error block, no reported failure). |
| Fixtures corpus renders correctly in dark + light — manual pass recorded | **Met.** 20 captured frames reviewed, one table row each above. One defect found and fixed: the stray space after a task box. |
| Images resolve relative to the note | **Met.** 14 Core tests over real files, five renderer tests, and the visual pass: a 900 px PNG resolved from the note's folder and held to the pane's width, a missing one showing the link as written. Also re-resolved when the folder changes — a Save As that moves the file, covered in `MarkdownPreviewViewModelTests`. |
| A parse/render exception yields the inline error block and a log entry, app alive | **Met**, three ways. A block that throws → error block in its place, rest of the note intact. The walk throwing → error block replaces the content. A `RenderFailed` handler that itself throws → still the error block, no unhandled exception. Every failure reaches `MarkdownPreviewViewModel.OnRenderFailed`, which logs at warning with the file path. A 33 000-character image link exercises the first of those from real input on Windows. |
