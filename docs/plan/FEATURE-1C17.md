# FEATURE-1C17 — Enigma.Avalonia.Desktop 1.2.0

**Status: DONE** · single phase · Roadmap: `docs/roadmap.md`
**Type:** FEATURE · **Branch:** `feature/feature-1c17-enigma-avalonia-1-2-0`, cut from the run branch at build time.
**Run:** feature/2026-10-14-infobars-tree-multi-select

## Objective

Move `Enigma.Avalonia.Desktop` from 1.0.0 to 1.2.0, the latest release on nuget.org, for its two
InfoBar improvements: an info bar that can close itself after a period (1.1.0, opt-in) and severity
fills that stand out from the panels around them in the Dark variant, with a readable message line
on them (1.2.0). The bump must not leave anything else in the window less readable than it was.

## Context & constraints

Established by reading the code and the package, not assumed.

- **The pin is `Directory.Packages.props:39`** (`<PackageVersion Include="Enigma.Avalonia.Desktop" Version="1.0.0" />`),
  inside the version-coupled Avalonia group. Projects reference it without a version (CPM).
- **1.2.0's dependencies** (nuspec, `net10.0` group): `Avalonia` / `Avalonia.Themes.Fluent` ≥ 12.1.1,
  `CommunityToolkit.Mvvm` 8.4.2, `Enigma.Core` 1.0.0 — exactly what 1.0.0 already brought. The solution
  pins Avalonia 12.1.2, which satisfies ≥ 12.1.1, so nothing else has to move for the build to resolve.
- **1.1.0 / 1.2.0 are backward compatible** with 1.0.0 per their release notes: `InfoBar.DisplayDuration`
  (default `null`, never closes on its own), the `IInfoBarService.ShowAsync(TimeSpan, …)` extension,
  `ContentDialog` `Classes="secondary"`, new keys `EnigmaDialogSecondaryBackground*` and
  `EnigmaInfoBarMessageForeground*`, and new Dark defaults for the eight `Enigma{Info,Success,Warning,Error}{Background,Border}Color`
  keys. Nothing renamed or removed.
- **The app paints its own content on the severity fills in five places** — the external-change bar
  (`EditorPaneView.axaml:183`), the missing-roots notice (`MainWindow.axaml:398`), the remove
  confirmation (`WorkspaceManagerView.axaml:205`), the search-hit highlight (`SearchPaneView.axaml:150`)
  and the vim mode chip (`MainWindow.axaml:64-90`). The first four put `EnigmaForegroundBrush` text on
  the fill, which 1.2.0 documents as WCAG AA on every new fill (4.85–5.06:1); their warning icons are
  non-text graphics and stay above 3:1.
- **The vim chip is the exception**: it paints `EnigmaSuccessBrush` / `EnigmaWarningBrush` *text* on
  `EnigmaSuccessBackgroundBrush` / `EnigmaWarningBackgroundBrush`. Computed from the published palette:

  | Chip | Dark 1.0/1.1 | Dark 1.2.0 |
  |---|---|---|
  | insert — `#59A869` on success fill | 4.81:1 | **3.10:1** |
  | visual — `#E8A33D` on warning fill | 6.81:1 | **4.28:1** |

  Both fall below AA's 4.5:1 for 10 px semibold text. The Light variant is unchanged by 1.2.0.
- **No MainWindow view test exists** — the window needs the whole ViewModel graph. Palette assertions
  live in headless tests that resolve keys through `Application.Current.TryFindResource(key, variant, …)`
  (`MarkdownPreviewThemeTests`), against the dictionaries `HeadlessTestApp` merges exactly as `App.axaml` does.
- **`TreatWarningsAsErrors` is on solution-wide**; `AVLN*` diagnostics are not promoted, so the build log
  is grepped for `AVLN` too. Tests run with `dotnet test --solution Enigma.MarkdownEditor.slnx`.

## Steps

1. `Directory.Packages.props`: `Enigma.Avalonia.Desktop` 1.0.0 → **1.2.0**. Nothing else in the coupled group moves.
2. `MainWindow.axaml`, vim chip: the insert and visual `TextBlock` foregrounds become
   `{DynamicResource EnigmaForegroundBrush}`. The fill and border keep their severity colours, which is
   what tells the modes apart — more strongly in Dark now that the fills are pastel. Rewrite the style's
   comment so it says why the text is the primary tone and not the accent.
3. New headless test `tests/…Desktop.UnitTests/Views/VimModeChipPaletteTests.cs`: in both variants, the
   primary foreground colour against the success and warning fills is at least 4.5:1 (WCAG relative
   luminance), and — as the regression this item exists to avoid — the accent-on-fill pairing the chip
   used to rely on is *not* what it paints. The contrast arithmetic is a private helper in the test.
4. Restore, Release build (0 warnings, no `AVLN`), whole suite.

## Acceptance criteria

- `dotnet list … package` (or `obj/project.assets.json`) resolves `Enigma.Avalonia.Desktop` **1.2.0**;
  Avalonia stays at 12.1.2.
- The vim chip's insert and visual text is `EnigmaForegroundBrush`, and that colour keeps ≥ 4.5:1 on
  both fills in both variants (asserted).
- Every existing test passes unchanged — the bump is backward compatible.
- Release build: 0 warnings, 0 errors, no `AVLN` diagnostic.

## Out of scope

- The coupled Avalonia set (12.1.2 → 12.1.3), `Enigma.Icons.Avalonia`, `xunit.v3` 4.x.
- Making any info bar close itself (`DisplayDuration`) — see *Decisions*.
- `ContentDialog` `Classes="secondary"` — nothing asked for a darker dialog.
- `RELEASENOTES.md`: written by the next release item, as for every item before this one.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Item type | `FEATURE` | A dependency move the user asked for, bringing new behaviour | `BUG` — nothing is broken today |
| Target version | 1.2.0 | Latest on nuget.org; contains 1.1.0's timed bars and 1.2.0's readable Dark fills — both are "InfoBar usage" | 1.1.0 alone (leaves the Dark fills sinking into the panels) |
| Move the coupled Avalonia set with it? | No — Enigma.Avalonia.Desktop alone | 1.2.0 needs Avalonia ≥ 12.1.1 and builds against what is pinned; the house rule exists so a mismatched set never breaks the build, and this one does not. The set moving to 12.1.3 is its own decision, as 1.7.0's release notes already say | Bumping Avalonia 12.1.3 + headless packages in the same commit (unrequested, and it couples a library bump to a framework bump) |
| Adopt `DisplayDuration` for the remaining bars? | No | After FEATURE-425B only Warning and Error bars remain, and both report something the user has to read; an error that disappears on a timer is an error that gets missed. The library default — stay until dismissed — is the right one here | Auto-close warnings after N seconds |
| Vim chip contrast after the bump | Chip text becomes `EnigmaForegroundBrush` | 1.2.0 documents AA for the primary foreground on every fill — a contract — whereas accent-on-fill never was one, and it is what broke. The fill still carries the mode's colour | A project-owned override restoring the 1.1 Dark fills (fights the library's own redesign); keep the accent text and accept 3.1:1 |
| How to test a style on a window with no view test | Assert the palette contract the style relies on, in both variants | MainWindow needs the full ViewModel graph; the contract is what a future bump could break, and the test fails the day it does | Building a MainWindow harness for one setter |
