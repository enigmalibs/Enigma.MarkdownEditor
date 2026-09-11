# FEATURE-53E4 — Release 1.4.0

**Status: DONE** · Single phase · Roadmap: `docs/roadmap.md`
Planned on `develop`; branches from `HEAD` at build time as `feature/feature-53e4-release-1-4-0`.

## Objective

Cut version **1.4.0**: set the version, refresh the two dependency groups agreed for this cycle,
write the release documentation, and commit the versioned MSI profile. **No artifacts are built** —
no MSI, no Linux tarball, no publish. The tag and push commands are printed for the user to run,
never run here.

Same shape as `FEATURE-449F` (1.3.0), `FEATURE-2760` (1.2.0), `FEATURE-6EF0` (1.1.0) and the second
phase of `FEATURE-5B28` (1.0.0), with one addition those four did not have: a dependency bump.
Deviating further from that shape is not this item's business.

## Version choice — why 1.4.0

SemVer MINOR. The bulk of the cycle is new vim functionality; nothing was removed and nothing changed
in a breaking way. A `settings.json` written by 1.3.0 loads unchanged — no key was added, renamed or
retyped this cycle — and so do a stored session and a workspace list.

| Shipped since 1.3.0 | Kind |
|---|---|
| `FEATURE-724A-PHASE01` — motions & editing extras | feature |
| `FEATURE-724A-PHASE02` — visual-mode commands | feature |
| `FEATURE-724A-PHASE03` — registers, repeat & macros | feature |
| `FEATURE-724A-PHASE04` — regex search, substitute & `:set` | feature |
| `FEATURE-724A-PHASE05` — screen motions & viewport scrolling | feature |
| `BUG-B549-PHASE01` — xUnit cancellation-token hint | internal (test-only) |
| `BUG-B549-PHASE02` — settings dialog chrome | change |

`BUG-B549-PHASE01` is a test-file suppression with no user-visible effect and **no release-note
entry**; it is recorded in its own completion doc and named here only so the cycle's ledger is
complete.

## Context & constraints

- **The version lives in three files** and they must move together: `<Version>` in
  `src/Enigma.MarkdownEditor.Desktop/…csproj:8`, `<Version>` in
  `src/Enigma.MarkdownEditor.Core/…csproj:13`, and the versioned MSI profile filename plus its
  `version` field. `build/Common.ps1` reads the Desktop csproj to name artifacts.
- **This is an app, not a library.** No `PackageId`, no `dotnet pack`, no `dotnet nuget push`, no
  pack-verify. `docs/RELEASE.md` says so in its opening paragraph.
- **Tag convention is a bare `X.Y.Z`**, no `v` prefix (`docs/RELEASE.md` §3). `1.0.0`, `1.1.0`,
  `1.2.0` and `1.3.0` exist; `1.4.0` must not.
- **`RELEASENOTES.md` is the single release-notes source.** No `CHANGELOG.md` is added.
- **The MSI profile filename convention in this repo is
  `Enigma.MarkdownEditor.Desktop.X.Y.Z.msipkg.json`** — four of them exist. That is the convention to
  follow, not the `*.msiprofile.*.json` default named in the release skill.
- **README screenshots stay pending.** `docs/images/` is empty; the *Captures pending* note and the
  commented-out image block are left exactly as they are, the §1 pre-flight checkbox stays unticked,
  and that is recorded in the completion doc rather than quietly ignored — as 1.3.0 did.
- **The vim key reference in the README is advertised as exhaustive** — *"This is the whole of it —
  the vim contract, exhaustive"* — with a §*Not implemented* list. Every `FEATURE-724A` phase updated
  it as it landed (each of the five commits touches `README.md`). **Verify, do not re-do:** check the
  reference and the *Not implemented* list against the engine as it then stands and fix only what the
  phases' own freshness sweeps missed.
- **`BUG-B549` must be `DONE` first** — this item documents its PHASE02 and re-verifies the suite
  against the bumped Avalonia set.

## Design

**1 — Version**

`<Version>1.4.0</Version>` in both `src/*` csproj files.

**2 — Dependencies** (`Directory.Packages.props`)

Two groups move; everything else is held deliberately.

