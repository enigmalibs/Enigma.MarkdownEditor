# FEATURE-11AA-PHASE04 — Search & ex commands

**Status: DONE** · Branch: `feature/feature-11aa-phase04-search-ex` · Plan: `docs/plan/FEATURE-11AA.md`

## Summary

The last phase of the vim contract, and the first one whose keys are not commands at all. `/` and `:`
open a **command line**: a stretch of typing that is collected a character at a time and only means
something when Enter is pressed. `/text` then `n` / `N` walk the matches; `:w`, `:q` and `:wq` reach
the same save and close the tab strip uses.

**The command line is state, not a mode.** That is the one design decision this phase turns on. While
it is open the engine is still in normal mode and the status bar still says `NORMAL` — which is what
vim looks like from the outside, and it keeps the mode vocabulary to the four names the contract
lists. `HandleKey` grows one branch, ahead of everything else including `Esc`, and while
`_commandLine` is non-null every key belongs to it. Nothing else in the engine changed shape.

**It is the engine's line, not a text box.** The strip in the status bar is a `TextBlock` bound to
what the engine has collected, re-sent on every keystroke through a new `IVimBuffer.ShowCommandLine`.
The alternative — a real `TextBox` that takes the focus and gives it back — buys cursor movement and
clipboard paste at the cost of the two things that matter more here: `:wq` would stop being something
a test can type, and the editor would lose the keyboard focus every time a colon was pressed. Typing,
Backspace and Esc is the whole of it; backspacing over the `:` closes the line, which is vim's own
answer and the only one that leaves no way to be stuck in an empty one.

**Ex commands leave by a different door from everything else.** `IVimBuffer` is the text the engine
edits, and saving a file is not that, so `:w` surfaces as `VimEngine.ExCommandRequested` — an event
beside `ModeChanged` rather than a method on the buffer. The plan sketched it as buffer members; this
is the same seam with the responsibility put where it belongs, and it means `FakeVimBuffer` did not
have to learn what a document is. `VimEditorBehavior` forwards it to
`DocumentManagerViewModel.ExecuteExCommandAsync`, which calls **the same two private methods the tab
strip's close button and the context menu's Save call** — so `:q` on a dirty document raises the same
Save/Discard/Cancel question, and Cancel there leaves the tab open. `:wq` is one enum value rather
than a `Write` followed by a `Quit`, because the save is asynchronous and two requests in a row would
be a close racing a write of the same file; it closes only if the save worked.

**Search is a plain substring and always wraps.** Ordinal, case-sensitive as typed, no regex — a
half-regex that honoured `.` but choked on `[` would be worse than either answer. The caret's own
match is skipped on the way out and found on the way round, which is why searching for the word you
are standing on says "search hit BOTTOM, continuing at TOP" and puts you back where you were rather
than reporting nothing. The **pattern** is kept, not the matches, so `n` after an edit searches the
text as it is now — and it outlives a document swap for the same reason the register does.

**Two keys had to be named.** Enter and Backspace were previously swallowed by `VimEditorBehavior`'s
`EditorKeys` set and never reached the engine. They are now translated like `Esc` and the arrows, and
the engine swallows them in every mode that has no command line open — so normal mode is unchanged
(neither key edits the document, and either cancels a half-typed sequence like any meaningless key),
and insert mode is unchanged too (the engine turns them down and AvaloniaEdit splits the line and rubs
a character out as it always did). Both halves are asserted, in the Core suite and on the real control.

**Nothing new is a command in a visual mode.** `:`, `/`, `n` and `N` all have visual meanings in vim —
a search there extends the selection — and all of them are on the v1 out list, so they join the nine
keys PHASE03 turns down by name with the selection left intact. They fall below the parser's existing
`if (visual) return Dead()` line, so this cost no code, only cases.

## Files/modules touched

### Created — `src/Enigma.MarkdownEditor.Core/Vim`

| File | Notes |
|---|---|
| `VimExCommand.cs` | The three ex commands as an enum. `WriteQuit` is one value and says why. |
| `ExCommandParser.cs` | `Parse(line)` → `VimExCommand?`. Exactly `w`, `q`, `wq`, whitespace-trimmed; every near miss (`q!`, `x`, `write`, `wqa`, `W`) is `null` on purpose, and the doc says why. Public, so the plan's "parser unit tests green" is a test against the parser rather than through the engine. |
| `VimSearch.cs` | `Find(buffer, pattern, caret, forward)` — the substring search with the wrap. Internal, like `VimWords` and `VimRanges`: it is a helper of the engine and is covered through it. Both directions are a span slice plus one `IndexOf`/`LastIndexOf`, so "the last match beginning at or before *n*" is a statement about the slice rather than a hand-rolled backwards scan. |
| `VimSearchHit.cs` | `(Offset, Wrapped)`. The wrap is carried back because the caller cannot work it out from an offset, and it is the one thing vim says out loud about a search that succeeded. |

