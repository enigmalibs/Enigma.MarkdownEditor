# FEATURE-FB91 — Release 1.11.0

**Status:** TODO
**Type:** FEATURE · single phase · Roadmap: `docs/roadmap.md`
**Branch:** `feature/feature-fb91-release-1-11-0`, cut from the run branch at build time.
**Run:** feature/2026-11-03-icons-msi-release

## Objective

Cut version **1.11.0**, carrying what this run merged:

- `FEATURE-8156`: the new icon set without the tile;
- `FEATURE-1E34`: the vendored Enigma.Msi worker and `buildmsi.ps1` / `buildmsi.bat`.

The release sets the version, re-checks the dependencies and writes the release documentation, as
FEATURE-B260 did for 1.10.0. **No artifact is built** (no MSI, no tarball), nothing is tagged, nothing
is pushed.

## Version choice: why 1.11.0

The user's rule: `x.y.z → x.(y+1).z`, and `z` resets to 0 when it was above 0. 1.10.0 has `z = 0`, so
the result is **1.11.0**. It is also a defensible SemVer MINOR: a new installer path and new art, and
nothing a user depends on breaks.

## Context & constraints

- **The version lives in two places**, which move together: `<Version>` in the Desktop csproj and in
  the Core csproj. Since FEATURE-1E34 there is **no MSI profile to commit**: `buildmsi.ps1` generates it
  from the csproj version.
- **This is an app, not a library**: no pack, no NuGet push. Tags are a bare `X.Y.Z`; `1.11.0` must not
  exist yet, and the run does not create it.
- **`RELEASENOTES.md` is the single release-notes source.** The top heading is
  `# Enigma Markdown Editor v1.11.0 Release Notes`. Sub-sections appear in this order, and only when
  non-empty: *New Features · Changes · Fixes · Dependencies · Compatibility · Version*. Earlier
  sections stay byte-identical.
- **README screenshots stay pending**, as in every release since 1.3.0.
- **The `dotnet-release` skill** is the house release procedure. Load it at build time and follow it as
  far as it applies to an app. Its MSI-profile step is replaced by FEATURE-1E34's script.
- **Dependencies.** FEATURE-2877 held `xunit.v3` 3.2.2 → 4.0.2 back (B260). The Avalonia set moves
  only as a whole, and only with what Enigma.Avalonia.Desktop is built against.
- The other run items must be `DONE` first. A quarantined item is left out of the notes, and that is
  recorded.

## Steps

1. Check that `git tag --list 1.11.0` is empty.
2. **Version**: `<Version>1.11.0</Version>` in both `src/*` csproj files. Then `git grep 1.10.0` and
   account for every tracked hit outside history (`docs/plan/`, `docs/done/`, `docs/roadmap.md`,
   `RELEASENOTES.md`, `msiProfiles/`).
3. **Dependencies**: `dotnet list Enigma.MarkdownEditor.slnx package --outdated`, logged in the done doc.
   Apply non-coupled patch/minor stable bumps. Hold the Avalonia set unless Enigma.Avalonia.Desktop
   moved with it. Hold majors (`xunit.v3` 4.x), recorded.
4. **`RELEASENOTES.md`**: prepend the 1.11.0 section, written from this run's `docs/done/*.md`. Every
   path or command it quotes is checked against the files.
   - *Changes*: the tile-less icon.
   - *New Features*: `buildmsi`.
   - *Compatibility*: the MSI's new file name and product name; it upgrades 1.x in place through the
     same upgrade code.
   - *Version*.
5. **`README.md`**: the what's-new callout for 1.11.0.
6. **`docs/RELEASE.md`**: bring any version-specific line to 1.11.0. The installer steps are
   FEATURE-1E34's; add nothing new unless this release leaves a check uncovered (record "no change"
   otherwise).
7. **Pre-flight, run here**:
   - `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`, with
     **0 Warning(s)** and no `AVLN` in the log;
   - `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`, green against the baseline;
   - both `AssemblyInfo.cs` files read `1.11.0`.

## Acceptance criteria

- `<Version>` reads `1.11.0` in both csproj files, and nothing tracked outside history still claims
  1.10.0 as current.
- `RELEASENOTES.md` has a 1.11.0 section above 1.10.0. It covers every `DONE` item of this run, its
  quoted paths match the files, and every earlier section is unmodified.
- The README what's-new callout reads 1.11.0.
- Release build: **0 warnings**, no `AVLN`. The full suite shows no new failure. The built assemblies
  carry 1.11.0.
- No `.msi`, no `.tar.gz` and no `artifacts/` content is left behind. The run creates no tag and pushes
  nothing.

## Out of scope

- Building or publishing the artifacts (the user runs `buildmsi` and `pack-linux.ps1` after merging).
- Tagging, pushing, merging into `develop`/`main`.
- Moving the Avalonia set on its own; major dependency bumps.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Version | 1.11.0 | The user's rule (y+1, z already 0) | 1.10.1; 2.0.0 (nothing breaks) |
| Order in the run | Last | The notes describe the icon and the installer, built first | First |
| A committed MSI profile for 1.11.0 | None | FEATURE-1E34's script generates it per build from the version | `msiProfiles/Enigma.MarkdownEditor.Desktop.1.11.0.msipkg.json` |
| Shape of the notes, callout, tag | As 1.10.0 | House convention | A single *Changes* list |
| Build artifacts here | No | The run never tags or pushes, and RELEASE.md §2 needs a Linux machine | Building the MSI and tarball |
| Dependencies | Non-coupled patch/minor bumps applied; Avalonia set held with the library; majors held | House rule from earlier releases | Bumping everything |
| A quarantined item | Left out of the notes, and recorded | Notes describe what ships | Releasing it as a known issue |

## Beyond-the-draft checklist (whole run)

| Dimension | Resolution |
|---|---|
| Security & auth | `buildmsi` runs local tools only; the vendored binaries are verified byte for byte against Enigma.Msi 1.7.0's build. Unsigned MSI, as before |
| Input validation & abuse | `buildmsi` validates the version format and agreement before acting |
| Performance & scalability | `compression None` trades MSI size for build speed (sibling choice) |
| Concurrency | N/A |
| Error handling & resilience | `buildmsi`: prerequisites checked before `git clean`; every failure is one `buildmsi:` line with the tool's exit code |
| UX edge cases | The tile-less icon on dark and light shells: a RELEASE.md §1 visual check |
| Accessibility & i18n | N/A; the manufacturer's accents are kept with `[char]0x00E9` in an ASCII script |
| Observability | `buildmsi` prints each step and the elapsed time; the generated profile stays in `artifacts/` as the build's record |
| Data migration & compatibility | Same upgrade code, so 1.11.0 upgrades earlier MSIs in place. ARP name changes from `Enigma.MarkdownEditor.Desktop` to `Enigma Markdown Editor` |
| Deployment & operations | `buildmsi` replaces the hand-run Enigma.Msi.Desktop step; RELEASE.md §4 rewritten |
| Licensing & dependencies | Vendored worker: MIT (Enigma.Msi, WixSharp, .NET) and MS-RL (WiX 4.0.1 assemblies), listed in its README; none ships in the MSI |
| Testing | `AppIconTests` (+2), `WindowsInstallerIdentityTests` (3); the script verified end to end in a scratch clone |
| Documentation | README, `docs/RELEASE.md`, `RELEASENOTES.md`, the worker's README |
