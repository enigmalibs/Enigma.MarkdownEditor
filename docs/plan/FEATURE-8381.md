# FEATURE-8381 — Release 1.6.0

**Status: DONE** · Single phase · Roadmap: `docs/roadmap.md`
**Type:** FEATURE · **Branch:** `feature/feature-8381-release-1-6-0`, cut from the run branch at build time.
**Run:** vibe/2026-09-10-title-colors-vim-release

## Objective

Cut version **1.6.0**, a SemVer MINOR carrying `FEATURE-E64A`, `FEATURE-A225` and `FEATURE-3505`: set
the version, write the release documentation, and commit the versioned MSI profile. **No artifacts
are built** — no MSI, no Linux tarball, no publish. The tag and push commands are printed for the
user to run, never run here.

Same shape as `FEATURE-F6A4` (1.5.0), `FEATURE-6A2F` (1.4.1), `FEATURE-53E4` (1.4.0), `FEATURE-449F`
(1.3.0), `FEATURE-2760` (1.2.0) and `FEATURE-6EF0` (1.1.0), **minus the dependency bump** 1.4.0
carried. Decided in the interview: this release moves no dependency.

## Version choice — why 1.6.0

SemVer MINOR. All three items add functionality and none removes, deprecates or changes existing
behaviour:

| Shipped since 1.5.0 | Kind |
|---|---|
| `FEATURE-E64A` — a new markdown file arrives with its own `#` title | feature |
| `FEATURE-A225-PHASE01` — folder colours in the workspaces file | feature |
| `FEATURE-A225-PHASE02` — folder colours in the tree | feature |
| `FEATURE-A225-PHASE03` — the same colour on the tab | feature |
| `FEATURE-3505` — vim `o`/`O` continue a list, task or quote item | feature |

Nothing migrates. `settings.json` gains no key. The workspaces file gains one **additive** key,
`FolderColors`, with `Version` still 1 — a file written by 1.5.0 loads unchanged and simply has no
colours, and a file written by 1.6.0 is still read by 1.5.0 (which ignores what it does not know).
A stored session is untouched. Creating a file now writes a heading where it used to write nothing,
which is an addition to a brand-new file rather than a change to an existing one.

If a quarantined item leaves any of the above unfinished, the table and the notes shrink to what
actually shipped — the version stays 1.6.0 as long as at least one feature landed, and this plan is
amended rather than quietly reinterpreted.

## Context & constraints

- **The version lives in three places** and they must move together: `<Version>` in
  `src/Enigma.MarkdownEditor.Desktop/…csproj:8`, `<Version>` in
  `src/Enigma.MarkdownEditor.Core/…csproj:13`, and the versioned MSI profile filename plus its
  `version` field. `build/Common.ps1` reads the Desktop csproj to name artifacts.
- **This is an app, not a library.** No `PackageId`, no `dotnet pack`, no `dotnet nuget push`.
  `docs/RELEASE.md` says so in its opening paragraph.
