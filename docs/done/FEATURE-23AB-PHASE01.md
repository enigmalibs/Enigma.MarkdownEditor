# FEATURE-23AB-PHASE01 — Character find & repeat

**Status: DONE** · Phase 01 of 2 · Branch: `feature/feature-23ab-phase01-character-find`
Plan: `docs/plan/FEATURE-23AB.md`

## Summary

`f<c>` `F<c>` `t<c>` `T<c>` and the two keys that repeat them, `;` and `,` — as bare motions, behind
`d`, `y` and `c`, and in both visual modes, with counts on either side of the operator. Entirely
inside `src/Enigma.MarkdownEditor.Core/Vim/`: no `IVimBuffer` change, no key-routing change, nothing
in `Desktop`.

**The parser opens a find the way it opens `r`.** A new `_awaitingFind` field holds the sequence open
and takes the *next* key literally whatever it spells — `fd` looks for a `d` rather than starting a
delete — read in `Feed` immediately after the `_awaitingLiteral` block. The opener branch itself sits
beside the `g` branch, above the operator, above `MotionFor` and above the visual cut-off, because a
find is a motion in every mode: behind an operator it says which text, in a visual mode it says how
far the selection reaches. `;` and `,` go in `MotionFor` instead, being complete the instant they are
typed.

**`VimFind` is where the scan lives**, sibling to `VimWords`, and both callers go through it so
`fx` and `dfx` cannot disagree about which `x` they mean. The scan is bounded by
`[line.Offset, line.EndOffset)` in both directions and whatever the count, which is the whole
difference between a find and `/`. A count that runs out of matches returns `null` rather than the
last match it passed — vim's rule, and what makes a count safe to type without looking.

**The engine holds the memory, so the engine resolves the request.** `_lastFind` /
`_lastFindCharacter` are written by the four typed finds — *including* when they matched nothing, so
`fz` on a line without one then `;` on the next line finds the `z` there — and never by `;` or `,`.
`ResolveFind` turns whichever of the six motions was typed into a concrete `VimFindRequest`, once,
before `Execute`'s switch, and both the move path and the operator path take it from there.

**No match is silent, and it falls out of the existing code.** `VimRanges.Resolve` returning `null`
is already how `ApplyOperator` says "nothing to do", so an abandoned `df.` needed no new branch;
`ApplyMotion` simply does not call `SetCaret`. Caret, buffer, mode and status bar are all untouched —
`NotACommand` stays reserved for keys that spell nothing, not for commands that ran and found
nothing.

## Files/modules touched

**Created — `src/Enigma.MarkdownEditor.Core/Vim`**

- `VimFind.cs` — the scan. `Target(buffer, caret, request, count)` → `int?`, line-bound, stepping
  `count` times with each step starting from the previous hit, the `t`/`T` ±1 applied once after the
  last step, and the repeat's skip applied once before the first.
- `VimFindRequest.cs` — `(VimMotion Motion, char Character, bool Repeat)`, with `IsForward` and
  `IsTill`. `Motion` is always one of the four concrete finds; `;` and `,` are resolved away before
  one is built. See *Deviations* — this type is not in the plan.

**Created — `tests/Enigma.MarkdownEditor.Core.UnitTests/Vim`**

- `VimFindTests.cs` — 71 cases across 21 facts and theories.

**Modified — `src/Enigma.MarkdownEditor.Core/Vim`**

- `VimMotion.cs` — the six new members, documented in the file's voice. They satisfy the enum's
  written contract (every member is both somewhere to move and something to operate on), so no new
  exception to `WholeLine`'s.
- `KeySequenceParser.cs` — `_awaitingFind`, its branch in `Feed`, the opener branch, `FindOpenerFor`,
  `;`/`,` in `MotionFor`, the `FindMotion` helper, and both `HasPendingSequence` and `Reset()`. The
  class remarks' "three keys hold the sequence open" paragraph is rewritten to four, naming the find
  family beside `r` as the second kind that takes its next key literally.
- `VimEngine.cs` — `_lastFind` / `_lastFindCharacter` beside `_lastSearch`; `ResolveFind` and
  `Reversed`; one arm in `ApplyMotion` covering all six; `find` threaded through to `ApplyOperator`.
  `ApplyMotion(VimMotion, int, bool)` becomes `ApplyMotion(VimCommand, VimFindRequest?)`, per the
  plan, so the argument arrives without a third loose parameter. `Reset()`'s remarks now name the
  last find alongside the register and the search pattern as state that outlives a document swap.
- `VimRanges.cs` — `Resolve` gains an optional `VimFindRequest? find`, checked before the switch, and
  a private `FindRange`: `Characters(caret, target + 1)` forwards for both `f` and `t` — `t`'s target
  is already the short one — and `Characters(target, caret)` backwards for both `F` and `T`.

**Modified — prose**

- `README.md` — §*Motions* gains three rows and a paragraph on the line-bound rule, the inclusive
  forward operator forms, the `;`-after-`t` step and `,` not overwriting the memory; §*Counts* gains
  the clause that a find which overshoots finds nothing rather than clamping. Accepted in the
  documentation freshness sweep — the section opens by calling itself exhaustive, so it was made
  false by this phase rather than merely incomplete. §*Not implemented* is untouched: it lists text
  objects, which is PHASE02's criterion and still true today.

**Workflow artifacts**

- `docs/roadmap.md` — `FEATURE-23AB` `TODO` → `IN PROGRESS`; PHASE01 `TODO` → `IN PROGRESS` → `DONE`.
  The item's own row stays `IN PROGRESS`: PHASE02 is still `TODO`.
