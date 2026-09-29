# FEATURE-3D59 — Release 1.7.0

**Status: DONE** · Single phase · Branch: `feature/feature-3d59-release-1-7-0` (cut from
`feature/2026-09-29-linux-installer-release`)
Plan: `docs/plan/FEATURE-3D59.md` · Roadmap: `docs/roadmap.md`

## Summary

Version 1.7.0 is cut. This item:

- set the version in both `src/*` csproj files;
- took the three non-coupled dependency bumps;
- cloned and committed the 1.7.0 MSI profile;
- wrote the release documentation.

**No artifacts were built**: no MSI, no Linux tarball, nothing left in `artifacts/`. No tag was
created and nothing was pushed. The tag commands are printed for the user to run.

**Why 1.7.0.** It is a SemVer MINOR. `FEATURE-4BB7` adds functionality: a build-from-checkout
installer, an uninstaller, a terminal command and a full icon set. `BUG-18AD`'s two phases are fixes.
Nothing a user depends on changed incompatibly:

- `settings.json`, `workspaces.json` and a stored session are untouched;
- the application itself changes only by the two fixes;
- a 1.x Linux install is migrated by the new installer.

The one removal is the tarball installer's `--prefix` option, a packaging-script flag with
`XDG_DATA_HOME` as its replacement. The notes list it under *Changes* rather than using it to call the
release a MAJOR.

**The dependency refresh followed `dotnet-release`, not the 1.5.0/1.6.0 precedent.** The last two
releases took nothing, because their interviews decided so. The skill's routine-release rule is to
take the non-coupled bumps, and `docs/done/FEATURE-8381.md` named Hosting 10.0.12 as worth taking with
the next release. Taken:

- Markdig 1.3.2 → 1.4.0;
- Microsoft.Extensions.Hosting 10.0.11 → 10.0.12;
- coverlet.collector 10.0.1 → 10.1.0.

Held back, and said so in the notes:

- the coupled Avalonia 12.1.3 set, together with Enigma.Avalonia.Desktop 1.1.0;
- xunit.v3 4.0.1.

The suite was green on the first run with all three bumps, so none had to be held back after the
fact.

**The notes were written from the completion docs, not from memory**: `docs/done/BUG-18AD-PHASE01.md`,
`…PHASE02.md` and `docs/done/FEATURE-4BB7.md`. The sub-sections follow the order the 1.3.0 section
already uses: *New Features · Changes · Fixes · Dependencies · Compatibility · Version*. The
*Changes* section exists because an upgrading Linux user needs to know the install moved, and that
nothing is asked of them, separately from what is new.

## Files/modules touched

**Created**

- `msiProfiles/Enigma.MarkdownEditor.Desktop.1.7.0.msipkg.json` — the 1.6.0 profile with `version`
  and `productId` changed and nothing else.
- `docs/done/FEATURE-3D59.md` — this document.

**Modified — version and dependencies**

- `src/Enigma.MarkdownEditor.Desktop/…csproj:8` — `<Version>` 1.6.0 → 1.7.0.
- `src/Enigma.MarkdownEditor.Core/…csproj:13` — `<Version>` 1.6.0 → 1.7.0.
- `Directory.Packages.props` — the three `<PackageVersion>` lines above; no comment in the file names
  a version that moved.

**Modified — prose**

- `RELEASENOTES.md` — a `v1.7.0` section prepended above 1.6.0. It is purely additive — **81
  insertions, 0 deletions** — so every earlier section is byte-identical. It is wrapped at 100
  columns like the rest of the file.
- `README.md` — the what's-new callout rewritten for 1.7.0. The *Installation → Linux* section and the
  *Repository layout* line were **verified rather than repeated**: `FEATURE-4BB7`'s sweep had already
  rewritten both.
- `docs/RELEASE.md` — two additions:
  - a §1 pre-flight item for the code-span chip, in both themes, in three contexts, plus the copy
    round trip (the manual pass `BUG-18AD-PHASE01` named as a follow-up);
  - a §2 item for installing from a checkout over the tarball's install.

  Read in full against the repository: nothing in it needed *correcting*. `FEATURE-4BB7` had already
  brought §2's install and uninstall items up to date, and §4's `pack-linux.ps1` command is unchanged.

**Workflow artifacts** — `docs/roadmap.md` (`TODO` → `IN PROGRESS` → `DONE`) and the plan file's
status.

## Deviations & follow-ups

- **None from the plan.** All three planned bumps were taken and none was held back.
- **`git tag --list` in this clone reads `1.6.0` only.** The 1.6.0 completion doc recorded `1.0.0` …
  `1.5.0` as present in its clone, so the older tags presumably live on the remote and were never
  fetched here. That is not a gap this item can or should close. `1.4.1` is still untagged, as
  recorded since 1.5.0 — the user's call.
