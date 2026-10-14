# FEATURE-23F7 — Release 1.8.0

**Status: TODO** · single phase · Roadmap: `docs/roadmap.md`
**Type:** FEATURE · **Branch:** `feature/feature-23f7-release-1-8-0`, cut from the run branch at build time.
**Run:** feature/2026-10-14-confirm-delete-release

## Objective

Cut version **1.8.0**, carrying everything merged since the `1.7.0` tag — `FEATURE-1C17`,
`FEATURE-425B`, `FEATURE-4B09` (both phases) — and `FEATURE-47F5` from this run: set the version,
refresh the dependencies the release skill takes, write the release documentation, and commit the
versioned MSI profile. **No artifacts are built** — no MSI, no Linux tarball, no publish. The tag and
push commands are printed for the user to run, never run here.

Same shape as `FEATURE-3D59` (1.7.0).

## Version choice — why 1.8.0

The version was given by the user, and it is also the SemVer answer — a MINOR: functionality was
added and nothing a user depends on changes incompatibly.

| Shipped since 1.7.0 | Kind |
|---|---|
| `FEATURE-1C17` — Enigma.Avalonia.Desktop 1.2.0; brighter InfoBar fills in Dark, vim chip text repainted | dependency / display |
| `FEATURE-425B` — no informational InfoBar after a trash delete | change |
| `FEATURE-4B09-PHASE01` — multi-selection in the file tree | feature |
| `FEATURE-4B09-PHASE02` — delete the whole selection | feature |
| `FEATURE-47F5` — every delete asks for confirmation | feature / change |

Not a MAJOR: `settings.json`, `workspaces.json` and a stored session are untouched. If `FEATURE-47F5`
is quarantined, the notes shrink to what actually shipped; 1.8.0 still holds (4B09 is a feature).

## Context & constraints

- **The version lives in three places** and they move together: `<Version>` in
  `src/Enigma.MarkdownEditor.Desktop/…csproj:8`, `<Version>` in
  `src/Enigma.MarkdownEditor.Core/…csproj:13`, and the versioned MSI profile filename plus its
  `version` field.
- **This is an app, not a library.** No `PackageId`, no `dotnet pack`, no `dotnet nuget push`.
- **Tag convention is a bare `X.Y.Z`.** `git tag --list` reads `1.6.0`, `1.7.0` in this clone; `1.8.0`
  must not exist.
- **`RELEASENOTES.md` is the single release-notes source.** No `CHANGELOG.md`. Sub-section order as
  1.7.0: *New Features · Changes · Fixes · Dependencies · Compatibility · Version*, only where non-empty.
- **MSI profile convention:** `msiProfiles/Enigma.MarkdownEditor.Desktop.X.Y.Z.msipkg.json`, cloned
  from 1.7.0 per `docs/RELEASE.md` §4 — `upgradeCode` verbatim, a new `productId` from a real
  generator, `version` set. The repo's field order, not the skill template's.
- **`dotnet list package --outdated`** (read at planning time, 2026-10-14):
  Microsoft.Extensions.Hosting 10.0.12 → 10.0.13; the coupled Avalonia set (Avalonia, .Desktop,
  .Themes.Fluent, .Fonts.Inter, .Headless, .Headless.XUnit) 12.1.2 → 12.1.4; xunit.v3 3.2.2 → 4.0.2.
  Core has no updates.
- **README screenshots stay pending**, as 1.3.0 … 1.7.0 left them.
- **Every item above must be `DONE` first** — this item documents them and re-verifies the suite.

## Design

1. **Version** — `<Version>1.8.0</Version>` in both `src/*` csproj files; grep for `1.7.0` afterwards
   and account for every remaining tracked hit.
2. **Dependencies** (`Directory.Packages.props`) — take the **non-coupled** bump,
   Microsoft.Extensions.Hosting 10.0.13. **Hold back** the coupled Avalonia 12.1.4 set and xunit.v3
   4.x (a major test-framework bump; Avalonia.Headless.XUnit 12.1.2 is built against 3.2.2). A bump
   that turns the suite red and does not yield to a small fix is held back too, and recorded.
3. **MSI profile** — `msiProfiles/Enigma.MarkdownEditor.Desktop.1.8.0.msipkg.json`, the 1.7.0 profile
   with `version` and a freshly generated `productId`; `upgradeCode` kept byte-for-byte.
4. **`RELEASENOTES.md`** — a `# Enigma Markdown Editor v1.8.0 Release Notes` section prepended, written
   from `docs/done/FEATURE-1C17.md`, `FEATURE-425B.md`, `FEATURE-4B09-PHASE0*.md` and
   `FEATURE-47F5.md`. Earlier sections byte-identical.
5. **`README.md`** — the what's-new callout for 1.8.0; verify, rather than repeat, what the items'
   sweeps already wrote.
6. **`docs/RELEASE.md`** — read in full against the repository; add standing §1 checks for what the
   shipped items left manually unverified, if not already present; record "no change" or the edit.
7. **Pre-flight, run here** — `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental
   -v normal` with **0 Warning(s)** and no `AVLN` in the log; `dotnet test --solution
   Enigma.MarkdownEditor.slnx -c Release` green; both `AssemblyInfo.cs` files read `1.8.0`.
8. **Printed, never run** — merge, `git tag 1.8.0`, `git push origin 1.8.0`; `docs/RELEASE.md`
   §2/§4/§5 deferred by decision, as in every release since 1.1.0.

## Acceptance criteria

- `<Version>` reads `1.8.0` in both csproj files, and nothing tracked outside history still claims
  1.7.0 as current.
- `Directory.Packages.props` carries the Hosting 10.0.13 bump (or it is held back with a recorded
  reason); the `--outdated` report is in the completion doc.
- The 1.8.0 MSI profile exists, is valid JSON, keeps the `upgradeCode` byte-for-byte, and carries a
  fresh `productId` differing from all earlier profiles'.
- `RELEASENOTES.md` has a 1.8.0 section above 1.7.0; every earlier section is unmodified.
- The README what's-new callout reads 1.8.0.
- Release build: **0 warnings**, no `AVLN` output, full suite green.
- No `.msi`, no `.tar.gz`, no `artifacts/` content left; no tag created and no push performed.

## Dependencies

Built **last**, after `FEATURE-47F5`.

## Out of scope

Building the MSI or the tarball · the manual Linux verification checklist · a GitHub Release · README
screenshots · the Avalonia 12.1.4 set · xunit.v3 4.x · a `CHANGELOG.md`.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Which version? | 1.8.0 | Given by the user, and the SemVer answer: features landed, nothing incompatible | 1.7.1 (cannot carry features); 2.0.0 (no data or behaviour break) |
| Dependency refresh | Take Hosting 10.0.13; hold the coupled Avalonia set and xunit.v3 4.x | `dotnet-release`'s routine-release rule and the 1.7.0 precedent | Taking nothing; taking the coupled set without an opt-in |
| Build the artifacts / tag? | No — printed only | Every release since 1.1.0 was cut this way; a run never tags or pushes | Producing the tarball or tagging here |
| Where does the delete confirmation go in the notes? | *New Features* for multi-selection, *Changes* for the confirmation and the dropped info bar | An upgrading user needs to know a delete now asks, separately from what is new | One combined entry |