### Modified — `src/Enigma.MarkdownEditor.Core/Vim`

| File | Change |
|---|---|
| `VimEngine.cs` | `_commandLine` (a `StringBuilder`, null when closed) and `_lastSearch`; the `ExCommandRequested` event; the `CommandLine` property; `HandleCommandLineKey`, `OpenCommandLine`, `CloseCommandLine`, `RunCommandLine`, `Search`; three cases in `Execute`; Enter and Backspace added to the swallow branch; `HasPendingSequence` counts an open line; `Reset` closes it. The three status messages are vim's own wording, down to the capitals. |
| `KeySequenceParser.cs` | `:` and `/` as `BeginCommandLine` carrying themselves as the argument, and `n` / `N` as the two search repeats. All four sit in `ActionFor`, below the visual gate, so they are refused in a visual mode without a line of their own. |
| `VimAction.cs` | `BeginCommandLine`, `SearchNext`, `SearchPrevious`. One action for `:` and `/` rather than two, because nothing between the key and Enter differs. |
| `VimSpecialKey.cs` | `Enter` and `Backspace`, both documented as named for the command line's sake and meaningless everywhere else. |
| `IVimBuffer.cs` | `ShowCommandLine(string?)`, beside `ShowStatus`. The interface doc now says which of its members are not about text, and that ex commands deliberately leave by the engine's event instead. |

### Modified — `src/Enigma.MarkdownEditor.Desktop`

| File | Change |
|---|---|
| `Views/VimEditorBehavior.cs` | A third constructor argument, `Action<VimExCommand>`, wired to the engine's event; `Key.Enter` and `Key.Back` translated instead of swallowed, and removed from `EditorKeys`; the buffer built with the extra command-line callback. |
| `Views/AvaloniaEditVimBuffer.cs` | `ShowCommandLine` and the `Action<string?>` it forwards to, mirroring `ShowStatus`. |
| `Views/EditorPaneView.axaml.cs` | Passes `command => _ = _manager?.ExecuteExCommandAsync(command)`, with the same "not awaited, and why" note the paste handler carries. |
| `Services/IVimSession.cs`, `Services/VimSession.cs` | `CommandLine` + `ReportCommandLine`, announced once like the mode and the message; switching vim off takes a half-typed line down with the mode. |
| `ViewModels/DocumentManagerViewModel.cs` | `VimCommandLineText`, `IsVimCommandLineVisible`, both notified in `OnVimChanged`; `ExecuteExCommandAsync` over the existing `SaveAsync`/`CloseAsync`. |
| `ViewModels/MainWindowViewModel.cs` | Mirrors both into the status bar, like the four slots beside them. |
| `Views/MainWindow.axaml` | The status message's cell became a `Panel` holding two mutually exclusive `TextBlock`s — the message, and the command line in monospace and the ordinary foreground, because it is the only thing in that bar being typed rather than reported. |

### Created / modified — tests (1337 total, was 1214)

| File | Notes |
|---|---|
| `Core.UnitTests/Vim/VimSearchTests.cs` (36, new) | Where `/` lands and what it says: substring not word, case-sensitive, a regex read as its own characters, the wrap in both directions, the caret's own match found on the way round, `n`/`N` with counts, a count that runs out leaving the caret where it started, an empty `/` repeating the last pattern, `n` after an edit and after a delete, no change to the document or the undo stack, the scroll request counted from *after* the line was typed, `$`'s desired column cleared by the jump, and the four refusals in a visual mode and behind an operator. |
| `Core.UnitTests/Vim/VimCommandLineTests.cs` (43, new) | Opening, typing, Backspace, backspacing the line away, Esc, Enter, the three commands, four unknown ones named in full, the empty line, and — the group that matters most — five sequences that would be commands in normal mode typed into the strip with the document asserted unchanged. Plus Enter and Backspace meaning nothing outside a line and still being the editor's inside insert mode, `Reset` closing the line, and Ctrl+S still belonging to the window while one is open. |
| `Core.UnitTests/Vim/ExCommandParserTests.cs` (22, new) | The three, the whitespace, and twelve near misses. |
| `Core.UnitTests/Vim/KeySequenceParserTests.cs` (93 → 103) | `n`/`N` as actions, counts on them, `:` and `/` carrying themselves as the argument, and the four new keys added to the visual-refusal theory. |
| `Core.UnitTests/Vim/FakeVimBuffer.cs` | `CommandLine` (what the engine last showed) and `Rub()` — Backspace as the editor performs it, so a case can prove insert mode leaves the key alone. |
| `Core.UnitTests/Vim/VimScript.cs` | `<CR>` is now the named Enter key rather than a typed `\n`, `<BS>` is new, `Type` handles both the way AvaloniaEdit does, and `VimRun` gained `ExCommands`, `Status` and `CommandLine`. |
| `Desktop.UnitTests/ViewModels/DocumentManagerViewModelTests.cs` (47 → 56) | `:w` saving and *not* closing; `:q` clean, `:q` dirty answered Cancel and answered Save; `:wq` saving then closing without asking; `:wq` over a save that cannot succeed (the file replaced by a folder of the same name) leaving the tab open, dirty, and the failure reported; an ex command with nothing open; and the two command-line slots. |
| `Desktop.UnitTests/Services/VimSessionTests.cs` (7 → 9) | The command line announced once and cleared, and taken down when vim is switched off. |
| `Desktop.UnitTests/ViewModels/MainWindowViewModelTests.cs` (16 → 17) | The strip reaching the shell's status bar and taking the slot while it is up. |

