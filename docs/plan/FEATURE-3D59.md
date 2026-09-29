# FEATURE-3D59 — Release 1.7.0

**Status: TODO** · Single phase · Roadmap: `docs/roadmap.md`
**Type:** FEATURE · **Branch:** `feature/feature-3d59-release-1-7-0`, cut from the run branch at build time.
**Run:** feature/2026-09-29-linux-installer-release

## Objective

Cut version **1.7.0**, carrying `BUG-18AD` (both phases) and `FEATURE-4BB7`: set the version, refresh
the dependencies the release skill takes, write the release documentation, and commit the versioned
MSI profile. **No artifacts are built** — no MSI, no Linux tarball, no publish. The tag and push
commands are printed for the user to run, never run here.

Same shape as `FEATURE-8381` (1.6.0), plus a dependency refresh as 1.4.0 carried.

## Version choice — why 1.7.0

SemVer MINOR. The highest-ranked change since 1.6.0 adds functionality, and nothing a user depends
on changes incompatibly:

| Shipped since 1.6.0 | Kind |
|---|---|
| `BUG-18AD-PHASE01` — code spans get their own chip colour in the preview | fix |
| `BUG-18AD-PHASE02` — the code chip gets room to breathe | fix |
| `FEATURE-4BB7` — a Linux installer that builds from a checkout, an uninstaller, a terminal command and a full icon set | feature |

Two fixes alone would be a PATCH (1.6.1); one feature makes it a MINOR. Not a MAJOR: `settings.json`,
`workspaces.json` and a stored session are untouched; the application's behaviour changes only by the
two fixes; and an upgrade over a 1.x Linux install is migrated by the installer. The one thing that
goes away is the tarball installer's `--prefix` option, a packaging-script flag with `XDG_DATA_HOME`
in its place — recorded under *Changes*, not a reason to tell every user their data needs attention.

If a quarantined item leaves any of the above unfinished, the table and the notes shrink to what
actually shipped, and the version follows: 1.6.1 if only fixes landed. This plan is amended rather
than quietly reinterpreted.

## Context & constraints

- **The version lives in three places** and they move together: `<Version>` in
  `src/Enigma.MarkdownEditor.Desktop/…csproj:8`, `<Version>` in
  `src/Enigma.MarkdownEditor.Core/…csproj:13`, and the versioned MSI profile filename plus its
  `version` field.
