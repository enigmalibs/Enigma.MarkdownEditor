# FEATURE-977D — Release 1.9.0

**Status: DONE** · single phase · Branch: `feature/feature-977d-release-1-9-0` (cut from
`feature/2026-10-28-new-icon-release`)
Plan: `docs/plan/FEATURE-977D.md` · Roadmap: `docs/roadmap.md`

## Summary

Version 1.9.0 is cut. This item:

- set the version in both `src/*` csproj files;
- took the two non-coupled dependency bumps;
- cloned and committed the 1.9.0 MSI profile;
- wrote the release documentation.

**No artifacts were built**: no MSI, no Linux tarball, nothing in `artifacts/`. This item created no
tag and pushed nothing.

**What 1.9.0 carries**, everything merged since the `1.8.0` tag: `FEATURE-EA38`, the new
application icon (this run).

**Why 1.9.0.** It is the user's rule: `x.y.z → x.(y+1).0`, so 1.8.0 becomes 1.9.0. It is also a valid
SemVer MINOR, because the application's look changed visibly and nothing a user depends on changed
incompatibly.

**Dependency refresh, per `dotnet-release`:**

- taken: Markdig 1.4.0 → 1.5.1, and Microsoft.Extensions.Hosting 10.0.13 → 10.0.14;
- held back, and said so in the notes:
  - the coupled Avalonia 12.1.4 set, with Enigma.Avalonia.Desktop 1.3.0, which
    `Directory.Packages.props` places in the same set;
  - xunit.v3 4.0.2.

**The notes were written from `docs/done/FEATURE-EA38.md`, not from memory.** Sub-section order as in
1.8.0, keeping only the non-empty ones: *Changes · Dependencies · Compatibility · Version*.

## Files/modules touched

**Created**

- `msiProfiles/Enigma.MarkdownEditor.Desktop.1.9.0.msipkg.json` — the 1.8.0 profile with only
  `version` and `productId` changed.
- `docs/done/FEATURE-977D.md` — this document.

**Modified — version and dependencies**

- `src/Enigma.MarkdownEditor.Desktop/Enigma.MarkdownEditor.Desktop.csproj:8` — `<Version>` 1.8.0 → 1.9.0.
- `src/Enigma.MarkdownEditor.Core/Enigma.MarkdownEditor.Core.csproj:13` — `<Version>` 1.8.0 → 1.9.0.
- `Directory.Packages.props` — the Markdig and Hosting `<PackageVersion>` lines.

**Modified — prose**

- `RELEASENOTES.md` — a `v1.9.0` section prepended above 1.8.0. It is purely additive (**49
  insertions, 0 deletions**), so every earlier section is byte-identical. Wrapped at 100 columns.
- `README.md` — the what's-new callout rewritten for 1.9.0.
- `docs/RELEASE.md` — one new §1 item: *The icon reads in the Windows shell, against a dark and a
  light taskbar*, which closes what `FEATURE-EA38` left open. §2's desktop-menu item already covers
  the Linux launcher. Nothing else in the runbook is stale: every icon path it names is unchanged.

**Workflow artifacts** — `docs/roadmap.md`, and the plan's status. The plan header's status moved to
its own `**Status:**` line so that `roadmap.py` can flip it.

## Deviations & follow-ups

- **None from the plan.** Both planned bumps were taken, and the suite was green on the first run
  with them in place.
- **The manual checks were not performed here.** They fall to the user, before the tag:
  - `docs/RELEASE.md` §1, including the new icon item;
  - §2, the whole Linux checklist, whose desktop-menu item now shows the new launcher icon;
  - §6, post-release verification.
- **Deferred by decision, as for every release since 1.1.0:** `docs/RELEASE.md` §2 (the manual
  Linux verification), §4 (building the MSI and the tarball) and §5 (the GitHub Release).
- **`docs/images/` still does not exist**, so §1's screenshots checkbox stays unticked.
- **Worth their own items:**
  - the held-back Avalonia 12.1.4 set, together with Enigma.Avalonia.Desktop 1.3.0;
  - separately, xunit.v3 4.x.
- **Line endings:** nothing to recommend. Every file touched is LF and `git diff --check` is clean.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal` **succeeded with 0
  Warning(s) and 0 Error(s)**. It ran after the version and dependency edits, and `CoreCompile` ran
  in all four projects (6 `CoreCompile:` entries).
- `grep -ci AVLN` over that log: **0**.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` gave **3636 passed, 0 failed, 0
  skipped**, with Markdig 1.5.1 and Hosting 10.0.14 in place. That is the count FEATURE-EA38 left
  green; no fix cycles were used.
- **Versions resolved as intended.**
  - `dotnet list package` reports Markdig 1.5.1 and Hosting 10.0.14, requested and resolved.
  - Both `AssemblyInfo.cs` files read `AssemblyFileVersion("1.9.0.0")` and
    `AssemblyInformationalVersion("1.9.0+a81bc77…")`.
- **The MSI profile.**
  - `diff` against 1.8.0 shows exactly the `version` and `productId` lines.
  - The new `productId` is `f4f0d539-c908-46ff-8465-f2eb83523a5e`, from Python's `uuid.uuid4()`.
  - All eleven profiles parse as JSON, with eleven distinct `productId`s and one shared
    `upgradeCode` (`a7b550b9-1338-458f-bf2b-66dcf1d25380`).
- **`1.8.0` no longer claimed as current.** `git grep 1.8.0` over tracked files outside `docs/plan/`,
  `docs/done/`, `RELEASENOTES.md`, `docs/roadmap.md` and `msiProfiles/` returns nothing.
- **Nothing produced, nothing tagged here.** `artifacts/` does not exist; `git tag --list` reads
  `1.6.0`, `1.7.0`, `1.8.0`.
- **`dotnet list package --outdated` at planning time (2026-10-28)**, the report the refresh was
  decided from:

  ```text
  Enigma.MarkdownEditor.Core:              Markdig 1.4.0 → 1.5.1
  Enigma.MarkdownEditor.Desktop:           Avalonia, .Desktop, .Fonts.Inter, .Themes.Fluent 12.1.2 → 12.1.4
                                           Enigma.Avalonia.Desktop 1.2.0 → 1.3.0
                                           Microsoft.Extensions.Hosting 10.0.13 → 10.0.14
  Enigma.MarkdownEditor.Core.UnitTests:    xunit.v3 3.2.2 → 4.0.2
  Enigma.MarkdownEditor.Desktop.UnitTests: Avalonia.Headless, .Headless.XUnit 12.1.2 → 12.1.4
                                           xunit.v3 3.2.2 → 4.0.2
  ```

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| A *New Features* or *Fixes* section? | Neither | The icon adds no capability and repairs no defect |
| Mention the icon cache in the notes? | Yes, under the icon entry | An upgraded Windows install can keep showing the old icon until the cache refreshes, which looks like a failed upgrade |
| Where the new runbook check goes | §1, before the MSI-profile item | It is a Windows check on the release build, and §2's desktop-menu item already covers Linux |
