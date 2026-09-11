# FEATURE-11AA-PHASE03 — Visual modes

**Status: DONE** · Branch: `feature/feature-11aa-phase03-visual` · Plan: `docs/plan/FEATURE-11AA.md`

## Summary

PHASE02 gave the editor a grammar — `[count] [operator] [count] [motion]` — where an operator waits
for a motion to tell it what to act on. This phase adds the other way round: **select first, then act**.
`v` and `V` put the engine in a mode where the motions move one end of a selection, and `y`, `d` and
`c` work on what is highlighted.

**One extra piece of state, and no new machinery.** A visual mode is an *anchor* — the end of the
selection that does not move — and nothing else. The motions are PHASE01's, unchanged; the operators
are PHASE02's, unchanged; what is new is that the range they act on comes from
`VimRanges.Selection(anchor, caret)` instead of from a motion typed after the operator. That is why
this phase adds ~250 Core lines (a good half of them comments) against 119 tests: the hard parts were
already built and the seam was already there.

**The selection is AvaloniaEdit's own.** `IVimBuffer` grew `SetSelection`/`ClearSelection` and the
adapter assigns `TextArea.Selection` — the same `SimpleSelection` a mouse drag produces, in the theme's
own colours, verified in the probe (`editor.TextArea.Selection is SimpleSelection`, and
`SelectionStart`/`SelectionLength`/`SelectedText` all agreeing with it). Nothing custom is drawn. It is
assigned rather than set through `TextEditor.Select`, and that is the one real trap in this phase:
`Select` drags the caret to the end of the range, and in a visual mode **the caret is the moving end** —
for a selection made leftwards it belongs at the *start*. Also proved in the probe.

**The grammar is genuinely different in a visual mode, so the parser is told which mode it is reading
for.** With a selection on screen an operator is a *complete* command: `d` runs immediately, and `dd`
cannot be typed at all because the first `d` has already gone. What is left is counts, motions, the
three operators and `v`/`V`; every other key of the base contract — `x`, `p`, `r`, `u`, `~`, the insert
keys, the three capitals — has a visual meaning in real vim and **every one of those meanings is on the
v1 out list**, so they are turned down by name ("Not a command: x") with the selection left intact. One
answer from one place, rather than a second gate in the engine.

**Vim's caret rules are in.** All three operators leave the caret at the start of what was selected,
whichever end the motions had taken it to — charwise at the exact offset, linewise at the first
non-blank of the first line, which is where every other linewise command in this engine leaves it.
A charwise selection is **inclusive** of the character under the caret (that is what makes `vd` on one
character delete it), and on an empty line the one thing it can cover is the line break, so `vd` there
joins the two lines — vim's own answer. A linewise selection *highlights* the line break but does not
*yank* it: `V` shows `abc\n` selected and `Vy` puts `abc` in the register, keeping the register
convention PHASE02 established (linewise text is stored without a trailing terminator).

**Everything that types nothing now behaves the same way.** `VimEditorBehavior` used to ask "is the
mode Normal"; it now asks "is the mode *not* Insert", which is the only question it ever really had.
The visual modes therefore swallow text input, take Enter and Tab away from AvaloniaEdit, and get the
block caret — while `Ctrl+S` and the rest of the window's shortcuts still fall through, unhandled.
The markdown assists needed no change at all: `VimEditingAssistPolicy` already answered "only insert
mode is typing", which is now true of four modes instead of two.

## Files/modules touched

### Created — `src/Enigma.MarkdownEditor.Core/Vim`

Nothing. Every type this phase needed already existed; six of them grew.

### Modified — `src/Enigma.MarkdownEditor.Core/Vim`

| File | Change |
|---|---|
| `VimMode.cs` | `Visual` and `VisualLine`. The doc now says what the four modes really divide into: in `Insert` a key is text, in every other mode it is a command. |
| `VimAction.cs` | `VisualCharacterwise` / `VisualLinewise` (one action each for `v` and `V` — enter, switch, or leave, which the engine decides from the mode it is already in), and `Operate` documented as covering "operator over the selection", which is what an operator with `VimMotion.None` means. |
| `KeySequenceParser.cs` | `Feed(key, mode)`. In a visual mode an operator completes immediately, `v`/`V` are commands in every mode, and **nothing else is a command** — so `x` in visual mode is invalid rather than "delete a character". `r` in particular does not arm itself there, which is what keeps the key after it out of the document. |
| `VimRanges.cs` | `Selection(buffer, anchor, caret, linewise)` — the visual selection as a range, alongside `Resolve`'s motion ranges. Charwise is inclusive of the character under the caret; linewise is `Lines(anchorLine, caretLine)`, which already existed. Which end is the anchor stops mattering here, which is why reversed selections need no code of their own. |
| `VimEngine.cs` | `_visualAnchor`, `EnterVisual` (enter / switch keeping the anchor / toggle off), `LeaveVisual`, `ShowSelection`, `VisualRange`, `ApplyVisualOperator`, the selection refresh at the end of every command, `Esc` leaving a visual mode, `Reset` clearing the highlight, and `Ctrl+R` named rather than acted on while there is a selection. The three operator helpers (`Yank`, `Delete`, `Change`) are called unchanged — the caret is positioned first so their existing rules produce the vim answer. |
| `IVimBuffer.cs` | `SetSelection(start, end)` and `ClearSelection()`. Two offsets and no kind, because a linewise selection is a charwise one that happens to land on line boundaries. The contract says **must not move the caret**, and says why. |