### Modified — documentation

| File | Change |
|---|---|
| `README.md` | The documentation freshness sweep. The status paragraph ended "Still to come: search and the ex commands — and the markdown preview"; it now describes `/text` with the wrap message, `n`/`N`, and the ex line saving and closing through the same unsaved-changes question, and leaves the preview as all that remains. |

## Deviations & follow-ups

1. **Ex commands are an engine event, not `IVimBuffer` members.** The plan's design bullet lists
   "`SaveRequested`/`CloseRequested` events for ex commands" among `IVimBuffer`'s members. They are
   instead `VimEngine.ExCommandRequested`, beside `ModeChanged`. Same seam, same test surface, but the
   buffer stays "the text the engine edits" and the fake buffer did not have to grow a concept of a
   document that can be saved. `ShowCommandLine` *did* go on the buffer, because a strip in the status
   bar is the same kind of thing as `ShowStatus`.
2. **Exactly three ex commands; `:q!` is refused by name.** `:q!` is the single most-typed ex command
   in vim and it answers "Not an editor command: q!" here. Deliberate: the contract names three, and
   a parser that read `:q!` as `:q` would raise a Save/Discard/Cancel dialog for a command whose whole
   meaning is "do not ask me". `:x`, `:wa`, `:qa`, `:write` and `:s` are refused the same way. Adding
   `!` variants later is one line each in `ExCommandParser` plus a "discard" route through
   `DocumentManagerViewModel`; it is not one line today, which is why it is not in.
3. **The command line is not a mode, so there is no `COMMAND` chip.** The status bar still reads
   `NORMAL` while `:wq` is being typed — vim's own behaviour, and it keeps the plan's four mode names
   exact. Say the word and it becomes a fifth `VimMode` in one enum value plus one ViewModel case.
4. **A count before `:` or `/` is parsed and ignored.** `3/foo` finds the third match in vim. Same
   treatment as PHASE03 gave `3v`, and for the same reason — it belongs to the machinery `.` repeat
   needs. Counts on `n` and `N` *are* honoured, which covers the useful half.
5. **No `?` for a backward search.** The contract says `/text` then `n`/`N`, and `N` is the backward
   half. `?` would be four lines (`VimSearch` already searches both ways) and is not in the contract.
6. **The command line has no cursor of its own.** Typing and Backspace only: the arrows are swallowed,
   there is no history to page through, and there is no `Ctrl+R` register insertion. A cursor that can
   be moved would need a caret drawn in the strip to be usable, which is a text box, which is the
   design this phase deliberately did not take (see the summary).
7. **Search is not a motion.** `d/foo` deletes up to the match in vim; here the sequence dies with
   "Not a command: d/". Search-as-motion needs `VimMotion` to carry a pattern, which is a change to the
   grammar rather than an addition to it, and it is not in the contract. Covered by a test.
8. **`Ctrl+Backspace` in normal mode still reaches AvaloniaEdit and deletes a word** — a pre-existing
   hole from PHASE01, not introduced here and not fixed here: every key held with Control or Alt is
   offered to the engine, turned down, and left to the window, which is right for `Ctrl+S` and wrong
   for the handful of Control combinations AvaloniaEdit itself edits with (`Ctrl+Backspace`,
   `Ctrl+Delete`). Out of this phase's scope; worth a small follow-up that lets the window's own
   shortcuts through but keeps the editing ones.
9. **`:w` on a document that has never been saved is not a case that exists.** Every document in this
   application arrives through `DocumentManagerViewModel.OpenAsync(filePath)`, so `ActiveDocument`
   always has a real path. When a "new, unsaved document" flow appears, `:w` will need to fall through
   to Save As, and this is the note that says so.
