# FEATURE-724A-PHASE05 — Screen motions & viewport

**Status: DONE** · Phase 05 of 5 · Branch: `feature/feature-724a-phase05-screen-motions`
Plan: `docs/plan/FEATURE-724A.md`

## Summary

The engine can now see the screen. `IVimBuffer` grew its second and last seam of this item — a
viewport trio — and on it sit the three screen motions, the six scroll commands vim spells with
Control, the two page keys and the `z` family. This is the final phase: `FEATURE-724A` is `DONE`, and
the app is ready for the 1.4.0 release the plan defers to its own item.

**The seam.** `IVimBuffer` gained `int FirstVisibleLine`, `int VisibleLineCount` (never less than one)
and `void ScrollToLine(int lineNumber, VimScrollAlignment alignment)` with the new
`VimScrollAlignment { Top, Center, Bottom }`. `VisibleLineCount` is *the window's height in lines*
rather than the lines the buffer happens to fill: a half-page in a five-line file is half the window,
so `Ctrl+D` goes to the last line as it does in vim. A new internal `VimViewport` holds the arithmetic
both the motions and the ranges need — the last visible line, `H`/`M`/`L`'s targets (vim's formulas,
including `M` being the upper of two middle lines and `H`'s count reaching past the window while `L`'s
stays inside it), half a page, a page (the window less two), and where `Ctrl+D` stops scrolling.

**Motions and commands.** `VimMotion` gained `ScreenTop`, `ScreenMiddle`, `ScreenBottom`, linewise
behind an operator through `VimRanges.Resolve`; `VimAction` gained the twelve scroll commands
(`ScrollHalfPageDown/Up`, `ScrollPageDown/Up`, `ScrollLineDown/Up`, `ScrollCaretTop/Center/Bottom`
and the three `…ToFirstNonBlank` variants for `z<CR>` `z.` `z-`). `VimSpecialKey` gained `PageUp`
and `PageDown`, and `VimKeyNotation` writes and reads `<PageUp>` `<PageDown>`. None of the twelve is a
change (`.` never repeats a scroll); all twelve work over a selection; a macro records them as keys.

**Parser.** A key held with Control is read before every pending state: one of the six scroll commands
completes with the count in front of it, anything else — or a held key after `r`, `f`, `"`, `q`, `g`,
`Z`, `z` or an operator — is a dead sequence named in full (`r<C-d>`, `d<C-f>`). The engine lets
exactly those six through its "held keys belong to the window" gate, via the new public
`KeySequenceParser.IsControlCommand`, so one table decides both. `z` is a third prefix beside `g` and
`Z` (`_awaitingUpperZ` / `_awaitingLowerZ`), pending until its partner, dead behind an operator, and
covered by `HasPendingSequence` and `Reset()`. The page keys complete as the page scrolls. `H` `M` `L`
sit in `MotionFor`.

**Engine.** `Ctrl+D`/`Ctrl+U` scroll the view (stopping where the last line reaches the bottom) and
move the caret the same number of lines with the column kept, exactly as `j`/`k` keep it; on the last
or first line already, nothing happens. `Ctrl+F`/`Ctrl+B` scroll a page with the caret on the first
non-blank of the new top / bottom line; the view may end with the last line alone at the top, as vim's
does. `Ctrl+E`/`Ctrl+Y` move the view and leave the caret unless it would leave, when it is pulled onto
the nearest line still shown. The `z` family scrolls the caret's line — or the line a count names,
moving the caret to it — and the `<CR>` `.` `-` variants take the caret to the first non-blank.

**Adapter.** `AvaloniaEditVimBuffer` reads the view off `TextView.VisualLines` after
`EnsureVisualLines()` — a line cut off at either edge is not counted, and a document too short to fill
the view reports the rows the view has room for — and scrolls through `TextView.MakeVisible` with a
rectangle the height of the view at the wanted offset, then runs the layout so a read in the same
command sees the new lines. `VimEditorBehavior` translates `Key.PageUp`/`Key.PageDown`, drops them
from `EditorKeys`, and the existing Ctrl+letter translation carries the six scroll keys; in insert
mode the engine turns them all down, so `Ctrl+B` is still bold.

## Files/modules touched

**Created — `src/Enigma.MarkdownEditor.Core/Vim`**

