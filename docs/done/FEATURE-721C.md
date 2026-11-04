# FEATURE-721C — Release 1.12.0

**Status: DONE** · single phase · Branch: `feature/feature-721c-release-1-12-0` (cut from
`feature/2026-11-04-icons-release-1-12`)
Plan: `docs/plan/FEATURE-721C.md` · Roadmap: `docs/roadmap.md`

## Summary

Version **1.12.0** is cut, carrying this run's `FEATURE-E208`: the icon that fills its frame. The
release edits:

- `<Version>1.12.0</Version>` in both csproj files. This is also the version `buildmsi` builds:
  `packaging/windows/buildmsi.ps1` holds no version literal. It reads the Desktop csproj's `<Version>`
  and checks that Core's agrees. Its own resolution was run against the bumped files (below);
- a 1.12.0 `RELEASENOTES.md` section;
- the README what's-new callout.

No artifact was built, no tag created, nothing pushed.

## Files/modules touched

- Created: `docs/done/FEATURE-721C.md`
- Modified:
  - `src/Enigma.MarkdownEditor.Desktop/Enigma.MarkdownEditor.Desktop.csproj`
  - `src/Enigma.MarkdownEditor.Core/Enigma.MarkdownEditor.Core.csproj`
  - `RELEASENOTES.md`, `README.md`
  - `docs/roadmap.md`, `docs/plan/FEATURE-721C.md`: statuses.

## Deviations & follow-ups

- **`buildmsi.ps1` is unchanged**, as the plan decided: the draft asked to "update the new version
  number in the buildmsi script", and the script takes it from the csprojs by design (as do all seven
  sibling scripts). A hard-coded copy would be a third place to keep in step.
- **Dependencies held, nothing applied**: same list as 1.11.0 (Avalonia set 12.1.4 → 12.1.5 held
  with `Enigma.Avalonia.Desktop` 1.3.0, still the latest; `xunit.v3` 4.0.2 held). The 1.11.0 follow-up
  stands: moving to `xunit.v3` 4.x and dropping `Avalonia.Headless.XUnit` is a separate item.
- **`docs/RELEASE.md`: no change here.** Its remaining `1.11.0` lines are history (the first `buildmsi`
  MSI; the profiles up to 1.10.0). FEATURE-E208 already updated the §1 icon check.
- **Open checks for the user:** RELEASE.md §1 (the larger icon on both taskbars and in About), §2
  (Linux) and §6 (upgrade over 1.11.0).

## Build/test evidence

- Baseline: see `docs/done/FEATURE-E208.md` (0 warnings; 3658 passed).
- `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`: **0 Warning(s),
  0 Error(s)**, `grep -ci AVLN` **0**.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` (`--no-build` on that build): **3658
  passed, 0 failed, 0 skipped**.
- Both `AssemblyInfo.cs` files read `AssemblyFileVersion("1.12.0.0")` and
  `AssemblyInformationalVersion("1.12.0+222b7ac…")`.
- **`buildmsi`'s version resolution**: the script's own `Get-ProjectVersion`, extracted from its AST
  and run in Windows PowerShell 5.1.26100 without the rest of the script (no `git clean`), returns
  `1.12.0` for Desktop and for Core. The `X.Y.Z` format check and the agreement check both pass, so the
  MSI would be `Enigma.MarkdownEditor.1.12.0.msi`.
- **`1.11.0` no longer claimed as current.** `git grep 1.11.0` outside `docs/plan/`, `docs/done/`,
  `docs/roadmap.md`, `RELEASENOTES.md` and `msiProfiles/` returns only `docs/RELEASE.md` history lines
  and §1's "larger than 1.11.0's" comparison.
- **`RELEASENOTES.md`:** everything from the 1.11.0 heading down is byte-identical to `HEAD`'s file
  (`diff` is empty). Every path the 1.12.0 section quotes exists.
- `dotnet list Enigma.MarkdownEditor.slnx package --outdated` (2026-11-04): the Avalonia set (six
  packages) 12.1.4 → 12.1.5 and `xunit.v3` 3.2.2 → 4.0.2; nothing else. All held.
- **Nothing produced, nothing tagged.** `artifacts/` does not exist; `git tag --list 1.12.0` is empty.
- Fix budget: 0 cycles used.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Notes sections | *Changes · Dependencies · Compatibility · Version* | No feature or fix shipped |
| How large "larger" is | "about 18 %", "a thin margin, where 1.11.0 left a wide one" | The SVG header's scale (1.1852) and live area (16 to 240), not a visual claim about other apps |
| Icon cache note under *Compatibility* | Added, with `ie4uinit.exe -show` | An upgrade that keeps the same `.exe` path is exactly when Windows shows the stale icon |
| Callout | 1.12.0 first, then the 1.11.0 sentence | House shape |
| How to check buildmsi without running it | Run its `Get-ProjectVersion` from the parsed script | Running the script would `git clean -dxf` the checkout and needs WiX; the function is the only version logic it has |

## Documentation sweep

- `README.md`: the what's-new callout.
- `docs/RELEASE.md`: nothing stale (see *Deviations*).
