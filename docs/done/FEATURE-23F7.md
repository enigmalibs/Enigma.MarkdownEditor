# FEATURE-23F7 — Release 1.8.0

**Status: DONE** · single phase · Branch: `feature/feature-23f7-release-1-8-0` (cut from
`feature/2026-10-14-confirm-delete-release`)
Plan: `docs/plan/FEATURE-23F7.md` · Roadmap: `docs/roadmap.md`

## Summary

Version 1.8.0 is cut. This item:

- set the version in both `src/*` csproj files;
- took the one non-coupled dependency bump;
- cloned and committed the 1.8.0 MSI profile;
- wrote the release documentation.

**No artifacts were built**: no MSI, no Linux tarball, nothing in `artifacts/`. No tag was created
and nothing was pushed. The tag commands are printed below for the user to run.

**What 1.8.0 carries**, everything merged since the `1.7.0` tag:

- `FEATURE-1C17` — Enigma.Avalonia.Desktop 1.2.0; lighter InfoBar fills in Dark; vim chip text
  repainted;
- `FEATURE-425B` — no informational bar after a trash delete;
- `FEATURE-4B09` — multi-selection in the tree (PHASE01) and deleting the whole selection (PHASE02);
- `FEATURE-47F5` — every delete asks first (this run).

**Why 1.8.0.** The user asked for it, and it is also the SemVer answer: a MINOR. Features were added
(multi-selection, deleting a selection) and deleting now confirms first. Nothing a user depends on
changed incompatibly: `settings.json`, `workspaces.json` and a stored session are untouched.

**Dependency refresh, per `dotnet-release`:**

- taken: Microsoft.Extensions.Hosting 10.0.12 → 10.0.13;
- held back, and said so in the notes: the coupled Avalonia 12.1.4 set, and xunit.v3 4.0.2.

`Enigma.Avalonia.Desktop` 1.0.0 → 1.2.0 was already on `develop` (FEATURE-1C17). The notes record it,
as 1C17's completion doc asked the next release item to.

**The notes were written from the completion docs, not from memory**:

- `docs/done/FEATURE-1C17.md`, `FEATURE-425B.md`, `FEATURE-4B09-PHASE01.md`, `FEATURE-4B09-PHASE02.md`
  and `FEATURE-47F5.md`;
- sub-section order as in 1.7.0, keeping only the non-empty ones: *New Features · Changes ·
  Dependencies · Compatibility · Version*. There is no *Fixes* section: nothing shipped since 1.7.0
  repairs a defect a user could have hit.

## Files/modules touched

**Created**

- `msiProfiles/Enigma.MarkdownEditor.Desktop.1.8.0.msipkg.json` — the 1.7.0 profile with only
  `version` and `productId` changed.
- `docs/done/FEATURE-23F7.md` — this document.

**Modified — version and dependencies**

- `src/Enigma.MarkdownEditor.Desktop/Enigma.MarkdownEditor.Desktop.csproj:8` — `<Version>` 1.7.0 → 1.8.0.
- `src/Enigma.MarkdownEditor.Core/Enigma.MarkdownEditor.Core.csproj:13` — `<Version>` 1.7.0 → 1.8.0.
- `Directory.Packages.props` — the Hosting `<PackageVersion>` line; no comment in the file names the
  version that moved.

**Modified — prose**

- `RELEASENOTES.md` — a `v1.8.0` section prepended above 1.7.0. It is purely additive — **73
  insertions, 0 deletions** — so every earlier section is byte-identical. Wrapped at 100 columns.
- `README.md` — the what's-new callout rewritten for 1.8.0. The *Workspaces and the file tree* bullet
  was **verified rather than repeated**: FEATURE-4B09's and FEATURE-47F5's sweeps had already
  written the multi-selection and the confirmation into it.
- `docs/RELEASE.md` — **no change** in this item. It was read in full against the repository and
  nothing in it is stale. FEATURE-47F5 already added this release's one new standing check, and it
  also exercises FEATURE-4B09's multi-delete. 4B09's gestures and 1C17's chip contrast are covered by
  headless tests through the real view and style, so neither left a manual gap.

**Workflow artifacts** — `docs/roadmap.md` (`TODO` → `IN PROGRESS` → `DONE`) and the plan file's
status.

## Deviations & follow-ups

- **None from the plan.** The planned bump was taken, and the suite was green on the first run with
  it in place.
