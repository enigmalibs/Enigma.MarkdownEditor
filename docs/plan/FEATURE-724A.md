# FEATURE-724A — Vim: repeat, registers & search

**Status: DONE** · 5 phases · Roadmap: `docs/roadmap.md`
Planned on `main`; each phase cuts its own branch from `HEAD` at build time:
`feature/feature-724a-phase01-motions-editing`, `feature/feature-724a-phase02-visual-commands`,
`feature/feature-724a-phase03-registers-repeat-macros`, `feature/feature-724a-phase04-search-substitute-set`,
`feature/feature-724a-phase05-screen-motions`.

## Objective

Take the next tier of vim off the engine's deferred list. `README.md` §*Not implemented* still opens
with `.` repeat, named registers, macros and regex search; the completion docs of `FEATURE-11AA` and
`FEATURE-23AB` name the visual-mode commands "refused by name, one line each", `?`, `:q!`, `Home`/`End`
and the screen motions as the obvious next additions. This item implements all of those, in five
thematic phases, and rewrites the README key reference as each one lands.

| Phase   | Title                           | Adds                                                                                                                                                                                                                                                                                              |
|---------|---------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| PHASE01 | Motions & editing extras        | `W` `B` `E` + `iW` `aW` · `^` `%` (and `N%`) `{` `}` `(` `)` `+` `-` `_` `\|` · `Home` `End` `Space` `Backspace` `Enter` as motions · `J` `gJ` · `s` `S` `X` · `>>` `<<` and the `>` `<` operators · `gu` `gU` `g~` (+ `guu` `gUU` `g~~`) · `ZZ` `ZQ` · `:q!` `:x` `:wq!` · `VimOptions` seeded by the adapter |
| PHASE02 | Visual-mode commands            | On the selection: `x` `X` `D` `p` `P` `r` `s` `C` `S` `Y` `~` `u` `U` `>` `<` `J` `gJ` `o` `gv`                                                                                                                                                                                                    |
| PHASE03 | Registers, repeat & macros      | `"a`–`"z`, `"A`–`"Z` append, `"0` yank, `"_` black hole · `.` repeat with a count, counts on `i` `a` `I` `A` `o` `O` · `q{a-z}` `q` `@{a-z}` `@@` · "recording @a" in the status slot                                                                                                              |
| PHASE04 | Regex search, substitute & :set | `/` `?` as .NET regex with `\c` `\C` · direction-aware `n` `N` · `*` `#` · search, `n` `N` `*` `#` behind an operator and in visual mode · count on `/` · `:[range]s/pat/rep/[g][i][I]` · `:N` `:$` · `:set`                                                                                     |
| PHASE05 | Screen motions & viewport       | `H` `M` `L` · `Ctrl+D/U/F/B/E/Y` · `PageUp` `PageDown` · `zz` `zt` `zb` `z<CR>` `z.` `z-` · a viewport seam on `IVimBuffer`                                                                                                                                                                    |

Behaviour follows vim unless a line in this plan says otherwise; every deliberate difference is listed
under the phase that introduces it and must be repeated in that phase's completion doc.

## Context & constraints

- **`Enigma.MarkdownEditor.Core` never references Avalonia.** Everything about *what a key does* lives
  in `src/Enigma.MarkdownEditor.Core/Vim/`. The Desktop side changes only where a new seam has to be
  implemented over AvaloniaEdit (`Views/AvaloniaEditVimBuffer.cs`), a key has to be translated or
  claimed (`Views/VimEditorBehavior.cs`), an ex request has to be carried out
  (`ViewModels/DocumentManagerViewModel.ExecuteExCommandAsync`), and in the README.
- **A future extraction into a standalone `Enigma.Vim` package is a recorded intention, not part of
  this item.** Decisions already taken for it (own repo, `Enigma.Vim` + `Enigma.Vim.AvaloniaEdit`,
  engine on `netstandard2.0;net8.0;net10.0`, adapter decoupled from `IVimSession` into events) mean
  this item must keep the engine free of Avalonia and put every new capability the engine needs from
  the editor on `IVimBuffer`, never anywhere else. The namespace stays
  `Enigma.MarkdownEditor.Core.Vim` throughout.
- **`IVimBuffer` grows twice, and `FakeVimBuffer` and `AvaloniaEditVimBuffer` move together each
  time:** a `Changed` event in PHASE03 and a viewport trio in PHASE05. Nothing else in this item
  touches the seam.
- **`TreatWarningsAsErrors` is on solution-wide**, with `EnforceCodeStyleInBuild`. A **0-warning**
  Release build per phase; `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` green.
- **The parser is a chain of pending states** (`_awaitingLiteral`, `_awaitingFind`, `_awaitingObject`,
  `_awaitingSecondG`), read in order at the top of `KeySequenceParser.Feed`. This item adds `g`, `Z`,
  `"`, `q`/`@` and `z` prefixes to that chain; the existing `_awaitingSecondG` becomes the general
  `g`-prefix state. `HasPendingSequence` and `Reset()` cover every new one, so `Esc` cancels any of
  them and the status bar's "half typed" reading stays right.
