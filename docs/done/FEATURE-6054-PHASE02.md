# FEATURE-6054-PHASE02 — Tables, code highlight & links

**Status: DONE** · Branch: `feature/feature-6054-phase02-tables-code` · Plan: `docs/plan/FEATURE-6054.md`

## Summary

The three gaps PHASE01 left in the preview are filled, and the links in it now go somewhere.

**Tables.** A GFM pipe table comes out as a real `Grid` of bordered cells: an emphasised header row on
the theme's high surface, one-pixel rules that never double up where cells meet (each cell draws its
top and left edge, the frame around them draws the two edges no cell is beyond), and the alignment the
separator row asked for — `:---`, `:---:`, `---:` — applied per column. Columns are `Auto`, so a
two-character column does not get the same room as a sentence, and cells stretch to their column,
which is what makes the alignment visible at all. A row with fewer cells than the header leaves the
rest empty rather than shifting everything left, and `ColumnSpan` is honoured. A table wider than the
pane keeps a horizontal scroll bar of its own rather than losing its last column — the page itself
still never scrolls sideways.

**Fenced code.** A fence is tokenized with the same TextMateSharp grammars and the same Dark+/Light+
themes `EditorAppearance` highlights the editor with, so a fence in the preview and the same fence two
panes to its left are the same colours. The language is resolved the way a person writes it rather
than the way a grammar is registered — language id (`csharp`), then the extension it looks like
(`cs`, `sh`), then the aliases a language declares (`c#`, `bash`) — and the answer is cached,
including the misses, because scanning every grammar definition for an alias is the expensive case and
a preview re-renders on a timer. Tokenizer state is carried line to line, so the second line of a
block comment is still a comment. An info string that carries more than a language
(`json title=settings.json`) keeps only the first word. Indented blocks are code too. A language
nothing knows, a grammar that fails to load and a tokenizer that gives up all come out the same way:
the code, uncoloured, in the preview's own body colour — never a bordered error where someone's code
was. Lines do not wrap; a long one goes behind the block's own horizontal scroll bar.

Token colours are the one thing in the preview that cannot be a `DynamicResource` — they arrive from a
TextMate theme as literal values — so the fence is a control of its own that re-reads them when the
window changes variant. Everything else in the rendered document still repaints without being rebuilt.

**Front matter.** Hidden from the output as before, and now announced: one quiet chip naming its
top-level keys (`Front matter · title · tags · created`) and nothing of what they hold. The reading is
deliberately shallow — names off the left margin, skipping fences, comments, sequence items and
anything indented — because nothing the reader sees depends on it and a YAML parser would be a
dependency and a class of failures bought for one line of text.

**Links.** Clicking one now does something. A link is a text span and a span cannot be clicked, so the
renderer records the character range each link occupies as the inlines are added — measured off
`InlineCollection.Text`, which is exactly what the layout is built from, so nobody has to know that a
line break contributes two characters on Windows and one elsewhere — and the block turns a pointer
back into a URL by hit-testing its own text layout. That keeps a link label wrapping like the rest of
the sentence, which a button in an `InlineUIContainer` would not. Hovering one shows a hand; a drag
that ended inside a link was a selection and is left alone.

Where a click goes is decided by `MarkdownLinkTarget` over two strings: an address goes to the
platform's launcher, a file goes to `IDocumentOpener`, and a link into the document itself does
nothing yet — there is nothing to scroll to until PHASE03 builds the line-to-block index, and
resolving `#a-heading` as a path would try to open a file by that name. `other.md#a-heading` opens the
file rather than looking for one named after the anchor, and `my%20note.md` finds the file the
escaping meant.

## Files/modules touched

**Created — Core**

- `Markdown/MarkdownLinkKind.cs` — `External | Local | Fragment | Unresolvable`.
- `Markdown/MarkdownLinkTarget.cs` — where a link points, resolved against the note's folder.
- `Markdown/MarkdownFrontMatter.cs` — the top-level keys the chip lists.

**Created — Desktop**

- `Controls/MarkdownCodeToken.cs` — one coloured run of a code block.
- `Controls/MarkdownCodeHighlighter.cs` — the TextMate tokenizer, its per-theme registries and the
  language→scope cache.
- `Controls/MarkdownCodeBlock.cs` — the fence control, which re-colours itself on a variant change.
- `Controls/MarkdownLinkRegions.cs` — the character ranges of a block's links, the hit-test that turns
  a point back into one, and the pointer handlers.

**Modified — Desktop**

- `Controls/MarkdownVisualBuilder.cs` — table, fenced/indented code and front-matter-chip arms; the
  link ranges threaded through the inline walk.
- `Controls/MarkdownPreview.cs` — the `LinkActivated` event and the callback into the builder.
- `Controls/MarkdownPreviewTheme.cs` — `CodeBlockBackgroundKey`, `TableHeaderBackgroundKey` and
  `MetadataBackgroundKey` (aliases of existing keys, so `BrushKeys` stays duplicate-free), and the
  three new block classes.
