# FEATURE-FB91 — Release 1.11.0

**Status: DONE** · single phase · Branch: `feature/feature-fb91-release-1-11-0` (cut from
`feature/2026-11-03-icons-msi-release`)
Plan: `docs/plan/FEATURE-FB91.md` · Roadmap: `docs/roadmap.md`

## Summary

Version **1.11.0** is cut, carrying this run's two items:

- `FEATURE-8156`: the new icon set without the tile;
- `FEATURE-1E34`: the vendored Enigma.Msi worker and `buildmsi.ps1` / `buildmsi.bat`.

The release edits:

- `<Version>1.11.0</Version>` in both csproj files;
- a 1.11.0 `RELEASENOTES.md` section;
- the README what's-new callout;
- one new standing `docs/RELEASE.md` §6 check: the MSI upgrades the previous one in place.

There is no MSI profile to commit: `buildmsi.ps1` writes one per build. No artifact was built, no tag
created, nothing pushed.

## Files/modules touched

- Created: `docs/done/FEATURE-FB91.md`
- Modified:
  - `src/Enigma.MarkdownEditor.Desktop/Enigma.MarkdownEditor.Desktop.csproj`
  - `src/Enigma.MarkdownEditor.Core/Enigma.MarkdownEditor.Core.csproj`
  - `RELEASENOTES.md`, `README.md`, `docs/RELEASE.md`
  - `docs/roadmap.md`, `docs/plan/FEATURE-FB91.md`: statuses.

## Deviations & follow-ups

- **1.10.0 is not tagged.** `git tag --list` ends at `1.9.0`, so 1.10.0 was merged but never tagged.
  Tagging is the user's (RELEASE.md §3). `1.11.0` does not exist yet either.
- **Dependencies held, nothing applied.**
  - The Avalonia set (12.1.4 → 12.1.5 available) stays with `Enigma.Avalonia.Desktop` 1.3.0, which is
    still the latest and is built against 12.1.4. GitClient's 6.14.0 held it the same way.
  - `xunit.v3` 4.0.2 is a major bump, held for the reason the notes give since 1.10.0. The siblings run
    it with their own headless fixture, which would let this repository drop
    `Avalonia.Headless.XUnit`. **Follow-up:** a separate item, not a release edit.
- **Open checks for the user:** RELEASE.md §2 (Linux) and the new §6 upgrade check. They need a Linux
  machine and an installation, so the run cannot do them.

## Build/test evidence

- Baseline: see `docs/done/FEATURE-8156.md` (0 warnings; 3648 passed).
- `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`: **0 Warning(s),
  0 Error(s)**, `grep -ci AVLN` **0**.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3658 passed, 0 failed, 0 skipped**.
- Both `AssemblyInfo.cs` files read `AssemblyFileVersion("1.11.0.0")` and
  `AssemblyInformationalVersion("1.11.0+f93684d…")`.
- **`1.10.0` no longer claimed as current.** `git grep 1.10.0` outside `docs/plan/`, `docs/done/`,
  `docs/roadmap.md`, `RELEASENOTES.md` and `msiProfiles/` returns only three lines, all history:
  - the README's layout line *the MSI profiles of 1.0.0–1.10.0*;
  - two `docs/RELEASE.md` lines naming the profiles up to 1.10.0.
- **`RELEASENOTES.md`:** everything from the 1.10.0 heading down is byte-identical to `HEAD`'s file
  (`diff` is empty). Every path the 1.11.0 section quotes exists.
- `dotnet list Enigma.MarkdownEditor.slnx package --outdated` (2026-11-03):
  - `Avalonia`, `.Desktop`, `.Fonts.Inter`, `.Themes.Fluent`, `Avalonia.Headless` and
    `Avalonia.Headless.XUnit`: 12.1.4 → 12.1.5;
  - `xunit.v3` 3.2.2 → 4.0.2;
  - nothing else. All of these are held; see *Deviations*.
- **Nothing produced, nothing tagged.** `artifacts/` does not exist. `git tag --list 1.11.0` is empty.
- Fix budget: 0 cycles used.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Notes sections | *New Features · Changes · Dependencies · Compatibility · Version* | No fix shipped. The install-path wording is a documentation correction, recorded under *Compatibility* |
| Mention the ARP name and MSI file name change | Yes, under *Changes*, with the old names | A user upgrading sees the new name in *Apps & features*; saying it was expected avoids a "second product?" scare |
| Mention the MSI size | Yes, about 240 MB, uncompressed | A visible change in the download, traded for build speed (`compression None`) |
| New RELEASE.md check | §6: upgrade over the previous MSI | The one claim of this release no test or table read can prove; 1.11.0 is the first `buildmsi` MSI and the first with the new product name |
| Callout | 1.11.0 first, then the 1.10.0 sentence | House callouts sum up the previous release after the new one |

## Documentation sweep

- `README.md`: the what's-new callout.
- `docs/RELEASE.md`: the new §6 upgrade check. Its other version-bearing lines are historical ranges,
  which are correct.
