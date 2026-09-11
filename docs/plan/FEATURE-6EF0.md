# FEATURE-6EF0 — Release 1.1.0

**Status: DONE** · Single phase · Roadmap: `docs/roadmap.md`
Branch at build time: `feature/feature-6ef0-release-1-1-0`.

## Objective

Cut version **1.1.0**: set the version, write the release documentation, and commit the versioned MSI profile. **No artifacts are built** — no MSI, no Linux tarball, no publish. The tag and push commands are printed for the user to run, never run here.

## Version choice — why 1.1.0

SemVer MINOR. Everything since 1.0.0 is additive and nothing was removed or changed in a breaking way:

| Shipped since 1.0.0 | Kind |
|---|---|
| `FEATURE-F3FA` — tab strip polish | additive |
| `FEATURE-E340` — typography settings (editor + preview) | additive |
| `FEATURE-C8F1` — interactive preview checkboxes | additive |
| `FEATURE-7FA8` — splash screen & about dialog | additive |
| `FEATURE-284C` — first-run setup | additive |

The three post-1.0.0 features have **never appeared in any release notes** — 1.0.0's notes were written before them. The 1.1.0 section therefore covers all five, not just the two new ones.

## Context & constraints

- **The version lives in three files** and they must move together: `<Version>` in `src/Enigma.MarkdownEditor.Desktop/…csproj`, `<Version>` in `src/Enigma.MarkdownEditor.Core/…csproj`, and the versioned MSI profile filename plus its `version` field. `build/Common.ps1` reads the Desktop csproj to name artifacts.
- **This is an app, not a library.** No `PackageId`, no `dotnet pack`, no `dotnet nuget push`, no pack-verify. `docs/RELEASE.md` says so in its opening paragraph.
- **`git tag` is empty** — the repository has no tags at all, so 1.1.0 will be the first. The convention `docs/RELEASE.md` §3 already commits to is a **bare `X.Y.Z`**, no `v` prefix.
- **`RELEASENOTES.md` is the single release-notes source.** No `CHANGELOG.md` is added.
- **Dependencies are not refreshed this release.** The `Avalonia.*` set is version-coupled and is deliberately held back as a set; this is stated in the notes rather than left silent.
- **`docs/RELEASE.md` §6 is now wrong.** It reads *"The application has no About box and its window title carries no version, so the file version and the ARP entry are what there is to check."* `FEATURE-7FA8` makes that false and the paragraph has to be rewritten.
- **README screenshots stay pending.** The *Captures pending* note and the commented-out image block are left exactly as they are; the §1 pre-flight checkbox stays unticked for 1.1.0 and that is recorded in the completion doc rather than quietly ignored.

## Design

**1 — Version**

- `<Version>1.1.0</Version>` in both `src/*` csproj files.

**2 — MSI profile** (committed, never built)

`msiProfiles/Enigma.MarkdownEditor.Desktop.1.1.0.msipkg.json`, cloned from the 1.0.0 profile with exactly three changes, per the clone rule in `docs/RELEASE.md` §4:

| Field | Value |
|---|---|
| `upgradeCode` | **verbatim** — `a7b550b9-1338-458f-bf2b-66dcf1d25380`. Changing it would make Windows Installer treat 1.1.0 as an unrelated product and accumulate it beside 1.0.0 instead of upgrading it. |
| `productId` | a **new** GUID, generated with `[guid]::NewGuid()` — never hand-written. |
| `version` | `1.1.0` |

Everything else (`install`, `scope`, `compression`, `output`, `controlPanel`, `shortcuts`) carries over unchanged. `releasePath` stays `artifacts\publish\win-x64` — the TFM did not move.

**3 — `RELEASENOTES.md`**

A `# Enigma Markdown Editor v1.1.0 Release Notes` section **prepended** above the 1.0.0 one, with sub-sections in the house order, only where non-empty:

- **New Features** — splash screen · About dialog (icon, name, version, build SHA, credits) · first-run setup (vim, theme, first workspace folder) · tab strip polish · editor font picker, size and line-number margin · preview typography (prose font, size, code font, line height) with a proportional heading scale · tickable task list boxes in the preview.
- **Compatibility** — unchanged: .NET 10, Avalonia 12.1.1, Windows x64 and Linux x64, both self-contained. The TFM set did not move.
- **Dependencies** — none changed; the coupled `Avalonia.*` set was deliberately held at 12.1.1.
- **Version** — `1.1.0`.

Also worth stating plainly in the notes: a first launch now asks three questions, and an existing install with no `settings.json` will be asked them once.

**4 — `README.md`**

- What's-new callout → `> **What's new in 1.1.0** — …  See [RELEASENOTES.md](RELEASENOTES.md).`
- Feature list extended with the splash, the About dialog and the first-run setup. The existing typography and task-checkbox bullets are already there and need no change.
- Screenshots section untouched.

**5 — `docs/RELEASE.md`**

- §6's "no About box" paragraph rewritten: the installed version is now readable from *Help → About* as well as from the file version and the ARP entry, and the About dialog becomes one of the post-release verification steps.
- §1 pre-flight gains nothing new — the MSI profile checkbox already exists and is now satisfiable.

**6 — Pre-flight, run here**

- `dotnet build Enigma.MarkdownEditor.slnx -c Release` — the bar is **0 Warning(s)**.
- `dotnet build Enigma.MarkdownEditor.slnx -c Release -v normal | grep -i AVLN` — expect no output. `AVLN*` codes are emitted from an MSBuild task and are **not** promoted by `TreatWarningsAsErrors`, so a green build can still hide a broken binding.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` — the `--solution` flag is not optional under the Microsoft Testing Platform runner this repo pins in `global.json`.

**7 — Printed, never run**

```bash
git switch main && git pull      # after merging the release branch
git tag 1.1.0
git push origin 1.1.0
```

Plus a note that `docs/RELEASE.md` §2 (manual Linux verification), §4 (artifacts) and §5 (GitHub Release) are **deferred by decision** for 1.1.0 — no artifacts are produced by this item.

## Acceptance criteria

- `<Version>` reads `1.1.0` in both csproj files, and nowhere in the repo still claims 1.0.0 as current.
- `msiProfiles/Enigma.MarkdownEditor.Desktop.1.1.0.msipkg.json` exists, is valid JSON, keeps the 1.0.0 `upgradeCode` byte-for-byte, and carries a freshly generated `productId` that differs from 1.0.0's.
- `RELEASENOTES.md` has a 1.1.0 section above 1.0.0 covering all five features, with 1.0.0's section unmodified.
- The README what's-new callout reads 1.1.0 and the feature list mentions the splash, the About dialog and the first-run setup.
- `docs/RELEASE.md` no longer claims the application has no About box.
- Release build: **0 warnings**, no `AVLN` output, full test suite green.
- No `.msi`, no `.tar.gz`, no `artifacts/` content produced; no tag created and no push performed by this item.

## Dependencies

Must be built **after** `FEATURE-7FA8` (both phases) and `FEATURE-284C` — it documents them.

## Out of scope

Building the MSI · building the Linux tarball · the manual Linux verification checklist · publishing a GitHub Release · README screenshots · any dependency version bump · a `CHANGELOG.md`.
