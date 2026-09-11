# FEATURE-11AA-PHASE01 — Engine core, modes & motions

**Status: DONE** · Branch: `feature/feature-11aa-phase01-engine-core` · Plan: `docs/plan/FEATURE-11AA.md`

## Summary

The editor has modes. It starts in **normal mode** — `VimEnabled` defaults to on — where the keys are
commands and nothing typed reaches the document, and the status bar says so in a chip beside the file
name. `i I a A o O` switch to **insert mode**, where the editor is exactly the editor it was before:
every keystroke passes straight through, including the ones an input method composes a character out
of. `Esc` comes back, stepping the caret onto the character just typed the way vim does.

**The motions of the contract are in**: `h j k l` and the arrow keys, `0`, `$`, `w`, `b`, `e`, `gg`,
`G` — plus `x`. `w`, `b` and `e` use vim's three character classes, so `foo.bar` is three words and
`foo_bar` is one, and an empty line is a word of its own, which is what walks a markdown file
paragraph by paragraph. `j` and `k` remember the column they started in and find it again on the far
side of a short line; after `$` they walk down the ends of the lines instead. The caret obeys normal
mode's rule everywhere — it sits *on* a character and never past the last one — including after a
mouse click in the empty space beyond a line's end, which is the one way a caret can arrive somewhere
vim would not have put it.

**An insert session is one undo step.** `i`, a sentence and `Esc` come back off in a single `Ctrl+Z`,
and `o` groups the line it opened together with what was typed on it.

**The markdown assists now answer to the mode.** `IEditingAssistPolicy` — the seam FEATURE-3C44 built
and left waiting — is implemented by `VimEditingAssistPolicy`: with vim off it answers yes to
everything exactly as the placeholder it replaces did, and with vim on only insert mode is typing. So
`Enter` on a list line continues the bullet in insert mode and does nothing in normal mode, and `o`
opens a plain line rather than a second bullet — the two things the FEATURE-3C44 completion doc listed
as this phase's debt.

**The setting is live.** Switching vim off in the settings window detaches the engine's key handlers
from the editor that is already open, hides the mode chip, gives the assists back and returns the
caret to its ordinary shape; switching it on puts everything back, in normal mode. The old
`EditingAssistPolicy` placeholder is deleted rather than left dead.

The engine itself is in Core and knows nothing about Avalonia: keys arrive as `VimKey` values and
everything it does happens through `IVimBuffer`, which is why the whole command surface is covered by
162 tests that hold a string instead of a text control.

## Files/modules touched

### Created — `src/Enigma.MarkdownEditor.Core/Vim`

