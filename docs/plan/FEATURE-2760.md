# FEATURE-2760 — Release 1.2.0

**Status: DONE** · Single phase · Roadmap: `docs/roadmap.md`
Planned on `develop`; branches from `HEAD` at build time as `feature/feature-2760-release-1-2-0`.

## Objective

Cut version **1.2.0**: set the version, write the release documentation, and commit the versioned MSI profile. **No artifacts are built** — no MSI, no Linux tarball, no publish. The tag and push commands are printed for the user to run, never run here.

Same shape as `FEATURE-6EF0` (1.1.0) and the second phase of `FEATURE-5B28` (1.0.0). Deviating from that shape is not this item's business.

## Version choice — why 1.2.0

SemVer MINOR. Everything since 1.1.0 is `BUG-7AB2`: four bug fixes plus one **new setting**. A release that is nothing but fixes would be PATCH (1.1.1), but `PreviewCodeFontSize` is additive new functionality with a new row in the settings window and a new key in `settings.json`, which is what makes this MINOR. Nothing was removed and nothing changed in a breaking way — an existing `settings.json` with no `PreviewCodeFontSize` key loads on the default.

| Shipped since 1.1.0 | Kind |
|---|---|
| `BUG-7AB2-PHASE01` — closing a tab leaves a live editor | fix |
| `BUG-7AB2-PHASE02` — dirty flag missed in insert mode | fix |
| `BUG-7AB2-PHASE03` — process hangs on exit | fix |
| `BUG-7AB2-PHASE04` — editor font family never applied | fix |
| `BUG-7AB2-PHASE05` — preview code font size & weight, `PreviewCodeFontSize` | fix + additive |

## Context & constraints

- **The version lives in three files** and they must move together: `<Version>` in `src/Enigma.MarkdownEditor.Desktop/…csproj`, `<Version>` in `src/Enigma.MarkdownEditor.Core/…csproj`, and the versioned MSI profile filename plus its `version` field. `build/Common.ps1` reads the Desktop csproj to name artifacts.
- **This is an app, not a library.** No `PackageId`, no `dotnet pack`, no `dotnet nuget push`, no pack-verify. `docs/RELEASE.md` says so in its opening paragraph.
- **Tag convention is a bare `X.Y.Z`**, no `v` prefix (`docs/RELEASE.md` §3). `1.1.0` should be the only existing tag; if the repository still has none, that is a note for the completion doc, not a blocker.
- **`RELEASENOTES.md` is the single release-notes source.** No `CHANGELOG.md` is added.
- **Dependencies are not refreshed this release.** The `Avalonia.*` set is version-coupled and is deliberately held at 12.1.1; AvaloniaEdit stays at 12.0.0. Stated in the notes rather than left silent.
- **README screenshots stay pending.** The *Captures pending* note and the commented-out image block are left exactly as they are; the §1 pre-flight checkbox stays unticked for 1.2.0 and that is recorded in the completion doc rather than quietly ignored.
- **Three of the five fixes are only verifiable by hand** — the exit hang, the editor font family and the code weight. Their manual verification belongs to their own phases' completion docs; this item does not re-verify them, it documents them.

## Design

**1 — Version**

`<Version>1.2.0</Version>` in both `src/*` csproj files.

**2 — MSI profile** (committed, never built)

`msiProfiles/Enigma.MarkdownEditor.Desktop.1.2.0.msipkg.json`, cloned from the 1.1.0 profile with exactly three changes, per the clone rule in `docs/RELEASE.md` §4:

| Field | Value |
|---|---|
| `upgradeCode` | **verbatim** from 1.1.0 — `a7b550b9-1338-458f-bf2b-66dcf1d25380`. Changing it would make Windows Installer treat 1.2.0 as an unrelated product and accumulate it beside 1.1.0 instead of upgrading it. |
| `productId` | a **new** GUID, generated with `[guid]::NewGuid()` — never hand-written, and verified different from both 1.0.0's and 1.1.0's. |
| `version` | `1.2.0` |