- `VimScrollAlignment.cs` — `Top`, `Center`, `Bottom`.
- `VimViewport.cs` — `LastVisibleLine`, `TopLine`, `MiddleLine`, `BottomLine`, `HalfPage`, `Page`,
  `LastTopLineWithoutGap`.

**Modified — `src/Enigma.MarkdownEditor.Core/Vim`**

- `IVimBuffer.cs` — the viewport trio and a remarks paragraph on the view.
- `VimSpecialKey.cs` — `PageUp`, `PageDown`; remarks no longer name Page Down as `Other`.
- `VimKeyNotation.cs` — `<PageUp>` `<PageDown>` names.
- `VimMotion.cs` — `ScreenTop`, `ScreenMiddle`, `ScreenBottom`. `VimAction.cs` — the twelve scroll
  commands.
- `VimRanges.cs` — three linewise arms for the screen motions.
- `KeySequenceParser.cs` — held keys read first (`ControlActionFor`, `IsControlCommand`,
  `PrefixPending`); `_awaitingZ` → `_awaitingUpperZ` plus `_awaitingLowerZ`; `PageActionFor`,
  `ScrollCaretActionFor`; `H` `M` `L` in `MotionFor`; class remarks.
- `VimEngine.cs` — the held-key gate defers to `IsControlCommand`; twelve `Execute` arms; three
  `ApplyMotion` arms; `ScrollHalfPage`, `ScrollPage`, `ScrollLines`, `ScrollCaret`; `PlacesView` and
  the conditional trailing `ScrollToCaret`; class and `HandleKey` remarks.

**Modified — `src/Enigma.MarkdownEditor.Desktop/Views`**

- `AvaloniaEditVimBuffer.cs` — `FirstVisibleLine`, `VisibleLineCount`, `ScrollToLine`, the private
  `Viewport()` reading; class remarks.
- `VimEditorBehavior.cs` — `Key.PageUp`/`Key.PageDown` translated; `EditorKeys` loses them; remarks.

**Created — tests (`tests/Enigma.MarkdownEditor.Core.UnitTests/Vim`)**