| File | Notes |
|---|---|
| `VimEngine.cs` | The state machine: mode, the caret rules, the motions, the six ways into insert mode, `x`, and the undo grouping. `HandleKey` returns whether it took the key — the whole contract with the view. Normal mode takes every bare key, even one it has no meaning for, because a key that fell through would be typed into a document nobody is typing into; keys held with Control or Alt are the exception and belong to the window. |
| `KeySequenceParser.cs` | What the keys spell, with no document in sight: a motion, an action, a half-typed `g`, or a sequence that can never become a command. Kept apart from the engine so the one interesting state — waiting for the second `g` — is reachable in one line. PHASE02 widens the grammar to `[count] [operator] [count] [motion]`, which is why the result is a record rather than an enum. |
| `IVimBuffer.cs` | The seam: line and offset queries, one character at a time for the word motions, replace, caret, begin/end undo group, scroll-into-view and a status sink. `BeginUndoGroup`/`EndUndoGroup` are a pair rather than a `using`, because the group an insert session opens stays open across every keystroke until `Esc`. |
| `VimLine.cs` | One line, handed out whole rather than queried a property at a time. Derives `LastCaretOffset` (normal mode's rule) and `FirstNonBlankOffset` (where `I`, `gg` and `G` land). |
| `VimWords.cs` | `w`, `b`, `e` over vim's three character classes, with an empty line as a stop for the first two. |
| `VimKey.cs`, `VimSpecialKey.cs`, `VimModifiers.cs` | A keystroke as a **character** plus a named-key enum. A character rather than a key code deliberately: which physical key produces `$` depends on the layout, and an engine matching key codes would be a vim that only works on a US keyboard. |
| `VimMode.cs`, `VimAction.cs`, `VimMotion.cs`, `VimCommand.cs`, `VimParseResult.cs`, `VimParseStatus.cs` | The vocabulary. Motions are named separately from actions because in vim a motion is a noun: PHASE02 puts the same list behind an operator. |

### Created — `src/Enigma.MarkdownEditor.Desktop`

| File | Notes |
|---|---|
| `Services/IVimSession.cs` / `VimSession.cs` | One piece of state with three readers — the pane that writes the mode, the status bar that shows it, the assist policy that reads it — and the setting behind it. Announces a change only when there is one: the status bar rebuilds a row on every notification and the mode changes several times a sentence. |
| `Services/VimEditingAssistPolicy.cs` | The answer to FEATURE-3C44's seam: the assists are on while vim is off, and while vim is on only in insert mode. |
| `Views/AvaloniaEditVimBuffer.cs` | `IVimBuffer` over the `TextEditor`. A mapping rather than a translation — AvaloniaEdit numbers lines from 1 and measures in offsets, which is vim's own arithmetic. Reads the document through the control on every call, because one control serves every tab; the open undo group remembers **which** stack it was started on, so a tab switched mid-session closes the group where it was opened. |
| `Views/VimEditorBehavior.cs` | The wiring. Commands come from **text input**, not from the key event, because that is the only place the actual character is known; the key event carries what types nothing — `Esc`, the arrows — and, in normal mode, swallows the nine keys AvaloniaEdit would otherwise act on itself (`Enter`, `Tab`, `Back`, `Delete`, `Home`, `End`, `PageUp`, `PageDown`, `Insert`). Insert mode touches neither event. Also attaches and detaches the handlers to follow the setting, and gives normal mode a block caret through AvaloniaEdit's overstrike mode. |

### Modified

| File | Change |
|---|---|
| `Desktop/Services/IEditingAssistPolicy.cs` | The remarks named the placeholder; they now name `VimEditingAssistPolicy`. |
| `Desktop/Services/EditingAssistPolicy.cs` | **Deleted.** It existed to be replaced by this phase, and with vim off the new policy gives the same answer. |
| `Desktop/ViewModels/DocumentManagerViewModel.cs` | Takes `IVimSession`; exposes it for the pane's own behaviour to attach to, plus the four slots the status bar mirrors (`IsVimEnabled`, `VimModeText`, `IsVimInsertMode`, `VimStatusText`). `OnSettingsChanged` tells the session first, so switching vim off reaches the key handlers whatever else changed. |
| `Desktop/ViewModels/MainWindowViewModel.cs` | `VimModeText` stops being a placeholder anyone can set and becomes the editor pane's, mirrored the way the caret and file slots already were; three new mirrored slots beside it. |
| `Desktop/Views/EditorPaneView.axaml.cs` | Builds `VimEditorBehavior` **before** `MarkdownAssists` — both tunnel from this control, so they see a keystroke in the order they were attached — refreshes it when the ViewModel arrives, when the pane loads and when `IsVimEnabled` changes, and restarts the engine when the control's document is swapped. |
| `Desktop/Views/MainWindow.axaml` | The status bar's first slot is a mode chip with two looks (quiet for normal, the InfoBar's success green for insert), hidden with its separator while vim is off, and a new slot for what the engine has to say. |
| `Desktop/Views/SettingsWindow.axaml` | The vim row is enabled and its "FEATURE-11AA" pending tag is gone; the group's description no longer promises vim as future work. |
| `Desktop/ViewModels/SettingsWindowViewModel.cs` | `VimEnabled` documents its consumer. |
| `Desktop/Extensions/ServiceCollectionExtensions.cs` | `IVimSession` registered singleton; `IEditingAssistPolicy` now resolves to `VimEditingAssistPolicy`. |
| `README.md` | The status blockquote said vim emulation was still to come. It now says what has landed and what has not. Documentation freshness sweep. |

### Created / modified — tests (878 total, was 698)

