# FEATURE-3C44-PHASE03 — Markdown editing assists

**Status: DONE** · Branch: `feature/feature-3c44-phase03-assists` · Plan: `docs/plan/FEATURE-3C44.md`

## Summary

The editor starts writing markdown with you. **Enter** inside a list carries the marker down — a
bullet repeats, a number counts on (`9.` → `10.`, `09.` → `10.`), a task item starts a fresh unchecked
box whatever state the one above it is in, a block quote stays quoted, and nesting and spacing are
copied exactly as they were written. Enter on an item that has nothing in it clears the marker instead
and ends the list, which is how a list is finished without reaching for the mouse.

**Tab and Shift+Tab** move an item between levels. Tab lines the item up with the *text* of the item
above it — 2 columns under `- foo`, 3 under `1. foo`, 6 under `- [ ] foo` — which is the indentation
CommonMark actually reads as nesting, so what the file means matches what it looks like. Shift+Tab
takes it back to the nearest shallower level. Neither touches a line that is not an item: there Tab is
still AvaloniaEdit's own.

**Ctrl+B, Ctrl+I and Ctrl+K** wrap the selection, or the word the caret is in, and Ctrl+B/Ctrl+I take
the markers back off when they are already there. Asked for italic over `**bold**` they add rather
than tear one asterisk off it, so you get `***bold italic***` instead of a broken pair. Ctrl+K builds
`[text](…)` with the caret waiting in the address. All three are in the editor's context menu too,
alongside the save commands and for the same reason: this window has no menu bar, so an undiscoverable
shortcut is an unused one.

**An image on the clipboard** becomes a file rather than nothing. Ctrl+V writes it as a PNG into the
note's image folder — `ImageFolderName`, `assets` by default, created on demand — named after the note
and the moment, and inserts `![](assets/note-20260828114500.png)` at the caret. Two pastes in the same
second do not collide: the second is numbered the way a file manager numbers, and because that puts a
space in the name the link comes back angle-bracketed (`![](<assets/note-… (2).png>)`) so a parser does
not end it at the space. Anything else on the clipboard pastes exactly as before, and an image that
cannot be filed says so in an info bar and falls through to the ordinary paste.

**Everything above is gated.** `IEditingAssistPolicy` is the seam vim emulation speaks through: under
FEATURE-11AA, normal and visual mode are not typing, and Enter there must be a command rather than a
new bullet. Until that lands the registered policy answers yes and the assists are always on — chosen
with you rather than assumed (see *Deviations* 1).

Every rule lives in Core as a decision over a plain string, which is what makes the whole continuation
matrix testable without a text control.

## Files/modules touched

### Created — `src/Enigma.MarkdownEditor.Core/Markdown`