- `docs/plan/FEATURE-23AB.md` — the same two statuses.

## Deviations & follow-ups

- **`VimFindRequest` is a new type the plan does not name.** The plan had `VimFind` "taking the
  last-find as parameters", which would have grown `VimRanges.Resolve` from six parameters to nine —
  motion, character and the repeat flag — for one motion family. The carrier makes it one optional
  parameter, keeps the resolution (the only part that reads engine state) in the engine, and keeps
  the scan (the only part that reads the buffer) in `VimFind`. Same split, one fewer seam.
- **`ApplyOperator` did grow a parameter**, from four to five. The plan only objected to growing
  `ApplyMotion`'s, and `ApplyOperator` has one call site.
- **A `t` whose character is already adjacent is a motion that ran, not one that failed.** `t.` on
  `|a.b` leaves the caret where it is, and `dt.` deletes the one character between them, because both
  `f` and `t` are inclusive motions in vim. Vim itself is equivocal here — some builds beep — but the
  inclusive reading is what `:help t` documents and what makes `dt` useful. Tested explicitly rather
  than left to fall out.
- **`;` and `,` are silent before any find**, as the plan specifies, rather than borrowing `n`'s
  `"No previous search pattern"`. Recorded because it is a deliberate asymmetry with `n`, not an
  oversight: `/` is a mode the user visibly entered, `;` has no such surface.
- **Follow-up, out of scope: `.` repeat.** The find family is the first thing a user reaches for after
  `;` fails to be `.`, and `_lastFind` is now a second piece of "what was the last thing typed" state
  living beside `Register` and `_lastSearch`. If `.` is ever planned, those three want looking at
  together rather than one at a time.
- **No line-ending noise.** Every touched and created file is LF, as `.gitattributes`
  (`* text=auto eol=lf`) requires. Nothing to recommend.

## Build/test evidence

- `dotnet build -c Release --no-incremental`: **succeeded, 0 errors, 0 warnings**, with
  `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` on solution-wide.
- `dotnet test -c Release`: **2038 passed, 0 failed, 0 skipped** across
  `Enigma.MarkdownEditor.Core.UnitTests` and `Enigma.MarkdownEditor.Desktop.UnitTests`. 1967 → 2038 is
  the 71 new cases and nothing else; no existing test changed, which is the claim that the new pending
  state and the new `Resolve` parameter cost the rest of the grammar nothing.
- `dotnet run -c Release --no-build -- -class "*VimFindTests"`: **71 passed**, 0.124s.
- **Acceptance criteria, one by one.**
  - `f` `F` `t` `T` `;` `,` behave as the contract table says, as motions and behind `d`, `y`, `c` ✓ —
    `F_GoesForwardOntoTheCharacter`, `CapitalF_GoesBackwardOntoTheCharacter`,
    `T_StopsOneShortOfTheCharacter`, `CapitalT_StopsOneShortGoingBackwards`,
    `Semicolon_RepeatsTheLastFindTheSameWayRound`, `Comma_RepeatsTheLastFindTheOtherWayRound`,
    `AnOperatorOverAFind_CoversWhatTheDirectionSays`,
    `AYankOverAFind_FillsTheRegisterWithWhatTheFindCovered`,
    `AChangeOverAFind_TakesTheTextAndEntersInsertMode`. Visual mode is covered too, though the plan
    did not require it here: `AFindInAVisualMode_MovesTheEndOfTheSelection`.
  - Counts work on either side and multiply ✓ — `ACountOnEitherSide_CountsOccurrences` covers `2d3f-`
    (six occurrences), `3f-` and `d2t-`; `3fa`, `2f.`, `2F.`, `2T.`, `d2f,`, `d2F,` and `f.2;` appear
    across the other theories.
  - No find crosses a line break, in either direction, with or without a count ✓ —
    `AFind_NeverCrossesALineBreak`, five cases including a count reaching for a match on the line
    below.
  - `;` after `t`/`T` advances; `,` reverses without overwriting the memory; `;`/`,` before any find do
    nothing ✓ — `ARepeatedTill_MakesProgressRatherThanStandingStill` (including the mirrored `,` case),
    `Comma_ReversesAFindWithoutBecomingOne` (`f.,,;` still goes forwards),
    `SemicolonOrCommaBeforeAnyFind_DoesNothingAndSaysNothing`.
  - Every no-match case leaves caret, buffer, mode and status bar untouched ✓ —
    `AFindThatMatchesNothing_SaysNothingAndChangesNothing` and
    `AnOperatorOverAFindThatMissed_IsAbandonedWithoutAWord`, both asserting `Status` is null, the mode
    is `Normal` and (for the operator) the register is still empty.
  - `Esc` cancels a half-typed `df`; `HasPendingSequence` is true while one is open ✓ —
    `AHalfTypedFind_IsPendingUntilItsCharacterArrives`, `Esc_CancelsAHalfTypedFind`,
    `AFindOpener_IsPendingAndSaysWhatHasBeenTyped`.
  - `_lastFind` survives `VimEngine.Reset()` ✓ — `TheLastFind_SurvivesAReset`, driving the engine
    directly across a `Reset()`; `AFindThatMissed_IsStillWhatSemicolonRepeats` covers the miss.
  - New file `tests/…Core.UnitTests/Vim/VimFindTests.cs` covers all of the above ✓.
  - Release build 0 warnings; full suite green ✓ — as above.