Everything else (`install`, `scope`, `compression`, `output`, `controlPanel`, `shortcuts`, `releasePath`) carries over unchanged — the TFM did not move.

**3 — `RELEASENOTES.md`**

A `# Enigma Markdown Editor v1.2.0 Release Notes` section **prepended** above the 1.1.0 one, with sub-sections in the house order, only where non-empty:

- **Fixes** — closing a tab no longer leaves a dead editor behind (and no longer raises an error popup on the next click); edits made in vim insert mode are now recognised straight away, so the tab shows its unsaved marker and closing it asks before discarding work; the application now actually exits when its window is closed; the editor font family chosen in Settings is applied.
- **New Features** — **Code font size** in the preview settings: code spans and fenced blocks are set at a size of your choosing rather than one point under the prose size, and code in the preview is no longer rendered bold.
- **Compatibility** — unchanged: .NET 10, Avalonia 12.1.1, Windows x64 and Linux x64, both self-contained. The TFM set did not move. An existing `settings.json` needs no migration — the new `PreviewCodeFontSize` key defaults to 14 when absent.
- **Dependencies** — none changed; the coupled `Avalonia.*` set was deliberately held at 12.1.1.
- **Version** — `1.2.0`.

Worth stating plainly in the fixes: the unsaved-changes bug could **lose work**, since a tab edited in insert mode closed without asking. That is the one entry a user needs to read.

**4 — `README.md`**

- What's-new callout → `> **What's new in 1.2.0** — …  See [RELEASENOTES.md](RELEASENOTES.md).`
- The preview settings list gains **Code font size** if `BUG-7AB2-PHASE05`'s own freshness sweep did not already add it (check first; do not duplicate the bullet).
- Screenshots section untouched.

**5 — `docs/RELEASE.md`**

Read it against the repository as it now stands and correct anything the five fixes made false. Nothing is expected to need changing — §6's About-box paragraph was already rewritten for 1.1.0 — so the likely outcome is no edit, which is recorded rather than assumed.

**6 — Pre-flight, run here**

- `dotnet build Enigma.MarkdownEditor.slnx -c Release` — the bar is **0 Warning(s)**.
- `dotnet build Enigma.MarkdownEditor.slnx -c Release -v normal | grep -i AVLN` — expect no output. `AVLN*` codes are emitted from an MSBuild task and are **not** promoted by `TreatWarningsAsErrors`, so a green build can still hide a broken binding — which is exactly the class of defect `BUG-7AB2-PHASE04` was.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` — the `--solution` flag is not optional under the Microsoft Testing Platform runner pinned in `global.json`.

**7 — Printed, never run**

```bash
git switch main && git pull      # after merging the release branch
git tag 1.2.0
git push origin 1.2.0
```

Plus a note that `docs/RELEASE.md` §2 (manual Linux verification), §4 (artifacts) and §5 (GitHub Release) are **deferred by decision** for 1.2.0 — no artifacts are produced by this item.

## Acceptance criteria

- `<Version>` reads `1.2.0` in both csproj files, and nowhere in the repo still claims 1.1.0 as current.
- `msiProfiles/Enigma.MarkdownEditor.Desktop.1.2.0.msipkg.json` exists, is valid JSON, keeps the 1.1.0 `upgradeCode` byte-for-byte, and carries a freshly generated `productId` differing from both earlier profiles'.
- `RELEASENOTES.md` has a 1.2.0 section above 1.1.0, covering all five phases including the work-loss note, with the 1.1.0 and 1.0.0 sections unmodified.
- The README what's-new callout reads 1.2.0 and the preview settings list mentions Code font size exactly once.
- Release build: **0 warnings**, no `AVLN` output, full test suite green.
- No `.msi`, no `.tar.gz`, no `artifacts/` content produced; no tag created and no push performed by this item.

## Dependencies

Must be built **after all five phases of `BUG-7AB2`** — it documents them.

## Out of scope

Building the MSI · building the Linux tarball · the manual Linux verification checklist · publishing a GitHub Release · README screenshots · any dependency version bump · a `CHANGELOG.md`.
