# FEATURE-11AA-PHASE02 — Operators, counts & registers

**Status: DONE** · Branch: `feature/feature-11aa-phase02-operators` · Plan: `docs/plan/FEATURE-11AA.md`

## Summary

PHASE01 gave the editor a vocabulary of eighteen commands, each one written down by name. This phase
replaces that with a **grammar** — `[count] [operator] [count] [motion]` — and the vocabulary becomes
what the grammar can say. Three operators (`d`, `y`, `c`) over eleven motions, with a count on either
side that multiplies, is thirty-three commands nobody had to enumerate: `dw`, `y3j`, `2d3w`, `cG`,
`d0`, `de`, `dgg` all work because the parts do, not because they were listed.

**Vim's two irregularities are in, because a vim without them is not one.** `cw` on a word changes to
the *end* of the word, like `ce`, so changing a word does not swallow the space after it — the quirk
vim's own manual apologises for and every vim user's fingers depend on. And `dw` on the last word of a
line stops at the line's end rather than running on to the next line's first word, so deleting the
last word of a paragraph does not pull the following line up onto it. Both live only in
`VimRanges`, because both are only true of a motion used *with an operator*: as caret motions `w` and
`e` are unchanged.

**The register is a pair, not a string.** What `dd`, `yy`, `cc` and `Y` take out is *lines*, and goes
back as lines above or below the caret's line; what `x`, `dw` and `cw` take out is *characters*, and
goes back beside the caret inside the line. `p` and `P` read the kind and place the text accordingly,
which is why `xp` transposes two characters and `yyp` duplicates a line. It survives `Reset` — vim's
register outlives the buffer it was filled from, which is what makes yanking in one tab and putting in
another work — and putting with nothing in it says so rather than doing nothing silently.

**The change family is here in full**: `cw ce cc C D Y r ~`. `r` is the one command that reads the
*next* key literally, so `rd` writes a `d` rather than starting a delete, and it refuses rather than
doing as much as it can — `3rx` with two characters left on the line leaves the line alone, so a
mistyped count cannot half-destroy a word. `~` swaps the case under the caret and steps past it,
touching the register not at all.

**`u` and `Ctrl+R` complete the base contract**, which PHASE01 left for here. The engine keeps no
history of its own: it asks the buffer, which is AvaloniaEdit's own undo stack, so `u` and `Ctrl+Z`
take back the same step. What the engine decides is how much *is* a step, and it decides it with one
rule — **every command that changes the buffer does it in exactly one write**. Counts do not change
that: `3x`, `5p`, `3rX` and `9~` each build the whole result and write it once. The single exception
is a change, where what is typed afterwards belongs to the same step, so `c` opens the insert
session's own undo group and leaves it open until `Esc`. That is the acceptance criterion, and it is
verified twice: against the fake's grouping, and against AvaloniaEdit's real `UndoStack` in the probe.

**Counts clamp rather than fail.** `9j` on a three-line file lands on line three, `9dd` takes what is
left, `9x` takes the rest of the line — which is what the plan's testing strategy asked for
("count overshoot clamps") and the rule is applied uniformly, including to an operator's linewise
motion. And `0` is a digit only once another one has been typed, which is exactly what leaves it free
to be the motion to the start of the line.

## Files/modules touched

### Created — `src/Enigma.MarkdownEditor.Core/Vim`

| File | Notes |
|---|---|
| `VimOperator.cs` | `Delete`, `Yank`, `Change`. Apart from `VimAction` because an operator is not a command on its own — `d` means nothing until a motion says what to delete, which is exactly the state the parser sits in between the two keystrokes. |
| `VimRegister.cs` / `VimRegisterKind.cs` | The unnamed register as a value: text plus **charwise or linewise**. Linewise text is stored joined by the buffer's terminator and *without* a trailing one — the terminator a put needs is added where the text lands, which is what stops a register taken from the last line of a file pasting differently to one taken from the middle. A value rather than an object so a test can name a whole expected register in one expression. |
| `VimTextRange.cs` | Two offsets and a kind. A linewise range is expressed as its **content** — first line's start to last line's end, terminator excluded — rather than as line numbers, so yanking is one operation for both kinds and only the deletion has to decide which terminator goes with the text. |
| `VimRanges.cs` | What a motion covers when an operator is waiting on it. Where the two vim irregularities live, and where the difference between a motion and a range does: `l` as a motion stops on the last character of a line, but `dl` on that character has to delete it, so the range runs one further than the caret ever would. |

