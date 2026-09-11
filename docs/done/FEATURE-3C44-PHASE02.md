# FEATURE-3C44-PHASE02 — TextMate highlighting & appearance

**Status: DONE** · Branch: `feature/feature-3c44-phase02-highlighting` · Plan: `docs/plan/FEATURE-3C44.md`

## Summary

The editor stops being grey text. Markdown is tokenized by the TextMate markdown grammar, so headings,
emphasis, quotes, lists, links and inline code each get their own colour — and a fenced block is handed
to whichever language it names, which is how `csharp`, `json` and `bash` come out coloured as C#, JSON
and shell rather than as a wall of markdown. A fence naming a language nobody has stays plain text: it
is still recognised as a code block, there is simply no embedded grammar to colour it with.

Two palettes meet in the editor, and the split is deliberate. **Token colours come from a TextMate
theme** — Dark+ and Light+, Visual Studio Code's own pair, chosen with you — because nothing in the
Enigma palette has an opinion about what colour a C# keyword is. **Everything else comes from the
Enigma brushes**: the plain-text foreground, the line-number gutter, the caret, the selection and the
current-line highlight. That is what keeps the middle of the window looking like the panes either side
of it instead of importing a second design language.

Both halves follow the theme variant live. Switching the app between dark and light — from the
settings window, or by the OS changing under a `System` setting — repaints the editor in the same step
as everything else, with no restart and no reopening of the document.

One thing worth knowing about the package: **AvaloniaEdit.TextMate 12 applies none of a theme's editor
colours itself.** Earlier versions pushed `editor.background`, `editor.selectionBackground` and friends
onto the control; 12's `Installation` colours tokens and leaves the rest to the caller. So the chrome
below is not overriding TextMate — without it those brushes would simply be whatever AvaloniaEdit's own
Fluent theme left them, which is where the mismatched blue selection and the mint-green current line
came from.

## Files/modules touched

### Created — `src/Enigma.MarkdownEditor.Desktop`

| File | Notes |
|---|---|
| `Views/EditorAppearance.cs` | Owns how one `TextEditor` is painted. Installs TextMate, gives it the markdown scope, and holds the theme pair; `Apply(ThemeVariant)` swaps the TextMate theme and re-resolves the five Enigma brushes in one call. `ThemeNameFor` is static and pure, which is the part a unit test can reach. Disposing it takes the line transformer back off the text view and stops the background tokenizer. |

### Modified

| File | Change |
|---|---|
| `Desktop/Enigma.MarkdownEditor.Desktop.csproj` | `AvaloniaEdit.TextMate` (12.0.0, already pinned in CPM). It brings `TextMateSharp` and `TextMateSharp.Grammars` 2.0.3 transitively — the grammars, the ones fenced blocks embed, and the themes all arrive with it, and neither needs a pin of its own because nothing references them directly. |
| `Desktop/Views/EditorPaneView.axaml.cs` | Builds `EditorAppearance` in `OnLoaded` and disposes it in `OnUnloaded`; forwards `ActualThemeVariantChanged` to it. Not the constructor — see *Deviations* 2. |
| `Desktop/Views/EditorPaneView.axaml` | Comment only: says that the editor's brushes are assigned by `EditorAppearance` rather than bound here, so the absence of `Foreground`/`LineNumbersForeground` in the markup is not read as an oversight. |
| `Desktop/ViewModels/DocumentManagerViewModel.cs` | `OnHighlightingFailed(Exception)` — the sink the tokenizer's background thread reports into. Logs once per run and shows the user nothing; see *Deviations* 3. |
| `README.md` | The status blockquote said syntax highlighting was still to come. Documentation freshness sweep. |

### Created — tests (588 total, was 570)

| File | Notes |
|---|---|
| `Desktop.UnitTests/Views/EditorAppearanceTests.cs` (7) | The variant → theme mapping in both directions, the fallback for a variant the app does not define, the null guard, and that both themes of the pair still load out of the package — the last one is what would catch a `TextMateSharp.Grammars` bump renaming or dropping one, which otherwise shows up only as an editor that stops re-colouring halfway through a switch. |
| `Desktop.UnitTests/Views/MarkdownHighlightingTests.cs` (11) | What the editor actually colours, driven through the grammar instead of the screen: `.md` resolves to `text.html.markdown`, the grammar loads, headings/bold/inline-code/quotes/both list kinds carry markdown scopes, `csharp`/`json`/`bash` fences produce `meta.embedded.block.{csharp,json,shellscript}`, and an invented language produces a fenced block with **no** embedded scope at all. |

## Deviations & follow-ups

1. **The grammar is markdown for every tab, not one picked from the file's extension.** That is what
   the plan says, and it is the right default for a markdown editor. It does mean a non-markdown file
   opened through *Show every file* is tokenized as markdown — harmless in practice (it comes out
   mostly plain) but not right. **Follow-up if you want it**: `EditorAppearance` already holds the
   `RegistryOptions`, so following the active document's extension is `GetScopeByExtension` on the tab
   switch plus a fallback to markdown when it returns nothing. Left out as scope.
2. **`EditorAppearance` is built in `OnLoaded`, not in the constructor.** Two reasons, both hard
   requirements: finding an `Enigma*` brush means walking up to the application's resources, which
   only works once the control is in a tree; and `ActualThemeVariant` is only resolved to `Dark` or
   `Light` at that point — asked in the constructor it is still `Default`, and the editor would have
   started on the light theme in a dark window. It is disposed in `OnUnloaded` for the symmetric
   reason: it owns a background tokenizer that should not outlive what it is colouring.