- **The engine's existing rules hold:** one `IVimBuffer.Replace` per command is one undo step; a
  change that enters insert mode opens the insert session's undo group; normal mode clamps the caret
  onto a character; a motion that cannot go anywhere is silent and `NotACommand` is reserved for keys
  that spell nothing; `Register`, `_lastSearch` and `_lastFind` survive `Reset()`.
- **Test harness:** `VimScript.Run("a|bc", "x")` → `"a|c"`, `a[b|c]d` for a selection. PHASE01 adds
  `<Home>`, `<End>`, `<Tab>`; PHASE03 moves the key-notation parser into Core (`VimKeyNotation`) and
  the harness reuses it; PHASE05 adds `<PageUp>`, `<PageDown>` and a simulated viewport on
  `FakeVimBuffer`. `|` is the harness's caret marker in the *text* argument only — `"3|"` as a *keys*
  argument is fine.
- **Deliberately changed existing behaviour** (each one covered by an updated test): `Enter`,
  `Backspace` and `Space` in normal mode become motions rather than being swallowed (PHASE01);
  `:q!`, `:x` and `:wq!` stop answering "Not an editor command" (PHASE01); `u` in a visual mode becomes
  vim's lowercase operator rather than "Not a command: u" (PHASE02); `n`/`N` follow the direction of
  the last search rather than always meaning forward/backward (PHASE04); `/` interprets its pattern
  as a regular expression, so `.` `*` `[` `(` `\` in a search change meaning (PHASE04); the six
  `Ctrl+D/U/F/B/E/Y` combinations are taken by the engine in command modes (PHASE05).
- **Documentation is part of every phase:** `README.md` §*Vim key reference* gains the new keys in its
  tables and §*Not implemented* loses them. `RELEASENOTES.md` is written when 1.4.0 is cut, not here.

## Cross-cutting design

### `VimOptions` (PHASE01, extended by PHASE04)

```
public sealed class VimOptions
{
    public int  ShiftWidth { get; set; } = 4;
    public bool ExpandTab  { get; set; } = true;
    public bool IgnoreCase { get; set; }         // PHASE04
    public bool SmartCase  { get; set; }         // PHASE04
    public bool WrapScan   { get; set; } = true; // PHASE04
}
```

`VimEngine(IVimBuffer buffer, VimOptions? options = null)`; the instance is exposed as a mutable
`Options` property. `VimEditorBehavior.Refresh()` seeds `ShiftWidth` from
`TextEditor.Options.IndentationSize` and `ExpandTab` from `TextEditor.Options.ConvertTabsToSpaces` on
every attach, so the app needs no new setting. `:set` (PHASE04) edits the same object.

### `VimKeyNotation` (PHASE03)

A static class in Core that formats a `VimKey` the way vim writes it and parses that notation back:
plain characters as themselves, `<Esc>` `<CR>` `<BS>` `<Tab>` `<Home>` `<End>` `<Left>` `<Right>`
`<Up>` `<Down>` `<PageUp>` `<PageDown>` `<C-x>` and `<lt>` for a literal `<`. `VimKey.ToString()`
delegates to it. It is what a macro register holds, what `@a` reads, and what the test harness parses
its keys argument with — one notation, one implementation.

### Recording insert sessions (PHASE03)

`IVimBuffer` gains `event EventHandler<VimTextChange>? Changed`, raised for every change with
`(Offset, RemovedLength, InsertedText)`. `FakeVimBuffer` raises it from `Replace`;
`AvaloniaEditVimBuffer` subscribes to `TextDocument.Changed` of whichever document the control shows.
The engine listens only while an insert session is open and builds the "text typed" record both `.`
and a macro need: an insertion at the caret appends, a one-character removal just before the caret
pops, and any other change ends the recording — what was typed up to that point is what gets
replayed. (Vim starts a new insert on an arrow key; the rule here is the same idea with one fewer
state.)

### Ex requests

`VimExCommand` stays an enum and gains `QuitDiscard` (PHASE01). File operations still leave the engine
through `ExCommandRequested`; commands the engine can run itself — `:s`, `:N`, `:set` (PHASE04) — never
do. `ExCommandParser` grows into a small real parser (range, name, bang, argument) whose result is
internal; its public `Parse` keeps returning `VimExCommand?` for the four file commands.

## PHASE01 — Motions & editing extras

**Status: DONE**

### Behaviour contract

| Keys                | Behaviour                                                                                                                                                                                   |
|---------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `W` `B` `E`         | WORD motions: a WORD is a run of non-blanks. Same shape as `w` `b` `e`, same empty-line rule, same operator rules (`cW` ≈ `cE`, `dW` stops at the line's end).                                |
| `iW` `aW`           | WORD text objects, mirroring `iw` `aw` with the two-class (blank / non-blank) rule. Counts as for `iw` `aw`.                                                                                  |
| `^`                 | First non-blank of the line. Exclusive behind an operator. A count is ignored.                                                                                                              |
| `%`                 | On `(` `)` `[` `]` `{` `}`: jump to the match, counting nesting, across lines. Not on one: scan forward on the line to the first bracket and jump to *its* match. Inclusive behind an operator. `N%` goes to line `(N × lines + 99) / 100` (vim's formula). No match: silent no-op. |
| `}` `{`             | Forward / backward to the next blank line (a line with nothing on it), or the buffer's end / start. Exclusive. Vim's exclusive-linewise rule applies behind an operator: a range that starts at or before the first non-blank and ends at column 0 becomes linewise, so `d}` from a paragraph's first column deletes whole lines. |
| `)` `(`             | Sentence forward / backward, vim's definition: a sentence ends at `.` `!` `?` followed by end of line or by a space or tab, with any run of `)` `]` `"` `'` allowed between; a paragraph boundary is a sentence boundary. Exclusive.                                              |
| `+` `-` `_`         | Linewise: `+` first non-blank of the line below (count lines), `-` of the line above, `_` of the current line, count−1 lines down. `Enter` is `+`.                                          |
| `\|`                | Column *count* (1-based) of the current line, clamped to the last character. Exclusive.                                                                                                   |
| `Home` `End`        | `0` and `$`.                                                                                                                                                                                 |
| `Space` `Backspace` | `l` and `h` that cross a line break: `Space` on the last character moves to the next line's first, `Backspace` on the first character moves to the previous line's last. Exclusive behind an operator. |
| `J` `gJ`            | Join *count* lines (minimum two): the following line's leading blanks are removed and one space inserted — none when the following line starts with `)` or the current line ends with a blank. `gJ` inserts nothing and removes nothing. Caret on the join point. On the last line: silent no-op. The register is untouched. |
| `s` `S` `X`         | `s` is `cl`, `S` is `cc`, `X` is `dh`. Counts as for the spelled-out forms.                                                                                                                  |
| `>>` `<<`           | Shift *count* lines by one `shiftwidth`: spaces when `expandtab`, otherwise tabs for whole widths and spaces for the rest. `<` removes up to one shiftwidth of leading white-space. `>` leaves an empty line empty. Caret on the first non-blank of the first line. Register untouched. |
| `>` `<` + motion / object | The same over any motion or text object, always linewise over the lines the range touches.                                                                                            |
| `gu` `gU` `g~` + motion / object | Lowercase / uppercase / toggle case over the range; `guu` `gUU` `g~~` (and `gugu` `gUgU` `g~g~`) over *count* lines. Caret to the start of the range. Register untouched.       |
| `ZZ` `ZQ`           | `ZZ` raises `WriteQuit`; `ZQ` raises the new `QuitDiscard`.                                                                                                                                  |
| `:q!` `:x` `:wq!`   | `:q!` raises `QuitDiscard`; `:x` and `:wq!` raise `WriteQuit`. `:wa` `:qa` `:wqa` `:e` stay refused by name.                                                                                  |

