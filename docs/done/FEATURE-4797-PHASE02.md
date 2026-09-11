# FEATURE-4797-PHASE02 — Rich-text (HTML) flavour

**Status: DONE** · Branch: `feature/feature-4797-phase02-preview-html` (cut from
`feature/feature-4797-phase01-preview-copy`)
Plan: `docs/plan/FEATURE-4797.md` · Roadmap: `docs/roadmap.md`

## Summary

A copy of the whole preview pane now puts two flavours on the clipboard in a single write: the plain
text PHASE01 already produced, and an HTML fragment beside it. The receiving application chooses at
paste time — Notepad takes the text, Word and Outlook take the HTML and keep the headings, bold,
links and tables. A selection inside one paragraph is unchanged and still goes as plain text alone.

The HTML is the same markdown through the same Markdig pipeline the pane renders from, so the two
cannot disagree about what the markup means. It is **deliberately unstyled** — no fonts, no sizes, no
colours — because the pane may be in dark theme and the page being pasted into is white; the only
`style` it ever emits is a table cell's `text-align`, which is content the author wrote as `:---:`
rather than appearance the pane imposed. The one thing rewritten is an image link: a relative `src`
means nothing once the markup has left this process, so a link that resolves to a file on this
machine becomes an absolute `file://` URI through `MarkdownImageSource` — the same resolution the
renderer draws from.

## Files/modules touched

**Created**

- `src/Enigma.MarkdownEditor.Core/Markdown/MarkdownHtml.cs` — `Render(markdown, basePath)`: parse,
  absolutize image links on the AST, render.
- `src/Enigma.MarkdownEditor.Desktop/Services/ClipboardHtmlFormat.cs` — the platform's format
  identifier, the CF_HTML envelope and its byte offsets, and the payload bytes.
- `tests/Enigma.MarkdownEditor.Core.UnitTests/Markdown/MarkdownHtmlTests.cs` — 20 tests, rule by rule.
- `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Services/ClipboardHtmlFormatTests.cs` — 19 tests,
  the offsets read back out of the produced document and used to index its own bytes rather than
  recomputed, so the assertions are what a consumer does.

**Modified**

- `Services/IClipboardWriter.cs` + `ClipboardWriter.cs` — `WriteTextAndHtmlAsync`.
- `ViewModels/MarkdownPreviewViewModel.cs` — `TextOf` became `FlavoursOf`, and `CopyAsync` picks the
  one-flavour or two-flavour write from it.
- `tests/…/Fakes/FakeClipboardWriter.cs` — records the HTML of each write as well as its text.
- `tests/…/ViewModels/MarkdownPreviewViewModelTests.cs` — 3 copy tests.
- `tests/…/Controls/MarkdownPreviewCopyTests.cs` — an unstyled-HTML theory over the fixture corpus and
  an image-resolution test, plus a `Styles` helper.

No XAML, no new package, no new setting, no DI change: the flavour rides the service PHASE01 already
registered.

## Deviations & follow-ups

**The plan's clipboard API did not exist, exactly as PHASE01's completion doc predicted.** Re-read
against Avalonia 12.1.2 before building. `DataObject` is gone; the equivalent is one
`DataTransferItem` carrying both formats, added to a `DataTransfer`, written with
`IClipboard.SetDataAsync`. One *item* rather than one per flavour is deliberate and documented by
Avalonia: Windows and X11 support a single item for every format except `File`, so a second item
would be dropped rather than offered as an alternative. The transfer is not disposed — `SetDataAsync`
documents that the clipboard takes ownership.

**A string HTML flavour would have been silently broken on Windows — a finding the plan could not
have had.** Decompiling `Avalonia.Win32.OleDataObjectHelper` shows `DataFormat<string>` goes through
`WriteStringToHGlobal`, which copies the `char` buffer verbatim — **UTF-16**. CF_HTML must be UTF-8,
because its own header counts UTF-8 *bytes*; a UTF-16 payload makes every offset wrong and the paste
arrive empty, which is the exact failure the plan named. `DataFormat<byte[]>` goes through
`WriteBytesToHGlobal`, which writes the span untouched, and X11's `SelectionDataProvider` writes a
`byte[]` format verbatim too. So the flavour travels as
`DataFormat.CreateBytesPlatformFormat(…)` and the encoding is decided in one place. This is a change
of mechanism, not of intent: it is what the plan's own acceptance criterion was asking for.

**The CF_HTML wrapper lives beside `ClipboardWriter`, not inside it.** The plan put it "in the one
place that knows about platforms", but the offsets have to be assertable and this solution has no
`InternalsVisibleTo` anywhere — adding one would have been a new pattern. `ClipboardHtmlFormat` is a
public static class in the same `Services` namespace, which keeps the platform knowledge in one place
and lets the test index the bytes.

