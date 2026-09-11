# FEATURE-449F — Release 1.3.0

**Status: DONE** · Single phase · Roadmap: `docs/roadmap.md`
Planned on `develop`; branches from `HEAD` at build time as `feature/feature-449f-release-1-3-0`.

## Objective

Cut version **1.3.0**: set the version, write the release documentation, and commit the versioned MSI
profile. **No artifacts are built** — no MSI, no Linux tarball, no publish. The tag and push commands
are printed for the user to run, never run here.

Same shape as `FEATURE-2760` (1.2.0), `FEATURE-6EF0` (1.1.0) and the second phase of `FEATURE-5B28`
(1.0.0). Deviating from that shape is not this item's business.

## Version choice — why 1.3.0

SemVer MINOR. Two of the six devs shipped since 1.2.0 are new functionality — the settings dialog and
the two vim additions — and nothing was removed or changed in a breaking way. A `settings.json`
written by 1.2.0 loads unchanged: no key was added, renamed or retyped this cycle.

| Shipped since 1.2.0 | Kind |
|---|---|
| `BUG-8F2A-PHASE01` — thin scrollbar on the tab strip | fix |
| `BUG-8F2A-PHASE02` — preview checkbox alignment & size | fix |
| `FEATURE-414B-PHASE01` — preview font labels aligned, editor default lowered | fix + change |
| `FEATURE-414B-PHASE02` — settings shown as a content dialog | feature |
| `FEATURE-23AB-PHASE01` — vim `f` `F` `t` `T` and `;` `,` | feature |
| `FEATURE-23AB-PHASE02` — vim text objects | feature |

The editor's default font size dropping from 18 to 16 is the one entry that changes what an existing
user sees — but only for a user who never set the value, since a stored setting wins. It goes in the
notes as a change, not buried in the fixes.

## Context & constraints

- **The version lives in three files** and they must move together: `<Version>` in
  `src/Enigma.MarkdownEditor.Desktop/…csproj:8`, `<Version>` in
  `src/Enigma.MarkdownEditor.Core/…csproj:13`, and the versioned MSI profile filename plus its
  `version` field. `build/Common.ps1` reads the Desktop csproj to name artifacts.
- **This is an app, not a library.** No `PackageId`, no `dotnet pack`, no `dotnet nuget push`, no
  pack-verify. `docs/RELEASE.md` says so in its opening paragraph.
- **Tag convention is a bare `X.Y.Z`**, no `v` prefix (`docs/RELEASE.md` §3). `1.0.0`, `1.1.0` and
  `1.2.0` exist; `1.3.0` must not.
- **`RELEASENOTES.md` is the single release-notes source.** No `CHANGELOG.md` is added.
- **Dependencies are not refreshed this release.** The `Avalonia.*` set is version-coupled and stays
  at 12.1.1; AvaloniaEdit stays at 12.0.0; Markdig at 1.3.2; xunit.v3 at 3.2.2. Stated in the notes
  rather than left silent. No outdated-check is run — the decision not to bump does not depend on
  its output.
- **README screenshots stay pending.** The *Captures pending* note and the commented-out image block
  are left exactly as they are; the §1 pre-flight checkbox stays unticked and that is recorded in the
  completion doc rather than quietly ignored.
- **The vim key reference in the README is advertised as exhaustive** — *"This is the whole of it —
  the vim contract, exhaustive"* — and its §*Not implemented* list names text objects. `FEATURE-23AB`
  PHASE02's own acceptance criteria already require both to be corrected. **Verify, do not
  re-do:** check the reference against the engine as it then stands and fix only what PHASE02's own
  freshness sweep missed.

## Design

**1 — Version**

`<Version>1.3.0</Version>` in both `src/*` csproj files.

**2 — MSI profile** (committed, never built)

`msiProfiles/Enigma.MarkdownEditor.Desktop.1.3.0.msipkg.json`, cloned from the 1.2.0 profile with
exactly three changes, per the clone rule in `docs/RELEASE.md` §4:

| Field | Value |
|---|---|
| `upgradeCode` | **verbatim** from 1.2.0 — `a7b550b9-1338-458f-bf2b-66dcf1d25380`. Changing it would make Windows Installer treat 1.3.0 as an unrelated product and accumulate it beside 1.2.0 instead of upgrading it. |
| `productId` | a **new** GUID, generated with `[guid]::NewGuid()` — never hand-written, and verified different from 1.0.0's, 1.1.0's and 1.2.0's (`a20d5fcf-77f5-4089-8713-4ba17e673ecf`). |
| `version` | `1.3.0` |