Vim differences recorded here: `^` ignores its count (vim also does); `%` matches only the three
bracket pairs (vim's default `matchpairs`); `J` never inserts two spaces after a full stop
(`nojoinspaces`).

### Design

- **`VimMotion`** gains `WordForwardBig`, `WordBackwardBig`, `WordEndBig`, `FirstNonBlank`,
  `MatchingBracket`, `ParagraphForward`, `ParagraphBackward`, `SentenceForward`, `SentenceBackward`,
  `LineDownFirstNonBlank` (`+`), `LineUpFirstNonBlank` (`-`), `CurrentLineFirstNonBlank` (`_`),
  `Column` (`|`), `RightWrapping` (`Space`), `LeftWrapping` (`Backspace`). Each documented in the
  file's voice. `VimTextObject` gains `InnerBigWord`, `AroundBigWord`.
- **`VimWords`** gains a two-class variant (`NextStartBig`, `PreviousStartBig`, `EndBig`,
  `ClassOfBig`) sharing the walking code with a class-function parameter rather than duplicating it;
  `VimTextObjects.Word` takes the same class function. New sibling **`VimParagraphs`** (paragraph and
  sentence scans) and **`VimBrackets`** (`%`, reusing the nesting walk `VimTextObjects` already has —
  lift `NextUnmatched` / `PreviousUnmatched` into it and have `VimTextObjects` call them).
- **`VimRanges.Resolve`** gains one arm per new motion, plus the exclusive-linewise adjustment as a
  post-step applied to `ParagraphForward`/`ParagraphBackward` only.
- **`VimOperator`** gains `ShiftRight`, `ShiftLeft`, `Lowercase`, `Uppercase`, `ToggleCase`.
  `OperatorFor` maps `>` and `<`; the case operators come out of the `g` prefix. `ApplyOperator`'s
  switch gains the five, all implemented in a new **`VimCase`** / **`VimShift`** pair of static
  helpers that return the replacement text for a range, so the engine keeps its one-`Replace` rule.
  Doubled operators (`>>`, `guu`, `gUgU`) go through the existing "operator typed twice" path, which
  learns that `gu` followed by `u` *or* `gu` counts as doubled.
- **`VimAction`** gains `Join`, `JoinRaw`, `SubstituteCharacter` (`s`), `SubstituteLine` (`S`),
  `DeleteCharacterBefore` (`X`), `WriteQuit` (`ZZ`), `QuitDiscard` (`ZQ`). `s`/`S`/`X` are cheapest
  as parser rewrites to `Operate(Change, Right)`, `Operate(Change, WholeLine)`, `Operate(Delete,
  Left)` — do that instead of new actions where the existing arm already gives the right answer.
- **Parser prefixes.** `_awaitingSecondG` becomes `_awaitingG` (bool) with a table:
  `gg` → `FirstLine`, `gJ` → `JoinRaw`, `gu`/`gU`/`g~` → operator pending. A new `_awaitingZ` for
  `ZZ`/`ZQ`. Both reset on `Esc`, both count toward `HasPendingSequence`.
- **`VimSpecialKey`** gains `Home`, `End`, `Tab`. `Enter`, `Backspace` and `Space` (a character) get
  their motions in `MotionFor`; `Tab` stays swallowed (no jump list in this item). `VimEditorBehavior.
  Translate` maps `Key.Home`/`Key.End`/`Key.Tab`, and `EditorKeys` drops the three so they reach the
  engine. The remarks on `VimSpecialKey.Enter`/`Backspace` (which say they mean nothing outside the
  command line) are rewritten, not left contradicting the code.
- **`VimOptions`** as in *Cross-cutting design*; `ShiftWidth` and `ExpandTab` only in this phase.
- **`VimExCommand.QuitDiscard`**; `ExCommandParser` accepts `q!`, `x`, `wq!`.
  `DocumentManagerViewModel.ExecuteExCommandAsync` gains a `QuitDiscard` case that closes the active
  tab without the unsaved-changes prompt — a `discard` path through `CloseAsync` (or a sibling) that
  skips `AskAsync`. The `default` arm that currently means `WriteQuit` becomes an explicit case.
- **`VimScript`** gains `<Home>`, `<End>`, `<Tab>`.

### Acceptance criteria

- Every row of the contract table behaves as stated, as a bare motion, behind `d` `y` `c`, and (for
  the operators) over motions, text objects and doubled forms, with counts on either side.
- `Enter` `Backspace` `Space` `Home` `End` move in normal mode and still type / delete / move in
  insert mode; `Tab` is still swallowed in normal mode and still indents in insert mode.
- `d}` from a paragraph's first column is linewise; `d}` from mid-line is charwise.
- `>>` with `expandtab` off writes tabs; `<<` on a line indented less than a shiftwidth removes what
  is there; `>` skips empty lines.
- `ZQ` and `:q!` close a dirty tab with no dialog (Desktop test on `DocumentManagerViewModel`); `ZZ`,
  `:x`, `:wq!` save then close; `:wa` is still "Not an editor command".
- `VimEditorBehavior.Refresh()` seeds the options from the editor's own (headless test).
- New Core test files: `VimBigWordTests.cs`, `VimLineMotionTests.cs` (`^ + - _ | Home End Space
  Backspace Enter`), `VimBracketParagraphTests.cs` (`% { } ( )`), `VimJoinTests.cs`,
  `VimShiftTests.cs`, `VimCaseOperatorTests.cs`, `VimQuitTests.cs`; `ExCommandParserTests.cs` and
  `KeySequenceParserTests.cs` extended.
- README §*Motions*, §*Operators*, §*Text objects*, §*Editing*, §*Search and ex commands* gain the
  keys; §*Not implemented* is rewritten accordingly. Release build 0 warnings; full suite green.

## PHASE02 — Visual-mode commands

**Status: DONE**

### Behaviour contract

Every command below acts on the selection, leaves visual mode, and leaves the caret at the start of
what was selected (linewise: first non-blank of the first line), as `d` `y` `c` already do.

| Keys      | Behaviour                                                                                                                                                                  |
|-----------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `x`       | Delete the selection — `d`.                                                                                                                                                |
| `X` `D`   | Delete the selected *lines* — linewise `d` whatever the mode.                                                                                                              |
| `Y`       | Yank the selected lines.                                                                                                                                                   |
| `s`       | Change the selection — `c`. `C` `S` change the selected lines (linewise `c`).                                                                                             |
| `p` `P`   | Replace the selection with the register (put kind decides: characterwise text goes inline, linewise text goes on its own lines). `p` leaves the replaced text in the unnamed register; `P` leaves the register as it was. Empty register: "Nothing in the register", selection kept. |
| `r<char>` | Overwrite every selected character (line breaks excluded) with `<char>`.                                                                                                   |
| `~` `u` `U` | Toggle / lower / upper the case of the selection.                                                                                                                        |
| `>` `<`   | Shift the selected lines *count* times.                                                                                                                                    |
| `J` `gJ`  | Join the selected lines (minimum two).                                                                                                                                     |
| `o`       | Swap anchor and caret; stays in visual mode.                                                                                                                               |
| `gv`      | In normal mode: reselect the last visual selection (same mode, same ends). In visual mode: swap the current and the previous selection.                                    |

Vim difference recorded here: `u` and `U` are case operators in visual mode exactly as in vim, which
means undo and redo are *not* available while selecting (they were "Not a command" before and remain
unavailable, now with a reason).

### Design

- **Parser, visual branch.** The `if (visual) return Dead();` cut-off is replaced by a
  `VisualActionFor(char)` table producing the actions above; `r` opens `_awaitingLiteral` in visual
  mode too; `gv` and `gJ` come from the `g` prefix; `>`/`<`/`J` share their normal-mode actions.
  `p`/`P`/`x`/`X`/`D`/`Y`/`s`/`C`/`S` are rewritten to the equivalent `Operate(...)` where one exists.
- **Engine.** `ApplyVisualOperator` grows the put-replace path (`Delete` then `Put` inside one undo
  group so it is one `u`), the `r` fill, and the case/shift/join arms reusing PHASE01's helpers.
  New state `_lastVisual` (anchor, caret, mode) recorded by `LeaveVisual`, read by `gv`.
- **`VimAction`** gains `SwapVisualEnds`, `ReselectVisual`, `ReplaceSelection` (put) — and nothing
  for the ones expressed as operators.

### Acceptance criteria

- Every row of the contract table holds in both `v` and `V`, for selections made forwards and
  backwards, on one line and across lines, including the last line of the buffer.
- `vjp` over a linewise register puts lines; `Vp` over a characterwise register puts the text on its
  own line; the register afterwards matches the `p`/`P` rule.
- `gv` after an operator reselects exactly what the operator acted on (where it still exists).
- New Core test file `VimVisualCommandTests.cs`; `VimVisualTests.cs` updated where it asserted the
  old refusals. README §*Modes* gains a visual-commands paragraph and the table rows.
  Release build 0 warnings; full suite green.

## PHASE03 — Registers, repeat & macros

**Status: DONE**

### Behaviour contract

**Registers**

| Register    | Behaviour                                                                                                                           |
|-------------|-------------------------------------------------------------------------------------------------------------------------------------|
| `"` unnamed | As today. Every yank, delete and change fills it (unless `"_` was named).                                                           |
| `"a`–`"z`   | `"ayy` fills `a`; `"ap` puts it. `"Ayy` appends to `a` (linewise onto linewise adds a line; mixing kinds makes the result linewise). |
| `"0`        | The last *yank*, whatever deletes happened since. Filled by `y` only.                                                                 |
| `"_`        | Black hole: `"_dd` deletes without touching any register.                                                                            |
| Prefix rule | `"x` may come before or after the count: `"a3yy` and `3"ayy` are the same. It applies to `y` `d` `c` `x` `X` `s` `S` `p` `P` `D` `C` `Y` and the visual equivalents. A prefix with nothing after it is cancelled by `Esc`; a key that takes no register (e.g. `"aj`) is "Not a command: "aj". |

**`.` repeat**

- `.` repeats the last *change*: an operator over a motion or object, `x` `X` `s` `S` `r` `~` `J` `gJ`
  `p` `P`, `>>` `<<`, the case and shift operators, an insert command (`i` `a` `I` `A` `o` `O`) with
  the text typed in that session, a change entering insert mode with its text, and a visual operation
  (replayed over the same number of lines / characters from the caret). Never a motion, a yank, `u`,
  `Ctrl+R`, an ex command, `q` or `@`.
- `3.` replaces the original count (`3.` after `dw` is `3dw`); a bare `.` reuses it.
- A count on an insert command repeats its text: `3ix<Esc>` types `xxx`, `2o…<Esc>` opens two lines
  each with the text, `3A!<Esc>` appends `!!!`.
- A `.` replay is exactly one undo step.
- Vim difference: an insert session interrupted by a discontiguous edit (arrow key, mouse click,
  paste) is repeatable up to the interruption (see *Cross-cutting design*); vim would start a new
  insert.

**Macros**

- `qa` starts recording into `a` (`qA` appends); `q` stops. While recording the status slot reads
  `recording @a` and keeps reading it across commands that would otherwise clear the status.
- `@a` replays *count* times; `@@` replays the last register replayed. A register filled by a yank
  replays as keys too (vim's rule — `"ayy` then `@a` runs the line as keys).
- Replay aborts at the first invalid sequence, failed find or failed search, leaving what ran; a
  nesting deeper than 100 (`@a` inside `a`) aborts with "Macro nesting too deep".
- A macro register holds the recorded keys as `VimKeyNotation` text, characterwise, so `"ap` pastes
  `ciwfoo<Esc>j` and it can be edited and yanked back into a register.

### Design

- **`VimRegisters`** — new class: a dictionary of `char → VimRegister`, with `Unnamed`, `Get(char)`,
  `Set(char, VimRegister)`, `Append`, the `0` and `_` rules, and `Fill(char? name, VimRegister, bool
  isYank)` which the engine's `Fill` calls. `VimEngine.Register` stays and reads `Unnamed`;
  `VimEngine.Registers` exposes the store read-only for the status/tests. Survives `Reset()`.
- **`VimCommand`** gains `char Register = '\0'`. Parser: `"` opens `_awaitingRegister`; the next key
  is taken literally and must be `a-zA-Z0_"`, else `Dead()`; the name is carried into the completed
  command. `Reset()` and `HasPendingSequence` cover it.
- **`VimKeyNotation`** and **`IVimBuffer.Changed` / `VimTextChange`** as in *Cross-cutting design*.
  `AvaloniaEditVimBuffer` re-subscribes when the control's `Document` changes (it already reads the
  document on every call; add a `DocumentChanged` hook or have `VimEditorBehavior.OnDocumentChanged`
  tell it).
- **`VimRepeat`** — the engine records `_lastChange` (`VimCommand` + register + the typed text +
  visual extent) at the end of every change; `.` re-executes through the same `Execute` path with the
  count substituted and the typed text replayed by inserting it at the caret inside the insert
  session's undo group and then leaving insert. Counts on insert commands are the same replay with
  count−1 extra copies.
- **`VimMacro`** — the recorder sits in `HandleKey`: while `RecordingRegister` is set, every key the
  engine is offered is appended (the stopping `q` excluded), and insert-mode text arrives through the
  same `Changed` listener as `.`. Replay parses the register with `VimKeyNotation` and runs
  `foreach key: if (!HandleKey(key)) TypeIntoBuffer(key)` — the harness's own loop, now in the engine
  — behind a depth counter. `VimAction` gains `RecordMacro`, `PlayMacro`, `PlayLastMacro`,
  `Repeat`.
- **Status.** A private `ClearStatus()` replaces the `ShowStatus(null)` calls and shows
  `recording @a` instead of clearing while a recording is open.
- **Harness.** `VimScript.Parse` is replaced by `VimKeyNotation.Parse`; `VimRun` gains
  `Register(char)`.

### Acceptance criteria

- Every register rule above holds for `y` `d` `c` `x` `p` `P` and the visual forms, with the prefix
  on either side of the count; `"_` leaves every register untouched; `"0` survives a delete.
- `.` repeats every command kind listed, with and without a new count, as one undo step; it never
  repeats the excluded kinds (`.` after `yy` still repeats the change before it).
- `3ix<Esc>`, `2o`, `3A` type repeated text; `u` takes the whole thing back in one step.
- `qa…q` then `@a` and `3@a` and `@@` replay; the status reads `recording @a` while recording and
  clears when it stops; a recursive macro aborts with the message; a macro that hits `fz` on a line
  with no `z` stops there.
- `VimKeyNotation` round-trips every key the harness knows (`VimKeyNotationTests.cs`).
- New Core test files: `VimRegisterTests.cs`, `VimRepeatTests.cs`, `VimMacroTests.cs`,
  `VimKeyNotationTests.cs`; a headless Desktop test proves `AvaloniaEditVimBuffer.Changed` fires for
  a typed character and stops firing for the previous document after a swap.
- README §*Editing* gains registers, `.` and macros; §*Not implemented* drops them.
  Release build 0 warnings; full suite green.

## PHASE04 — Regex search, substitute & :set

**Status: DONE**

### Behaviour contract

**Search**

- `/pat<CR>` and `?pat<CR>` compile `pat` as a .NET regular expression with `Multiline` and
  `CultureInvariant`. Case: `IgnoreCase` option, overridden to case-sensitive when `SmartCase` is on
  and the pattern contains an uppercase letter; `\c` anywhere forces ignore-case and `\C` forces
  match-case, both stripped before compiling. A match timeout of one second; a timeout or an invalid
  pattern shows `Invalid pattern: <message>` on the status bar and leaves the caret where it was.
  An empty match is stepped over by one character so a search never stands still.
- `n` continues in the direction of the last search; `N` reverses it. `wrapscan` off stops at the
  buffer's ends with `search hit BOTTOM without match` / `TOP`.
- `*` / `#` search forward / backward for the word under the caret: `\b<word>\b` when the caret is on
  a word-class run, the escaped run alone for punctuation; no word on the line → silent no-op. Both
  set the last search pattern.
- Search is a motion: `d/foo<CR>` deletes from the caret to the match (exclusive, charwise), `dn`,
  `dN`, `d*`, `d#` likewise; `c/foo<CR>` changes; `y?bar<CR>` yanks backwards. In a visual mode
  `/` `?` `n` `N` `*` `#` extend the selection.
- `3/foo<CR>` lands on the third match; a count that runs out of matches (without `wrapscan`) is
  silent.
- Vim difference: vim's own regex dialect (`\<` `\>` `\(` `\)` `\{n,m}` `\1`, `\v`) is **not**
  translated — the README documents ".NET regex syntax".

**Substitute**

- `:[range]s/pat/rep/[flags]`. Delimiter: `/` or any other single non-alphanumeric, non-blank,
  non-`\` character. Range: none (current line), `%`, `N`, `N,M`, `.`, `$`, `'<,'>`, and `+N`/`-N`
  arithmetic on `.`/`$`/a number. Flags `g` (all occurrences), `i` / `I` (force ignore / match case).
  An empty `pat` reuses the last search pattern; `pat` becomes the last search pattern.
- `rep` is .NET replacement syntax (`$1`, `$0`, `$$`) plus vim's `\r` (line break, the buffer's own),
  `\t` and `\\`. `&` is not special.
- Result: `N substitutions on M lines` on the status bar, or `Pattern not found: pat`; caret on the
  first non-blank of the last substituted line; the whole command is one `Replace`, hence one undo
  step. The `c` (confirm) flag and `&` / `g&` are out of scope.

**Go to line and options**

- `:N` goes to line N (clamped), `:$` to the last line, first non-blank in both cases.
- `:set name` / `:set noname` / `:set name!` / `:set name=value` / `:set name?` over `ignorecase`
  (`ic`), `smartcase` (`scs`), `wrapscan` (`ws`), `shiftwidth` (`sw`), `expandtab` (`et`). `?` and a
  bare `:set sw` on a number option show the value (`shiftwidth=4`, `noignorecase`); a bad value
  answers `Invalid argument: …`; an unknown name `Unknown option: …`.

### Design

- **`VimSearch`** takes a compiled `Regex` and direction; `Find` returns the match start and wrap
  flag as now. A new **`VimPattern`** turns pattern text plus options into a `Regex` (flag stripping,
  case rules, timeout) and is the single compile point for `/` `?` `*` `#` `:s`.
- **`VimMotion`** gains `SearchForward`, `SearchBackward` (carry the pattern in a new
  `string? Pattern` on `VimCommand`), `SearchNext`, `SearchPrevious`, `SearchWordForward`,
  `SearchWordBackward`; the `SearchNext`/`SearchPrevious` *actions* are removed in favour of the
  motions, so `VimRanges` and `ApplyMotion` handle them uniformly. The command line, when opened
  with an operator pending, keeps that operator and completes an `Operate` on Enter; `Esc` abandons
  it. Engine state gains `_lastSearchForward`.
- **`ExCommandParser`** becomes a real parser: `Range` (a small record of two line addresses),
  `Name`, `Bang`, `Argument`; internal `VimExLine` result. The engine runs `s`, a bare number/`$`,
  and `set` itself; `w` `q` `wq` `x` (and bangs) still raise `ExCommandRequested`.
  New **`VimSubstitute`** builds the replacement text for a range and the message.
- **`VimOptions`** gains the three search options; **`VimSet`** parses `:set` arguments.
- **Harness.** Nothing new; regex patterns are typed through the command line as today.

### Acceptance criteria

- Regex, case rules, `\c`/`\C`, timeout and error reporting hold; `n`/`N` follow direction;
  `wrapscan` off stops with the message; `*`/`#` build the right pattern on words and punctuation.
- `d/foo<CR>`, `dn`, `c?x<CR>`, `d*` operate on the right range; `v/foo<CR>` extends the selection.
- `:s` over every range form and flag; `\r` splits a line; `$1` substitutes a group; the message and
  caret rule hold; `u` restores the whole substitute in one step.
- `:42`, `:$`, `:set` in all five spellings, and every error message.
- New Core test files: `VimRegexSearchTests.cs`, `VimSearchMotionTests.cs`, `VimSubstituteTests.cs`,
  `VimSetTests.cs`; `VimSearchTests.cs`, `VimCommandLineTests.cs`, `ExCommandParserTests.cs` updated.
- README §*Search and ex commands* rewritten for regex, `?`, `*` `#`, `:s`, `:N`, `:set`, with the
  ".NET syntax, not vim's" note; §*Not implemented* drops regex search and `:s`.
  Release build 0 warnings; full suite green.

## PHASE05 — Screen motions & viewport

**Status: DONE**

### Behaviour contract

| Keys                | Behaviour                                                                                                                                                   |
|---------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `H` `M` `L`         | First non-blank of the top / middle / bottom visible line; `3H` third line from the top, `3L` third from the bottom. Linewise behind an operator (`dL`).       |
| `Ctrl+D` `Ctrl+U`   | Scroll down / up half the visible lines (a count scrolls that many lines, for this command only) and move the caret the same number of lines, keeping the column. |
| `Ctrl+F` `Ctrl+B`   | Scroll a page (visible lines − 2) down / up, count pages; caret to the first non-blank of the new top / bottom line as vim does. `PageDown` `PageUp` are the same. |
| `Ctrl+E` `Ctrl+Y`   | Scroll one line (count lines) down / up without moving the caret unless it would leave the view, in which case it is pulled to the nearest visible line.       |
| `zz` `zt` `zb`      | Scroll so the caret's line is centred / at the top / at the bottom; caret unchanged.                                                                          |
| `z<CR>` `z.` `z-`   | The same three, and the caret moves to the line's first non-blank.                                                                                            |

### Design

- **`IVimBuffer`** gains `int FirstVisibleLine { get; }`, `int VisibleLineCount { get; }` (never
  less than one) and `void ScrollToLine(int lineNumber, VimScrollAlignment alignment)` with
  `VimScrollAlignment { Top, Center, Bottom }`. `Ctrl+E`/`Ctrl+Y` are expressed as
  `ScrollToLine(first ± count, Top)`.
- **`AvaloniaEditVimBuffer`** reads `TextArea.TextView.VisualLines` (first line's
  `FirstDocumentLine.LineNumber`, last line's `LastDocumentLine.LineNumber`) after
  `EnsureVisualLines()`, and scrolls with `TextView.ScrollOffset` / `TextEditor.ScrollToVerticalOffset`
  from `TextView.GetVisualTopByDocumentLine`. Word wrap is honoured because visual lines are what is
  measured. On a headless platform the view reports one visual line; the adapter test is a smoke
  test and the engine's logic is covered through the fake.
- **`FakeVimBuffer`** gains a simulated viewport (`FirstVisibleLine`, settable `ViewportLines`,
  default the whole buffer) and records `ScrollToLine` calls.
- **`VimMotion`** gains `ScreenTop`, `ScreenMiddle`, `ScreenBottom`; **`VimAction`** gains
  `ScrollHalfPageDown/Up`, `ScrollPageDown/Up`, `ScrollLineDown/Up`, `ScrollCaretCenter/Top/Bottom`
  and their caret-moving `z<CR>`/`z.`/`z-` variants. Parser: `_awaitingZ2` (lowercase `z` prefix);
  `Ctrl+D/U/F/B/E/Y` are recognised in `HandleKey` beside `Ctrl+R` — the `HasCommandModifier` early
  return learns the six new letters and takes them in command modes only.
- **`VimSpecialKey`** gains `PageUp`, `PageDown`; `VimEditorBehavior.Translate` maps them and
  `EditorKeys` drops them. `Ctrl+B` in insert mode still reaches the markdown assists (bold), because
  the engine is not consulted there.
- **Harness.** `<PageUp>`, `<PageDown>`; `VimScript.Run` overload taking a viewport height.

### Acceptance criteria

- Every row of the contract table holds against the fake viewport, including at the top and bottom
  of the buffer, with a buffer shorter than the viewport, and with counts.
- `dL`, `yH`, `cM` are linewise ranges; `Ctrl+D` keeps the column and clamps at the last line.
- `Ctrl+D/U/F/B/E/Y` are handled in normal and visual mode and pass through in insert mode
  (`VimEditorBehavior` headless test); `Ctrl+B` in insert mode still bolds.
- `AvaloniaEditVimBuffer` viewport smoke test over a headless `TextEditor`: `FirstVisibleLine` is 1,
  `ScrollToLine` does not throw, and the `Changed` event from PHASE03 still fires.
- New Core test file `VimScreenMotionTests.cs`. README §*Motions* gains a screen-motions table;
  §*Keyboard shortcuts* notes the six Control keys vim takes in normal mode.
  Release build 0 warnings; full suite green.

## Dependencies

PHASE01 → PHASE02 (visual `>` `<` `J` `u` `U` reuse PHASE01's operators) → PHASE03 (`.` and macros
must know every command shape, and the register prefix touches the visual grammar) → PHASE04 (`:s`
sets the last search pattern the register/repeat work already treats as engine state; search-as-motion
needs the `.`-recordable operator path) → PHASE05 (the last `IVimBuffer` change, kept last so the
seam moves once for `Changed` and once for the viewport). Building out of order is possible but each
later phase's plan assumes the earlier ones are in.

After PHASE05 the app is ready for a **1.4.0** release, planned as its own item when it is cut.

## Out of scope

Marks, jump list, `gi` · `ip` `ap` `is` `as` `it` `at` objects and `2i(` walking outwards · `R`
replace mode · `Ctrl+A` / `Ctrl+X` · visual block `Ctrl+V` · `hlsearch` / incremental search ·
`:wa` `:qa` `:wqa` `:e` · command-line cursor, history and `Ctrl+R{reg}` · the system clipboard
register `"+` · `:s` flag `c`, `&`, `g&` · translating vim's regex dialect · the two known adapter
follow-ups (sticky column after a mouse click, `Ctrl+Backspace` reaching AvaloniaEdit in normal mode)
· the extraction of the engine into `Enigma.Vim` / `Enigma.Vim.AvaloniaEdit` (decisions recorded in
*Context & constraints*, deliberately not planned here).