### Modified — `src/Enigma.MarkdownEditor.Desktop`

| File | Change |
|---|---|
| `Views/AvaloniaEditVimBuffer.cs` | `SetSelection` assigns `TextArea.Selection = Selection.Create(...)` (clamped to the document), `ClearSelection` calls `TextArea.ClearSelection()`. |
| `Views/VimEditorBehavior.cs` | Every `Mode == Normal` test became `Mode != Insert`: text input swallowed, `EditorKeys` taken from AvaloniaEdit, and overstrike (the cheap block caret) on. The mid-event switch test became `Mode == Insert`, so a composed word whose first character starts a *visual* mode keeps being read as commands rather than typed. |
| `ViewModels/DocumentManagerViewModel.cs` | `VimModeText` is now a switch over the four modes — `NORMAL` / `INSERT` / `VISUAL` / `V-LINE`, six characters each so the chip does not change width — plus `IsVimVisualMode` for the accent. |
| `ViewModels/MainWindowViewModel.cs` | Mirrors `IsVimVisualMode` into the status bar, like the three slots beside it. |
| `Views/MainWindow.axaml` | A third look for the mode chip: the InfoBar's amber (warning) pair for either visual mode, beside the existing quiet normal and green insert. Not a warning — the two accents the palette already carries, and the pair a status chip can be told apart by at a glance. |

### Created / modified — tests (1214 total, was 1095)

| File | Notes |
|---|---|
| `Core.UnitTests/Vim/VimVisualTests.cs` (90, new) | What `v` and `V` select, how each motion and count changes it, all three operators over both kinds, and every way out. Selections are asserted as pictures — `a[b|c]d` — so **which end the caret is at** is visible in the case: reversed selections (`vh`, `v0`, `vk`, `Vk`) appear throughout, as do the empty line (`vd` joins the lines), the end of the buffer (`v` covers nothing; `c` still enters insert), the single-line buffer, indented lines, the mode toggles and switches, undo grouping, and the nine refused keys. |
| `Core.UnitTests/Vim/KeySequenceParserTests.cs` (70 → 93) | The visual grammar on its own: operators complete with no motion and nothing pending, counts still reach them, motions and `gg` unchanged, `v`/`V` identical in all three modes, and eleven normal-mode commands invalid while there is a selection — including `r` not arming itself. |
| `Core.UnitTests/Vim/FakeVimBuffer.cs` | `Selection` (the offsets the engine asked for) and `SelectedText`. |
| `Core.UnitTests/Vim/VimScript.cs` | `RenderSelection` — the `[`/`|`/`]` notation, in one pass over the offsets so that markers landing on the same position come out in a fixed order — plus `VimRun.Highlight` and `VimRun.SelectedText`. |
| `Desktop.UnitTests/ViewModels/DocumentManagerViewModelTests.cs` (+4) | The mode slot over all four modes, with the two accent flags. |
| `Desktop.UnitTests/Services/VimEditingAssistPolicyTests.cs` (+2) | The assists are off in both visual modes, as they are in normal mode. |

### Modified — documentation

| File | Change |
|---|---|
| `README.md` | The documentation freshness sweep: the status paragraph listed visual modes under "Still to come". It now names all four modes, `v`/`V` selecting by character or by whole line with the motions moving one end and `y d c` acting on the highlight, and leaves search and the ex commands as what remains of vim. |

## Deviations & follow-ups

1. **Only `y d c` act on a selection; nine other keys are refused by name.** The contract's visual
   bullet lists exactly those three. `x` (delete the selection), `~`, `p` (replace it), `o` (swap the
   ends), `i`/`a` (text objects) and `u` all have visual meanings in vim and all of them are on the v1
   out list, so each is answered with "Not a command: …" and the selection stays. Cheap to add later,
   one line each in the parser; deliberately not added now.
2. **`u` and `Ctrl+R` are refused in a visual mode** rather than undoing and leaving it, which is what
   vim does. Same reason as above, and it keeps "what can act on a selection" to one short list.
3. **A count on `v`/`V` is parsed and ignored.** `3v` in vim re-selects three times the size of the
   last selection, which is part of the machinery `.` repeat needs — on the out list. Covered by a test
   so the behaviour is stated rather than accidental.
4. **A linewise operation leaves the caret on the first non-blank of the first selected line, not in the
   column it was in.** Vim keeps the column. This is the same one-column deviation PHASE02 recorded for
   `yk`/`ygg` (its item 7), applied consistently: `dd`, `Vd`, `Vy` and `vy` all leave the caret in the
   same place, which beat matching vim in one case and not the others.