**`Payload` appends a `NUL` on Windows; `WrapCfHtml` does not.** `WriteBytesToHGlobal` allocates
exactly the span's length, so nothing terminates the buffer unless we do, and a consumer reading the
handle as a C string would run past the allocation. The terminator belongs to the bytes rather than
to the document, because every offset in the header is measured against the document.

**Raw HTML is carried through as HTML, which the pane does not do.** The pane shows
`<div class="callout">…</div>` as the literal text it was written as; this flavour emits it as a real
div, because `Markdown.ToHtml` is what the plan specified and because the author who wrote HTML meant
HTML. A deliberate difference from PHASE01's "what is copied is what is on screen" rule, asserted by
`RawHtml_IsCarriedThroughAsHtml` so it is a decision on the record rather than something nobody
noticed.

**The manual verification criterion was superseded by decision** (yours, during the build) after the
Windows path turned out to be provable without a paste target — see *Build/test evidence* and the
struck-through criterion in the plan. What remains genuinely unverified is narrow but real, and is
listed below.

**Follow-ups (not acted on)**

- **Nobody has watched Word render the paste.** The bytes are proven correct on Windows; how Word,
  Outlook and LibreOffice *display* valid markup is not, and neither is the X11 `text/html` target on
  a real Linux session. A minute with each before the 1.5.0 release item would close it. Declining
  the documentation sweep left this out of `docs/RELEASE.md` §2, so it lives here only.
- **The clipboard does not survive the application exiting.** Avalonia writes through
  `OleSetClipboard`, which delegates rendering to the writing process, and nothing calls
  `IClipboard.FlushAsync`. Observed directly during verification: a copy made by a probe process was
  gone from the clipboard once that process ended. Copy-then-paste while the editor is running — the
  normal case — is unaffected. **Not introduced here**: PHASE01's plain-text write goes through the
  same call, so this is pre-existing behaviour for every copy the application makes, and worth its own
  work item rather than a change smuggled into this one.
- **macOS would get the wrong identifier.** `Identifier` is `"HTML Format"` on Windows and
  `"text/html"` everywhere else; macOS wants the UTI `"public.html"`. Only `win-x64` and `linux-x64`
  ship, so this is correct for every platform that exists today, and a branch for one that does not
  would be untested speculation. Worth a line if macOS is ever added.
- `FixtureCorpusTests` and `MarkdownPreviewCopyTests` still each hold a private copy of the
  fixture-folder listing — carried over from PHASE01, still worth hoisting if a third class needs it.
- Line endings: nothing to report. Every file touched here is LF, and `.gitattributes` already
  normalizes.

## Build/test evidence

- **Release build, clean**: `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental
  -v normal` → **0 Warning(s), 0 Error(s)**, and no `AVLN` diagnostics in the normal-verbosity log.
- **Full suite green**: `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` →
  **3432 passed, 0 failed, 0 skipped** (3379 before this dev, so **53 new**).
- **New tests**: 53, accounting exactly for the delta — 20 in `MarkdownHtmlTests`, 19 in
  `ClipboardHtmlFormatTests` (8 facts and 4 theories over 11 cases), 3 in
  `MarkdownPreviewViewModelTests`, and 11 added to `MarkdownPreviewCopyTests` (a theory over all 10
  fixtures, plus the image-resolution fact).
- **Real Windows clipboard round-trip, verified end to end.** The write was driven through a real
  Avalonia desktop window using the product's own `ClipboardHtmlFormat`, and the clipboard read back
  out of process through Win32 `RegisterClipboardFormat` / `GetClipboardData` / `GlobalLock`:
  - the formats on the clipboard were `System.String, UnicodeText, Text, HTML Format` — both flavours
    present, and CF_HTML registered under the name Word looks for;
  - the `HGLOBAL` was **380 bytes**, last byte `0x00`, matching `EndHTML:0000000379` + the terminator;
  - the bytes decoded as **UTF-8** — `Café — 日本語` came back intact (PowerShell's `Get-Clipboard`
    shows it as `CafÃ© â€” æ—¥æœ¬èªž`, which is that same UTF-8 read as CP1252 and is itself
    confirmation the bytes are right);
  - slicing the buffer at the declared `StartFragment:139` / `EndFragment:345` returned **exactly**
    the fragment, multi-byte characters unbroken at both ends.

  This is the criterion the plan expected to be unprovable without a paste target. It covers
  everything the manual check was there to catch except how Word chooses to render valid markup — see
  *Follow-ups*.
- **Every acceptance criterion met**, with the manual-verification criterion superseded by decision
  and recorded as such in the plan.