### Modified — `src/Enigma.MarkdownEditor.Core/Vim`

| File | Change |
|---|---|
| `KeySequenceParser.cs` | The grammar widened from "one key, or `g` and one more" to `[count] [operator] [count] [motion]`. Three states now hold a sequence open — `g`, an operator, and `r` waiting for the character to write — and the last one takes the next key **literally**, whatever it spells. The two counts are kept apart until the command completes and then multiplied, which is vim's own rule and the reason `2d3w` is `d6w`. A count is capped at 100 000, which is far past anything typed on purpose and low enough that ten times it plus nine cannot overflow. |
| `VimEngine.cs` | The register, `ApplyOperator`, the three operators, `p`/`P`, `r`, `~`, `u`/`Ctrl+R`, and counts through every motion. `MoveToLine` now clamps instead of refusing, so a count that overshoots lands on the last line. `LinewiseSpan` is where `dd` decides **which** terminator goes with the lines: normally the one after the last, but for the last line of the buffer the one *before* the first, or `dd` at the bottom of a file would leave an empty line where the text was. |
| `VimAction.cs` | `Operate`, `PutAfter`, `PutBefore`, `ReplaceCharacter`, `ToggleCase`, `Undo`, `Redo`. |
| `VimMotion.cs` | `WholeLine` — what an operator doubled up on itself means (`dd`, `yy`, `cc`, `Y`). The one member that is not a place for a caret to go, and the one the parser never produces as a move. |
| `VimCommand.cs` | Grew `Count`, `Operator` and `Argument`. `Count` is **zero** when none was typed rather than one, because `G` is the last line and `5G` is line 5 — a command that could not tell 1 from "none" would have no way to say which. |
| `IVimBuffer.cs` | `GetText(offset, length)` (what the register is filled from), `Undo()` and `Redo()`. |

### Modified — `src/Enigma.MarkdownEditor.Desktop`

| File | Change |
|---|---|
| `Views/AvaloniaEditVimBuffer.cs` | `GetText`, and `Undo`/`Redo` onto the editor's own stack rather than a history of the engine's — so `u` and `Ctrl+Z` take back the same step, which a file edited half in normal mode and half from the menu depends on. |
| `Views/VimEditorBehavior.cs` | `Ctrl+R` is the one command in the contract that is a **held key** rather than a typed character, and a modified key produces no text input at all, so it is recognised from the key code. Every `Ctrl`/`Alt` letter combination is built the same way and offered to the engine, which turns all but that one down — so `Ctrl+S` and `Ctrl+W` still reach the window's own bindings unhandled. |

### Created / modified — tests (1095 total, was 878)

