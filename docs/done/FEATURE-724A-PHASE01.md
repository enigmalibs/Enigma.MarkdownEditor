# FEATURE-724A-PHASE01 — Motions & editing extras

**Status: DONE** · Phase 01 of 5 · Branch: `feature/feature-724a-phase01-motions-editing`
Plan: `docs/plan/FEATURE-724A.md`

## Summary

The next tier of normal-mode vim, entirely inside the engine except for three seams: `W` `B` `E` with
`iW` `aW`; `^` `%` (and `N%`) `{` `}` `(` `)` `+` `-` `_` `|`; `Home` `End` `Space` `Backspace`
`Enter` as motions; `J` `gJ`; `s` `S` `X`; the `>` `<` operators with `>>` `<<`; the `gu` `gU` `g~`
operators with their doubled forms; `ZZ` `ZQ`; `:q!` `:x` `:wq!`; and a `VimOptions` object the
Desktop adapter seeds from the editor's own indentation settings.

**The parser's `g` became a table and `Z` joined it.** `_awaitingSecondG` is now `_awaitingG`, read
by `SecondG`: `gg` is the motion it always was, `gJ` a command, and `gu` `gU` `g~` are operators that
then wait for a motion like any other — or double, in either spelling (`guu` and `gugu`), through a
`DoublingKeyFor` check placed before the motions so that `u` behind a pending `gu` is not undo. A new
`_awaitingZ` reads `ZZ` and `ZQ`. Both prefixes are in `HasPendingSequence` and `Reset()`, so `Esc`
cancels them and the status bar's half-typed reading stays right.

**The operator path is one switch for both grammars.** `ApplyOperator` (motion or object) and
`ApplyVisualOperator` (selection) both hand their resolved range to a new `Operate(operator, range,
shiftSteps)`, which dispatches to `Yank` / `Delete` / `Change` as before and to the new `Shift` and
`ChangeCase`. The engine's one-`Replace`-per-command rule holds: `VimShift` and `VimCase` are static
helpers that return the replacement text for a range, and the engine writes it in one call. Neither
touches the register; a shift lands the caret on the first non-blank, a case change on the range's
start — vim's landings, both.

**Vim's exclusive-linewise rule, in one place.** `VimRanges.ExclusiveLinewise` is applied as a
post-step to the paragraph motions and to the two that cross a line break on purpose. An exclusive
range whose end lands in the first column of a later line is pulled back to the end of the line
before, and when it also started at or before the first non-blank it becomes linewise. That is what
makes `d}` from a paragraph's first column take whole lines and stop short of the blank line, `d}`
from mid-line stop at the paragraph's end, and `d<Space>` on a line's last character the same as `x`
rather than a join.

