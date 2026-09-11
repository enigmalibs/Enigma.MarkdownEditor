# FEATURE-2760 — Release 1.2.0

**Status: DONE** · Single phase · Branch: `feature/feature-2760-release-1-2-0`
Plan: `docs/plan/FEATURE-2760.md`

## Summary

Version **1.2.0** is set and documented. This item ships documentation and one JSON profile — **no
artifact was built**: no MSI, no Linux tarball, no publish, no tag. `artifacts/` still holds only what
earlier work left there (`EnigmaMarkdownEditor-0.0.0-dev-linux-x64.tar.gz` and
`EnigmaMarkdownEditor-1.0.0-linux-x64.tar.gz`, plus `publish/`); nothing named `1.2.0` was produced —
verified with `find artifacts -name "*1.2.0*"`, which returns nothing.

`<Version>` moved `1.1.0` → `1.2.0` in **both** `src/*` csproj files, which is all the version
plumbing there is: `build/Common.ps1` reads the Desktop csproj to name artifacts, `AppInfo` reads the
`AssemblyInformationalVersionAttribute` the SDK stamps from it, and no script or source file carries a
literal version of its own. Confirmed on the built assembly rather than assumed — the Release
`AssemblyInfo.cs` now stamps `AssemblyFileVersion 1.2.0.0` and
`AssemblyInformationalVersion 1.2.0+040927c…`, so the About dialog will read 1.2.0.

The MSI profile was cloned from 1.1.0's with **exactly two lines changed** — `version` and a
`productId` generated with `[guid]::NewGuid()` (`a20d5fcf-77f5-4089-8713-4ba17e673ecf`, different from
both 1.0.0's `02ef086b-…` and 1.1.0's `c749531f-…`). `upgradeCode` is byte-for-byte the value both
earlier profiles carry (`a7b550b9-1338-458f-bf2b-66dcf1d25380`): change it and Windows Installer stops
treating 1.2.0 as an upgrade of 1.1.0 and accumulates it alongside instead. A `diff` of the two
profiles shows those two lines and nothing else.

**Why MINOR and not PATCH.** Everything since 1.1.0 is `BUG-7AB2`, which would be a patch release on
its own — but `PHASE05` could not fix the preview's code size without adding
`AppSettings.PreviewCodeFontSize`, a new key in `settings.json` and a new row in the settings window.
That is additive functionality, so 1.2.0. Nothing was removed and nothing changed in a breaking way.

**The release notes lead with the work-loss fix**, in the opening paragraph and again in the *Fixes*
bullet, because it is the only entry with a consequence a reader needs to act on: a tab edited in vim
insert mode showed no unsaved marker and closed without asking. Sub-sections follow the house order
from the `dotnet-release` skill (*New Features · Fixes · Dependencies · Compatibility · Version*),
which is where the plan's own bullet list differed from it by a swap — the plan says "in the house
order" and then lists *Fixes* first.

**No dependency was refreshed**, deliberately, and the notes say so rather than staying silent: the
`Avalonia.*` set is version-coupled and moves as a set, so it stays at 12.1.1 (AvaloniaEdit 12.0.0,
Markdig 1.3.2 — read off `Directory.Packages.props`, not from the previous notes).

## Files/modules touched

**Modified**

- `src/Enigma.MarkdownEditor.Desktop/Enigma.MarkdownEditor.Desktop.csproj` — `<Version>` → `1.2.0`.
- `src/Enigma.MarkdownEditor.Core/Enigma.MarkdownEditor.Core.csproj` — `<Version>` → `1.2.0`.
- `RELEASENOTES.md` — a `v1.2.0` section prepended above 1.1.0's; both earlier sections are untouched
  (the diff is 65 insertions, 0 deletions).
- `README.md` — the what's-new callout now reads 1.2.0 and names the two things a 1.1.0 user cares
  about: the five fixes, one of which could lose work in vim insert mode, and code in the preview
  taking a size of its own. Two lines changed, nothing else.
- `docs/roadmap.md`, `docs/plan/FEATURE-2760.md` — status `TODO` → `IN PROGRESS` → `DONE`.

**Created**

- `msiProfiles/Enigma.MarkdownEditor.Desktop.1.2.0.msipkg.json` — the committed, never-built profile.
- `docs/done/FEATURE-2760.md` — this file.