- **The manual checks were not performed here.** This environment cannot judge a colour on screen or
  paste into another application. On the user before the tag:
  - `docs/RELEASE.md` §1 — every standing item, including the new code-span one;
  - §2 — the whole Linux checklist, now including the install from a checkout;
  - §6 — post-release verification.

  `FEATURE-4BB7`'s own dry runs covered the installer end to end in scratch roots, but not with the
  real `pack-linux.ps1` output: there is no PowerShell on this machine.
- **Deferred by decision for 1.7.0, as for every release since 1.1.0:** `docs/RELEASE.md` §2 (the
  manual Linux verification), §4 (building the MSI and the tarball) and §5 (the GitHub Release).
- **`docs/images/` still does not exist**, so §1's screenshots checkbox stays unticked. The README's
  *Captures pending* note is untouched.
- **Worth its own item:** the held-back Avalonia 12.1.3 set together with Enigma.Avalonia.Desktop
  1.1.0, and separately xunit.v3 4.x (which also needs Avalonia.Headless.XUnit built against it).
- **No line-ending noise.** `git diff --check` is clean, and every file touched is LF.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`: **succeeded,
  0 Warning(s), 0 Error(s)**. It was run after the version and dependency edits. `CoreCompile` ran in
  all four projects (11 `CoreCompile:` entries, `csc` invoked), so the result is not an up-to-date
  skip.
- `grep -i AVLN` over that same log: **no output**. (This release changes no XAML; the check is
  run anyway because a new Markdig can change the tree the preview is built from.)
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3582 passed, 0 failed, 0
  skipped** with Markdig 1.4.0, Hosting 10.0.12 and coverlet 10.1.0 in place. That is the same count
  `FEATURE-4BB7` left green; this item adds no tests because it ships no behaviour. No fix cycles were
  used.
- **Versions resolved as intended.** `dotnet list package` reports Markdig 1.4.0, Hosting 10.0.12 and
  coverlet.collector 10.1.0 requested and resolved. Both `AssemblyInfo.cs` files under
  `obj/Release/net10.0/` read `AssemblyFileVersion("1.7.0.0")` and
  `AssemblyInformationalVersion("1.7.0+068f6dd…")`, the `X.Y.Z+<sha>` shape `docs/RELEASE.md` §6
  expects.
- **The MSI profile.** `diff` against 1.6.0 shows exactly the `version` and `productId` lines. The new
  `productId` is `5e6503d7-1e38-47b8-b3e1-5d2bf31b50a6`, from `/proc/sys/kernel/random/uuid`. All
  nine profiles parse as JSON, with nine distinct `productId`s and one shared `upgradeCode`
  (`a7b550b9-1338-458f-bf2b-66dcf1d25380`).
- **`1.6.0` no longer claimed as current.** `git grep 1.6.0` over tracked files outside `docs/plan/`,
  `docs/done/`, `RELEASENOTES.md` and `docs/roadmap.md` returns only the 1.6.0 MSI profile, which is a
  historical record.
- **Nothing produced, nothing tagged.** `artifacts/` does not exist; `find` for `*.msi` / `*.tar.gz`
  outside `.git/` returns nothing; `git tag --list` is unchanged.
- **`dotnet list package --outdated` at planning time**, the report the refresh was decided from:

  ```text
  Enigma.MarkdownEditor.Core:            Markdig 1.3.2 → 1.4.0
  Enigma.MarkdownEditor.Desktop:         Avalonia, .Desktop, .Fonts.Inter, .Themes.Fluent 12.1.2 → 12.1.3
                                         Enigma.Avalonia.Desktop 1.0.0 → 1.1.0
                                         Microsoft.Extensions.Hosting 10.0.11 → 10.0.12
  Enigma.MarkdownEditor.Core.UnitTests:  coverlet.collector 10.0.1 → 10.1.0; xunit.v3 3.2.2 → 4.0.1
  Enigma.MarkdownEditor.Desktop.UnitTests: Avalonia.Headless, .Headless.XUnit 12.1.2 → 12.1.3
                                         coverlet.collector 10.0.1 → 10.1.0; xunit.v3 3.2.2 → 4.0.1
  ```

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| Release-notes sub-section order | *New Features · Changes · Fixes · Dependencies · Compatibility · Version* | The order this file already uses (1.3.0), which the skill says to match where it differs |
| The fix's wording | "read as code", not "read as code again" | `BUG-18AD`'s plan describes the chip as never having worked, not as a regression |
| New `docs/RELEASE.md` items | One §1 (code chip), one §2 (install from a checkout) | Every release since 1.4.1 has turned what its items left unverified into a standing check; these are the two this release leaves open |

## Printed for the user — never run here

After merging the run branch into `main`:

```bash
git switch main && git pull
git tag 1.7.0
git push origin 1.7.0
```

`docs/RELEASE.md` §2 (manual Linux verification), §4 (building the MSI and the Linux tarball) and §5
(the GitHub Release) are **deferred by decision** for 1.7.0. §1's manual checks come before the tag.