| File | Notes |
|---|---|
| `Core.UnitTests/Vim/VimScript.cs` | The notation the matrix is written in: text with a `\|` where the caret is, a string of keys (`<Esc>`, `<Up>`, `<C-s>`, …), and the same notation back. A key the engine passes through is **typed into the buffer, exactly as the editor would type it** — without that, insert mode would be untestable, because the engine never sees the text it lets past. |
| `Core.UnitTests/Vim/FakeVimBuffer.cs` | A string, a caret, and counters for the undo groups, the scroll requests and the status message. |
| `Core.UnitTests/Vim/VimMotionTests.cs` (72) | Every motion, at every edge: both ends of every line, empty lines, empty documents, single lines, the caret pulled back from past a line's end, the remembered column through a short line and held against the last line, `$` then `j`, the word classes, the empty-line stop, and the first-non-blank rule for `I`, `gg` and `G`. |
| `Core.UnitTests/Vim/VimInsertTests.cs` (39) | The six entries, what lands where when it is typed, Esc's step back, `x` including the line-end and empty-line cases, and the undo grouping — one session is one group, two are two, an open one is still open, and a motion opens none. |
| `Core.UnitTests/Vim/VimModeTests.cs` (20) | The two answers to the view: normal mode takes every bare key and leaves `Ctrl`/`Alt` alone, insert mode lets everything but `Esc` through. Plus the status message, the cancelled sequence, `ModeChanged` firing once per switch, and `Reset`. |
| `Core.UnitTests/Vim/KeySequenceParserTests.cs` (31) | The whole key→command table, the pending `g`, the dead `gx` named in full, and that nothing survives into the next sequence. |
| `Desktop.UnitTests/Services/VimSessionTests.cs` (7) | Starts from the setting, announces a change once and a non-change never, and puts the mode back when vim is switched off. |
| `Desktop.UnitTests/Services/VimEditingAssistPolicyTests.cs` (5) | The gate: on with vim off, off in normal mode, on again in insert, and following the mode rather than reading it once. |
| `Desktop.UnitTests/Fakes/TempSettings.cs` | Real settings over a throw-away folder, for the two services that need nothing else. |
| `Desktop.UnitTests/Fakes/EditorHarness.cs` | Holds a **real** `VimSession` over its own settings, for the same reason it holds the real paste service: there is nothing in it a test cannot see. |
| `Desktop.UnitTests/ViewModels/DocumentManagerViewModelTests.cs` (+3) | The four mirrored slots, the engine's message reaching them, and the settings toggle arriving through `OnSettingsChanged`. The ten-argument guard test grew a position. |
| `Desktop.UnitTests/ViewModels/MainWindowViewModelTests.cs` (+2 net) | The shell mirrors the pane's mode, message and enabled flag. The old test that *set* `VimModeText` is gone with the placeholder it was testing. |
| `Desktop.UnitTests/Extensions/ServiceCollectionExtensionsTests.cs` (+1) | `IVimSession` as a singleton. |

## Deviations & follow-ups

1. **Commands are read from text input rather than from key events — the one design decision worth
   knowing about.** The plan says "preview Key/TextInput handlers"; what it does not say is which one
   carries the commands. It has to be text input: on a Swiss keyboard `$` is Shift+4 on a different
   physical key than on a US one, and `Key.D4` means nothing without the layout. The key event is used
   only for what types no character (`Esc`, the arrows) and to take the nine editing keys away from
   AvaloniaEdit in normal mode. Verified in the probe: `Enter`, `Tab`, `Backspace` and `Delete` do
   nothing in normal mode, and `Ctrl+Z` still reaches AvaloniaEdit's own undo.
2. **A block caret was delivered, though the plan put it out of scope.** AvaloniaEdit already draws a
   full-character-wide caret in overstrike mode, so normal mode simply switches that on — nothing is
   overwritten, because normal mode types nothing. If it ever proves distracting it is one line to
   drop. The plan's fallback (a caret brush per mode) was **not** taken: `EditorAppearance` assigns
   `CaretBrush` on every theme switch, and two owners of one property is the kind of thing that works
   until someone changes the theme.
3. **`gg` and `G` go to the line's first non-blank character**, which is what vim does; the plan just
   named the keys. `I` uses the same rule.
4. **`Esc` steps the caret back one character**, which the plan also did not spell out. It is vim's
   behaviour and the reason `x` after a typed word deletes the character you are looking at.
5. **`o` and `O` open a plain line, with no indentation carried down.** That is vim's default
   (`autoindent` off), and it is also what the FEATURE-3C44 plan asked for when it said `o` must not
   continue a markdown list.
6. **`IVimBuffer` carries what PHASE01 and PHASE02 use, not the whole seam the plan describes.** The
   selection members (PHASE03) and the `SaveRequested` / `CloseRequested` events (PHASE04) are not
   there: an interface member nothing implements meaningfully is dead weight, and adding them costs
   one line each when the phase that needs them arrives.
7. **The status-message sink got a consumer now rather than in PHASE04.** The plan gives the parser a
   "status ping" for an invalid sequence, and a sink with nowhere to write is not a sink; so the
   status bar grew a slot beside the mode chip, which is the one PHASE04's search messages will use.
   Only sequences that could plausibly have been commands are named — a function key or a Page Down is
   swallowed silently.
