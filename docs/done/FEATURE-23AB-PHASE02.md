# FEATURE-23AB-PHASE02 — Text objects

**Status: DONE** · Phase 02 of 2 · Branch: `feature/feature-23ab-phase02-text-objects`
Plan: `docs/plan/FEATURE-23AB.md`

## Summary

`iw` `aw`, the four bracket pairs and the three quote pairs, behind `d`, `y` and `c` and in both
visual modes, with counts on the word objects. Entirely inside `src/Enigma.MarkdownEditor.Core/Vim/`:
no `IVimBuffer` change, no key-routing change, nothing in `Desktop`.

**A text object is a different kind of noun, so it is a different type.** `VimMotion`'s own remarks
promise that every member is somewhere a caret can go, and there is nowhere for `iw` to move a caret
to on its own — so `VimTextObject` is its own enum and `VimCommand` carries it beside the motion
rather than instead of it. `ApplyOperator` picks the resolver off whichever noun is set; everything
downstream — the empty-range rule, `Yank` / `Delete` / `Change` — is untouched.

**`i` and `a` are resolved by position, which is what vim does.** They are the insert keys and they
are also both halves of every text object, and the collision is real: the parser opens an object only
where one could be handed to something — an operator waiting, or a selection on screen — and leaves
them as insert commands everywhere else. `iw` typed on its own still inserts a `w`. The branch sits
below `MotionFor` and above the `_operator != None` dead-end, which is the one position that catches
both cases; the comment there previously used `di` as its example of a sequence that never becomes a
command, and now uses `dp`.

**Three families, and the only thing they disagree about is reach.** `VimTextObjects` holds all
three, sibling to `VimWords` and `VimFind`. A word is a run of one of `VimWords`' three character
classes — shared, not reimplemented, so `iw` cannot come to mean something `w` does not — and stays
on the caret's line, which is what makes `aw`'s fallback to the blanks *before* the word meaningful.
A bracket pair scans the whole buffer counting nesting, because `ci{` over a fenced block is most of
the reason to have the feature. A quote pair stays on the line and pairs off from its start, because
nothing in the text says which of two identical characters opens and which closes.

**An empty object is a match, a missing one is not.** `i(` on `()` covers nothing and still resolves,
so `ci(` enters insert mode between the parentheses — the rule `ApplyOperator` already applies to
`c0`. `i(` with no parentheses round the caret is `null`, and the engine answers that with the
silence it already answers a find that missed with: caret, buffer, mode and status bar all untouched.

## Files/modules touched

**Created — `src/Enigma.MarkdownEditor.Core/Vim`**

- `VimTextObject.cs` — the enum, in inner/around pairs, each member documented with the rule that
  governs its family.
- `VimTextObjects.cs` — `Resolve(buffer, caret, textObject, count)` → `VimTextRange?`. Reads the
  inner/around half off once and dispatches on the pair, so the three resolvers are written once each
  rather than sixteen times. Always characterwise.

**Created — `tests/Enigma.MarkdownEditor.Core.UnitTests/Vim`**

- `VimTextObjectTests.cs` — 92 cases across 22 facts and theories.

**Modified — `src/Enigma.MarkdownEditor.Core/Vim`**

- `VimCommand.cs` — `TextObject` as the last positional member, and the `Object(operator, object,
  count)` factory beside `Move` and `Operate`. Still a `VimAction.Operate`: only the noun differs.
- `KeySequenceParser.cs` — `_awaitingObject` (the `i` or the `a` itself, because which one was typed
  is half of what the object is), its branch in `Feed`, the opener branch, `IsObjectOpener`,
  `TextObjectFor`, `ObjectCommand`, and both `HasPendingSequence` and `Reset()`. The class remarks'
  "four kinds of key hold the sequence open" paragraph is now five, with a paragraph of its own on
  the `i`/`a` collision; the `_operator != None` guard's comment is rewritten.
- `VimEngine.cs` — `SelectTextObject` beside the other visual helpers; the `VimAction.Move` arm routes
  to it when the command names an object; `ApplyOperator` becomes `ApplyOperator(VimCommand,
  VimFindRequest?)` and chooses between `VimRanges.Resolve` and `VimTextObjects.Resolve`.
- `VimWords.cs` — `ClassOf` and `CharacterClass` go from private to internal, with the reason written
  in: a rule with two implementations would come apart on `foo.bar` first.

**Modified — tests**

- `VimScript.cs` — `<lt>` added to the harness's key names. See *Deviations*.
- `KeySequenceParserTests.cs` — `di` in the "operator over something that is not a motion" theory
  becomes `diz`, since `di` is now a half-typed object; `'i'` leaves the "no visual meaning" theory.
