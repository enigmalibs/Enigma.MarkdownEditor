# FEATURE-724A-PHASE03 — Registers, repeat & macros

**Status: DONE** · Phase 03 of 5 · Branch: `feature/feature-724a-phase03-registers-repeat-macros`
Plan: `docs/plan/FEATURE-724A.md`

## Summary

The three things that make vim remember what it did: the named registers, `.` repeat, and macros.
One seam moved for it — `IVimBuffer` gained a `Changed` event — and everything else is in the engine.
`README.md` §*Not implemented* no longer lists `.`, named registers or macros.

**The registers became a store.** A new `VimRegisters` holds the unnamed register, the twenty-six
named ones, the yank register `0` and the black hole `_`, with vim's rules: the unnamed register
follows every fill but the black hole's, `0` is filled by a yank alone and survives the deletes since,
a capital appends (lines onto anything make lines), and `_` keeps nothing. `VimEngine.Register` stays
and now reads `Unnamed`; `VimEngine.Registers` exposes the store read-only. The parser learned the
`"{name}` prefix — it opens `_awaitingRegister`, takes the next key literally, and carries the name on
`VimCommand.Register` into the completed command, on either side of the count and in both grammars. A
prefix rides only on a command that reads or fills a register (`TakesRegister`); on anything else,
`"aj`, it is a dead sequence named in full.