Everything else (`install`, `scope`, `compression`, `output`, `controlPanel`, `shortcuts`,
`releasePath`, `manufacturer`) carries over unchanged — the TFM did not move.

**3 — `RELEASENOTES.md`**

A `# Enigma Markdown Editor v1.3.0 Release Notes` section **prepended** above the 1.2.0 one, with
sub-sections in the house order, only where non-empty:

- **New Features** — the vim additions are the headline. `f` `F` `t` `T` with `;` / `,` to repeat,
  countable and usable behind `d` `y` `c`; and text objects — `iw` `aw`, the four bracket pairs and
  the three quote characters, with `i`/`a` for inside and around, in normal and visual mode. Both
  were on the engine's published *Not implemented* list since 1.0.0, so the note should say so:
  this release shortens that list rather than adding to an open-ended one. Second entry: **Settings
  is now a dialog** rather than the last separate window in the application.
- **Changes** — the editor's default font size is 16 rather than 18, for a first run or a settings
  file that never carried the key; the preview settings rows are labelled like the editor's
  (*Font family*, *Font size*, *Code font family*).
- **Fixes** — the tab strip's scrollbar is thin and no longer eats a row of height; preview
  checkboxes line up with their text and are sized with it.
- **Compatibility** — unchanged: .NET 10, Avalonia 12.1.1, Windows x64 and Linux x64, both
  self-contained. The TFM set did not move. An existing `settings.json` needs **no migration** — no
  key was added, renamed or retyped this cycle.
- **Dependencies** — none changed; the coupled `Avalonia.*` set was deliberately held at 12.1.1.
- **Version** — `1.3.0`.

**4 — `README.md`**

- What's-new callout → `> **What's new in 1.3.0** — …  See [RELEASENOTES.md](RELEASENOTES.md).`,
  leading with the vim keys.
- Vim key reference: verify `FEATURE-23AB` left it correct and exhaustive — the *Motions* table
  carrying `f` `F` `t` `T` `;` `,`, the *Operators* table carrying the text objects, and
  §*Not implemented* no longer naming them. Fix only what is still wrong; do not duplicate rows.
- Screenshots section untouched.

**5 — `docs/RELEASE.md`**

Read it against the repository as it then stands and correct anything the six devs made false.
Nothing is expected to need changing, so the likely outcome is no edit — which is recorded rather
than assumed.

**6 — Pre-flight, run here**

- `dotnet build Enigma.MarkdownEditor.slnx -c Release` — the bar is **0 Warning(s)**.
- `dotnet build Enigma.MarkdownEditor.slnx -c Release -v normal | grep -i AVLN` — expect no output.
  `AVLN*` codes are emitted from an MSBuild task and are **not** promoted by `TreatWarningsAsErrors`,
  so a green build can still hide a broken binding — and `FEATURE-414B-PHASE02` moved every settings
  row into a new view, which is exactly the change that class of defect hides in.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` — the `--solution` flag is not
  optional under the Microsoft Testing Platform runner pinned in `global.json`.

**7 — Printed, never run**

```bash
git switch main && git pull      # after merging the release branch
git tag 1.3.0
git push origin 1.3.0
```

Plus a note that `docs/RELEASE.md` §2 (manual Linux verification), §4 (artifacts) and §5 (GitHub
Release) are **deferred by decision** for 1.3.0 — no artifacts are produced by this item.

## Acceptance criteria

- `<Version>` reads `1.3.0` in both csproj files, and nowhere in the repo still claims 1.2.0 as
  current.
- `msiProfiles/Enigma.MarkdownEditor.Desktop.1.3.0.msipkg.json` exists, is valid JSON, keeps the
  1.2.0 `upgradeCode` byte-for-byte, and carries a freshly generated `productId` differing from all
  three earlier profiles'.
- `RELEASENOTES.md` has a 1.3.0 section above 1.2.0 covering all six devs, with the 1.2.0, 1.1.0 and
  1.0.0 sections unmodified.
- The README what's-new callout reads 1.3.0, and the vim key reference is exhaustive against the
  engine — including the corrected *Not implemented* list.
- Release build: **0 warnings**, no `AVLN` output, full test suite green.
- No `.msi`, no `.tar.gz`, no `artifacts/` content produced; no tag created and no push performed by
  this item.

## Dependencies

Must be built **after both phases of `FEATURE-23AB`** — it documents them. `BUG-8F2A` and
`FEATURE-414B` are already `DONE`.

## Out of scope

Building the MSI · building the Linux tarball · the manual Linux verification checklist · publishing
a GitHub Release · README screenshots · any dependency version bump or outdated-check · a
`CHANGELOG.md`.
