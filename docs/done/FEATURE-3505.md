# FEATURE-3505 — Vim o/O continues list items

**Status: DONE** · Single phase · Branch: `feature/feature-3505-vim-open-list` (cut from
`vibe/2026-09-10-title-colors-vim-release`)
Plan: `docs/plan/FEATURE-3505.md` · Roadmap: `docs/roadmap.md`

## Summary

`o` and `O` now open a line that continues the markdown item the caret is on — `- ` repeats, `1.`
counts on, `- [x]` becomes `- [ ]`, `> ` keeps its depth, and the indentation and spacing are the
ones the user wrote. Before this, the only way to get a second bullet with vim on was `A` then Enter.

**No new rules were written.** `ListContinuation` already held the whole continuation matrix, exactly
so that it would not have to be restated in a key handler — its own class comment says as much. This
item adds two three-line methods to it (`OpenBelow`, `OpenAbove`) and one property to
`ListItemPrefix` (`Opening`), and the engine calls them. Everything about *what* a continued item
looks like is still in one place, and the existing Enter matrix is untouched and green.

**`O` uses the item's own marker, and that is the one place `o` and `O` genuinely differ.** A line
opened above an item takes that item's place in the list and pushes it down, so counting on would
number the new item *after* the one it precedes — `3.` above `2.`, which the user then has to fix by
hand. `ListItemPrefix.Opening` is `Continuation` with `Marker` instead of `NextMarker`; the empty task
box stays, for the same reason `Continuation`'s does.

**An empty item continues nothing, and the current line is never touched.** Enter *clears* an empty
item, because the caret is on it and the user is ending the list. `o` and `O` in vim never edit the
line they are on, so the analogue is to open a plain line and leave the item exactly as it is.

**One `Replace`, one undo step — still.** The marker rides in the same write as the line break rather
than following it, so the engine's stated rule holds and a single `u` takes the marker back with
whatever was typed on it. `FakeVimBuffer` gained a `Replacements` counter so this is now asserted
rather than assumed, which it had never been for any command.

**The marker is the command's work, not the typing's**, and the ordering that makes that true is
unchanged: `_typed.Begin` still runs *after* the prefix is written. So `.` re-derives the marker from
whatever line the caret is on at that moment — the vim-correct answer, and it needed no extra code —
and a macro replays `o` rather than `o- `. A count is the one place that did need code: `RepeatTyped`
re-derives the prefix per copy, so `3ox` walks down a list writing `2.`, `3.`, `4.` exactly as three
separate `o`s would.

## Files/modules touched

**Created**

- `tests/…/Core.UnitTests/Vim/VimOpenLineTests.cs` — 42 cases: the marker matrix for `o` (11 kinds
  including a tab-spaced item and a quoted list item), the own-marker rule for `O` (6), a line that is
  not an item opening plain (7), an empty item (5), what is typed landing after the marker (3), a
  count over a numbered and over a bulleted list, `.`, one undo taking the marker with it, the single
  `Replace`, and a CRLF buffer keeping its own terminator.
- `docs/done/FEATURE-3505.md` — this document.

**Modified**

- `Core/Markdown/ListItemPrefix.cs` — the `Opening` property.
- `Core/Markdown/ListContinuation.cs` — `OpenBelow` and `OpenAbove`.
- `Core/Vim/VimEngine.cs` — one `using`, the prefix in `OpenLine`, and the prefix in both open-line
  arms of `RepeatTyped`.
- `Core/Vim/VimAction.cs` — a `<remarks>` on each of the two actions, so the enum does not still say
  the line is opened plain.
- `tests/…/Core.UnitTests/Markdown/ListContinuationTests.cs` — 25 cases over the two new methods.
- `tests/…/Core.UnitTests/Vim/FakeVimBuffer.cs` — the `Replacements` counter.
- `tests/…/Core.UnitTests/Vim/VimInsertTests.cs` — **one existing case changed**; see *Deviations*.
- `README.md` — the `o` `O` row of the vim key table and the writing-assists bullet, from the
  documentation sweep.

## Deviations & follow-ups