- `VimScreenMotionTests.cs` — `H` `M` `L` (counts, the buffer's ends, a short buffer, even and odd
  screens, behind `d` `y` `c`, in visual mode), `Ctrl+D`/`Ctrl+U` (half page, count, the view
  stopping, the caret going on, column kept through a short line, no-op at the ends, short buffer),
  `Ctrl+F`/`Ctrl+B`/`PageDown`/`PageUp` (page, count pages, the ends, the alignment asked for),
  `Ctrl+E`/`Ctrl+Y` (count, both pulls, the ends, column remembered), the six `z` commands (alignment
  recorded, caret rule, counts, the buffer's ends, `Esc`, `zx`), dead sequences, visual mode, insert
  mode pass-through, `.`, macros, and which commands ask for the caret to be shown afterwards.

**Modified — tests**

- `FakeVimBuffer.cs` — `ViewportLines`, settable `FirstVisibleLine`, `VisibleLineCount`,
  `LastVisibleLine`, `Scrolls`, `ScrollToLine` (vim's alignment arithmetic).
- `VimScript.cs` — `Run(text, keys, viewportLines[, firstVisibleLine])`; `VimRun.FirstVisibleLine`,
  `LastVisibleLine`, `CaretLine`, `CaretColumn`.
- `KeySequenceParserTests.cs` — the helper feeds `VimKeyNotation`; `H` `M` `L` in the motion, visual
  and operator theories; `z` pending and dead; the six `z` commands; the scroll keys with counts and
  in visual mode; held keys after prefixes; `IsControlCommand`; `Reset` covers `z`; `'z'` swapped for
  `'Q'`/`'K'` in the spells-nothing theory and `"az` for `"azz` / `"a<C-d>`.
- `VimKeyNotationTests.cs` — `<PageUp>` `<PageDown>` rows and a round trip.
- `VimCommandLineTests.cs`, `VimModeTests.cs` — `z` (now a prefix) replaced by `Q` as the dead key.
  `VimUndoTests.cs` — the held-key fact renamed and extended to the six.
- `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Views/TabStrip.cs` — `ShowFileAsync(name, content)`,
  `Press(key, modifiers)`, `LastKeyHandled` (a tunnelling spy on the text area), `AssistsActive`.
- `VimEditorBehaviorTests.cs` — `Ctrl+D`, `Ctrl+B` (page up in normal mode, bold in insert mode),
  `PageDown`/`PageUp`, and `L` `zt` `H` over a real 1000-line document.
- `AvaloniaEditVimBufferTests.cs` — the viewport smoke test (never laid out; no document).

**Modified — prose**

- `README.md` — §*Motions* gains the screen table and a paragraph; §*Keyboard shortcuts* notes the
  six Control keys vim takes in normal and visual mode.
- `docs/RELEASE.md` — the *Vim smoke set* checklist item gains `Ctrl+D`/`Ctrl+U`, `Ctrl+F`/`PageDown`,
  `zt`/`zz`, `H`/`L`, `Ctrl+E` and `Ctrl+B` in insert mode (documentation freshness sweep).

**Workflow artifacts**

- `docs/roadmap.md` — PHASE05 `TODO` → `IN PROGRESS` → `DONE`; `FEATURE-724A` → `DONE`.
- `docs/plan/FEATURE-724A.md` — PHASE05's status and the item's.

## Deviations & follow-ups

- **A view-placing command is not followed by the usual "show the caret".** The plan did not say;
  the real editor did. AvaloniaEdit's `BringCaretToView` keeps a 30 px margin round the caret, so a
  `zt` followed by it pulled the view back a line and a half, and a held `Ctrl+E` with the caret on
  the top line would have scrolled a line down and a line back for ever. `Ctrl+F`/`Ctrl+B`,
  `Ctrl+E`/`Ctrl+Y` and the six `z` commands therefore skip the trailing `ScrollToCaret`; `Ctrl+D`
  and `Ctrl+U`, whose caret sits mid-screen, keep it as a safety net. Covered by
  `ACommandThatPlacesTheView_DoesNotAskForTheCaretToBeShownAfterwards` and its counterpart.
- **`Ctrl+B` and a pulled `Ctrl+Y` align the view on the caret's line at the bottom**, where the plan
  expresses every scroll as `ScrollToLine(top, Top)`. With the trailing show-the-caret gone, the
  engine has to be sure the caret's line is on screen from the alignment alone; aligning the line the
  caret lands on is exact under wrapping, while the top line computed from a count of lines is not.
  On the fake both spellings give the same first visible line; the recorded `Scrolls` say which was
  asked.
- **`TextEditor.ScrollToVerticalOffset` is a no-op in AvaloniaEdit 12.0** over this control (the
  ScrollViewer's offset stays where it was, before and after a layout pass), which is why the adapter
  scrolls through `TextView.MakeVisible` instead. *Follow-up:* `EditorPaneView.CentreOn` (search
  hits) and the per-document scroll restore in `ShowDocument` call the same method and are very
  likely no-ops today — worth checking in the running app and switching to the same route.
- **Tunnelling handlers on one control run in reverse order of registration**, so the markdown
  assists see a key before the vim behaviour does, not after as the comment in `EditorPaneView`'s
  constructor says. Harmless — the assists stand down in the command modes on their own — but the
  comment is wrong; a one-line follow-up. The `LastKeyHandled` spy lives on the text area for this
  reason.
- **`VisibleLineCount` is the window's height in lines, not the lines the buffer fills** (see
  *Summary*), and the adapter reports the rows the view has room for when the document is shorter than
  the view. When lines wrap the count is of document lines actually on screen, as the plan says.
- **`M` ignores its count**, as vim's does; the plan's table did not say.
- **A `z` command with a count moves the caret to the line named** — `3zt` keeps the column,
  `3z<CR>` takes the first non-blank — vim's rule; the plan's table covered the count-less forms only.
- **Beeps are silence.** `Ctrl+D` on the last line, `Ctrl+U` on the first, `Ctrl+F` with the last
  line at the top, `Ctrl+B`/`Ctrl+Y` with the first line at the top and `Ctrl+E` with the last line
  there do nothing, without a message — the engine's rule for a motion that cannot go anywhere.
- **`"az` is pending rather than dead**, exactly as `"ag` is: a prefix waits for its partner and the
  register rule is applied to the whole command (`"azz` dies, named in full). The parser test that
  used `"az` as a dead sequence was rewritten.
- **Known limitation carried forward:** under word wrap the number of lines a page or a half-page
  moves is the count of document lines on screen, so a page over a paragraph that wraps to many rows
  is shorter in rows than vim's would be. `Ctrl+D`/`Ctrl+U` keep the trailing show-the-caret for the
  case where that count leaves the caret just under the bottom edge.
- **No line-ending noise.** Every touched and created file is LF, as `.gitattributes` requires.
  Nothing to recommend.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release`: **succeeded, 0 errors, 0 warnings**, with
  `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` on solution-wide.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3290 passed, 0 failed, 0 skipped**
  — `Enigma.MarkdownEditor.Core.UnitTests` 2543 (2365 before this phase; the five that failed on the
  unchanged tests were the deliberate changes — `z` a prefix rather than a dead key, `Ctrl+D`/`Ctrl+U`
  taken rather than left to the window — and were updated) and
  `Enigma.MarkdownEditor.Desktop.UnitTests` 747 (741 before).
- **Acceptance criteria, one by one.**
  - Every row of the contract table holds against the fake viewport, including at the top and bottom
    of the buffer, with a buffer shorter than the viewport, and with counts ✓ — `VimScreenMotionTests`
    (`HMAndL_GoToTheTopMiddleAndBottomOfTheScreen`, `InABufferShorterThanTheWindow_TheScreenIsTheBuffer`,
    `CtrlDAndCtrlU_ScrollHalfAWindowAndMoveTheCaretWithIt`, `CtrlD_InABufferShorterThanTheWindow_GoesToTheLastLine`,
    `CtrlF_ScrollsAPageAndPutsTheCaretOnTheNewTopLine`, `CtrlB_ScrollsAPageBackAndPutsTheCaretOnTheNewBottomLine`,
    `CtrlF_InABufferShorterThanTheWindow_PutsTheLastLineAtTheTop`,
    `CtrlEAndCtrlY_ScrollTheViewAndLeaveTheCaretUnlessItWouldLeave`, `CtrlY_PullsACaretThatWouldLeaveOntoTheBottomLine`,
    `ZtZzAndZb_ScrollTheCaretsLineIntoPlaceAndLeaveTheCaret`, `ZEnterZDotAndZMinus_ScrollAndMoveTheCaretToTheFirstNonBlank`,
    `ACountOnZ_NamesTheLineAndMovesTheCaretToIt`, `ZAtEitherEndOfTheBuffer_GoesAsFarAsTheViewCan`).
  - `dL`, `yH`, `cM` are linewise ranges; `Ctrl+D` keeps the column and clamps at the last line ✓ —
    `AnOperatorOverAScreenMotion_TakesWholeLines`, `AYankOverAScreenMotion_FillsTheRegisterWithTheLines`,
    `AChangeOverAScreenMotion_EmptiesTheLinesAndEntersInsertMode`, `CtrlD_KeepsTheColumnThroughAShortLine_LikeJAndK`,
    `CtrlD_WithTheViewAlreadyAtTheEnd_MovesOnlyTheCaret`.
  - `Ctrl+D/U/F/B/E/Y` are handled in normal and visual mode and pass through in insert mode
    (`VimEditorBehavior` headless test); `Ctrl+B` in insert mode still bolds ✓ —
    `InAVisualMode_AScrollMovesTheCaretEndOfTheSelection`, `InInsertMode_TheScrollKeysAreTheEditors`
    (Core); `VimEditorBehaviorTests.CtrlD_IsAScrollInNormalModeAndTheEditorsInInsertMode`,
    `CtrlB_IsAPageUpInNormalModeAndStillBoldsInInsertMode`, `PageDown_IsAPageInNormalModeAndTheEditorsInInsertMode`
    (Desktop, over a real window with a 1000-line file).
  - `AvaloniaEditVimBuffer` viewport smoke test over a headless `TextEditor`: `FirstVisibleLine` is 1,
    `ScrollToLine` does not throw, and the `Changed` event from PHASE03 still fires ✓ —
    `AvaloniaEditVimBufferTests.TheViewport_IsReportedForAnEditorThatWasNeverLaidOut_AndAScrollDoesNotThrow`,
    `WithNoDocument_TheViewportIsOneLineAndAScrollIsSurvived`; and beyond the plan,
    `VimEditorBehaviorTests.TheScreenMotions_ReadTheRealViewAndZtScrollsIt` proves `L`, `zt` and `H`
    against the laid-out text view.
  - New Core test file `VimScreenMotionTests.cs` ✓. README §*Motions* gains a screen-motions table;
    §*Keyboard shortcuts* notes the six Control keys vim takes in normal mode ✓. Release build
    0 warnings; full suite green ✓ — as above.