- `ViewModels/MarkdownPreviewViewModel.cs` — `ActivateLinkAsync`, and the two services it needs.
- `Services/IFileLauncher.cs`, `Services/FileLauncher.cs` — `LaunchUriAsync`.
- `Views/MainWindow.axaml.cs` — the `LinkActivated` wiring, the same seam `RenderFailed` uses.

**Modified — tests, docs**

- New: `Markdown/MarkdownLinkTargetTests.cs`, `Markdown/MarkdownFrontMatterTests.cs` (Core);
  `Controls/MarkdownTableTests.cs`, `Controls/MarkdownCodeBlockTests.cs`,
  `Controls/MarkdownLinkRegionsTests.cs` (Desktop).
- Updated: `Controls/MarkdownPreviewTests.cs` (the two PHASE01 tests that asserted tables, fences and
  front matter were *not* drawn are replaced by a document-order test over every block kind, plus the
  chip tests), `Controls/FixtureCorpusTests.cs` (a remark), `Fakes/FakeFileLauncher.cs`,
  `ViewModels/MarkdownPreviewViewModelTests.cs`, `ViewModels/MainWindowViewModelTests.cs`.
- Fixtures `06-tables.md`, `07-fences.md`, `09-front-matter.md`, `10-links-and-html.md` grew into the
  manual checklist for what this phase draws.
- `README.md` — the status paragraph's "still to come in the preview" list (documentation sweep).

## Deviations & follow-ups

- **`OpenDocumentRequest` does not exist.** The plan names it for "relative `.md` opens a tab"; the
  codebase's equivalent seam is `IDocumentOpener`, which the file tree already activates files
  through. Reusing it (confirmed with you before building) means a link to a note and a double-click
  on it in the tree do the same thing, including for a non-note, which that service hands to the
  operating system. One consequence worth naming: with **Show all files** on, a relative link to a
  `.txt` opens in a tab rather than in the OS default app — the tree's rule, applied consistently,
  rather than the plan's stricter markdown-only wording.
- **A `MarkdownLinkKind.Fragment`** is in the classification beyond the plan's three-way split, so
  `[x](#section)` does not try to open a file called `#section`.
- **The front-matter chip** was the plan's optional half; you chose the key-listing variant.
- **Text blocks carrying links get `Background = Transparent`.** Avalonia does not hit-test a visual
  with no background, so without it the pointer goes straight through the paragraph to the scroll
  presenter behind it and no click ever reaches a link. Invisible, and set only on the blocks that
  have a link.
- **The manual visual pass was not performed** — this session has no way to look at the running
  window. Everything asserted below is the structural half. What still wants a human eye, in dark and
  in light, on `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Fixtures/`: `06-tables.md` (rule weight
  and header contrast, the wide table's scroll bar), `07-fences.md` (that Dark+/Light+ token colours
  sit well on the Enigma surface, and the long-line scroll bar), `09-front-matter.md` (the chip is
  quiet enough to read as metadata rather than as content), `10-links-and-html.md` (hand cursor over a
  label, browser for an address, tab for `./other-note.md`, OS default for `./budget.xlsx`).
- **Line endings:** no CRLF/LF churn was observed in this dev's diff; nothing was done about line
  endings either way, per the workflow's recommendation-only rule.
- **Follow-up, not done:** the block panel is still rebuilt wholesale per render, and a fence is
  re-tokenized with it. PHASE03's debounce makes that a per-keystroke-batch cost on a large note; if
  it ever shows, caching a fence's tokens by (code, language, variant) is the cheapest fix, and
  virtualizing the block panel is the one after that.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release` — succeeded, **zero warnings**
  (`TreatWarningsAsErrors` is on solution-wide, so a warning would have failed it).
- `dotnet test --solution Enigma.MarkdownEditor.slnx` — **1509 passed, 0 failed, 0 skipped.**
- Tests added for this phase: 13 in Core (`MarkdownLinkTargetTests`, `MarkdownFrontMatterTests` —
  external/local/fragment/unresolvable classification, anchors, escaped names, absent base path; key
  reading incl. nesting, comments, duplicates, quoted keys, CRLF) and 39 in Desktop
  (`MarkdownTableTests` — grid shape, cell placement, per-column alignment, header emphasis, inline
  formatting in a cell, short rows, the scroller, neighbours; `MarkdownCodeBlockTests` — scope
  resolution across id/extension/alias, colouring, plain fallback, blank lines, multi-line tokenizer
  state, dark vs light, the rendered control, monospace/no-wrap/scroller, line breaks, the live
  variant switch; `MarkdownLinkRegionsTests` — ranges incl. inside emphasis, after a hard line break
  and after an image, autolinks, headings, blocks with no links, and two real clicks driven through
  the headless window), plus the front-matter chip tests and the block-order test in
  `MarkdownPreviewTests`, and the link-activation tests in `MarkdownPreviewViewModelTests`.
- The acceptance criteria's manual verification of link activation is **not** covered — see
  *Deviations & follow-ups*.