| File | Notes |
|---|---|
| `Core.UnitTests/Vim/VimOperatorTests.cs` (69) | The three operators over the eleven motions: what comes out, what is left, where the caret lands, and what goes in the register with which kind. `dd` at the top, the bottom, the middle, on an empty line and on the only line there is; the `dw` line-end rule at four different shapes of line break; the `cw`≈`ce` quirk and the blank it does not apply to; `D`/`C`/`Y`; and the sequences that never become commands (`di`, `dy`, `3dz`), each named in full on the status bar. |
| `Core.UnitTests/Vim/VimPutTests.cs` (24) | Where `p` and `P` land, char vs line, including `xp` transposing two characters, a put at the end of a line where there is nowhere past the caret to go, a linewise put into an almost-empty buffer where the put has to make the terminator it needs, an empty line put back as an empty line, and a put with nothing yanked saying so. |
| `Core.UnitTests/Vim/VimCountTests.cs` (33) | The count as one rule rather than a feature of any one command: it multiplies every motion, survives an operator in front of it, clamps at both edges of the document, and `0` is a digit only once another one has been typed. |
| `Core.UnitTests/Vim/VimReplaceTests.cs` (27) | `r` and `~`: the literal next key, the refusal when the count does not fit, both at the end of a line and on an empty one, the case of what has no case, and that neither touches the register. |
| `Core.UnitTests/Vim/VimUndoTests.cs` (25) | What one step costs. Every command that changes something is one step; every command that does not is none; a change and everything typed in its place is **one**; and `Ctrl+R` is the one held key the engine claims, with `Ctrl+S`, `Ctrl+W` and `Ctrl+U` asserted to fall through. |
| `Core.UnitTests/Vim/KeySequenceParserTests.cs` (31 → 70) | The widened grammar on its own: operator×motion, the doubled forms, the three capitals, the two counts multiplying, `r`'s literal key, and that nothing — count, operator or half-typed `g` — survives into the next sequence. One PHASE01 case changed rather than was added: `5` was asserted to be "not a command", and is now the start of a count. |
| `Core.UnitTests/Vim/FakeVimBuffer.cs` | `GetText`, and an undo stack of whole-text snapshots with the real one's grouping rule — a change inside an open group joins the step already there. Crude next to AvaloniaEdit's and exactly as crude as it needs to be: what the tests ask is how many *steps* a command cost, which is a question about the grouping, not the diffing. Exposes `UndoSteps`. |
| `Core.UnitTests/Vim/VimScript.cs` | `VimRun.Register`, so a case can assert on what was taken as well as on what is left. |

## Deviations & follow-ups

1. **Operator + motion was built, though the contract's bullet list names only `dd yy p P` and the
   change family.** The plan's *Design* section specifies the grammar `[count] [operator] [count]
   [motion]`, `VimMotion`'s own doc comment (written in PHASE01) says "PHASE02 puts the same list
   behind an operator, so that `dw` is the delete action over `WordForward`", and the acceptance
   criterion asks for "every operator×count×edge case". Building only the doubled forms would have
   satisfied the bullet list and none of those three.
2. **`u` and `Ctrl+R` landed here.** The contract lists them under "Base (PHASE01–02)"; PHASE01 did
   not implement them, and this phase's acceptance criterion ("`u` after `cc` restores in exactly one
   step") cannot be met without `u`. They brought `IVimBuffer.Undo`/`Redo` with them.
3. **`Ctrl+R` is read from the key code, not from a character** — the one place in the engine's input
   path where that is true, and the opposite of PHASE01's rule. It has to be: a modified key produces
   no text input at all, so there is no character to read. It is safe for this one command because vim
   names it by the letter and every layout puts it on the key marked R. Verified in the probe that
   `Ctrl+S` still reaches the window unhandled.
4. **Counts clamp; they do not refuse.** Vim beeps and refuses some overshooting counts. The plan's
   testing strategy says "count overshoot clamps", so clamping is the rule, and it is applied
   uniformly — which also means **`dj` on the last line deletes that line** rather than doing nothing.
   One rule everywhere beat two rules that disagree, but it is the one place where clamping is
   slightly more destructive than vim.
5. **A count on an insert command is parsed and ignored.** `3iX<Esc>` in vim types `XXX`; here it types
   `X`. Repeating inserted text is the `.`-repeat machinery, which is on the v1 out list, and the
   contract's count examples are all motions and operators (`3dd`, `2w`, `5j`, `4x`, `2p`).
6. **A key with no vim meaning now cancels a half-typed sequence** rather than being swallowed on top
   of it. PHASE01 swallowed a function key or a Page Down silently and left the pending state alone;
   with `r` in the grammar that would leave it armed, so the next letter typed would be written into
   the document instead of running the command it spells. Covered by a test.
7. **A linewise yank that reaches above the caret leaves it on the first line's first non-blank**,
   rather than keeping the column. Vim keeps the column. `yy`, `yj` and `Y` — the common cases — do
   not move the caret at all, which is the behaviour that matters; `yk` and `ygg` differ by a column.
8. **`Y` is `yy`, not `y$`.** Vim's own default, and what the contract's grouping implies by listing
   `Y` beside `D` and `C` rather than defining it.
9. **`VimTextRange` and `VimRanges` are `internal`.** They are the shape of an answer between two
   classes in the same assembly, not part of what the editor sees. If PHASE03 needs a visual selection
   expressed as a range, that is the moment to reconsider — not before.
10. **Linux was not verified** — no Linux machine was available in this session. Nothing added here is
    platform-specific: the engine is string arithmetic, and the one new view-layer path reads
    `KeyModifiers` and `Key`, both of which Avalonia normalises across backends.
11. **Line endings (recommendation only, no action taken).** Every file in this diff is LF and
    `.gitattributes` (`* text=auto eol=lf`) keeps it that way; there is no CRLF noise in the diff. A
    CRLF *document* is a different matter and is handled — five probe checks cover `dd`, `2dd`, `dw`
    and `D` against a CRLF buffer, since the Core fake can only hold LF.
12. **Two stale rows in the settings window are still disabled**, carried over from the PHASE01
    completion doc and still outside this phase's scope: "Show every file" carries a `FEATURE-0DE1`
    pending tag and "Image folder" is disabled, though both features shipped.

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
  total: 1095   failed: 0   succeeded: 1095   skipped: 0
```

