# FEATURE-6A2F — Release 1.4.1

**Status: DONE** · Single phase · Roadmap: `docs/roadmap.md`
Planned on `main`; branches from `HEAD` at build time as `feature/feature-6a2f-release-1-4-1`.

## Objective

Cut version **1.4.1**, a SemVer PATCH carrying `BUG-38F5` and nothing else: set the version, write the
release documentation, and commit the versioned MSI profile. **No artifacts are built** — no MSI, no
Linux tarball, no publish. The tag and push commands are printed for the user to run, never run here.

Same shape as `FEATURE-53E4` (1.4.0), `FEATURE-449F` (1.3.0), `FEATURE-2760` (1.2.0) and `FEATURE-6EF0`
(1.1.0), **minus the dependency bump** 1.4.0 carried. A patch release moves no dependency.

## Version choice — why 1.4.1

SemVer PATCH. One defect fix, no new feature, nothing removed, nothing changed shape. A
`settings.json`, a stored session and a workspace list written by 1.4.0 load unchanged.

| Shipped since 1.4.0 | Kind |
|---|---|
| `BUG-38F5` — save leaves the dirty dot in vim | fix |

If further items land before this one is built, they join the table and the release notes — and if
any of them is a feature, the version becomes 1.5.0 and this plan is amended first, not quietly.

## Context & constraints

- **The version lives in three files** and they must move together: `<Version>` in
  `src/Enigma.MarkdownEditor.Desktop/…csproj:8`, `<Version>` in
  `src/Enigma.MarkdownEditor.Core/…csproj:13`, and the versioned MSI profile filename plus its
  `version` field. `build/Common.ps1` reads the Desktop csproj to name artifacts.
- **This is an app, not a library.** No `PackageId`, no `dotnet pack`, no `dotnet nuget push`.
  `docs/RELEASE.md` says so in its opening paragraph.
- **Tag convention is a bare `X.Y.Z`**, no `v` prefix (`docs/RELEASE.md` §3). `1.0.0` … `1.4.0`
  exist; `1.4.1` must not.
- **`RELEASENOTES.md` is the single release-notes source.** No `CHANGELOG.md` is added.
- **MSI profile filename convention:** `msiProfiles/Enigma.MarkdownEditor.Desktop.X.Y.Z.msipkg.json`
  — five exist. Follow it, not the release skill's default name.
- **README screenshots stay pending**, exactly as 1.3.0 and 1.4.0 left them: `docs/images/` empty,
  the *Captures pending* note untouched, recorded in the completion doc.
- **No dependency moves.** Run `dotnet list package --outdated` once and **record** its output in the
  completion doc; take nothing, whatever it reports. A bump is its own decision, not a passenger on a
  patch release.
- **`BUG-38F5` must be `DONE` first** — this item documents it and re-verifies the suite.

## Design

**1 — Version**

`<Version>1.4.1</Version>` in both `src/*` csproj files. Grep the repo for `1.4.0` afterwards: the
only remaining hits must be the 1.4.0 MSI profile, the 1.4.0 release-notes section, and
`docs/plan/` + `docs/done/` history.

**2 — MSI profile** (committed, never built)

`msiProfiles/Enigma.MarkdownEditor.Desktop.1.4.1.msipkg.json`, cloned from the 1.4.0 profile with
exactly two field changes, per the clone rule in `docs/RELEASE.md` §4:

| Field | Value |
|---|---|
| `upgradeCode` | **verbatim** — `a7b550b9-1338-458f-bf2b-66dcf1d25380`. Changing it would make Windows Installer treat 1.4.1 as an unrelated product instead of upgrading 1.4.0. |
| `productId` | a **new** GUID from `[guid]::NewGuid()` (or `uuidgen`) — never hand-written, and verified different from all five earlier ones (`02ef086b-…`, `c749531f-…`, `a20d5fcf-…`, `4dec3add-…`, `2193b810-…`). |
| `version` | `1.4.1` |

Everything else carries over unchanged. A `diff` against the 1.4.0 profile shows exactly those two
lines.

**3 — `RELEASENOTES.md`**

A `# Enigma Markdown Editor v1.4.1 Release Notes` section **prepended** above the 1.4.0 one, written
from `docs/done/BUG-38F5.md`, not from memory. Sub-sections in the house order, only where non-empty:

- **Fixes** — one entry: saving while vim was in insert mode — `Ctrl+S`, autosave, or `:w` after
  the session — left the tab's unsaved dot showing and made closing the tab ask about changes that
  were already on disk; a save in normal mode after such a session could leave it too. The file was
  always written; only the flag was wrong. The dot now clears on every save and stays clear across
  `Esc`. Also mention the second thing the fix closed: an edit landing during the disk write no
  longer marks the buffer clean.
- **Compatibility** — .NET 10, Avalonia 12.1.2, Windows x64 and Linux x64, both self-contained. No
  migration for `settings.json`, sessions or workspaces.
- **Version** — `1.4.1`, a SemVer PATCH.

No *New Features*, *Changes* or *Dependencies* sections: there is nothing to put in them. The 1.4.0
and earlier sections stay byte-identical.

**4 — `README.md`**

What's-new callout → `> **What's new in 1.4.1** — …  See [RELEASENOTES.md](RELEASENOTES.md).`, one
sentence on the dot fix. Vim key reference and screenshots section untouched.

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
git tag 1.4.1
git push origin 1.4.1
```

Plus a note that `docs/RELEASE.md` §2 (manual Linux verification), §4 (artifacts) and §5 (GitHub
Release) are **deferred by decision** for 1.4.1 — no artifacts are produced by this item.

## Acceptance criteria

- `<Version>` reads `1.4.1` in both csproj files, and nowhere in the repo still claims 1.4.0 as
  current.
- `Directory.Packages.props` is untouched; the `--outdated` report is in the completion doc.
- `msiProfiles/Enigma.MarkdownEditor.Desktop.1.4.1.msipkg.json` exists, is valid JSON, keeps the
  `upgradeCode` byte-for-byte, and carries a freshly generated `productId` differing from all five
  earlier profiles'.
- `RELEASENOTES.md` has a 1.4.1 section above 1.4.0 with a Fixes entry for `BUG-38F5`, and every
  earlier section is unmodified.
- The README what's-new callout reads 1.4.1.
- Release build: **0 warnings**, no `AVLN` output, full test suite green.
- No `.msi`, no `.tar.gz`, no `artifacts/` content produced; no tag created and no push performed by
  this item.
- Completion doc `docs/done/FEATURE-6A2F.md` written, roadmap and this file flipped to `DONE`.

## Dependencies

Must be built **after `BUG-38F5` is `DONE`**.

## Out of scope

Building the MSI · building the Linux tarball · the manual Linux verification checklist · publishing
a GitHub Release · README screenshots · any dependency bump · a `CHANGELOG.md`.