- `VimOperatorTests.cs` — the same `di` → `diz` substitution, same reason.
- `VimVisualTests.cs` — `i` and `a` leave the "no visual meaning" theory, and its comment, which
  listed them as text objects on the out list, now says they are not.

**Modified — prose**

- `README.md` — a new §*Text objects* between §*Operators* and §*Editing*: the table of objects, what
  an object is for, the three families' reach rules, and the count rule. §*Operators* gains a row,
  §*Modes*' `i` `a` row gains the clause that says when they mean something else, and §*Not
  implemented* no longer lists text objects — it now names what of the family is still out (the WORD
  motions and their objects, sentence/paragraph/tag, `2i(`).
- `docs/plan/FEATURE-11AA.md` — the out-of-v1 list on line 25 no longer claims text objects are out,
  and points at this item.
- `docs/RELEASE.md` — the *Vim smoke set* row of the Linux release check gains `f<c>` then `;`, `diw`
  and `ci(`, with the reason those keys are worth typing on that platform: both read the key after
  them through the platform's own text input. Accepted in the documentation freshness sweep; the row
  had never gained PHASE01's find family either. `RELEASENOTES.md` was offered and declined — it is
  1.2.0's document, and `FEATURE-449F` (Release 1.3.0) is the item that rewrites it.

**Workflow artifacts**

- `docs/roadmap.md` — PHASE02 `TODO` → `IN PROGRESS` → `DONE`, and `FEATURE-23AB`'s own row to `DONE`
  with it: both phases are now finished.
- `docs/plan/FEATURE-23AB.md` — the same two statuses, and the item header's.

## Deviations & follow-ups

- **The harness did need one change after all.** The plan said no harness change was needed, and it
  was wrong about exactly one character: `VimScript` reserves `<` to open a key name, so `di<` was
  read as an unclosed `<Down>`-style name and threw. `<lt>` — vim's own spelling for a literal `<` —
  is now one more entry in `Named`. Three test cases use it; nothing else about the harness moved.
- **`ApplyOperator` takes the command rather than five loose parameters.** The plan had it gaining the
  text object as a sixth; it has one call site, and `ApplyMotion` was already reshaped this way in
  PHASE01, so `(VimCommand, VimFindRequest?)` keeps the two symmetrical instead of growing one of
  them a sixth time.
- **Word objects are bounded by the caret's line; vim's are not.** Vim's `aw` will take a line break
  and the blanks after it. Here the scan stops at the line's end, which is what makes the contract's
  "or before it, at end of line" fallback the rule rather than an edge case, and what keeps `daw`
  from joining two lines. Deliberate, and the plan's contract table already describes this behaviour.
- **A quote object does not jump forward to the next quoted run.** In vim, `ci"` with the caret before
  the first quote on the line changes the string further along; here the caret has to be inside a
  pair, delimiters included, and is otherwise a miss. The plan specifies the pairing rule and not the
  jump; recorded because it is the one place a vim user may notice something missing.
- **Escapes inside quotes are not honoured.** Vim skips a `\"` when pairing; this counts it. A line
  like `"a\"b"` therefore pairs the first quote with the escaped one. Out of scope for the pairing
  rule the plan specified, and worth a follow-up only if it bites in practice.
- **`2i(` ignores its count**, as the plan requires — real vim walks one nesting level outwards per
  count. `d2i(` is `di(`, silently, rather than being turned down. Covered by a test that asserts the
  two are equal so the behaviour is pinned rather than incidental.
- **An object range is always characterwise.** Vim promotes `i{` to linewise when the braces sit alone
  on their own lines, which changes where a later `p` puts the text back. Not implemented; the plan
  says always characterwise.
- **A count on a word object clamps rather than failing.** `d9iw` on a two-word line takes both words
  — unlike a find, where an overshooting count deliberately finds nothing. The difference is defensible
  (a word object is bounded by the line either way, so there is no wrong place for it to land) but it
  is an inconsistency between two things built in the same item, and is tested explicitly rather than
  left to be discovered.
- **Follow-up, out of scope: `.` repeat.** Unchanged from PHASE01 — it is now the top entry on the
  README's *Not implemented* list, and the state it would need (`Register`, `_lastSearch`, `_lastFind`)
  still wants looking at together rather than one piece at a time.
- **No line-ending noise.** Every touched and created file is LF, as `.gitattributes`
  (`* text=auto eol=lf`) requires. Nothing to recommend.

## Build/test evidence

- `dotnet build -c Release --no-incremental`: **succeeded, 0 errors, 0 warnings**, with
  `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` on solution-wide.