5. **`Esc` in normal mode now also drops a selection made with the mouse.** It has to clear the
   engine's own highlight on the way out of a visual mode, and a highlight nothing is acting on any
   more is exactly the kind of state `Esc` is for. Covered by a test.
6. **A mouse click during a visual mode is not intercepted.** AvaloniaEdit moves the caret and clears
   its selection; the engine stays in the visual mode with its old anchor, so the next motion re-selects
   from the anchor to the new caret — which is what vim with the mouse enabled does, so it is left
   alone rather than "fixed".
7. **The two visual modes share one accent colour.** The plan asks for "distinct accent colors" per
   mode; `VISUAL` and `V-LINE` are told apart by the word, and a fourth colour for a difference the
   text already carries would be one more thing to learn. Say so and it changes in one XAML block.
8. **A linewise selection's highlight includes the line break, while the range an operator takes does
   not.** Deliberate, and the pair is asserted together in one test: the highlight is what makes `V`
   read as whole lines, and the register convention (no trailing terminator) is PHASE02's.
9. **Linux was not verified** — no Linux machine was available in this session. Nothing added here is
   platform-specific: the engine is offset arithmetic, and the view layer touches only
   `TextArea.Selection` and `OverstrikeMode`, both of which Avalonia implements above the backends.
10. **Line endings (recommendation only, no action taken).** Every file in this diff is LF and
    `.gitattributes` (`* text=auto eol=lf`) keeps it that way; there is no CRLF noise in the diff. A
    CRLF *document* is a different matter and is covered: the probe drives `V`, `d` and a linewise
    change over a real CRLF buffer, since the Core fake can only hold LF.
11. **Two stale rows in the settings window are still disabled**, carried over from the PHASE01 and
    PHASE02 completion docs and still outside this phase's scope: "Show every file" carries a
    `FEATURE-0DE1` pending tag and "Image folder" is disabled, though both features shipped.

## Build/test evidence

```
> dotnet build Enigma.MarkdownEditor.slnx --no-incremental          (Debug)
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
  total: 1214   failed: 0   succeeded: 1214   skipped: 0
```

1214 tests = 1095 before, plus 119 added here: 90 in the new `VimVisualTests`, 23 net in
`KeySequenceParserTests` (70 → 93), and 6 in the Desktop suites. The Vim suites now hold 492 of the
solution's tests.

### How the acceptance criteria were verified

| Criterion | Result |
|---|---|
| Selection visuals match AvaloniaEdit's native selection | **Met.** The adapter assigns `TextArea.Selection`; the probe asserts the live object *is* a `SimpleSelection` — the type a mouse drag produces — and that `SelectionStart`, `SelectionLength` and `SelectedText` all agree with it. Nothing custom is drawn, and the caret is not dragged to either end (the reason `TextEditor.Select` is not used, also asserted). |
| Operators leave the caret per vim rules (start of former selection) | **Met.** 31 table cases and two facts assert where the caret ends up after `y`, `d` and `c` — including selections made leftwards or upwards, where the start is where the caret already was, and ones made rightwards, where it has to come back from the far end. Linewise lands on the first non-blank of the first line (deviation 4). Confirmed on the real control in the probe for `vjd` and `Vy`. |
| Linewise `V` operations are linewise in the register | **Met.** The register-kind theory covers `Vy`, `Vjy`, `Vky`, `Vd` and `Vc` as `Linewise` and their charwise counterparts as `Characterwise`, including an empty line yanked as an empty *line*. Proved end to end in the probe: `Vy` then `p` on the real document puts the text back as a whole line. |
| Tests cover char/line selections incl. reversed (anchor after caret) | **Met.** Reversed selections run through every group — `vh`, `v0`, `vk`, `Vk`, `Vky`, `vhy`, `vhd`, `vhc` — and the `[ | ]` notation makes the caret's end visible in each case rather than implied. |

The Desktop half was verified by a throwaway probe outside the repository (a headless Avalonia session
over the **real** `TextEditor`, `AvaloniaEditVimBuffer` and `VimEditorBehavior`, driving real key and
text-input events through a real window with `HeadlessWindowExtensions.KeyTextInput`/`KeyPress`).
**51 checks, all green**: the adapter's offsets, clamping and caret-preservation; `v`/`l`/`h` past the
anchor and `V` switching while keeping it; the highlight being the control's own selection; `V` covering
the line break, and both characters of a CRLF one; `vjd`, `Vy` + `p`, and a visual change plus its
typing coming back in one `u` with `CanUndo` false; `Esc` clearing the highlight; overstrike on in
normal and visual and off in insert; `Esc` dropping a selection made another way; `Ctrl+S` still
reaching the window while `Ctrl+R` is named; and the settings toggle leaving no highlight behind and the
editor an ordinary editor again.