- **This is an app, not a library.** No `PackageId`, no `dotnet pack`, no `dotnet nuget push`.
- **Tag convention is a bare `X.Y.Z`.** `git tag --list` reads `1.6.0` only in this clone
  (`1.4.1` was never tagged — the user's call, recorded since 1.5.0); `1.7.0` must not exist.
- **`RELEASENOTES.md` is the single release-notes source.** No `CHANGELOG.md`.
- **MSI profile convention:** `msiProfiles/Enigma.MarkdownEditor.Desktop.X.Y.Z.msipkg.json`, cloned
  from 1.6.0 per `docs/RELEASE.md` §4 — `upgradeCode` verbatim, a new `productId` from a real
  generator, `version` set. The repo's field order, not the skill template's.
- **`dotnet list package --outdated`** (read at planning time): Markdig 1.3.2 → 1.4.0;
  Microsoft.Extensions.Hosting 10.0.11 → 10.0.12; coverlet.collector 10.0.1 → 10.1.0; the coupled
  Avalonia set (Avalonia, .Desktop, .Themes.Fluent, .Fonts.Inter, .Headless, .Headless.XUnit)
  12.1.2 → 12.1.3; Enigma.Avalonia.Desktop 1.0.0 → 1.1.0 (in the coupled group per
  `Directory.Packages.props`); xunit.v3 3.2.2 → 4.0.1.
- **README screenshots stay pending**, as 1.3.0 … 1.6.0 left them.
- **Every item above must be `DONE` first** — this item documents them and re-verifies the suite.

## Design

**1 — Version** — `<Version>1.7.0</Version>` in both `src/*` csproj files; grep for `1.6.0`
afterwards and account for every remaining tracked hit.

**2 — Dependencies** (`Directory.Packages.props`) — take the **non-coupled** bumps, per
`dotnet-release`: Markdig 1.4.0, Microsoft.Extensions.Hosting 10.0.12, coverlet.collector 10.1.0.
**Hold back** the coupled Avalonia 12.1.3 set together with Enigma.Avalonia.Desktop 1.1.0 (a coupled
set moves only on an explicit opt-in), and xunit.v3 4.x (a major test-framework bump, and
Avalonia.Headless.XUnit 12.1.2 depends on xunit.v3 3.2.2). A bump that turns the suite red and does
not yield to a small fix is held back too, and recorded.

**3 — MSI profile** — `msiProfiles/Enigma.MarkdownEditor.Desktop.1.7.0.msipkg.json`, the 1.6.0 profile
with `version` and a freshly generated `productId`; `upgradeCode`
`a7b550b9-1338-458f-bf2b-66dcf1d25380` kept byte-for-byte.

**4 — `RELEASENOTES.md`** — a `# Enigma Markdown Editor v1.7.0 Release Notes` section prepended,
written from `docs/done/BUG-18AD-PHASE0*.md` and `docs/done/FEATURE-4BB7.md`: *New Features* (the
installer), *Fixes* (the code chip), *Changes* (the installer's new locations, `--prefix` gone, the
1.x install retired automatically), *Dependencies* (every `old → new`, and what was held back),
*Compatibility*, *Version*. Earlier sections byte-identical.

**5 — `README.md`** — the what's-new callout for 1.7.0; verify, rather than repeat, what
`FEATURE-4BB7`'s sweep already wrote.

**6 — `docs/RELEASE.md`** — read in full against the repository and record "no change" or the edit.

**7 — Pre-flight, run here** — `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental
-v normal` with **0 Warning(s)** and no `AVLN` in the log; `dotnet test --solution
Enigma.MarkdownEditor.slnx -c Release` green; both `AssemblyInfo.cs` files read `1.7.0`.

**8 — Printed, never run** — merge, `git tag 1.7.0`, `git push origin 1.7.0`; `docs/RELEASE.md`
§2/§4/§5 deferred by decision, as in every release since 1.1.0.

## Acceptance criteria

- `<Version>` reads `1.7.0` in both csproj files, and nothing tracked outside history still claims
  1.6.0 as current.
- `Directory.Packages.props` carries exactly the three non-coupled bumps (or fewer, each held one
  recorded); the `--outdated` report is in the completion doc.
- The 1.7.0 MSI profile exists, is valid JSON, keeps the `upgradeCode` byte-for-byte, and carries a
  fresh `productId` differing from all eight earlier profiles'.
- `RELEASENOTES.md` has a 1.7.0 section above 1.6.0; every earlier section is unmodified.
- The README what's-new callout reads 1.7.0.
- Release build: **0 warnings**, no `AVLN` output, full suite green.
- No `.msi`, no `.tar.gz`, no `artifacts/` content left; no tag created and no push performed.

## Dependencies

Built **last**, after `FEATURE-4BB7`.

## Out of scope

Building the MSI or the tarball · the manual Linux verification checklist · a GitHub Release · README
screenshots · the Avalonia 12.1.3 / Enigma.Avalonia.Desktop 1.1.0 set · xunit.v3 4.x · a
`CHANGELOG.md` · retroactively tagging 1.4.1.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Which version? | 1.7.0 | A feature landed (the installer) beside two fixes; no stored file, setting or application behaviour changes incompatibly, and the old Linux install is migrated | 1.6.1 (a patch cannot carry a feature); 2.0.0 (the one removal is a packaging-script flag with a replacement — a MAJOR would signal a data or behaviour break that is not there) |
| Dependency refresh | Take Markdig 1.4.0, Hosting 10.0.12, coverlet 10.1.0; hold the coupled Avalonia/Enigma set and xunit.v3 4.x | `dotnet-release`'s routine-release rule; the 1.6.0 completion doc named Hosting 10.0.12 as worth taking with the next release | Taking nothing (1.5.0/1.6.0 precedent, decided in those interviews); taking the coupled set without an opt-in |
| Build the artifacts? | No — printed only | §2/§4/§5 need a Windows machine, PowerShell and a GitHub Release; every release since 1.1.0 was cut this way | Producing the tarball here |
| Where do the installer changes go in the notes? | *New Features* for the capability, *Changes* for the moved locations and `--prefix` | A user upgrading needs to know the install moved, separately from what is new | One combined entry |