10. **Linux was not verified** — no Linux machine was available in this session. Nothing added here is
    platform-specific: the search is span arithmetic over a string, and the view layer touches only a
    `TextBlock`'s text and two key codes that Avalonia normalises above the backends.
11. **Line endings (recommendation only, no action taken).** Every file in this diff is LF and
    `.gitattributes` (`* text=auto eol=lf`) keeps it that way; there is no CRLF churn in the diff. A
    CRLF *document* is a different matter and is covered: the probe searches a real `\r\n` buffer and
    checks the offsets count both characters.
12. **Two stale rows in the settings window are still disabled** — carried over from the PHASE01–03
    completion docs and still outside this phase's scope: "Show every file" carries a `FEATURE-0DE1`
    pending tag and "Image folder" is disabled, though both features shipped.

## Build/test evidence

```
> dotnet build Enigma.MarkdownEditor.slnx                            (Debug)
Build succeeded.
    0 Warning(s)
    0 Error(s)

> dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental
Build succeeded.
    0 Warning(s)
    0 Error(s)
    AVLN[0-9]+ matches over the full normal-verbosity log → 0

> dotnet test --solution Enigma.MarkdownEditor.slnx
Test run summary: Passed!
  total: 1337   failed: 0   succeeded: 1337   skipped: 0
```

1337 tests = 1214 before, plus 123 added here: 36 in `VimSearchTests`, 43 in `VimCommandLineTests`,
22 in `ExCommandParserTests`, 10 net in `KeySequenceParserTests` (93 → 103), and 12 in the Desktop
suites. The Core vim suites now hold 603 of the solution's tests.

### How the acceptance criteria were verified

| Criterion | Result |
|---|---|
| `/` + `n`/`N` find and wrap with correct messages | **Met.** 36 cases in `VimSearchTests` cover the first match ahead, the wrap in both directions with vim's own two messages, the caret's own match found on the way round, "Pattern not found: …" for a pattern that is nowhere, "No previous search pattern" for a repeat that was never made, and counts including one that goes round on the way. Confirmed on the real control in the probe: `/baz`, a wrap that reports "search hit BOTTOM…", `n`, `N` and `2N` all moving `TextEditor.CaretOffset` to the right place. |
| Re-searching current term after edits | **Met.** Two cases. One searches for a pattern that is not there yet, types it in with `Abar<Esc>`, and finds it with `n`; the other finds `bar` at offset 4, deletes the word in front of it with `0dw`, and shows `n` finding it at 0 rather than walking back to the offset it used to have. The pattern is what is kept, and both cases fail if it were the match. |
| `:wq` on a dirty doc saves then closes | **Met.** `ExWq_OnAChangedDocument_SavesThenClosesWithoutAsking` writes the edited text to the real file, empties the tab list, and asserts the unsaved-changes question was **never** put. Its partner, `ExWq_WhenTheSaveFails_LeavesTheTabOpenAndTheWorkInIt`, replaces the file with a folder of the same name and asserts the tab is still open, still dirty, and the failure reported. |
| `:q` on a dirty doc triggers the Save/Discard/Cancel prompt | **Met.** `ExQ_OnAChangedDocument_AsksTheSameQuestionTheCloseButtonDoes` answers Cancel and asserts the tab is still open, the question was asked about `notes.md`, and the file on disk is untouched; the Save answer is a second case that writes and closes. Both run through the same `CloseAsync` the close button uses, which is the point. |
| Parser unit tests green | **Met.** `ExCommandParserTests` — 22 cases: the three commands, four whitespace forms, twelve near misses (`q!`, `w!`, `wq!`, `x`, `qa`, `wqa`, `write`, `quit`, `w notes.md`, `s/a/b/`, `W`, `Q`), the empty line, and the null argument. |

The Desktop half was verified by a throwaway probe outside the repository (a headless Avalonia session
over the **real** `TextEditor`, `AvaloniaEditVimBuffer` and `VimEditorBehavior`, driving real key and
text-input events through a real window with `HeadlessWindowExtensions.KeyTextInput`/`KeyPressQwerty`).
**48 checks, all green**: `:` opening the strip and the typing never reaching the document or moving
the caret; Backspace, backspacing the line away, and Esc; all three ex commands raising exactly one
request of the right kind and closing the strip; `:q!` named rather than run; `/` moving the real
caret, the wrap and its message, `n`/`N`/`2N`; a search over a real CRLF document landing on the right
offset; Enter and Backspace editing nothing in normal mode and still splitting the line and rubbing
characters out in insert mode; `Ctrl+S` bubbling to the window unhandled while a line is open; the
settings toggle taking the strip down and handing both keys back to the editor; and the mode still
reading `NORMAL`, block caret and all, with a line open.