- **Tag convention is a bare `X.Y.Z`**, no `v` prefix (`docs/RELEASE.md` §3). `git tag --list` reads
  `1.0.0 1.1.0 1.2.0 1.3.0 1.4.0 1.5.0` — so `1.4.1` was never tagged (recorded by
  `docs/done/FEATURE-F6A4.md`, still the user's call) and `1.6.0` must not exist.
- **`RELEASENOTES.md` is the single release-notes source.** No `CHANGELOG.md` is added.
- **MSI profile filename convention:** `msiProfiles/Enigma.MarkdownEditor.Desktop.X.Y.Z.msipkg.json`
  — seven exist. Follow it, not the release skill's default name, and keep the repo's own field
  order (`schemaVersion`, `appName`, `version`, `productId`, `upgradeCode`, `manufacturer`, `scope`,
  `install`, `output`, `compression`, `controlPanel`, `shortcuts`) rather than the skill's template
  order.
- **README screenshots stay pending**, exactly as 1.3.0 … 1.5.0 left them: `docs/images/` absent, the
  *Captures pending* note untouched, recorded in the completion doc.
- **No dependency moves.** Run `dotnet list package --outdated` once and **record** its output in the
  completion doc; take nothing, whatever it reports. `xunit.v3` 4.0.0 is the standing entry, held for
  the fourth release running: a major test-framework bump is its own work item, not a passenger.
- **Every item above must be `DONE` first** — this item documents them and re-verifies the suite.

## Design

**1 — Version**

`<Version>1.6.0</Version>` in both `src/*` csproj files. Grep the repo for `1.5.0` afterwards: the
only remaining tracked hits must be the 1.5.0 MSI profile, the 1.5.0 release-notes section, the
roadmap's row for `FEATURE-F6A4`, and `docs/plan/` + `docs/done/` history.

**2 — MSI profile** (committed, never built)

`msiProfiles/Enigma.MarkdownEditor.Desktop.1.6.0.msipkg.json`, cloned from the 1.5.0 profile with
exactly two field changes, per the clone rule in `docs/RELEASE.md` §4:

| Field | Value |
|---|---|
| `upgradeCode` | **verbatim** — `a7b550b9-1338-458f-bf2b-66dcf1d25380`. Changing it would make Windows Installer treat 1.6.0 as an unrelated product instead of upgrading 1.5.0. |
| `productId` | a **new** GUID from `[guid]::NewGuid()` — never hand-written, and verified different from all seven earlier ones, `2385d51d-…` (1.5.0) included. |
| `version` | `1.6.0` |

Everything else carries over unchanged. A `diff` against the 1.5.0 profile shows exactly those two
lines plus the version, and the file parses as JSON.

**3 — `RELEASENOTES.md`**

A `# Enigma Markdown Editor v1.6.0 Release Notes` section **prepended** above the 1.5.0 one, written
from `docs/done/FEATURE-E64A.md`, `docs/done/FEATURE-A225-PHASE0*.md` and
`docs/done/FEATURE-3505.md`, not from memory. Sub-sections in the house order, only where non-empty:

- **New Features** — three entries: a new note arriving with its own title; folder colours and the
  matching tab colours; `o`/`O` continuing a list under vim.
- **Compatibility** — .NET 10, Avalonia 12.1.2, Windows x64 and Linux x64, both self-contained. The
  workspaces file gains one additive key and stays at `Version` 1, readable by 1.5.0 in both
  directions; no migration for `settings.json` or a stored session. No dependency moved.
- **Version** — `1.6.0`, a SemVer MINOR.

No *Fixes*, *Changes*, *Breaking Changes* or *Dependencies* sections: there is nothing to put in
them. The 1.5.0 and earlier sections stay byte-identical.

**4 — `README.md`**

What's-new callout → `> **What's new in 1.6.0** — …  See [RELEASENOTES.md](RELEASENOTES.md).`, one
or two sentences covering the three features. The feature bullets and the `Ctrl+`/vim reference
tables are updated by the items themselves through their own documentation sweeps; **verify rather
than repeat**, and fold in anything an item's sweep left short — that is exactly what 1.5.0's sweep
caught.

**5 — `docs/RELEASE.md`**

Read it in full against the repository as it then stands, and record "no change" or the small edit
rather than assuming. Two things are likely to be worth a §1 pre-flight line, both
platform-independent and neither coverable by a test: that a folder colour reaches the tab strip and
survives a restart, and that `o`/`O` continue a list in the running editor.

**6 — Pre-flight, run here**

- `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal` — **0 Warning(s)**.
- `grep -i AVLN` over that same log — expect no output. Non-incremental, so the XAML compiler
  actually runs rather than being skipped as up to date; this release changes XAML, so the check
  earns its place.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` — all green.
- Both `obj/Release/net10.0/…AssemblyInfo.cs` files read `1.6.0`.

**7 — Printed, never run**

```bash
git switch main && git pull      # after merging the release branch
git tag 1.6.0
git push origin 1.6.0
```

Plus a note that `docs/RELEASE.md` §2 (manual Linux verification), §4 (artifacts) and §5 (GitHub
Release) are **deferred by decision** for 1.6.0 — no artifacts are produced by this item.

## Acceptance criteria

- `<Version>` reads `1.6.0` in both csproj files, and nothing tracked outside `docs/plan/`,
  `docs/done/`, `RELEASENOTES.md`, `docs/roadmap.md` and the older MSI profiles still claims 1.5.0 as
  current.
- `Directory.Packages.props` is untouched; the `--outdated` report is in the completion doc.
- `msiProfiles/Enigma.MarkdownEditor.Desktop.1.6.0.msipkg.json` exists, is valid JSON, keeps the
  `upgradeCode` byte-for-byte, and carries a freshly generated `productId` differing from all seven
  earlier profiles'.
- `RELEASENOTES.md` has a 1.6.0 section above 1.5.0 with a *New Features* entry per shipped item, and
  every earlier section is unmodified.
- The README what's-new callout reads 1.6.0.
- Release build: **0 warnings**, no `AVLN` output, full test suite green.
- No `.msi`, no `.tar.gz`, no `artifacts/` content produced; no tag created and no push performed by
  this item.
- Completion doc `docs/done/FEATURE-8381.md` written, roadmap and this file flipped to `DONE`.

## Dependencies

Must be built **last**, after `FEATURE-E64A`, all three phases of `FEATURE-A225`, and
`FEATURE-3505`.

## Out of scope

Building the MSI · building the Linux tarball · the manual Linux verification checklist · publishing
a GitHub Release · README screenshots · any dependency bump · a `CHANGELOG.md` · retroactively
tagging 1.4.1.

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Which version? | 1.6.0 | Three items add functionality; nothing is removed, deprecated or changed in shape, and no stored file needs migrating | 1.5.1 (a patch cannot carry features); 2.0.0 (nothing breaks) |
| Bump any dependency? | No | A dependency move is its own decision, not a passenger on a feature release. `xunit.v3` 4.0.0 is a major test-framework bump held for the fourth release running | Taking `xunit.v3` 4.0.0 with the release |
| Build the artifacts? | No — printed only | `docs/RELEASE.md` §2/§4/§5 need a Linux session, a signed local build and a GitHub Release; all three are the user's steps, and every release since 1.1.0 has been cut this way | Producing the MSI here |
| A `Known issues` section? | No | The two open items — the clipboard not surviving exit, and 1.4.1 never being tagged — are pre-existing and each recorded where it belongs; a section for them would be a redesign of this plan | Listing them in the notes |
