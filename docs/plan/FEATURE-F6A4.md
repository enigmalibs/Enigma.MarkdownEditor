# FEATURE-F6A4 — Release 1.5.0

**Status: DONE** · Single phase · Roadmap: `docs/roadmap.md`
Planned on `develop`; branches from `HEAD` at build time as `feature/feature-f6a4-release-1-5-0`.

## Objective

Cut version **1.5.0**, a SemVer MINOR carrying `FEATURE-4797` and nothing else: set the version,
write the release documentation, and commit the versioned MSI profile. **No artifacts are built** —
no MSI, no Linux tarball, no publish. The tag and push commands are printed for the user to run,
never run here.

Same shape as `FEATURE-6A2F` (1.4.1), `FEATURE-53E4` (1.4.0), `FEATURE-449F` (1.3.0), `FEATURE-2760`
(1.2.0) and `FEATURE-6EF0` (1.1.0), **minus the dependency bump** 1.4.0 carried. Decided in the
interview: this release moves no dependency.

## Version choice — why 1.5.0

SemVer MINOR. `FEATURE-4797` adds functionality — document-wide select-all, a plain-text copy and a
rich-text clipboard flavour in the preview pane — and removes nothing, deprecates nothing and changes
no existing behaviour: per-paragraph selection and its `Ctrl+C` work exactly as they did in 1.4.1. A
`settings.json`, a stored session and a workspace list written by 1.4.1 load unchanged; no key was
added, renamed or retyped.

| Shipped since 1.4.1 | Kind |
|---|---|
| `FEATURE-4797-PHASE01` — preview select-all & plain-text copy | feature |
| `FEATURE-4797-PHASE02` — rich-text (HTML) clipboard flavour | feature |

If further items land before this one is built, they join the table and the release notes — and if
any of them breaks compatibility, the version becomes 2.0.0 and this plan is amended first, not
quietly.

## Context & constraints

- **The version lives in three files** and they must move together: `<Version>` in
  `src/Enigma.MarkdownEditor.Desktop/…csproj:8`, `<Version>` in
  `src/Enigma.MarkdownEditor.Core/…csproj:13`, and the versioned MSI profile filename plus its
  `version` field. `build/Common.ps1` reads the Desktop csproj to name artifacts.
- **This is an app, not a library.** No `PackageId`, no `dotnet pack`, no `dotnet nuget push`.
  `docs/RELEASE.md` says so in its opening paragraph.
- **Tag convention is a bare `X.Y.Z`**, no `v` prefix (`docs/RELEASE.md` §3). `1.0.0` … `1.4.1`
  exist; `1.5.0` must not.
- **`RELEASENOTES.md` is the single release-notes source.** No `CHANGELOG.md` is added.
- **MSI profile filename convention:** `msiProfiles/Enigma.MarkdownEditor.Desktop.X.Y.Z.msipkg.json`
  — six exist. Follow it, not the release skill's default name.
- **README screenshots stay pending**, exactly as 1.3.0, 1.4.0 and 1.4.1 left them: `docs/images/`
  empty, the *Captures pending* note untouched, recorded in the completion doc.
- **No dependency moves.** Run `dotnet list package --outdated` once and **record** its output in the
  completion doc; take nothing, whatever it reports. A bump is its own decision, not a passenger on a
  feature release.
- **Both phases of `FEATURE-4797` must be `DONE` first** — this item documents them and re-verifies
  the suite.

## Design

**1 — Version**

`<Version>1.5.0</Version>` in both `src/*` csproj files. Grep the repo for `1.4.1` afterwards: the
only remaining hits must be the 1.4.1 MSI profile, the 1.4.1 release-notes section, and
`docs/plan/` + `docs/done/` history.

**2 — MSI profile** (committed, never built)

`msiProfiles/Enigma.MarkdownEditor.Desktop.1.5.0.msipkg.json`, cloned from the 1.4.1 profile with
exactly two field changes, per the clone rule in `docs/RELEASE.md` §4:

| Field | Value |
|---|---|
| `upgradeCode` | **verbatim** — `a7b550b9-1338-458f-bf2b-66dcf1d25380`. Changing it would make Windows Installer treat 1.5.0 as an unrelated product instead of upgrading 1.4.1. |
| `productId` | a **new** GUID from `[guid]::NewGuid()` (or `uuidgen`) — never hand-written, and verified different from all six earlier ones, `4c91f181-…` (1.4.1) included. |
| `version` | `1.5.0` |

Everything else carries over unchanged. A `diff` against the 1.4.1 profile shows exactly those two
lines plus the version.

**3 — `RELEASENOTES.md`**

A `# Enigma Markdown Editor v1.5.0 Release Notes` section **prepended** above the 1.4.1 one, written
from `docs/done/FEATURE-4797-PHASE01.md` and `docs/done/FEATURE-4797-PHASE02.md`, not from memory.
Sub-sections in the house order, only where non-empty:

- **New Features** — the preview pane can be selected and copied. `Ctrl+A` selects the whole rendered
  note and `Ctrl+C` copies it; there is a right-click menu with *Copy* and *Select all*, and a copy
  button in the pane's header. What lands on the clipboard is the note as it reads — no `#`, no `**`
  — with list markers kept, code verbatim, table rows tab-separated so a paste into a spreadsheet
  lands one cell per column, and task items keeping their `[ ]` / `[x]`. A whole-pane copy also
  carries a rich-text flavour, so pasting into Word or Outlook keeps the headings, bold, links and
  tables while pasting into a plain editor gets the text. Selecting inside a single paragraph and
  copying that works as before.
- **Compatibility** — .NET 10, Avalonia 12.1.2, Windows x64 and Linux x64, both self-contained. No
  migration for `settings.json`, sessions or workspaces. No dependency moved.
- **Version** — `1.5.0`, a SemVer MINOR.

No *Fixes*, *Changes* or *Dependencies* sections: there is nothing to put in them. The 1.4.1 and
earlier sections stay byte-identical.

**4 — `README.md`**

What's-new callout → `> **What's new in 1.5.0** — …  See [RELEASENOTES.md](RELEASENOTES.md).`, one
sentence on the preview being copyable. The Preview bullets and the `Ctrl+` reference table are
already updated by `FEATURE-4797` itself; verify rather than repeat.

**5 — `docs/RELEASE.md`**

Read it against the repository as it then stands; nothing is expected to need changing. Record "no
change" or the small edit, rather than assuming.

**6 — Pre-flight, run here**

- `dotnet build Enigma.MarkdownEditor.slnx -c Release` — **0 Warning(s)**.
- `dotnet build Enigma.MarkdownEditor.slnx -c Release -v normal | grep -i AVLN` — expect no output.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` — all green.

**7 — Printed, never run**

```bash
git switch main && git pull      # after merging the release branch
git tag 1.5.0
git push origin 1.5.0
```

Plus a note that `docs/RELEASE.md` §2 (manual Linux verification), §4 (artifacts) and §5 (GitHub
Release) are **deferred by decision** for 1.5.0 — no artifacts are produced by this item.

## Acceptance criteria

- `<Version>` reads `1.5.0` in both csproj files, and nowhere in the repo still claims 1.4.1 as
  current.
- `Directory.Packages.props` is untouched; the `--outdated` report is in the completion doc.
- `msiProfiles/Enigma.MarkdownEditor.Desktop.1.5.0.msipkg.json` exists, is valid JSON, keeps the
  `upgradeCode` byte-for-byte, and carries a freshly generated `productId` differing from all six
  earlier profiles'.
- `RELEASENOTES.md` has a 1.5.0 section above 1.4.1 with a New Features entry for `FEATURE-4797`,
  and every earlier section is unmodified.
- The README what's-new callout reads 1.5.0.
- Release build: **0 warnings**, no `AVLN` output, full test suite green.
- No `.msi`, no `.tar.gz`, no `artifacts/` content produced; no tag created and no push performed by
  this item.
- Completion doc `docs/done/FEATURE-F6A4.md` written, roadmap and this file flipped to `DONE`.

## Dependencies

Must be built **after both phases of `FEATURE-4797` are `DONE`**.

## Out of scope

Building the MSI · building the Linux tarball · the manual Linux verification checklist · publishing
a GitHub Release · README screenshots · any dependency bump · a `CHANGELOG.md`.