**The sentence scan is a transcription, not a simplification.** `VimParagraphs.Sentence` follows
vim's `findsent()` position by position — the rewind over trailing punctuation, the `)]"'` run after a
full stop, the blank-line boundary, the FAIL on a count that runs out — because every shorter rule
breaks on the second example. The paragraph scan follows `findpar()`: only a line with nothing on it
is a boundary, and a count that overshoots moves nowhere at all.

**Three keys that used to be swallowed now move.** `Enter`, `Backspace` and `Space` in the command
modes are `+`, `h`-across-a-line-break and `l`-across-a-line-break. `Home`, `End` and `Tab` are
translated by the adapter and reach the engine, which moves with the first two and swallows the third
(no jump list). All six still do in insert mode exactly what AvaloniaEdit did with them before.

## Files/modules touched

**Created — `src/Enigma.MarkdownEditor.Core/Vim`**

- `VimOptions.cs` — `ShiftWidth` (4) and `ExpandTab` (true), mutable and shared; `VimEngine(buffer,
  options?)` keeps the instance and exposes it as `Options`.
- `VimBrackets.cs` — `Match` for `%` (bracket under the caret or first along the line, then its match
  across lines counting nesting), plus `NextUnmatched` / `PreviousUnmatched` lifted out of
  `VimTextObjects`, which now calls them.
- `VimParagraphs.cs` — `Paragraph(buffer, caret, forward, count)` and `Sentence(...)`, both honouring
  the count inside the scan and returning `null` for a count that runs out.
- `VimShift.cs` — the rewritten lines for `>`/`<`: indent measured in columns (a tab counts as one
  width, advancing to the next stop), re-emitted as spaces or as tabs-then-spaces per `ExpandTab`;
  empty lines untouched.
- `VimCase.cs` — `Convert(text, operator)` character by character, invariant culture; `~` now uses it
  too.
- `VimWhichWrap.cs` — the one-step `Right` / `Left` that `Space` and `Backspace` walk with.

**Modified — `src/Enigma.MarkdownEditor.Core/Vim`**

- `VimMotion.cs` — fifteen new members, each documented in the file's voice.
- `VimTextObject.cs` — `InnerBigWord`, `AroundBigWord`.
- `VimOperator.cs` — `ShiftRight`, `ShiftLeft`, `Lowercase`, `Uppercase`, `ToggleCase`; remarks
  rewritten for eight operators and the register rule.
- `VimAction.cs` — `Join`, `JoinRaw`, `WriteQuit`, `QuitDiscard`. `s` `S` `X` are parser rewrites (see
  *Deviations*), so they add no members.
- `VimSpecialKey.cs` — `Home`, `End`, `Tab`; the `Enter` and `Backspace` remarks rewritten to say what
  they mean outside the command line.
- `VimExCommand.cs` — `QuitDiscard`; remarks cover the six spellings and `ZZ`/`ZQ`.
- `ExCommandParser.cs` — accepts `q!` → `QuitDiscard`, `x` and `wq!` → `WriteQuit`; remarks rewritten.
- `VimWords.cs` — the three walks take a classifier; `NextStartBig` / `PreviousStartBig` / `EndBig`
  and `ClassOfBig` are the two-class variants.
- `VimTextObjects.cs` — `Word` takes the classifier and serves `iW`/`aW`; bracket nesting delegated to
  `VimBrackets`.
- `VimRanges.cs` — one arm per new motion; `PercentLine`, `ColumnOffset`; `RightWrapping` (inclusive
  when the walk hits the end of the buffer); `ExclusiveLinewise`; `WordForward` takes the two walks.
- `KeySequenceParser.cs` — `_awaitingG`, `_awaitingZ`, `SecondG`, `Operator`, `CaseOperatorFor`,
  `DoublingKeyFor`; `>` `<` in `OperatorFor`; the new motions in `MotionFor` (characters and the five
  named keys); `s` `S` `X` `J` in `ActionFor`; `W` in `TextObjectFor`; class remarks rewritten.
- `VimEngine.cs` — options constructor and `Options`; `Operate`, `Shift`, `ChangeCase`, `Join`;
  `ApplyVisualOperator(VimCommand)` so a visual shift reads its count; the new motion arms in
  `ApplyMotion`; `Join`/`JoinRaw`/`WriteQuit`/`QuitDiscard` in `Execute`; `Redo` made an explicit
  case; the swallow list in `HandleKey` drops Enter and Backspace and gains Tab.

**Modified — `src/Enigma.MarkdownEditor.Desktop`**

- `Views/VimEditorBehavior.cs` — `Translate` maps `Key.Home`/`End`/`Tab`; `EditorKeys` drops the
  three; `Refresh()` seeds `Options.ShiftWidth` from `IndentationSize` and `ExpandTab` from
  `ConvertTabsToSpaces` on every call; `Options` exposed for the headless test.
- `ViewModels/DocumentManagerViewModel.cs` — `ExecuteExCommandAsync` gains an explicit `WriteQuit`
  case and a `QuitDiscard` case that calls `CloseAsync(target, discard: true)`, the one path past the
  unsaved-changes question.

**Created — tests**

- `tests/…Core.UnitTests/Vim/VimBigWordTests.cs` (67 cases), `VimLineMotionTests.cs` (84),
  `VimBracketParagraphTests.cs` (87), `VimJoinTests.cs` (30), `VimShiftTests.cs` (53),
  `VimCaseOperatorTests.cs` (53), `VimQuitTests.cs` (18).
- `tests/…Desktop.UnitTests/Views/VimEditorBehaviorTests.cs` — the option seeding (twice), and Home,
  End, Tab and Enter driven through a shown pane by real key events in both modes.

**Modified — tests**

- `VimScript.cs` — `<Tab>` `<Home>` `<End>`; the harness plays Tab as a `\t`, Home/End as caret moves;
  `Run` gains an optional `VimOptions`.
- `ExCommandParserTests.cs` — `q!` `x` `wq!` move from the refusals to the accepted spellings; `wa`,
  `qa!`, `x!`, `e`, `X` join the refusals.
- `VimCommandLineTests.cs` — `:x`/`:q!` leave the "unknown command" theory for `:wa`/`:w!`; the
  Enter/Backspace-mean-nothing cases become Enter/Backspace-are-motions; `d<CR>` is `d+`.
- `KeySequenceParserTests.cs` — the new motions, named keys, operators, doubled forms, prefixes, the
  `Z` pair, counts, dead sequences, and the visual grammar's new refusals and acceptances.
- `DocumentManagerViewModelTests.cs` — `ExQBang_OnAChangedDocument_ClosesItWithoutAskingAndWithoutSaving`,
  `ExQBang_OnACleanDocument_ClosesItLikeExQ`; `QuitDiscard` in the nothing-open case.
- `Views/TabStrip.cs` — `Press(PhysicalKey)`.

**Modified — prose**

- `README.md` — §*Motions* gains nine rows and three paragraphs (WORDs, paragraph/sentence/bracket
  definitions, the exclusive-linewise rule); §*Operators* gains the shift and case rows and a
  paragraph; §*Text objects* gains `iW` `aW`; §*Editing* gains `X` `s` `S` `J` `gJ` `ZZ` `ZQ` and the
  join rules; §*Search and ex commands* gains `:x` `:wq!` `:q!`; §*Not implemented* drops the WORD
  family and names the visual commands still out.
- `docs/RELEASE.md` — the *Vim smoke set* row of the Linux release check gains `W`, `d}`, `>>`/`<<`,
  `gUiw`, `J`, Space/Backspace across a line break, Home/End in both modes, `:q!` on a dirty tab and
  `ZZ`, with a note on why `>>` is worth typing on that platform. Accepted in the documentation
  freshness sweep; `RELEASENOTES.md` was offered and declined — it is 1.3.0's document, and the 1.4.0
  release item rewrites it.

**Workflow artifacts**

- `docs/roadmap.md` — `FEATURE-724A` `TODO` → `IN PROGRESS`; PHASE01 `TODO` → `IN PROGRESS` → `DONE`.
- `docs/plan/FEATURE-724A.md` — the item header and PHASE01's status.

## Deviations & follow-ups

- **The exclusive-linewise post-step covers `Space` and `Backspace` as well as the paragraphs.** The
  plan named the paragraph motions only. Without it, `d<Space>` on a line's last character would join
  the lines where vim (and `x`) deletes the character, and `d<Backspace>` in the first column would
  join them where vim deletes the previous line's last character. Both are the o_v rules vim applies
  to every exclusive motion; the sentence motions are left without it, as the plan says, so `d(` from
  a first column joins where vim would not — recorded as the one place this shows.
- **`d<Space>` at the very end of the buffer is inclusive.** Vim's `nv_right` makes the motion
  inclusive when there is no line to wrap to and the line is not empty, so the last character still
  goes; `VimRanges.RightWrapping` tracks whether the walk got stuck for that reason.
- **`J` inserts no space beside an empty line.** The plan's rule named `)` and a trailing blank; vim's
  `do_join` also inserts none when the text so far is empty or the joined line is empty after its
  leading blanks go. Followed vim, per the plan's "behaviour follows vim unless a line says otherwise".
- **`N%` past 100 clamps to the last line**, as every other overshooting count in this engine does,
  where vim beeps. `%` matches the three bracket pairs only (plan).
- **`s` `S` `X` are parser rewrites**, as the plan preferred: `Operate(Change, Right)`, `Operate(Change,
  WholeLine)`, `Operate(Delete, Left)`. No `SubstituteCharacter` / `SubstituteLine` /
  `DeleteCharacterBefore` members were added.
- **Visual `>` `<` `gu` `gU` `g~` work now**, ahead of PHASE02, as a by-product of the shared operator
  table: the parser already completes an operator over a selection, and `Operate` runs it. A few
  sanity cases pin them (`Vj>`, `V3>`, `vegU`, `Vjgu`); PHASE02's `VimVisualCommandTests.cs` still
  owns the visual matrix, and the visual `J` `gJ` `u` `U` `~` remain refused by name until then.
- **`<<` on a line with nothing to remove writes nothing**, so it costs no undo step and does not mark
  the document changed. A small refinement over "one `Replace` per command".
- **A paragraph count that overshoots moves nowhere**, which is vim's rule and differs from `j`'s
  clamping. Pinned by `ACountThatOvershoots_MovesNowhere`.
- **`VimWhichWrap` is a class the plan did not name** — the one-step `Right`/`Left` walks needed by
  both `ApplyMotion` and `VimRanges`. Kept beside `VimWords` rather than folded into either caller.
- **`Refresh()` seeds the options on every call**, not only when the attachment changes: cheap, and it
  means a settings change that leaves vim enabled still brings the options up to date.
- **Harness spelling.** `<` opens a key name in `VimScript`, so `<<` and `<j` are typed `<lt><lt>` and
  `<lt>j` in the shift tests — the same rule PHASE02 of `FEATURE-23AB` recorded for `di<`.
- **No line-ending noise.** Every touched and created file is LF, as `.gitattributes`
  (`* text=auto eol=lf`) requires. Nothing to recommend.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release`: **succeeded, 0 errors, 0 warnings**, with
  `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` on solution-wide.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **2617 passed, 0 failed, 0 skipped**
  — `Enigma.MarkdownEditor.Core.UnitTests` 1883 (1400 before this phase; the eight cases that failed
  on the unchanged tests were exactly the eight deliberate behaviour changes — `:q!` `:x` `:wq!` no
  longer refused, Enter and Backspace no longer swallowed — and were rewritten as described above) and
  `Enigma.MarkdownEditor.Desktop.UnitTests` 734 (6 new).