| Package(s) | From | To | Note |
|---|---|---|---|
| `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`, `Avalonia.Headless`, `Avalonia.Headless.XUnit` | 12.1.1 | **12.1.2** | The version-coupled set — all six in one edit, never one at a time. A patch bump. |
| `coverlet.collector` | 6.0.4 | **10.0.1** | Test-only, `PrivateAssets="all"`; it is not redistributed, so it cannot affect shipped behaviour. |

Held, and said out loud in the notes rather than left silent:

- **`xunit.v3` stays at 3.2.2.** 4.0.0 is available; a major test-framework bump is its own work
  item, not a passenger on a release.
- `Avalonia.AvaloniaEdit` / `AvaloniaEdit.TextMate` 12.0.0, `Markdig` 1.3.2,
  `Microsoft.Extensions.Hosting` 10.0.11, `AvaloniaUI.DiagnosticsSupport` 2.2.3 and the
  `Enigma.*` 1.0.0 packages — `dotnet list package --outdated` reports nothing newer for any of them.

**Two things to check while doing it, both of which the build will answer:**

- **The version number appears inside comments**, and a stale comment is worse than no comment:
  `Directory.Packages.props` ("the whole group is held back until it moves as a unit", and the
  `Avalonia.Headless.XUnit` note asserting that *its 12.1.1 build depends on
  `xunit.v3.extensibility.core` 3.2.2*), `src/…Desktop.csproj:24` ("Version-coupled Avalonia set
  (12.1.1)") and `tests/…Desktop.UnitTests.csproj` ("Part of the version-coupled Avalonia set
  (12.1.1)"). Update each to 12.1.2.
- **`Avalonia.Headless.XUnit` 12.1.2 pins its own `xunit.v3.extensibility.core` floor.** The comment
  above records that 12.1.1's floor is exactly the 3.2.2 this repo pins. If 12.1.2 raises that floor,
  holding `xunit.v3` at 3.2.2 becomes a downgrade conflict at restore. **If that happens, hold the
  whole Avalonia set at 12.1.1** rather than dragging `xunit.v3` 4.0.0 into a release, and record the
  decision in the completion doc.
- **Fallback on `coverlet.collector`.** If 10.0.1 misbehaves under the Microsoft Testing Platform
  runner, revert that one pin to 6.0.4 and record it. It is a coverage collector; nothing in the
  release depends on it.

**3 — MSI profile** (committed, never built)

`msiProfiles/Enigma.MarkdownEditor.Desktop.1.4.0.msipkg.json`, cloned from the 1.3.0 profile with
exactly three changes, per the clone rule in `docs/RELEASE.md` §4:

| Field | Value |
|---|---|
| `upgradeCode` | **verbatim** from 1.3.0 — `a7b550b9-1338-458f-bf2b-66dcf1d25380`. Changing it would make Windows Installer treat 1.4.0 as an unrelated product and accumulate it beside 1.3.0 instead of upgrading it. |
| `productId` | a **new** GUID, generated with `uuidgen` (or `[guid]::NewGuid()`) — never hand-written, and verified different from all four earlier ones (`02ef086b-…`, `c749531f-…`, `a20d5fcf-…`, `4dec3add-…`). |
| `version` | `1.4.0` |

Everything else (`install`, `scope`, `compression`, `output`, `controlPanel`, `shortcuts`,
`releasePath`, `manufacturer`) carries over unchanged — the TFM did not move.

**4 — `RELEASENOTES.md`**

A `# Enigma Markdown Editor v1.4.0 Release Notes` section **prepended** above the 1.3.0 one, with
sub-sections in the house order, only where non-empty. Write each entry from the phase's own
completion doc in `docs/done/`, not from memory:

- **New Features** — the vim work is the whole headline, and it is large enough to deserve one entry
  per phase rather than a single paragraph: the extra motions and editing commands (PHASE01); the
  visual-mode command set (PHASE02); registers, `.` repeat and `q`/`@` macros (PHASE03); regex
  search, `:s` substitution and `:set` (PHASE04); and the screen motions with viewport scrolling —
  `Ctrl+D`/`Ctrl+U`, `Ctrl+F`/`Ctrl+B`, `zt`/`zz`, `H`/`L`, `Ctrl+E` (PHASE05). Where a phase
  shortened the README's *Not implemented* list, say so — the same framing 1.3.0 used, since it is
  what makes the list meaningful.
- **Changes** — **the settings dialog is tidier**: the heading icon is gone (the title carries it),
  the padding is applied once rather than twice, and the groups now sit on a darker well so they read
  as raised rather than sunken, in both themes. Note the practical part: the dialog's content was
  being clipped against the host's width cap and no longer is. Nothing a setting does changed — no
  group or row was added, removed or reordered.
- **Compatibility** — .NET 10, **Avalonia 12.1.2**, Windows x64 and Linux x64, both self-contained.
  The TFM set did not move. An existing `settings.json` needs **no migration** — no key was added,
  renamed or retyped this cycle.
- **Dependencies** — the coupled `Avalonia.*` set 12.1.1 → 12.1.2 and `coverlet.collector` 6.0.4 →
  10.0.1 (test-only, not shipped); `xunit.v3` deliberately held at 3.2.2.
- **Version** — `1.4.0`.

**5 — `README.md`**

- What's-new callout → `> **What's new in 1.4.0** — …  See [RELEASENOTES.md](RELEASENOTES.md).`,
  leading with the vim work (registers, macros, `:s`, the screen motions) and closing on the tidier
  settings dialog.
- Vim key reference: verify the five `FEATURE-724A` phases left it correct and exhaustive — the
  motion, operator and ex-command tables carrying what landed, and §*Not implemented* no longer
  naming any of it. Fix only what is still wrong; do not duplicate rows.
- Screenshots section untouched.

**6 — `docs/RELEASE.md`**

Read it against the repository as it then stands and correct anything the seven devs made false. The
§2 vim smoke set in particular now under-covers what the engine does — extend it only if a line in it
became *wrong*; growing it is `FEATURE-724A`'s business, not this item's. Nothing else is expected to
need changing, so the likely outcome is a small edit or none — which is recorded rather than assumed.

**7 — Pre-flight, run here**

- `dotnet build Enigma.MarkdownEditor.slnx -c Release` — the bar is **0 Warning(s)**. This is also
  the gate on both dependency bumps.
- `dotnet build Enigma.MarkdownEditor.slnx -c Release -v normal | grep -i AVLN` — expect no output.
  `AVLN*` codes are emitted from an MSBuild task and are **not** promoted by
  `TreatWarningsAsErrors`, so a green build can still hide a broken binding — and both an Avalonia
  bump and `BUG-B549-PHASE02`'s new view root are exactly the changes that class of defect hides in.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` — the `--solution` flag is not
  optional under the Microsoft Testing Platform runner pinned in `global.json`.

**8 — Printed, never run**

```bash
git switch main && git pull      # after merging the release branch
git tag 1.4.0
git push origin 1.4.0
```

Plus a note that `docs/RELEASE.md` §2 (manual Linux verification), §4 (artifacts) and §5 (GitHub
Release) are **deferred by decision** for 1.4.0 — no artifacts are produced by this item.

## Acceptance criteria

- `<Version>` reads `1.4.0` in both csproj files, and nowhere in the repo still claims 1.3.0 as
  current.
- `Directory.Packages.props` pins all six `Avalonia*` packages at 12.1.2 and `coverlet.collector` at
  10.0.1, with `xunit.v3` still at 3.2.2 — and every comment naming a version number reads 12.1.2.
  If the set had to be held at 12.1.1 instead, the reason is in the completion doc.
- `msiProfiles/Enigma.MarkdownEditor.Desktop.1.4.0.msipkg.json` exists, is valid JSON, keeps the
  1.3.0 `upgradeCode` byte-for-byte, and carries a freshly generated `productId` differing from all
  four earlier profiles'.
- `RELEASENOTES.md` has a 1.4.0 section above 1.3.0 covering the six user-facing devs, with the
  1.3.0, 1.2.0, 1.1.0 and 1.0.0 sections unmodified.
- The README what's-new callout reads 1.4.0, and the vim key reference is exhaustive against the
  engine — including the *Not implemented* list.
- Release build: **0 warnings**, no `AVLN` output, full test suite green.
- No `.msi`, no `.tar.gz`, no `artifacts/` content produced; no tag created and no push performed by
  this item.

## Dependencies

Must be built **after both phases of `BUG-B549`** — it documents PHASE02 and re-verifies the suite
over the bumped Avalonia set. `FEATURE-724A`'s five phases are already `DONE`.

## Out of scope

Building the MSI · building the Linux tarball · the manual Linux verification checklist · publishing
a GitHub Release · README screenshots · `xunit.v3` 4.0.0 · any dependency bump beyond the two groups
above · a `CHANGELOG.md`.