**`.` reads the change off the buffer, not the keys.** The engine records the last change at the end
of every command that edits — `IsChange` is the one list of which those are — and for an insert
command at the `Esc` that closes it. What was typed in insert mode is not something the engine sees
(a key there is the editor's), so a new `VimInsertRecorder` follows `IVimBuffer.Changed` while a
session is open and keeps the text: an insertion at its end appends, a one-character rub just before
its end pops, anything else ends the record. `.` re-runs the same `VimCommand` through `Execute` with
a new count substituted (and kept, so `3.` then `.` is `3.` again), the typed text played back inside
the insert session's undo group so the whole replay is one `u`. A visual change is remembered as an
*extent* — mode, lines, and a column — and replayed by selecting that much again from the caret. A
count on an insert command (`3ix`, `2o`, `3A`) repeats its text the same way.

**A macro is the harness's own loop, moved into the engine.** `q{a-z}` opens a recording; every key
the engine takes is appended in `VimKeyNotation`, and an insert session's text is written in at its
`Esc` — the `q`s that bound it are not part of it. The stopping `q` is caught in `HandleKey` before
the parser, because only the engine knows a recording is open. `@a` parses the register and runs
`foreach key: if (!HandleKey(key)) TypeIntoBuffer(key)` behind a depth counter; a find or search that
misses, a dead sequence, an unrecognised ex command or an empty register sets a fail flag that unwinds
every level, and a nesting past 100 stops with `Macro nesting too deep`. A register filled by a yank
runs as keys too (a linewise one ends each line with `<CR>`), which is vim's rule and the reason `@a`
after `"ayy` runs the line. The status slot reads `recording @a` across commands that would otherwise
clear it, through a new `ClearStatus` that replaces the bare `ShowStatus(null)` calls.

**One notation, one implementation.** `VimKeyNotation` formats a `VimKey` the way vim writes it and
parses it back — `<Esc>` `<CR>` `<C-r>` `<lt>` and the rest — and is now what `VimKey.ToString()`
delegates to, what a macro register holds, what a dead-sequence status message is named with, and what
the test harness (`VimScript`) parses its keys argument with. The harness's own key parser was deleted
in favour of it.

## Files/modules touched

**Created — `src/Enigma.MarkdownEditor.Core/Vim`**

- `VimRegisters.cs` — the store: `Unnamed`, `Get`, `Fill` (the `0`/`_`/unnamed/append rules),
  `Record`, `SetUnnamed`, `IsName`.
- `VimKeyNotation.cs` — `Format(VimKey)` / `Format(IEnumerable<VimKey>)` / `Parse(string)`; vim's
  names, the modifier prefixes, `<lt>`, and lenient reading (a `<` that opens no known name is a `<`).
- `VimTextChange.cs` — `(Offset, RemovedLength, InsertedText)`, the shape of `IVimBuffer.Changed`.
- `VimInsertRecorder.cs` — follows `Changed` while an insert session is open and keeps the text typed.

**Modified — `src/Enigma.MarkdownEditor.Core/Vim`**

- `IVimBuffer.cs` — `event EventHandler<VimTextChange>? Changed`, with the swap-following contract in
  its remarks.
- `VimCommand.cs` — `char Register`.
- `VimAction.cs` — `Repeat`, `RecordMacro`, `PlayMacro`, `PlayLastMacro`.
- `VimKey.cs` — `ToString()` delegates to `VimKeyNotation`.
- `VimRegister.cs` — remarks rewritten (it is one register's content; the store is `VimRegisters`).
- `KeySequenceParser.cs` — `_awaitingRegister`, `_register`, `_awaitingMacro`; the `"` prefix, the
  `q`/`@` openers, `MacroCommand`, `TakesRegister`, `.` in `ActionFor`; `Complete` attaches the
  register and refuses a prefix on a command that takes none; class remarks and `HasPendingSequence`
  and `Reset()` extended.
- `VimEngine.cs` — `Registers`, `RecordingRegister`; the `Changed` subscription and
  `VimInsertRecorder`; `_activeRegister` threaded through `Fill`, `Put`, `DeleteCharacters` and
  `ReplaceSelection`; `IsChange`, `RepeatLastChange`, `CurrentExtent`/`Reselect`, `CloseInsertRecord`
  / `RepeatTyped` / `TypeText` / `TypedKeys`; `StartRecording` / `StopRecording` / `PlayMacro` /
  `RegisterKeys` / `TypeIntoBuffer`; `ClearStatus` / `Fail`; the `HandleKey`/`Dispatch` split that
  records taken keys; find/search/ex misses routed through `Fail`; `VisualExtent` and `RecordedChange`
  records. Register, last change and last macro survive `Reset()`.

**Modified — `src/Enigma.MarkdownEditor.Desktop`**

- `Views/AvaloniaEditVimBuffer.cs` — implements `Changed` over `TextDocument.Changed`, following the
  control's `DocumentChanged` swap so the event fires only for the document on screen.

**Created — tests**

- `tests/…Core.UnitTests/Vim/VimKeyNotationTests.cs` — write, read, and the round trip.
- `tests/…Core.UnitTests/Vim/VimRegisterTests.cs` — the prefix on either side of the count and in
  visual mode, `0`, `_`, the capital append, survives-reset.
- `tests/…Core.UnitTests/Vim/VimRepeatTests.cs` — `.` over every change kind, the excluded kinds,
  counts on `.` and on the insert commands, one-undo-step.
- `tests/…Core.UnitTests/Vim/VimMacroTests.cs` — record/replay, `3@a`, `@@`, the status slot, the
  recorded notation, a macro that enters insert mode, a yank register run as keys, the find-miss stop,
  the recursion abort, survives-reset.
- `tests/…Desktop.UnitTests/Views/AvaloniaEditVimBufferTests.cs` — `Changed` fires for a typed
  character and a removal, follows a document swap, and never fires for the document left behind.

**Modified — tests**

- `KeySequenceParserTests.cs` — the register prefix, `q`/`@`/`@@`/`.` grammar, and the refusals; the
  two assertions that named a literal `<` were updated to its `<lt>` notation, and `q` moved off the
  "spells nothing" theory (it is now a pending prefix).
- `VimScript.cs` — parses keys through `VimKeyNotation`; `VimRun.RegisterNamed(char)`; the harness's
  own key parser removed.
- `FakeVimBuffer.cs` — raises `Changed` from `Replace` and from an undo/redo restore.

**Modified — prose**

- `README.md` — §*Editing* gains the register, `.` and macro rows and two paragraphs; §*Not
  implemented* drops `.`, named registers and macros (regex search and `:s` stay for PHASE04) and adds
  the system-clipboard register `"+` to the out list.

**Workflow artifacts**

- `docs/roadmap.md` — PHASE03 `TODO` → `IN PROGRESS` → `DONE`; the item stays `IN PROGRESS`.
- `docs/plan/FEATURE-724A.md` — PHASE03's status.

## Deviations & follow-ups

- **`VimKey.ToString()` now names a literal `<` as `<lt>` in a status message.** The plan has
  `ToString` delegate to `VimKeyNotation` (its example: `Not a command: d<Esc>`), and the notation
  writes a literal `<` as `<lt>` because `<` is where a key name begins. So a half-typed or dead
  sequence containing the `<` shift operator now reads `<lt>` on the status bar — `Not a command:
  ><lt>` — vim's own unambiguous spelling. Two parser tests that asserted the old bare `<` were
  updated. Named keys also read vim's spellings now (`<Esc>`, `<CR>`, `<BS>`) rather than the enum
  names; no test asserted the old form.
- **The register prefix is not carried across `v`/`V`.** `"aviwd` is a dead sequence (`"a` then `v`,
  which takes no register); the register is typed in visual mode before the operator, `viw"ad`, as in
  vim. `v`/`V` are not on the plan's list of register-taking commands, so this matches both the plan
  and vim's own behaviour.
- **`VimInsertRecorder` is a class the plan did not name.** The plan put the "text typed" logic in
  *Cross-cutting design* without a home; it is cleanest as its own small state machine that both `.`
  and a macro read, rather than fields on the engine.
- **The insert record ends on any discontiguous edit**, exactly as the plan's *Recording insert
  sessions* says — one fewer state than vim, which starts a new insert on an arrow key. Recorded as
  the phase's one deliberate difference; the acceptance criteria do not exercise a mid-insert arrow.
- **`RepeatTyped` handles the count on an insert command**, kept separate from the `.` replay path:
  `3ix` is remembered as `3i`+`x` (not `xxx`), so a later `.` types one `x`, which is vim's rule.
- **`No previously used register`** is vim's exact wording for `@@` before any `@`; `Macro nesting
  too deep` matches the plan.
- **No line-ending noise.** Every touched and created file is LF, as `.gitattributes`
  (`* text=auto eol=lf`) requires. Nothing to recommend.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release`: **succeeded, 0 errors, 0 warnings**, with
  `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` on solution-wide.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **2868 passed, 0 failed, 0 skipped**
  — `Enigma.MarkdownEditor.Core.UnitTests` 2127 (2014 before this phase; the three that failed on the
  unchanged tests were the deliberate changes — `q` now a pending prefix, and `<` named `<lt>` in two
  sequence-display assertions — and were updated) and `Enigma.MarkdownEditor.Desktop.UnitTests` 741
  (734 before; 7 new in `AvaloniaEditVimBufferTests`).
- **Acceptance criteria, one by one.**
  - Every register rule holds for `y` `d` `c` `x` `p` `P` and the visual forms, with the prefix on
    either side of the count; `"_` leaves every register untouched; `"0` survives a delete ✓ —
    `VimRegisterTests` and the `ARegisterPrefix_RidesOnTheCommand` theory in `KeySequenceParserTests`.
  - `.` repeats every command kind listed, with and without a new count, as one undo step; it never
    repeats the excluded kinds ✓ — `VimRepeatTests` (`Dot_RepeatsTheLastChange`,
    `Dot_RepeatsAnOperatorOverAnObject`, `Dot_RepeatsAPut`, `Dot_RepeatsAnInsertWithTheTextTypedInIt`,
    `Dot_RepeatsAChangeWithItsText`, `Dot_RepeatsAVisualOperationOverTheSameExtent`,
    `Dot_DoesNotRepeatAMotionOrAYank`, the undo-step facts).
  - `3ix`, `2o`, `3A` type repeated text; `u` takes the whole thing back in one step ✓ —
    `ACountOnAnInsertCommand_RepeatsItsText`, `ACountedInsert_IsOneUndoStep`.
  - `qa…q` then `@a`, `3@a`, `@@` replay; the status reads `recording @a` while recording and clears
    when it stops; a recursive macro aborts with the message; a macro that hits a failing find stops
    there ✓ — `VimMacroTests`.
  - `VimKeyNotation` round-trips every key the harness knows ✓ — `VimKeyNotationTests`.
  - New Core test files as named; a headless Desktop test proves `AvaloniaEditVimBuffer.Changed`
    fires for a typed character and stops firing for the previous document after a swap ✓ —
    `AvaloniaEditVimBufferTests`.
  - README §*Editing* gains registers, `.` and macros; §*Not implemented* drops them ✓. Release build
    0 warnings; full suite green ✓ — as above.