1095 tests = 878 before, plus 217 added here: 178 in the four new suites
(`VimOperatorTests` 69, `VimCountTests` 33, `VimReplaceTests` 27, `VimUndoTests` 25, `VimPutTests` 24)
and 39 net in `KeySequenceParserTests` (31 → 70, one PHASE01 case changed rather than added). The Vim
suites now hold 379 of the solution's tests.

### How the behavioural criteria were verified

| Criterion | Result |
|---|---|
| Table-driven tests cover every operator×count×edge case | **Met.** 178 new cases across the four suites plus 39 in the parser. Named edge cases from the plan: last-line `dd` (three shapes — only line, last of many, empty line in the middle); count overshoot on `j`, `k`, `G`, `l`, `h`, `x`, `dd` and `dj`; `p`/`P` line vs char placement including into an almost-empty buffer and at a line's end; `r` at EOL and `r` refusing a count that does not fit; `~` advancing the caret and stopping at the line's end; `p` on an empty buffer and with nothing yanked. |
| `u` after `cc` restores in exactly one step | **Met, twice.** Against the fake's grouping (`AChangeAndWhatWasTypedInItsPlace_ComeBackInExactlyOneStep`, which also asserts the stack is then empty), and against AvaloniaEdit's real `UndoStack` in the probe — `cc`, three typed characters and `Esc`, then one `u` back to the original text with `CanUndo` false. |
| The `cw`≈`ce` quirk | **Met.** `cw` and `ce` produce identical results on a word; on a blank `cw` falls back to `dw`'s range, which is what vim does and what the quirk does *not* apply to. |
| Undo grouping — insert session = one undo, each operator = one undo | **Met.** Every buffer-changing command issues exactly one write, asserted by step count for `3x`, `5p`, `3rX` and `3~`; an insert session is one step and two sessions are two; a yank and a motion are none. |

The Desktop half was verified by a throwaway probe outside the repository (a headless Avalonia app
over the **real** `TextEditor`, `AvaloniaEditVimBuffer` and `VimEditorBehavior`, driving real key and
text-input events through a real window with `HeadlessWindowExtensions.KeyPress`/`KeyTextInput`).
**32 checks, all green**: `dd`/`p`/`u`/`u`/`Ctrl+R` against the real undo stack; `u` after `cc` in one
step with `CanUndo` false afterwards; the editor's own `Undo()` seeing what `x` did; `dw`'s line-end
rule and the register kinds on a real document; a put keeping the document's own line ending; five
CRLF cases (`dd` mid-document, `dd` on the last line, `dw`, `2dd`, `D`); `Ctrl+R` claimed through a
real key event while `Ctrl+S` reaches the window unhandled; `2dw` and `r`+`d` through real text input;
insert mode still ordinary typing; and the settings toggle still detaching mid-session.