- **The manual checks were not performed here.** On the user, before the tag:
  - `docs/RELEASE.md` §1, including the new *Every delete asks first* item;
  - §2, the whole Linux checklist, whose tree step now includes the confirmation;
  - §6, post-release verification.
- **Deferred by decision for 1.8.0, as for every release since 1.1.0:** `docs/RELEASE.md` §2 (the
  manual Linux verification), §4 (building the MSI and the tarball) and §5 (the GitHub Release).
- **`docs/images/` still does not exist**, so §1's screenshots checkbox stays unticked. The README's
  *Captures pending* note is untouched.
- **Worth its own item:**
  - the held-back Avalonia 12.1.4 set;
  - separately, xunit.v3 4.x, which also needs an Avalonia.Headless.XUnit built against it.
- **Line endings:** nothing to recommend. Every file touched is LF and `git diff --check` is clean.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`: **succeeded, 0
  Warning(s), 0 Error(s)**.
  - Run after the version and dependency edits.
  - `CoreCompile` ran in all four projects (6 `CoreCompile:` entries), so the result is not an
    up-to-date skip.
- `grep -i AVLN` over that same log: **no output**.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3634 passed, 0 failed, 0
  skipped**, with Hosting 10.0.13 in place.
  - The same count FEATURE-47F5 left green. This item adds no tests because it ships no behaviour.
  - No fix cycles were used.
- **Versions resolved as intended.**
  - `dotnet list package` reports Hosting 10.0.13 and Enigma.Avalonia.Desktop 1.2.0, requested and
    resolved.
  - Both `AssemblyInfo.cs` files under `obj/Release/net10.0/` read
    `AssemblyFileVersion("1.8.0.0")` and `AssemblyInformationalVersion("1.8.0+eacdf67…")`, the
    `X.Y.Z+<sha>` shape `docs/RELEASE.md` §6 expects.
- **The MSI profile.**
  - `diff` against 1.7.0 shows exactly the `version` and `productId` lines.
  - The new `productId` is `0b8aa8fe-e497-4ec9-9ec9-a3e0a974c036`, from `[guid]::NewGuid()`.
  - All ten profiles parse as JSON, with ten distinct `productId`s and one shared `upgradeCode`
    (`a7b550b9-1338-458f-bf2b-66dcf1d25380`).
- **`1.7.0` no longer claimed as current.** `git grep 1.7.0` over tracked files outside `docs/plan/`,
  `docs/done/`, `RELEASENOTES.md`, `docs/roadmap.md` and `msiProfiles/` returns nothing.
- **Nothing produced, nothing tagged.** `artifacts/` does not exist; `git tag --list` still reads
  `1.6.0`, `1.7.0`.
- **`dotnet list package --outdated` at planning time (2026-10-14)**, the report the refresh was
  decided from:

  ```text
  Enigma.MarkdownEditor.Core:              no updates
  Enigma.MarkdownEditor.Desktop:           Avalonia, .Desktop, .Fonts.Inter, .Themes.Fluent 12.1.2 → 12.1.4
                                           Microsoft.Extensions.Hosting 10.0.12 → 10.0.13
  Enigma.MarkdownEditor.Core.UnitTests:    xunit.v3 3.2.2 → 4.0.2
  Enigma.MarkdownEditor.Desktop.UnitTests: Avalonia.Headless, .Headless.XUnit 12.1.2 → 12.1.4
                                           xunit.v3 3.2.2 → 4.0.2
  ```

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| A *Fixes* section? | None | Nothing since 1.7.0 repairs a defect a user could have hit; the chip repaint follows from the new fills and sits under *Changes* with them |
| Where the confirmation goes in the notes | *Changes*, first item | An upgrading user's muscle memory changes — a delete that used to be immediate now asks — which matters more than the new capability beside it |
| New `docs/RELEASE.md` items | None in this item | FEATURE-47F5's §1 item covers the multi-delete too; the other items' risks are under headless test |

## Printed for the user — never run here

After merging the run branch into `main`:

```bash
git switch main && git pull
git tag 1.8.0
git push origin 1.8.0
```

`docs/RELEASE.md` §2 (manual Linux verification), §4 (building the MSI and the Linux tarball) and §5
(the GitHub Release) are **deferred by decision** for 1.8.0. §1's manual checks come before the tag.
