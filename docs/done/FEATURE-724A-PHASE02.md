# FEATURE-724A-PHASE02 — Visual-mode commands

**Status: DONE** · Phase 02 of 5 · Branch: `feature/feature-724a-phase02-visual-commands`
Plan: `docs/plan/FEATURE-724A.md`

## Summary

The commands that act on a selection without being `d` `y` `c`, entirely inside the engine: `x` `X`
`D` `Y` `s` `C` `S` as spellings of the three operators; `p` `P` putting the register over the
selection; `r<char>` over every selected character; `~` `u` `U` as the case operators; `J` `gJ` over
the selected lines; `o` swapping the two ends; and `gv` reselecting the last selection, or swapping it
with the current one while selecting. Every one of them was "refused by name, one line each" since
`FEATURE-11AA`; the README's §*Not implemented* no longer lists them.

**The parser's visual cut-off became a table.** `if (visual) return Dead();` is now
`VisualActionFor(char)`, and most of the table is spelling: `x` is `Operate(Delete, None)`, `s` is
`Operate(Change, None)`, and the capitals are the same operators with `VimMotion.WholeLine` in place
of `None` — the command's way of saying "the selection, made linewise whatever the mode".
`~` `u` `U` are `Operate` with the three case operators, so `u` is not undo while there is a
selection. `p`/`P` are one `ReplaceSelection` action carrying their key as the argument, the way `:`
and `/` already ride on `BeginCommandLine`. `r` moved above the cut-off — it takes the next key
literally in both grammars — and `gJ` and `gv` come out of the `g` table in both modes. Nothing new
is pending, so `HasPendingSequence` and `Reset()` needed no change.

**The engine reads the range, leaves the mode, acts.** `ApplyVisualOperator` resolves the range
linewise when the command's motion is `WholeLine`, then hands it to the shared `Operate` as before, so
the capitals cost no new engine code. `ReplaceSelection` is the one two-write command: a delete and a
put inside one undo group, so `u` takes the whole replacement back. The shape follows vim's
`nv_put`: characters over characters go inline where the selection *began* (not where the delete left
the clamped caret); lines over characters split the line and go between the halves, an empty half
staying as an empty line; anything over whole lines goes in as lines where the lines were, and a
selection that was the whole buffer leaves no empty line behind. `p` leaves what it replaced in the
register — `vp` on one word then on another swaps them — and `P` restores it. With nothing in the
register the selection stays and the status bar says so.

**`gv` remembers lines and columns, not offsets.** `LeaveVisual` records both ends and the mode in a
private `RememberedSelection` on every way out — an operator, `v` again, Esc — and before the operator
runs, so `gv` after a yank covers exactly what was yanked. Because the record is line/column, a `>`
that indented the lines does not move it off them; an end whose line or column has since gone is
pulled onto the nearest that is still there. `Reset()` forgets it: a selection is a place in a
document, and after a swap the document may be another one.

## Files/modules touched

**Modified — `src/Enigma.MarkdownEditor.Core/Vim`**

- `VimAction.cs` — `ReplaceSelection`, `SwapVisualEnds`, `ReselectVisual`; the remarks on
  `ReplaceCharacter`, `Join` and `Undo` say what they mean over a selection.
- `KeySequenceParser.cs` — `VisualActionFor`; `r` above the visual cut-off; `SecondG` reads `gJ` and
  `gv` in both grammars (and refuses both behind an operator); class remarks rewritten for the visual
  grammar.
- `VimEngine.cs` — `_lastVisual` and the nested `RememberedSelection`; `ApplyVisualOperator` honours
  `WholeLine`; `ReplaceSelection` with `PutOverCharacters` / `PutOverLines`; `FillSelection` (`r`) and
  its static `Overwrite`; `JoinSelection`; `SwapVisualEnds`; `ReselectVisual` with
  `CurrentSelection` / `OffsetOf`; `LeaveVisual` records the selection; `Reset()` forgets it;
  `Execute` gains the three arms and makes `ReplaceCharacter`, `Join` and `JoinRaw` mode-dependent;
  the `NothingInRegister` constant shared with `Put`; the `Ctrl+R` remark rewritten.