| File | Notes |
|---|---|
| `ListContinuation.cs` | `Parse` splits a line into indent / quote / marker / spacing / checkbox / content; `OnEnter`, `OnIndent` and `OnOutdent` answer with a `TextEdit` or with `null` for "leave this to the editor". The two indentation methods take the lines above as a **lazily enumerated, nearest-first** `IEnumerable<string>` and read only as far as the first answer — the editor walks the document backwards, so a rule that read all of them would cost the whole file on every Tab. |
| `ListItemPrefix.cs` | The parts, kept verbatim so the next line is written the way this one was. Derives `Kind`, `IsTask`, `IsEmpty`, `Length`, `IndentWidth`, `ContentColumn` and `Continuation` (the marker counted on, the task box reset). `ColumnAfter` expands tabs to CommonMark's 4-column stops. |
| `ListMarkerKind.cs` | `None` (a quote line), `Bullet`, `Ordered`. |
| `TextEdit.cs` | One replacement plus where the caret and selection land. Offsets are into **the string that was passed in** — a line for the list assists, the lines a selection covers for the formatting ones — which is the rule that lets the editor add a window offset and a test assert on a string. |
| `InlineFormatting.cs` | `Toggle` and `Link`. Recognises the delimiter inside the selection or just outside it, and only when the run of that character is exactly the delimiter's length — that last part is what keeps italic off `**bold**`. With nothing selected it takes the word under the caret; with no word, it inserts the pair around the caret. |
| `ImageLink.cs` | `FileName` (note stem + `yyyyMMddHHmmss` + `.png`), `RelativePath` (relative to the note's folder, forward slashes), `Markdown` (angle-bracketed when the path holds a space, tab or bracket). |

### Created — `src/Enigma.MarkdownEditor.Desktop`

| File | Notes |
|---|---|
| `Services/IEditingAssistPolicy.cs` | The seam. One property: are the assists active for the keystroke about to be handled. |
| `Services/EditingAssistPolicy.cs` | The answer for an editor with no modes — yes. What FEATURE-11AA replaces. |
| `Services/IPastedImage.cs` / `Services/ClipboardImage.cs` | An image on its way to a file. The interface exists so the filing rule — folder, name, collision, link — is reachable from a unit test; the implementation is the one line that genuinely needs the graphics stack, `Bitmap.Save(path, PngBitmapEncoderOptions.Default)`. |
| `Services/IImagePasteService.cs` / `Services/ImagePasteService.cs` | Creates the folder, numbers past a taken name via `UniqueName`, writes, and returns the markdown. Nothing throws at the caller: a paste is a keystroke, and the only useful answer to one that failed is a sentence saying so, so every failure ends as an info bar and a `null`. |
| `Views/MarkdownAssists.cs` | The editor half. A **tunnelling** `KeyDown` handler on the `TextEditor`, for the same reason the window takes Ctrl+Tab that way — Enter and Tab already belong to AvaloniaEdit's input handler, and a bubbling handler would only see what it left. Reads the caret's line out of the document, applies the answer inside one `RunUpdate()` so an assist is one Ctrl+Z, and leaves every keystroke it does not want unhandled. Also the public `Bold()` / `Italic()` / `Link()` / `PasteAsync()` the context menu calls. |

### Modified

| File | Change |
|---|---|
| `Desktop/ViewModels/DocumentManagerViewModel.cs` | Two new dependencies (`IEditingAssistPolicy`, `IImagePasteService`); `AssistsActive` (read per keystroke, not bound — it is a question about this instant); `PasteImageAsync(IPastedImage)`, which is here because *which note* the image is filed beside is this class's question while reading the clipboard is the view's. |
| `Desktop/Views/EditorPaneView.axaml.cs` | Builds `MarkdownAssists` in the constructor — unlike `EditorAppearance` it needs neither theme resources nor a resolved variant. Paste routed through it so the context menu files images too; three new menu handlers. |
| `Desktop/Views/EditorPaneView.axaml` | Bold / Italic / Link rows in the editor's context menu, with their gestures. |
| `Desktop/Extensions/ServiceCollectionExtensions.cs` | `IImagePasteService` and `IEditingAssistPolicy`, both singleton. |
| `README.md` | The status blockquote described an editor that only opened, saved and coloured files. Now says it writes markdown with you, and that the middle pane is finished. Documentation freshness sweep. |

### Created / modified — tests (698 total, was 588)

| File | Notes |
|---|---|
| `Core.UnitTests/Markdown/ListContinuationTests.cs` (71) | The continuation matrix: every marker kind, nesting, quote depth, spacing, numbered increment including zero-padding, empty-item termination, the caret-inside-the-marker case, the whole Tab/Shift+Tab table, tab-column expansion, and a counting enumerable proving the lines above are read only as far as the answer. |
| `Core.UnitTests/Markdown/InlineFormattingTests.cs` (17) | Wrap, unwrap from inside and from outside, word-under-caret, bare caret, the bold/italic interaction in both directions, round trip, the link's caret placement, and the range/argument guards. |
| `Core.UnitTests/Markdown/ImageLinkTests.cs` (9) | Naming, the relative link and its forward slashes, and the angle-bracketed form. |
| `Core.UnitTests/Markdown/Edits.cs` | Applies a `TextEdit` the way the editor does, so a test asserts on the text that comes out rather than on four numbers. |
| `Desktop.UnitTests/Services/ImagePasteServiceTests.cs` (6) | The folder created on demand, the folder setting honoured, a note in a subfolder, a failed write reported and unlinked, cancellation, and the guards. |
| `Desktop.UnitTests/ViewModels/DocumentManagerViewModelTests.cs` (+5) | The policy gate; a paste filed and linked; a second paste in the same second not overwriting the first; a paste with nothing open; a failed paste reported. The nine-argument guard test grew two positions. |
| `Desktop.UnitTests/Fakes/FakeEditingAssistPolicy.cs`, `FakePastedImage.cs`, `EditorHarness.cs` | A switchable policy, an image without a graphics stack, and the harness wiring — the harness uses the **real** `ImagePasteService` over its own throw-away folder, because what lands on disk is the point. |

## Deviations & follow-ups

1. **The vim gate was built now rather than left to FEATURE-11AA — your call, asked before building.**
   The plan's fourth acceptance criterion is about vim modes, and the vim engine does not exist yet, so
   the criterion could not be met as written. `IEditingAssistPolicy` is the seam: injected, registered,
   covered by the composition-root test, and exercised through `AssistsActive`. **The vim half of that
   criterion is deferred to FEATURE-11AA**, which has to (a) register a mode-aware policy in place of
   `EditingAssistPolicy` and (b) keep `o`/`O` from continuing lists, which the plan calls for
   explicitly and which falls out of the gate for free since those never reach this handler.
2. **Tab aligns to the parent item's content column — also your call, from three options.** The
   consequence worth knowing: **Tab on an item that is already a child of the one above it does
   nothing.** That is not a missing case. Markdown expresses nesting by lining a child up with its
   parent's text, and an item already there has no deeper level to move to without a sibling to nest
   under; adding spaces anyway would widen the file without changing what it means. Pressing Tab on the
   *next* item, which then has a sibling above it, nests as expected.
3. **The formatting shortcuts also went into the context menu.** Not named in the plan. The pane's
   existing comment gives the reason for the save commands being there — no menu bar, so an
   unadvertised shortcut is an unused one — and it applies to Ctrl+B/I/K identically. Three rows of
   XAML; say the word and they come out.
4. **`InlineFormatting` and `ImageLink` live in Core, though the plan only named `ListContinuation`
   there.** Same rule, same reason: both are pure decisions over strings, and in Core they are covered
   by 28 tests that would otherwise need a text control and a clipboard.
5. **Ctrl+V is intercepted whatever is on the clipboard.** It has to be: the clipboard can only be
   asked asynchronously, so the keystroke is taken before the answer is known, and the ordinary paste
   is then performed here rather than left to AvaloniaEdit. Verified both ways in the probe below.
6. **A clipboard that throws is silent.** A clipboard another application is holding open, or an image
   that cannot be decoded, falls through to the ordinary paste with nothing said — that paste *is* the
   answer, and it is the one the user asked for. A failure while **filing** an image does reach an info
   bar, because there the work was started and then lost.
7. **`ImagePasteService` catches broadly**, with the same justification `DocumentOpener` records for
   the file launcher: encoding a bitmap goes through the platform's graphics stack, whose failures are
   not a documented set, and a paste that silently does nothing is the one outcome the user cannot act
   on.
8. **The image folder setting is used as written, including `../shared` or an absolute path.** Those
   resolve and the link comes out relative to the note (`../shared/…`). No extra rule was added to
   forbid them: they are reachable only by hand-editing the setting or typing it into the settings
   window, and they are as likely to be deliberate as not.
9. **A non-item Enter in a document with no line break at all inserts `Environment.NewLine`** — CRLF on
   Windows. That is AvaloniaEdit's own fallback and pre-dates this phase: it takes the delimiter from
   the line it is splitting, and a file the editor read has LF everywhere (normalized on read), so this
   only shows up in a buffer that has no line yet. It is harmless in every case — `DocumentFile.Write`
   re-applies the file's own style on save — and the probe checks both paths.
10. **No headless UI test project was added.** Verified with a throwaway probe outside the repository,
    the same way PHASE01 and PHASE02 verified their behavioural criteria; `Avalonia.Headless` is still
    pinned-but-unreferenced, and `docs/plan/FEATURE-6054.md` is where it is planned to enter the test
    project for real.
11. **Linux was not verified** — no Linux machine was available in this session. Nothing here is
    platform-specific except the PNG encode, which is Skia on both, and the path handling, which goes
    through `Path` and is asserted with `Path.Combine` in the tests. The plan asks for the Linux
    paste-image check to be recorded: **it has not been done.**
12. **Line endings (recommendation only, no action taken).** Every file in this diff is LF and
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
  total: 698   failed: 0   succeeded: 698   skipped: 0
```

698 tests = 588 before, plus 110 added here: 97 in `Core.UnitTests/Markdown` (71 + 17 + 9), 6 in
`ImagePasteServiceTests`, 5 in `DocumentManagerViewModelTests`, and 2 more cases on the
composition-root theory.

### How the behavioural criteria were verified

A throwaway probe outside the repository hosted the **real `EditorPaneView`** — bound to a real
`DocumentManagerViewModel` resolved from a container composed with the app's own `AddEnigmaServices` /
`AddShell`, with the real `App` supplying the FluentTheme, the AvaloniaEdit control theme and the
Enigma dictionary — in a headless window (`Avalonia.Headless` + `Avalonia.Skia`,
`UseHeadlessDrawing = false`). It opened a real file, focused the text area, and drove **real key
events through the window** (`HeadlessWindowExtensions.KeyPress`), so every result below went through
the same tunnel/bubble route and the same AvaloniaEdit input handler as a keypress in the running app.
**27 checks, all green.**

| Criterion | Result |
|---|---|
| Continuation matrix green in Core.UnitTests | **Met.** 71 cases over every marker kind, nesting, quote depth, spacing, the numbered increment (including `09.` → `10.` and `007.` → `008.`), and empty-item termination. |
| Shortcuts work with selection and bare caret, and toggle cleanly | **Met.** In the probe, through real keystrokes: `Ctrl+B` on a bare caret in `hello world` → `**hello** world`; `Ctrl+B` again → `hello world`; `Ctrl+I` → `*hello* world`; `Ctrl+K` → `[hello]() world`; `Ctrl+B` and `Ctrl+K` over a selection → `see the **docs** here` / `see the [docs]() here`. 17 further cases in `InlineFormattingTests`. |
| Paste-image produces the file + correct relative link on Windows | **Met.** Through `Ctrl+V` with a real bitmap on the real clipboard: a 94-byte file whose first four bytes are `137 80 78 71` (PNG) landed in `<note dir>\assets`, and `![](<assets/note-20260828073348 (3).png>)` went into the document. A second paste in the same second produced a second file, not an overwrite. Ctrl+V with **text** on the clipboard still pasted the text. |
| Paste-image on Linux | **Not verified** — no Linux machine this session. See *Deviations* 11. |
| Graceful info-bar fallback otherwise | **Met.** `ImagePasteServiceTests` and `DocumentManagerViewModelTests`: a write that throws produces one info bar and a `null` link, and nothing is inserted. |
| With vim enabled: assists inactive in normal/visual | **Deferred to FEATURE-11AA** — there is no vim engine to enable. The seam it will speak through is built, registered and tested (`AssistsActive` follows the policy; the composition root registers `IEditingAssistPolicy` as a singleton). See *Deviations* 1. |
| Enter in insert mode still continues lists | **Met** for the standard editing that insert mode will be: `- a` → `- a\n- `, `1. a` → `1. a\n2. `, `- [x] a` → `- [x] a\n- [ ] `, `> a` → `> a\n> `, `  - a` → `  - a\n  - `, and `- ` → an empty line. |
| Assists leave everything else alone | **Met.** Enter on `one\nplain` inserted LF via AvaloniaEdit's own handler; Tab on `plain` inserted a literal tab. |
| One assist, one undo | **Met.** A continued bullet came off in a single `Undo()`, back to `- a`. |