**Deliberately unchanged**

- `docs/RELEASE.md` — see *Deviations*: read against the repository as it now stands and nothing in it
  is false.
- `README.md` *Features* → *Preview* — the "a separate font *and size* for code spans and fenced
  blocks" sentence was already added by `BUG-7AB2-PHASE05`'s own sweep, so no bullet was added. The
  plan asked for exactly this check; the list mentions code sizing once.
- `README.md` *Screenshots* — the *Captures pending* note and the commented-out image block are as
  they were.
- `Directory.Packages.props` — no dependency bump this release.

## Deviations & follow-ups

- **Plan step 5 needed no edit, and that was verified by reading `docs/RELEASE.md` in full**, not
  assumed. It is written against `X.Y.Z` placeholders throughout, §6's About/ARP/file-version triple
  was already rewritten for 1.1.0, and none of the five fixes made any of its instructions false.
- **`RELEASENOTES.md` sub-section order follows the skill, not the plan's bullet sequence** (see
  *Summary*). Same call as `FEATURE-6EF0` made for 1.1.0, and it keeps the three sections in the file
  consistent with each other.
- **The repository still has no tags** — `git tag` lists nothing, so 1.1.0 was never tagged. The plan
  anticipated this and asked for it as a note rather than a blocker. Consequence for the runbook: when
  you tag, `1.1.0` is missing from history, and `docs/RELEASE.md` §3's *"This repository has no tags
  yet, so the convention starts here"* is still literally true. **Follow-up for the user to decide:**
  tag 1.1.0 retroactively on its merge commit, or let 1.2.0 be the first tag.
- **README screenshots stay pending**, per the plan. `docs/RELEASE.md` §1's screenshots checkbox
  therefore **stays unticked for 1.2.0** — a known, accepted gap, recorded rather than ignored.
- **Deferred by decision for 1.2.0**, per the plan's *Out of scope*: `docs/RELEASE.md` §2 (the manual
  Linux verification checklist), §4 (both artifacts and the MSI) and §5 (the GitHub Release).
- **Three of the five fixes are only verifiable by hand** — the exit hang, the editor font family and
  the code weight. This item documents them; their verification is recorded in
  `docs/done/BUG-7AB2-PHASE03.md`, `-PHASE04.md` and `-PHASE05.md`, and was not repeated here.
- **Two files carry `1.1.0` as literal data and were left alone**:
  `tests/…Core.UnitTests/ProductVersionTests.cs` uses it as sample input to `ProductVersion.From`, and
  `src/…Core/ProductVersion.cs:46` uses `1.1.0-rc.1` in an XML-doc example. Neither claims to be the
  product's current version, so neither is stale; changing them would be churn with no acceptance
  criterion behind it. Worth knowing they exist the next time someone greps for the old version.
- **Documentation sweep:** three candidates offered — skip (my recommendation, on the reading that
  `docs/RELEASE.md` is version-agnostic and no documented behaviour changed), §3's tag sentence, and
  the README screenshots note. **Skip was chosen**; no doc was edited by the sweep.
- **Line endings:** nothing to report — every file this item touched is LF-only, including the cloned
  MSI profile, and the diffs carry no CRLF churn.

## Build/test evidence

| Check | Result |
|---|---|
| `dotnet build Enigma.MarkdownEditor.slnx -c Release` | **Build succeeded — 0 Warning(s), 0 Error(s)** |
| `dotnet build … -c Release -v normal`, grepped for `AVLN` | **0 matches** (grep exit 1) |
| `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` | **Passed — 1958/1958**, 0 failed, 0 skipped |

Both the build and the suite were green on the **first** run, unlike 1.1.0's cut — the order-dependent
`ServiceCollectionExtensionsTests.TheComposedContainer_ResolvesTheShellViewModel` recorded in
`docs/done/FEATURE-6EF0.md` did not fail this time. It is still order-dependent and still unfixed; a
green run here is not evidence it was repaired.

**No tests were added.** This item changes one version string and three documents; there is no
behaviour to cover, and the plan's acceptance criteria call for none. The MSI profile was verified
instead by `diff` against 1.1.0's (two lines) and by parsing it as JSON to confirm `version`,
`productId` and the carried-over `upgradeCode`.