- **An existing test asserted the opposite, and it was changed on purpose.**
  `VimInsertTests.OAndCapitalO_OpenALineAndInsertOnIt` carried
  `[InlineData("- it|em", "oX", "- item\nX|")]` under a comment reading *"The line is opened plain. A
  markdown list is not carried down here: the assists that do that are switched off outside insert
  mode, which is what keeps `o` from continuing a bullet."* That is precisely the behaviour this item
  reverses, so the case and its comment were **replaced** — with a heading (`# a heading`), which
  keeps the plain-open path covered where it still applies, and a pointer to `VimOpenLineTests` for
  the item case. Called out because "a test changed" is exactly the shape of a weakened assertion,
  and this one is a behaviour change the plan asked for, not an assertion bent to go green.
- **The tests live in a file of their own, not in `VimInsertTests`.** The plan named
  `VimInsertTests.cs`; 42 cases about one pair of keys is a topic, and the repository already keeps
  one file per topic (`VimJoinTests`, `VimPutTests`, `VimShiftTests`, …). `VimInsertTests` keeps the
  plain-open cases it always had.
- **`FakeVimBuffer` gained a counter.** `Replacements` is a test-harness addition, not production
  code, and it pins an engine rule that was stated in `VimEngine`'s own class comment but never
  asserted anywhere: one command, one `Replace`. Available to every future vim case.
- **No `VimOptions` field and no `:set` name**, as the plan decided. `VimSet`'s own comment says an
  unknown option is *named as unknown* rather than accepted, and vim has no option that means this —
  inventing one would teach a vim user something false about vim.
- **The ordered marker is not renumbered downwards.** `O` above `2. bar` writes `2. ` and leaves the
  old `2. bar` below it, so the source briefly has two items numbered 2. CommonMark reads only the
  *first* number of a list and renders them 2, 3, so what the reader sees is correct; rewriting the
  rest of the list is out of scope and would be a change the user did not ask for. Worth its own item
  if it is ever wanted, for Enter as much as for `O`.
- **`o` on the last line of a buffer with no trailing newline** behaves as it always did — the line
  break is written first, then the marker — because nothing about that path changed.
- **The README's two edits are corrections, not additions.** The key table said `o` `O` "Open a line
  below / above and insert", which is now incomplete in a way a user meets immediately, and the
  writing-assists bullet credited `Enter` alone for carrying a marker down. Both are sections this
  diff made stale rather than merely quiet.
- **No line-ending noise.** `git diff --check` is empty; every file touched or created is LF
  throughout.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release -v normal`: **succeeded, 0 Warning(s), 0
  Error(s)**.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3566 passed, 0 failed, 0
  skipped** — 3499 before this item, plus the 67 added here.
- **One fix cycle used of the three available**: an `InlineData` case (`-\titem`) was written without
  its `|` caret marker, so the harness refused it (`ArgumentException: A case has to say where the
  caret is`). Fixed, and the build and the whole suite were green on the next cycle. Worth noting that
  the harness *refused* rather than guessed — a case with no caret cannot mean anything, and it says
  so.
- **Acceptance criteria, one by one.**
  - Every listed case passes and the Enter matrix is untouched ✓ — `ListContinuationTests` keeps all
    its `OnEnter` cases unchanged; the two new methods are covered beside them.
  - `o`/`O` remain a single undo step and a single `Replace` ✓ —
    `OneUndo_TakesTheMarkerBackWithWhatWasTypedOnIt`,
    `OpeningAContinuedLine_IsStillOneWriteToTheBuffer`.
  - `.` and a count both reproduce the continuation ✓ — `Dot_OpensAnotherContinuedItem`,
    `ACount_WritesTheMarkerOncePerLineAndCountsOn`, `ACountOverABullet_RepeatsTheBullet`.
  - No new `VimOptions` field and no new `:set` name ✓ — neither file was touched.
  - Release build 0 warnings, full suite green ✓ — above.
- **Not verified here, and worth a look before the release tag:** nobody has pressed `o` in a live
  window. The engine is driven end to end through `VimScript` — the same path the real editor takes,
  keys in and buffer out — but `docs/RELEASE.md` §2's vim smoke set is where a person confirms it, and
  the release item is where `o` and `O` over a list get added to it.