- **Acceptance criteria, one by one.**
  - Every row of the contract table behaves as stated, as a bare motion, behind `d` `y` `c`, and (for
    the operators) over motions, text objects and doubled forms, with counts on either side ✓ — the
    seven new Core files, theory by theory: `VimBigWordTests` (W B E iW aW, `cW` ≈ `cE`, `dW` at a
    line's end, counts, visual), `VimLineMotionTests` (`^` `+` `-` `_` `|` Home End Space Backspace
    Enter, each bare and behind `d`/`c`/`y`, counts, insert-mode pass-through), `VimBracketParagraphTests`
    (`%` `N%` `}` `{` `)` `(`, bare, behind operators, register kinds, counts that overshoot),
    `VimJoinTests` (every space rule, counts, `gJ`, last line, register, one undo),
    `VimShiftTests` (doubled, over motions and objects, over a selection with a count, both options,
    register, one undo), `VimCaseOperatorTests` (all three, both doubled spellings, motions, objects,
    selection, register, one undo, dead sequences), `VimQuitTests`.
  - `Enter` `Backspace` `Space` `Home` `End` move in normal mode and still type / delete / move in
    insert mode; `Tab` is still swallowed in normal mode and still indents in insert mode ✓ —
    `VimLineMotionTests` (`InInsertMode_*`, `Tab_*` facts) and, over a real key event,
    `VimEditorBehaviorTests.HomeAndEnd_…`, `Tab_…`, `Enter_…`.
  - `d}` from a paragraph's first column is linewise; `d}` from mid-line is charwise ✓ —
    `AnOperatorOverAParagraphMotion_FollowsTheExclusiveLinewiseRule` and
    `WhatAParagraphMotionTakes_GoesInTheRegisterWithItsKind`.
  - `>>` with `expandtab` off writes tabs; `<<` on a line indented less than a shiftwidth removes what
    is there; `>` skips empty lines ✓ — `WithoutExpandTab_TheIndentIsWrittenInTabs`,
    `ShiftLeftDoubled_RemovesUpToOneWidthOfIndent`, `ShiftRightDoubled_IndentsTheLinesByOneWidth`.
  - `ZQ` and `:q!` close a dirty tab with no dialog; `ZZ`, `:x`, `:wq!` save then close; `:wa` is
    still "Not an editor command" ✓ — `VimQuitTests` for the requests raised;
    `ExQBang_OnAChangedDocument_ClosesItWithoutAskingAndWithoutSaving` and the existing `ExWq_*`
    cases on `DocumentManagerViewModel`; `TheExCommandsStillOut_AreStillRefusedByName`.
  - `VimEditorBehavior.Refresh()` seeds the options from the editor's own (headless test) ✓ —
    `Refresh_SeedsTheEnginesOptionsFromTheEditors`, `Refresh_ReseedsTheOptionsEvenWhenNothingElseChanged`.
  - New Core test files as named, `ExCommandParserTests.cs` and `KeySequenceParserTests.cs` extended ✓.
  - README sections gain the keys; §*Not implemented* rewritten ✓. Release build 0 warnings; full
    suite green ✓ — as above.