**Created — tests**

- `tests/…Core.UnitTests/Vim/VimVisualCommandTests.cs` (121 cases) — every row of the contract table
  in `v` and `V`, forwards and backwards, on one line, across lines and on the last line; the register
  after each; one undo step for the put, the fill, the case change and the join; the four put shapes
  by register kind × selection kind, with counts; `o`; `gv` after Esc, `v`, a yank, a `>`, a delete,
  in a visual mode (swap and swap back), with nothing to go back to, after `Reset()`, and behind an
  operator.

**Modified — tests**

- `VimVisualTests.cs` — the refusal theory now lists what is *still* refused over a selection
  (`:` `/` `n` `N` `I` `A` `O` `R`); the `r` fact asserts the fill rather than the refusal; the
  `Ctrl+R` fact says why redo stays named.
- `KeySequenceParserTests.cs` — the visual refusal list shrinks to `I` `A` `O` `Z` `:` `/` `n` `N`;
  new theories for the visual table, `p`/`P` with their argument and count, `gJ`/`gv` in visual,
  `gv` in normal mode and `dgv` dead, and `r` arming itself in visual.
- `VimCaseOperatorTests.cs` — `InAVisualMode_ULowercaseIsNotHereYet` becomes the three-case theory
  `InAVisualMode_TheBareLettersAreTheCaseOperators`.
- `VimJoinTests.cs` — `InAVisualMode_JoinIsNotACommandYet` becomes `InAVisualMode_JoinTakesTheSelectedLines`.

**Modified — prose**

- `README.md` — §*Modes* gains the `o` and `gv` rows and a visual-commands paragraph (the spellings,
  the four put shapes, the `p`/`P` register rule, `r`, the case trio, the joins, the caret rule, and
  the note that undo and redo are unavailable while selecting); §*Not implemented* drops the visual
  commands and names what is still out over a selection (a search or `:`).
- `docs/RELEASE.md` — the *Vim smoke set* row of the Linux release check gains `yiw` then `viwp` on
  another word (the swap), `Vjr-`, `vjJ` and `gv` after each. Accepted in the documentation freshness
  sweep; `RELEASENOTES.md` was offered and declined again — it is 1.3.0's document, and the 1.4.0
  release item rewrites it.

**Workflow artifacts**

- `docs/roadmap.md` — PHASE02 `TODO` → `IN PROGRESS` → `DONE`; the item stays `IN PROGRESS`.
- `docs/plan/FEATURE-724A.md` — PHASE02's status.

## Deviations & follow-ups

- **Where the caret lands is per command, not one rule.** The plan's opening sentence says every
  command leaves the caret at the start of what was selected (linewise: the first non-blank). Three
  follow vim instead, where vim differs from that sentence:
  - `~` `u` `U` land at the *first column* of a linewise selection, which is `ChangeCase`'s landing
    from PHASE01 (`Vjgu` already pins it) and vim's own for its case operators. `Vu` and `Vjgu` are the
    same operator over the same lines and could not sensibly land differently.
  - `J` `gJ` land on the seam of the last join, where `J` lands in normal mode and in vim. On the
    last line there is nothing to join, and the caret goes to the start of the selection instead.
  - `p` `P` land where a put lands: on the last character of text put inline, on the first non-blank
    of the first line put as a line.
  `r` lands at the first column too, which after every character became the same one is also the
  first non-blank — except for `r<Space>`, where the first-non-blank rule would send the caret to the
  end of the line and vim does not.
- **`_lastVisual` is line/column, not offsets.** The plan said "(anchor, caret, mode)"; stored as
  offsets, `Vj>gv` would reselect the wrong text because the indent shifted every offset. Lines and
  columns are what vim's `'<` `'>` marks are, and they make the plan's "reselects exactly what the
  operator acted on (where it still exists)" hold for the shifts.