3. **A highlighting failure is logged once and never shown.** Tokenizing runs per line on a background
   thread, so a grammar that throws throws for every line of every document; without the interlocked
   once-per-run guard the log file would be nothing else. Nothing reaches the user because there is
   nothing they can do — the text is still there, still correct and still editable, only its colours
   are missing. Passing no handler at all was the alternative and is worse: the exception would come
   out of a background thread and take the process down over a colour.
4. **The theme pair is fixed in code, not a setting.** Dark+/Light+ was your choice from the 21 themes
   `TextMateSharp.Grammars` ships. **Follow-up if you want it configurable**: it is one `AppSettings`
   property, one settings-window row and one line in `ThemeNameFor` — but it is not in this plan, so
   it is not here.
5. **The current line is filled, not outlined.** `CurrentLineBorder` is set to a transparent pen so
   only `EnigmaHoverBrush` shows. AvaloniaEdit's default draws a 1 px mint-green border, which reads
   as a box round the text rather than as the hint the plan asked for.
6. **`SelectionForeground` and `SelectionBorder` are left alone.** Both default to null in AvaloniaEdit
   and nothing in this app sets them, so selected text keeps its token colours over the Enigma
   selection fill. Worth knowing if the Enigma selection brush ever turns opaque enough to swallow
   them.
7. **No headless UI test project was added.** Verified instead with a throwaway headless probe outside
   the repository, the same way FEATURE-0F9A PHASE02 and FEATURE-0DE1 PHASE01 verified their visual
   criteria — `Avalonia.Headless` is still pinned-but-unreferenced, and `docs/plan/FEATURE-6054.md` is
   where it is planned to enter the test project for real.
8. **Linux was not verified** — no Linux machine was available in this session. Nothing here is
   platform-specific: the grammars and themes are managed resources inside `TextMateSharp.Grammars`,
   and the brushes come from the Enigma theme.
9. **Line endings (recommendation only, no action taken).** Every file in this diff is LF and
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

> dotnet test Enigma.MarkdownEditor.slnx
Test run summary: Passed!
  total: 588   failed: 0   succeeded: 588   skipped: 0
```

588 tests = 570 before, plus 18 added here (7 `EditorAppearanceTests`, 11 `MarkdownHighlightingTests`).

### How the visual criteria were verified

A throwaway probe outside the repository hosted the **real `EditorPaneView`** — bound to a real
`DocumentManagerViewModel` resolved from a container composed with the app's own `AddEnigmaServices` /
`AddShell` — inside a headless window rendered by `Avalonia.Headless` + `Avalonia.Skia`
(`UseHeadlessDrawing = false`, `CaptureRenderedFrame()`), with the real `App` supplying the FluentTheme,
the AvaloniaEdit control theme and the Enigma dictionary. It opened a sample containing every markdown
construct plus four fenced blocks, then switched `RequestedThemeVariant` at run time. Nothing in
`%APPDATA%\Enigma.MarkdownEditor` was written beyond the log the app always writes.

`editor.TextArea.TextView.LineTransformers` reported `SelectionColorizer, TextMateColoringTransformer`
— the installation is on the control the pane actually shows.

| Variant | Chrome (Enigma) | Token colours in the rendered frame (TextMate) |
|---|---|---|
| `Dark` | fg `#bcbec4` · gutter `#4e5157` · caret `#bcbec4` · selection `#214283` · current line `#2e3035` | 10 distinct: `#569cd6` `#4ec9b0` `#ce9178` `#6a9955` `#dcdcaa` `#9cdcfe` `#b5cea8` `#6796e6` `#d4d4d4` `#bcbec4` |
| `Light` | fg `#1e1f22` · gutter `#a0a3aa` · caret `#1e1f22` · selection `#c4d8f8` · current line `#e8eaed` | 11 distinct: `#001080` `#0451a5` `#098658` `#a31515` `#795e26` `#267f99` `#1e1f22` and the four Light+ names `Blue` `Navy` `Maroon` `Black` |
| `Dark` again | identical to the first row, to the byte | identical to the first row |

| Criterion | Result |
|---|---|
| Markdown structure and fenced C#/JSON/bash are coloured | **Met.** In the captured frames: heading blue and bold, `**bold**` bold, `` `inline code` `` orange, the quote marker green, both list kinds marked; the C# fence shows keyword/type/string/interpolation colours, the JSON fence property-name/string/number/`true`, the bash fence its builtin and its string. Asserted independently at grammar level by `MarkdownHighlightingTests`. |
| Unknown fence languages degrade to plain | **Met.** The ```` ```wubbalubba ```` block renders in the plain foreground; the grammar test confirms it carries `markup.fenced_code.block.markdown` and **no** `meta.embedded.block.*` scope. |
| Switching app theme swaps editor colours live, no restart | **Met.** Table above: one assignment to `RequestedThemeVariant`, no reload of the document and no rebuild of the pane, and both the chrome and all eleven token colours changed — then changed back exactly on the return trip. |
| A ~1 MB markdown file stays responsive | **Met.** A generated 1,000,031-byte / 65,437-line markdown file (headings, lists, emphasis, a C# fence per section) opened into a second tab, fully highlighted: **99 ms** to open and read, **18 ms** to the first rendered frame, and **8.2 ms** per edit averaged over 50 consecutive insertions at offset 0 — the tokenizer runs on its own thread and the viewport is tokenized first, so neither the open nor a keystroke waits for the other 65,000 lines. |
