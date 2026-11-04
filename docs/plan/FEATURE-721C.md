# FEATURE-721C — Release 1.12.0

**Status:** TODO
**Type:** FEATURE · single phase · Roadmap: `docs/roadmap.md`
**Branch:** `feature/feature-721c-release-1-12-0`, cut from the run branch at build time.
**Run:** feature/2026-11-04-icons-release-1-12

## Objective

Cut version **1.12.0**, carrying this run's `FEATURE-E208` (the icon that fills the frame). Set the
version where `buildmsi` reads it, re-check the dependencies, and write the release documentation, as
FEATURE-FB91 did for 1.11.0. **No artifact is built** (no MSI, no tarball), nothing is tagged, nothing
is pushed.

## Version choice: why 1.12.0

The user's rule is `x.y.z → x.(y+1).z`. 1.11.0 → **1.12.0**. It is also a defensible SemVer MINOR:
new art, nothing a user depends on breaks.

## Context & constraints

- **"Update the new version number in the buildmsi script."** `packaging/windows/buildmsi.ps1` holds
  no version literal. It reads `<Version>` from the Desktop csproj, refuses to build unless the Core
  csproj repeats it, and names the MSI and its profile `Enigma.MarkdownEditor.<version>`. The version
  `buildmsi` builds therefore lives in those two `<Version>` elements, which this release moves
  together. The same holds for every sibling Enigma app's `buildmsi.ps1`, and the 1.11.0 notes promise
  *Nothing to edit per release*.
- **This is an app, not a library**: no pack, no NuGet push. Tags are a bare `X.Y.Z`; `1.11.0` exists,
  `1.12.0` must not, and the run does not create it.
- **`RELEASENOTES.md` is the single release-notes source.** The top heading is
  `# Enigma Markdown Editor v1.12.0 Release Notes`. Sub-sections appear in this order, and only when
  non-empty: *New Features · Changes · Fixes · Dependencies · Compatibility · Version*. Earlier sections
  stay byte-identical.
- **The `dotnet-release` skill** is the house release procedure. Load it at build time and follow it as
  far as it applies to an app; its MSI-profile step is replaced by `buildmsi.ps1`.
- **Dependencies.** The Avalonia set moves only as a whole, and only with what Enigma.Avalonia.Desktop is
  built against. `xunit.v3` 4.x is held (1.10.0's reason).
- FEATURE-E208 must be `DONE` first. If it is quarantined, it is left out of the notes, and that is
  recorded.

## Steps

1. Check that `git tag --list 1.12.0` is empty.
2. **Version**: `<Version>1.12.0</Version>` in both `src/*` csproj files. Then `git grep 1.11.0` and
   account for every tracked hit outside history (`docs/plan/`, `docs/done/`, `docs/roadmap.md`,
   `RELEASENOTES.md`, `msiProfiles/`).
3. **`buildmsi`**: run its version resolution without its side effects — the script's own
   `Get-ProjectVersion` XPath over both csprojs, in PowerShell 5.1 — and confirm it yields `1.12.0` for
   both, so the MSI would be `Enigma.MarkdownEditor.1.12.0.msi`. `buildmsi.ps1` and `buildmsi.bat` stay
   unchanged.
4. **Dependencies**: `dotnet list Enigma.MarkdownEditor.slnx package --outdated`, logged in the done doc.
   Apply non-coupled patch/minor stable bumps; hold the Avalonia set unless Enigma.Avalonia.Desktop
   moved with it; hold majors, recorded.
5. **`RELEASENOTES.md`**: prepend the 1.12.0 section from this run's `docs/done/*.md`. *Changes*: the
   icon fills its frame. *Dependencies*, *Compatibility*, *Version*. Every quoted path is checked.
6. **`README.md`**: the what's-new callout for 1.12.0, then the 1.11.0 sentence.
7. **`docs/RELEASE.md`**: bring any version-specific line to 1.12.0, or record "no change" when every
   version-bearing line is history.
8. **Pre-flight, run here**:
   - `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`, with
     **0 Warning(s)** and no `AVLN` in the log;
   - `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`, green against the baseline;
   - both `AssemblyInfo.cs` files read `1.12.0`.

## Acceptance criteria

- `<Version>` reads `1.12.0` in both csproj files, and nothing tracked outside history still claims
  1.11.0 as current.
- `buildmsi.ps1`'s version resolution yields `1.12.0` from both csprojs (step 3).
- `RELEASENOTES.md` has a 1.12.0 section above 1.11.0, covering every `DONE` item of this run, with
  quoted paths that exist; every earlier section is byte-identical.
- The README what's-new callout reads 1.12.0.
- Release build: **0 warnings**, no `AVLN`; the full suite shows no new failure; the built assemblies
  carry 1.12.0.
- No `.msi`, `.tar.gz` or `artifacts/` content left behind; no tag created; nothing pushed.

## Out of scope

- Building or publishing the artifacts (the user runs `buildmsi` and `pack-linux.ps1` after merging).
- Tagging, pushing, merging into `develop`/`main`.
- Moving the Avalonia set on its own; major dependency bumps.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Version | 1.12.0 | `x.(y+1).z` from 1.11.0 | 1.11.1 (not the user's rule); 2.0.0 (nothing breaks) |
| What "update the version in buildmsi" means | Bump the two csproj `<Version>`s the script reads; verify its resolution yields 1.12.0; leave the script unchanged | The script has no version literal by design; it derives the version and checks the two csprojs agree. All seven sibling scripts do the same | Adding a hard-coded version to `buildmsi.ps1` (a third source of truth that can disagree, against the published "nothing to edit per release" promise and the sibling convention) |
| A committed MSI profile | None | `buildmsi.ps1` writes it per build | `msiProfiles/...1.12.0.msipkg.json` |
| Order in the run | Last | The notes describe the icon | First |
| Shape of the notes, callout, tag | As 1.11.0 | House convention | A single *Changes* list |
| Build artifacts here | No | The run never tags or pushes; RELEASE.md §2 needs a Linux machine | Running `buildmsi` (its `git clean -dxf` would also wipe the run's untracked state) |
| Dependencies | Non-coupled patch/minor applied; Avalonia set with the library; majors held | House release rule | Bumping everything |

## Beyond-the-draft checklist (whole run)

| Dimension | Resolution |
|---|---|
| Security & auth | N/A: art and a version bump; the MSI stays unsigned, as before |
| Input validation & abuse | `buildmsi` already validates the version format and agreement |
| Performance & scalability | N/A |
| Concurrency | N/A |
| Error handling & resilience | A partial icon swap fails `AppIconTests`; a csproj disagreement stops `buildmsi` |
| UX edge cases | The larger mark on dark/light shells and at 16 px: RELEASE.md §1 visual check |
| Accessibility & i18n | N/A |
| Observability | N/A |
| Data migration & compatibility | Same upgrade code: 1.12.0 upgrades 1.x in place; no data shape changes |
| Deployment & operations | Unchanged: `buildmsi.bat`, `pack-linux.ps1` |
| Licensing & dependencies | Recorded in the done doc after `--outdated` |
| Testing | `AppIconTests`; the version check in the built assemblies; buildmsi's version resolution |
| Documentation | `RELEASENOTES.md`, README callout, `docs/RELEASE.md` §1 |
