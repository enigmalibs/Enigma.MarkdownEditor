# FEATURE-1C17 — Enigma.Avalonia.Desktop 1.2.0

**Status: DONE** · single phase · Branch: `feature/feature-1c17-enigma-avalonia-1-2-0`
Plan: `docs/plan/FEATURE-1C17.md`

## Summary

`Enigma.Avalonia.Desktop` moves from 1.0.0 to 1.2.0, and the one thing in the window the move made
harder to read is repainted.

- **What the app gets from the bump.** The InfoBar's four severity fills are lifted to pastel tints
  in the Dark variant, so a warning or an error now stands out from the panels around it rather than
  sitting level with `EnigmaSurface`, and its message line is painted in the primary tone on them
  (`EnigmaInfoBarMessageForeground`). An info bar *can* now close itself after a period
  (`DisplayDuration`, 1.1.0), but nothing here asks it to — see *Decisions*. Light is unchanged.
- **Only the pin moves.** 1.2.0 asks for Avalonia ≥ 12.1.1 and the solution pins 12.1.2, so the rest
  of the coupled group stays where it is; `project.assets.json` resolves `Enigma.Avalonia.Desktop/1.2.0`
  and `Avalonia/12.1.2`.
- **The vim chip's text is now the primary foreground.** The chip painted green text on the success
  fill (insert) and amber text on the warning fill (visual). On 1.2.0's lighter Dark fills those fall
  to 3.1:1 and 4.28:1 — under AA for 10 px text. The fill and border still carry the mode's colour,
  more visibly than before; the text uses the one pairing the library documents as AA on every fill.
  The two styles that set the text were folded into one selector, since they now set the same thing.
- **Every other surface painted on a severity fill already used the primary foreground** (the
  external-change bar, the missing-roots notice, the workspace remove confirmation, the search-hit
  highlight); their warning icons are graphics, which need 3:1 and keep it (4.28:1 on the warning fill).

## Files/modules touched

**Modified**

- `Directory.Packages.props` — `Enigma.Avalonia.Desktop` 1.0.0 → 1.2.0.
- `src/Enigma.MarkdownEditor.Desktop/Views/MainWindow.axaml` — the vim chip's insert and visual text
  styles merged into one, painting `EnigmaForegroundBrush`; the chip comment says why.

**Created**

- `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Views/VimModeChipPaletteTests.cs` — four headless
  tests: insert and visual text is the primary foreground in both variants; normal mode is still the
  secondary tone; insert and visual are still told apart by the success and warning fills; and the
  primary foreground keeps ≥ 4.5:1 on both fills in both variants (WCAG relative luminance, computed in
  the test).

## Decisions taken at build time

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Test the chip through the palette alone, or through the real style | The real style too: a `MainWindow` built with no ViewModel, its chip found by class, classes set by the test | The constructor is parameterless and the chip's look depends only on its classes, so the style the user sees can be asserted directly — stronger than the plan's palette-only test | Palette-only assertions (the plan's fallback, written when no window harness seemed possible) |
| Two styles setting the same foreground | One style with a two-selector `Selector` | Same value, same reason; one place to change it | Keeping two identical setters |

## Deviations & follow-ups

- **The test does more than the plan said.** The plan expected a palette-only test because no
  `MainWindow` view test exists; building the window without a ViewModel turned out to be enough to
  read the chip's real style, so the test asserts the style and the palette both. Checked against a
  mutation: with the accent text put back, `InInsertAndVisual_TheTextIsThePrimaryForeground` fails;
  with this dev's style, it passes.
- **House rule on the coupled Avalonia group.** `Directory.Packages.props` says the Enigma control
  library moves with Avalonia. Here it moved alone because the user asked for exactly this package and
  1.2.0 builds against the Avalonia already pinned, which is what the rule protects. Avalonia 12.1.3
  is still out and still its own decision.
- **Not adopted from 1.1.0:** `DisplayDuration` (decided in the plan: only Warning and Error bars
  remain after FEATURE-425B, and neither should vanish on a timer), and `ContentDialog`
  `Classes="secondary"`.
- **Follow-up for the next release item:** 1.7.0's notes say `Enigma.Avalonia.Desktop` "stays at 1.0.0";
  the next `RELEASENOTES.md` section should record 1.0.0 → 1.2.0 and the chip's new text colour.
- **Line endings:** nothing to recommend — `.gitattributes` pins `* text=auto eol=lf`, and this diff
  has no line-ending churn.
- **Documentation freshness sweep:** nothing edited. README does not name the library's version or the
  InfoBar's colours; `docs/RELEASE.md` does not mention the package; `RELEASENOTES.md` is written by the
  release item (above). No `CLAUDE.md`, `AGENTS.md`, `CHANGELOG.md` or `CONTRIBUTING.md` exists.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release` — **0 warnings, 0 errors**; no `AVLN` line in
  the normal-verbosity log.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` — **3586 passed, 0 failed, 0 skipped**
  (3582 before; 4 added). Green on the first run; no fix cycle used.
- Acceptance criteria: 1.2.0 resolved with Avalonia still 12.1.2; chip text is `EnigmaForegroundBrush`
  and ≥ 4.5:1 on both fills in both variants (asserted); every pre-existing test passes unchanged.
