# FEATURE-11AA — Vim emulation

**Status: DONE** · Multi-phase (4) · Roadmap: `docs/roadmap.md`
Branches at build time: `feature/feature-11aa-phase01-engine-core`, `feature/feature-11aa-phase02-operators`, `feature/feature-11aa-phase03-visual`, `feature/feature-11aa-phase04-search-ex`.

## Objective

Modal vim-style editing on top of AvaloniaEdit — the agreed command surface, toggleable in settings (default ON), with the current mode shown in the status bar. Not a full vim: the contract below is exhaustive for v1.

## Command surface (the v1 contract)

**Base (PHASE01–02):**
- Modes: Normal ⇄ Insert; `Esc` always returns to Normal and cancels any pending sequence.
- Motions: `h j k l` (+ arrow keys), `0`, `$`, `w`, `b`, `e`, `gg`, `G`.
- Enter insert: `i I a A o O`.
- Edit: `x`, `dd`, `yy`, `p`, `P`; undo/redo `u` / `Ctrl+R`.
- Ex commands: `:w`, `:q`, `:wq`.

**Tiers (all in v1 scope, per interview):**
- **Counts** on motions and operators: `3dd`, `2w`, `5j`, `4x`, `2p`, …
- **Change family**: `cw`, `ce`, `cc`, `C`, `D`, `Y`, `r<char>`, `~` (vim quirk implemented: `cw` on a word behaves like `ce`).
- **Search**: `/text` + Enter, then `n` / `N` — plain substring, case-sensitive as typed, wrap-around with status message ("search hit BOTTOM…", "Pattern not found").
- **Visual modes**: `v` (charwise) and `V` (linewise); motions extend the selection; `y d c` operate on it; `Esc`/`v`/`V` exit or switch.

**Explicitly out of v1** (most-wanted follow-ups first): `.` repeat, marks, named registers, macros, regex search, `:s` substitute, jump lists, folds. *(Text objects were on this list and are no longer out of scope: `FEATURE-23AB` added them, along with the character-find family — `f` `F` `t` `T` `;` `,` — that this list never mentioned.)*

## Design

- **Core `Vim/`** (zero Avalonia): 
  - `VimEngine` — pure state machine. Input: `VimKey` (char / special key + modifiers). Output: operations on an `IVimBuffer`. Holds mode, pending count/operator state, the unnamed register.
  - `KeySequenceParser` — grammar `[count] [operator] [count] [motion|target]`; invalid sequences reset with a status ping; `Esc` cancels pending state.
  - `Register` — unnamed only; stores text + kind (**charwise** vs **linewise**), which drives `p`/`P` placement semantics (`yy`/`dd` → linewise, `x`/`cw` → charwise).
  - `IVimBuffer` — the testable seam: line/offset queries, get/insert/delete text ranges, caret get/set, selection set/clear (char/line), begin/end undo group, scroll-into-view, status message sink, and `SaveRequested`/`CloseRequested` events for ex commands.
  - Caret rules: Normal mode clamps the caret to the last character of a line (not EOL); Insert allows EOL; vertical motions keep a "desired column".
- **Testing strategy** (the largest test surface in the app): `FakeVimBuffer` in Core.UnitTests (string content + caret/selection markers); table-driven `[Theory]` cases of the shape *given text+caret → keys → expected text+caret+mode+register*. Every command in the contract gets cases, including edge cases (empty document, single line, last line, count overshoot clamps, `p` on empty buffer).
- **Desktop adapter**: `VimEditorBehavior` attached to the AvaloniaEdit `TextArea` — preview Key/TextInput handlers; Normal/Visual swallow text input and feed the engine; Insert passes everything through except `Esc`. `AvaloniaEditVimBuffer : IVimBuffer` maps to `TextDocument`/`Caret`/`Selection`/`UndoStack`.
- **Undo**: `u`/`Ctrl+R` → AvaloniaEdit `UndoStack`; one insert session (`i…Esc`) and each change-family operation are grouped as single undo units via undo-group calls on the buffer seam.
- **Mode indicator**: status-bar segment (`NORMAL` / `INSERT` / `VISUAL` / `V-LINE`) with distinct accent colors; caret visual best-effort (brush/width tweak per mode — a true block caret is out of scope for v1).
- **Ex/search line**: a command strip in the status-bar area appears on `:` or `/`; Enter executes, Esc cancels. `:w` → save active doc; `:q` → close active tab through the normal unsaved-close flow; `:wq` chains both; unknown command → status message.
- **Toggle**: `AppSettings.VimEnabled` attaches/detaches the behavior at runtime; indicator hidden when off.

## Phases

### PHASE01 — Engine core, modes & motions — **Status: DONE**

Steps: engine skeleton + `IVimBuffer` + `FakeVimBuffer` → Normal/Insert + `Esc` → base motions + desired-column + clamps → `i I a A o O`, `x` → Desktop behavior + buffer adapter + mode indicator → settings toggle.

Acceptance: full base-motion/insert/x matrix green in Core.UnitTests; insert mode is 100 % pass-through (typing, IME-safe); toggle attaches/detaches live without breaking the editor; caret clamp rules verified at line ends, empty lines, document edges.

### PHASE02 — Operators, counts & registers — **Status: DONE**

Steps: unnamed register + linewise/charwise semantics → `dd yy p P` → counts across motions/operators → change family `cw ce cc C D Y r ~` (incl. the `cw`≈`ce` quirk) → undo grouping (insert session = one undo; each operator = one undo).

Acceptance: table-driven tests cover every operator×count×edge case (last line `dd`, count overshoot, `p`/`P` line vs char placement, `r` at EOL, `~` advances caret); `u` after `cc` restores in exactly one step.

### PHASE03 — Visual modes — **Status: DONE**

Steps: `v`/`V` state + selection mapping to AvaloniaEdit selection → motions extend → `y d c` on selection with correct register kind → mode switching/exit rules.

Acceptance: selection visuals match AvaloniaEdit's native selection; operators leave caret per vim rules (start of former selection); linewise `V` operations are linewise in the register; tests cover char/line selections incl. reversed (anchor after caret).

### PHASE04 — Search & ex commands — **Status: DONE**

Steps: command strip UI (`:` and `/`) → search service (substring, wrap, status messages) + `n`/`N` → ex parser (`:w :q :wq`, unknown → message) wired to DocumentManager save/close (respecting dirty prompts).

Acceptance: `/`+`n`/`N` find and wrap with correct messages, re-searching current term after edits; `:wq` on a dirty doc saves then closes; `:q` on a dirty doc triggers the Save/Discard/Cancel prompt; parser unit tests green.

## Dependencies

FEATURE-3C44 PHASE01 (editor + tabs + save/close flows) · status bar from FEATURE-0F9A PHASE02.

## Out of scope

Everything in the "explicitly out" list · block-caret pixel fidelity · vim bindings outside the editor control (tree/panels keep standard navigation).