8. **`VimSpecialKey` has no `Home`/`End`.** The contract is exhaustive for v1 and names only the
   arrows, so in normal mode those two keys are swallowed and do nothing. They are the obvious
   candidates for the first small addition, along with `Space` and `Backspace` as motions.
9. **`Home` and `End` are swallowed only when unmodified.** `Ctrl+End` reaches AvaloniaEdit and can
   leave the caret past the end of a line; the next vim command pulls it back, which is the same rule
   that covers a mouse click there.
10. **One text-input event can carry several characters** — an input method delivers a composed word in
    one go. If a character partway through switches to insert mode, the rest is inserted by the
    behaviour rather than dropped, because the editor will not see that event again. Reachable only
    through an IME or a paste-as-keystrokes tool, and covered in the probe rather than in a unit test:
    it lives in the view layer, which has no test project.
11. **`DocumentManagerViewModel` now takes ten constructor arguments.** The vim session is there because
    the editor control is built by the view and has no container to ask, so the pane's ViewModel is
    where it reaches the session from. Worth watching, not worth splitting yet.
12. **Two stale rows in the settings window are still disabled**, from earlier phases and outside this
    one's scope: "Show every file" carries a `FEATURE-0DE1` pending tag and "Image folder" is disabled,
    though both features shipped. One-line fixes whenever you want them.
13. **Linux was not verified** — no Linux machine was available in this session. Nothing here is
    platform-specific: the engine is plain string arithmetic, and the view uses only Avalonia's own key
    and text-input events. The `Meta` modifier is treated as `Control` so that a macOS-style layout
    would not swallow a command key, which is speculative and untested.
14. **Line endings (recommendation only, no action taken).** Every file in this diff is LF and
    `.gitattributes` (`* text=auto eol=lf`) keeps it that way; there is no CRLF noise here.

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
  total: 878   failed: 0   succeeded: 878   skipped: 0
```

878 tests = 698 before, plus 180 added here: 162 in `Core.UnitTests/Vim` (72 + 39 + 20 + 31), 12 in the
two new Desktop service suites, and 6 across the ViewModel and composition-root suites.

### How the behavioural criteria were verified

A throwaway probe outside the repository hosted the **real `EditorPaneView`** — bound to a real
`DocumentManagerViewModel` resolved from a container composed with the app's own `AddEnigmaServices` /
`AddShell` over a throw-away settings file — in a headless window (`Avalonia.Headless` +
`Avalonia.Skia`, `UseHeadlessDrawing = false`) with the FluentTheme, the AvaloniaEdit control theme and
the Enigma dictionary loaded. It opened real files, focused the text area, and drove **real key and
text-input events through the window** (`HeadlessWindowExtensions.KeyPress` / `KeyTextInput`), so every
result below went through the same tunnel/bubble route and the same AvaloniaEdit input handler as a
keypress in the running app. **41 checks, all green.**

| Criterion | Result |
|---|---|
| Full base-motion / insert / `x` matrix green in Core.UnitTests | **Met.** 162 cases: 72 motion, 39 insert and `x`, 20 mode, 31 parser. |
| Insert mode is 100 % pass-through (typing, IME-safe) | **Met.** The engine returns "not mine" for every key in insert mode except `Esc` — asserted directly, and end to end in the probe: `i` then `Hello, ` landed verbatim, `A!` appended, and `Enter` still continued a markdown list. **IME-safe by construction rather than by test:** insert mode installs no filter at all, so a composed character is delivered by the platform exactly as it is in an editor with no vim in it. No IME was available to drive in this session. |
| The toggle attaches/detaches live without breaking the editor | **Met.** In the probe, with a file open: switching `VimEnabled` off mid-session hid the chip, restored the ordinary caret, gave the assists back, and made typing type and `Enter` split a line again; switching it on came back in normal mode with the block caret, and typing was commands again. |
| Caret clamp rules at line ends, empty lines, document edges | **Met.** 72 motion cases cover both ends of a line, empty lines, a one-line document, an empty document, and a caret left past the last character; the probe confirmed `$`, `G`, `gg` and `x` against the real control. |
| Vim's own debt from FEATURE-3C44 (assists off in normal mode; `o` does not continue a list) | **Met.** In the probe: on `- item`, `Enter` in insert mode continued the bullet, the assists reported inactive in normal mode, and `o` opened a plain line — which then came off, with its text, in one undo. |
| Mode indicator in the status bar | **Met.** The chip follows the session in both ViewModels (unit tests), and the probe drove the mode through the real pane. Its colours were not eyeballed — a headless run has no eyes; the two style classes use documented Enigma keys. |
| Linux | **Not verified** — see *Deviations* 13. |