- **One `ReplaceSelection` action for `p` and `P`**, the key in `Argument`, following the
  `BeginCommandLine` precedent rather than two enum members.
- **The capitals are `Operate(op, WholeLine)` in visual mode**, as the plan preferred; no
  `VimAction` members were added for them, and `ApplyVisualOperator` grew one condition.
- **A put over the whole buffer leaves no trailing empty line** (`Vp` on a one-line file gives the
  register's lines and nothing else), which is vim's `nv_put` rule and a step beyond "delete then
  put".
- **A put over the empty selection on an empty last line** takes nothing out and leaves the register
  alone, matching what `vd` does there; the put still goes in.
- **`Ctrl+R` stays "Not a command" in a visual mode**, as before; the reason is now that `u` is an
  operator there (the plan's recorded vim difference), and the comment says so.
- **Still refused over a selection:** `:` (the range command line), `/` `n` `N` (PHASE04 makes them
  extend the selection), and `I` `A` `O` `R` (block-mode spellings). The README's §*Not implemented*
  names "a search or `:` typed over a selection" so the refusal is documented rather than surprising.
- **Test-setup note.** `yy` resets the remembered column (`ApplyOperator` clears `_desiredColumn`),
  so `jyyk` does not return to the column `j` left; vim keeps `curswant` across a yank. Not this
  phase's to change — recorded as a possible follow-up, and the affected cases were re-keyed.
- **No line-ending noise.** Every touched and created file is LF, as `.gitattributes` requires.
  Nothing to recommend.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release`: **succeeded, 0 errors, 0 warnings**, with
  `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` on solution-wide.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **2748 passed, 0 failed, 0 skipped**
  — `Enigma.MarkdownEditor.Core.UnitTests` 2014 (1883 before this phase: 121 in the new file, the
  rest from the rewritten theories) and `Enigma.MarkdownEditor.Desktop.UnitTests` 734 (unchanged; no
  Desktop code was touched).
- **Acceptance criteria, one by one.**
  - Every row of the contract table holds in both `v` and `V`, forwards and backwards, on one line
    and across lines, including the last line ✓ — `VimVisualCommandTests`, theory by theory: `X_…`,
    `CapitalXAndD_…`, `CapitalY_…`, `SAndTheCapitals_…`, `P_OverACharacterwiseSelection_…` (two),
    `P_OverALinewiseSelection_…`, `R_…`, `TildeUAndCapitalU_…`, `TheShifts_…`, `JAndGJ_…`,
    `AJoinOnTheLastLine_…`, `O_…`, `GV_…` (seven).
  - `vjp` over a linewise register puts lines; `Vp` over a characterwise register puts the text on
    its own line; the register afterwards matches the `p`/`P` rule ✓ —
    `P_OverACharacterwiseSelection_PutsLinesOnLinesOfTheirOwn`,
    `P_OverALinewiseSelection_PutsWholeLinesWhereTheLinesWere`,
    `P_LeavesTheReplacedTextInTheRegister_AndCapitalPLeavesTheRegisterAlone`, `P_Twice_SwapsTwoWords`.
  - `gv` after an operator reselects exactly what the operator acted on (where it still exists) ✓ —
    `GV_InNormalMode_ReselectsWhatWasLastSelected` (after `y`),
    `GV_AfterALinewiseSelection_ReselectsTheSameLines` (after `y` and after `>`),
    `GV_WhereTheSelectionNoLongerExists_SelectsWhatIsStillThere` (after `d`).
  - New Core test file `VimVisualCommandTests.cs`; `VimVisualTests.cs` updated where it asserted the
    old refusals ✓ (and `KeySequenceParserTests.cs`, `VimCaseOperatorTests.cs`, `VimJoinTests.cs`,
    which asserted them too).
  - README §*Modes* gains a visual-commands paragraph and the table rows ✓. Release build 0 warnings;
    full suite green ✓ — as above.