- `dotnet test -c Release`: **2127 passed, 0 failed, 0 skipped** across
  `Enigma.MarkdownEditor.Core.UnitTests` and `Enigma.MarkdownEditor.Desktop.UnitTests`. 2038 → 2127 is
  the 92 new cases less the 3 removed ones (`'i'` and `"i"`/`"a"` from the two "no visual meaning"
  theories, whose behaviour this phase changes), and nothing else.
- `dotnet run -c Release -- -class "*VimTextObjectTests"`: **92 passed**, 0.208s.
- **Acceptance criteria, one by one.**
  - Every object in the contract table resolves correctly behind `d`, `y` and `c`, and in `v` ✓ —
    `Iw_IsTheRunUnderTheCaret`, `Aw_TakesTheBlanksBesideTheWordToo`,
    `ABracketObject_IsThePairRoundTheCaret`, `AQuoteObject_IsThePairTheCaretFallsIn` and
    `EitherDelimiterAndTheMnemonic_NameTheSamePair` cover the whole table under `d` including both
    spellings and the `b`/`B` mnemonics; `AYankOverATextObject_FillsTheRegisterWithWhatItCovered`
    walks the whole table again under `y`, asserting the register's contents and kind;
    `AChangeOverATextObject_TakesTheTextAndEntersInsertMode` covers `c`; and
    `ATextObjectInAVisualMode_SelectsWhatItCovers` covers `v` for all three families.
  - `iw`/`aw` follow `VimWords`' three classes and honour a count; delimiter objects ignore theirs ✓ —
    `AWordObject_UsesTheSameThreeClassesAsTheWordMotions` (`foo.bar` is three, `foo_bar` is one, a run
    of `+` is a word of its own), `ACountOnAWordObject_TakesThatManyOfThem` (`d2iw`, `d3iw`, `d2aw`,
    `d9iw`), `ACountOnADelimiterObject_IsIgnoredRatherThanRefused`.
  - Brackets nest and span lines; quotes do neither and stay on the line ✓ —
    `ABracketObject_CountsNestingAndSpansLines` (including `di{` and `da{` across three lines),
    `ACaretOnADelimiter_BelongsToThatPair`, `AQuoteObject_NeitherNestsNorLeavesTheLine` (the pairing
    rule from both sides, a pair the line break cuts off, and an odd quote).
  - `ci(` on an empty pair enters insert mode between the delimiters ✓ —
    `ChangeOnAnEmptyPair_EntersInsertModeBetweenTheDelimiters`, with the round trip through a typed
    character and `Esc`; `DeleteOnAnEmptyPair_HasNothingToDoAndTakesNothing` is the other half.
  - Every no-match case is a silent no-op that leaves the mode alone ✓ —
    `ATextObjectThatMatchesNothing_SaysNothingAndChangesNothing` (six cases: no pair at all, an
    unmatched delimiter on either side, and a caret beside a pair rather than inside it), asserting
    text, mode, a null status and an empty register; `AWordObjectWithNothingUnderIt_IsAMatchWithNothingInIt`
    for the empty-buffer case; `AKeyThatNamesNoObject_LeavesAVisualSelectionAlone` for the visual one.
  - `i` and `a` still insert when no operator is pending and the mode is not visual ✓ —
    `IAndA_StillInsertWhereNoObjectCouldBeUsed`, whose third case types `iw` and gets a `w` in the
    document.
  - New file `tests/…Core.UnitTests/Vim/VimTextObjectTests.cs` covers all of the above ✓, plus the
    grammar itself: `AHalfTypedObject_IsPendingUntilItsKeyArrives` (behind an operator and in visual
    mode), `Esc_CancelsAHalfTypedObject`, `AnObjectOpener_IsPendingAndSaysWhatHasBeenTyped`,
    `AnObjectBehindAnOperator_ParsesAsThatOperatorOverIt` (with counts on both sides multiplying to
    six), `AnObjectInAVisualMode_ParsesAsAMoveThatNamesTheText`,
    `ATextObjectInAVisualMode_MovesBothEndsRatherThanExtendingTheSelection` and
    `ATextObjectInLinewiseVisualMode_StaysLinewiseOverTheLinesItTouches`.
  - `README.md` §*Not implemented* no longer lists text objects, and its tables gain the new keys;
    `docs/plan/FEATURE-11AA.md` line 25's out-of-v1 list is corrected ✓ — as described under *Files
    touched*. The README gained a §*Text objects* section rather than folding eleven objects into a
    §*Motions* row, because they are not motions and the reach rules need prose.
  - Release build 0 warnings; full suite green ✓ — as above.
