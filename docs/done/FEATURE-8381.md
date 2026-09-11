# FEATURE-8381 — Release 1.6.0

**Status: DONE** · Single phase · Branch: `feature/feature-8381-release-1-6-0` (cut from
`vibe/2026-09-10-title-colors-vim-release`)
Plan: `docs/plan/FEATURE-8381.md` · Roadmap: `docs/roadmap.md`

## Summary

Version 1.6.0 cut: the version set in both `src/*` csproj files, the 1.6.0 MSI profile cloned and
committed, and the release documentation written. **No artifacts were built** — no MSI, no Linux
tarball, no publish — and no tag was created or pushed; the tag commands are printed for the user to
run.

**A minor release carrying three items.** `FEATURE-E64A`, `FEATURE-A225` (three phases) and
`FEATURE-3505` are the whole of it, so the version is a SemVer MINOR and the release notes have a
*New Features* section and nothing else: no *Fixes*, no *Changes*, no *Breaking Changes*, and — as
with 1.5.0 and 1.4.1 — no *Dependencies*, because no dependency moved.

**The one compatibility question this release actually had, answered in the notes.**
`workspaces.json` gains a key (`FolderColors`) and its `Version` deliberately stays at 1, so the
compatibility is two-way: a 1.5.0 file opens in 1.6.0 with no colours, and a 1.6.0 file still opens
in 1.5.0, which ignores what it does not know. That is worth a user knowing before they downgrade,
so it is stated as such rather than folded into a generic "no migration" line. `settings.json` and a
stored session gain nothing at all.

**The `--outdated` report has a new entry, and nothing was taken.** The plan asks for the report to
be recorded whatever it says and for nothing to be taken from it; both were honoured.
`Microsoft.Extensions.Hosting` 10.0.11 → **10.0.12** is new since 1.5.0 — a patch bump on a `src/`
dependency, which is the first time in four releases the report has named anything outside the test
projects. It was **not taken**, for the plan's stated reason: a dependency move is its own decision,
not a passenger on a feature release. `xunit.v3` 3.2.2 → 4.0.0 is the standing entry, held for the
fourth release running. `Directory.Packages.props` is byte-identical (`git diff` on it is empty).
Full output under *Build/test evidence*, and see *Deviations* for the follow-up.

**The MSI profile is a two-field clone, per `docs/RELEASE.md` §4.** `version` → `1.6.0` and
`productId` → a freshly generated GUID (`ca4ae014-007d-426e-af0d-92da04aff950`, from
`[guid]::NewGuid()`), with `upgradeCode` kept **byte-for-byte** from 1.5.0 —
`a7b550b9-1338-458f-bf2b-66dcf1d25380`. A `diff` against the 1.5.0 profile shows exactly those two
lines and nothing else, the file parses with `json.load`, and the eight `productId`s across the eight
profiles are eight distinct values against one shared `upgradeCode`.

**The release notes were written from the four completion docs, not from memory.** Each *New
Features* entry leads with the gesture and then with the rule a user can actually see: the title is
the file name *verbatim* (so `2026-09-10.stand-up.md` keeps its dots) and markdown only; a colour
applies to everything under a folder with a **sub-folder's colour overriding its parent's**, and the
tabs follow live through a colour change, a *Save as* and a workspace switch; `O` uses the item's
**own** marker rather than the next one, and an empty item continues nothing. None of the internals
are in the notes — not `FolderColorMap`, not `MarkdownScaffold`, not the `FolderColorsChanged` event
— they are not user-facing.

**Three pre-flight items were added to `docs/RELEASE.md` §1**, one per shipped item, because all
three left the same kind of gap: covered by tests down to the bytes and the brushes, and never once
*looked at* in a running window. The colour item is the long one, and deliberately — it lists the six
live transitions (open, colour-while-open, *Save as*, sub-folder override, workspace switch, restart)
plus the judgement no test can make, whether 4px of colour reads at a glance on the selected tab.

## Files/modules touched

**Created**

- `msiProfiles/Enigma.MarkdownEditor.Desktop.1.6.0.msipkg.json` — the 1.5.0 profile with `version`
  and `productId` changed and nothing else.
- `docs/done/FEATURE-8381.md` — this document.

**Modified — version**

- `src/Enigma.MarkdownEditor.Desktop/…csproj:8` — `<Version>` 1.5.0 → 1.6.0.
- `src/Enigma.MarkdownEditor.Core/…csproj:13` — `<Version>` 1.5.0 → 1.6.0.

No comment in either file names a version number that moved this release — the Desktop csproj's
"version-coupled Avalonia set (12.1.2)" note is about a dependency pin, and no pin moved.

**Modified — prose**

- `RELEASENOTES.md` — a `v1.6.0` section prepended above 1.5.0: *New Features* (three entries),
  *Compatibility* (three bullets), *Version*. Purely additive — **55 insertions, 0 deletions** — so
  the 1.5.0, 1.4.1, 1.4.0, 1.3.0, 1.2.0, 1.1.0 and 1.0.0 sections are byte-identical.
- `README.md` — the what's-new callout rewritten for 1.6.0. The feature bullets and the vim key table
  were **verified rather than repeated** and needed nothing: all three items' own sweeps had already
  updated them (see *Deviations*).
- `docs/RELEASE.md` — three §1 pre-flight items added (one per shipped item), and §2's vim smoke set
  gained `o` and `O` on a list item. Nothing in the file was *corrected*: it was read in full and
  every claim in it still matches the repository.

**Untouched, deliberately**

- `Directory.Packages.props` — no dependency moves on a feature release.

**Workflow artifacts**

- `docs/roadmap.md` — `FEATURE-8381` `TODO` → `IN PROGRESS` → `DONE`. `IN PROGRESS` is the widest
  Status cell the table holds and was already the column's width, so no other row moved.
- `docs/plan/FEATURE-8381.md` — the item header's status.

## Deviations & follow-ups

- **The README's feature bullets needed nothing, and that is a change from 1.5.0.** The plan says to
  verify rather than repeat, and to fold in anything an item's sweep left short — which is what
  1.5.0's release item had to do. Verified: `FEATURE-E64A` added the new-note clause to the file-tree
  bullet, `FEATURE-A225-PHASE02` and `PHASE03` wrote the colour bullet in two halves, and
  `FEATURE-3505` corrected both the `o` `O` row of the vim key table and the writing-assists bullet.
  Nothing was left for this item but the callout. Each of those three completion docs records that
  the edit was an *addition* rather than a correction and why it was made anyway; the outcome here is
  the argument for having done so.
- **`Microsoft.Extensions.Hosting` 10.0.11 → 10.0.12 is available and was not taken.** A patch bump on
  a shipping dependency is the most take-able entry the report has produced in four releases, and it
  was still held: this item's plan says the report is recorded and nothing is taken from it, and
  changing that mid-item would be rewriting the plan rather than executing it. **Worth its own small
  item** — a patch-level bump of one non-coupled package, with the suite as its evidence — before or
  alongside the next release.
- **`docs/RELEASE.md` needed no correction, only additions.** Read in full against the repository as
  it now stands. §1's version, notes, callout, build, `AVLN` and profile items are exactly the
  pre-flight this item ran; §2's checklist is unaffected by three additive features except for the
  vim smoke set, which gained the two keys; §3's bare-`X.Y.Z` tag convention is what the printed
  commands use; §4's clone rule is what the 1.6.0 profile was made by; §5 and §6 are unchanged by a
  minor release. So the three new §1 items are a *gap* being closed, not staleness being fixed — the
  same shape as 1.5.0's single addition.
- **The `1.4.1` tag still does not exist.** `git tag --list` reads `1.0.0 1.1.0 1.2.0 1.3.0 1.4.0
  1.5.0`. `docs/done/FEATURE-F6A4.md` recorded this a release ago and said it was worth settling
  before `1.5.0` went on; `1.5.0` went on and the hole is still there. Still not acted on here —
  tagging is the user's step and a retroactive `1.4.1` on `b5eed77` is a decision, not a chore — but
  it is now two releases old.
- **The manual checks the three items asked for were not performed here**, and all three are now
  standing `docs/RELEASE.md` §1 items so that no future release reaches a tag without someone having
  looked at them. This environment cannot show a window or drive a keystroke into one. Open, cheap,
  and on the user before the tag:
  - **Nobody has seen the colour bars.** Every binding, brush, width and re-resolve is asserted
    through a shown headless window, but not one pixel has been looked at.
  - **Nobody has pressed `o` in the running editor.** The engine is driven end to end through the
    real key path in tests; the editor's own key handling around it is not what changed.
  - **Nobody has watched a new note open with its heading.** The bytes are pinned exactly; the tab,
    the caret and the preview are not.
- **`docs/RELEASE.md` §1's two older manual items were not performed either** — the insert-mode
  dirty-dot check (`FEATURE-6A2F`) and the preview-copy check (`FEATURE-4797`). Both stay unticked,
  and both are on the user before the tag, for the same reason as above.
- **`docs/images/` still does not exist**, so `docs/RELEASE.md` §1's screenshots checkbox stays
  **unticked** — the same pre-flight item 1.3.0, 1.4.0, 1.4.1 and 1.5.0 all shipped without. The
  README's *Captures pending* note and its commented-out image block are untouched, per the plan.
- **Deferred by decision for 1.6.0**, and not attempted here: `docs/RELEASE.md` §2 (manual Linux
  verification), §4 (building the MSI and the Linux tarball) and §5 (the GitHub Release). No
  artifacts were produced — `artifacts/` does not exist in the tree at all.
- **No line-ending noise.** `git diff --check` is empty, and every file this dev touched or created is
  **LF throughout** in the working tree, as `.gitattributes` (`* text=auto eol=lf`) requires. Nothing
  was done and nothing needs to be.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`: **succeeded,
  0 Warning(s), 0 Error(s)**, with `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` on
  solution-wide.
- `grep -i AVLN` over that same normal-verbosity log: **no output**, from a `--no-incremental` build
  across all four projects, so the XAML compiler actually ran rather than being skipped as
  up-to-date. This release *does* change XAML — the tree row template and the tab template both
  gained a control — so the check earned its place: `AVLN*` codes come from an MSBuild task and are
  not promoted by `TreatWarningsAsErrors`, meaning a green build can hide a binding that stopped
  resolving.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3566 passed, 0 failed, 0
  skipped**. The same 3566 `FEATURE-3505` left green — 134 more than the 3432 that shipped in 1.5.0;
  no test was added by this item, because it ships no behaviour.
- **Version stamped as expected**: both `AssemblyInfo.cs` files under `obj/Release/net10.0/` read
  `AssemblyFileVersion("1.6.0.0")` and `AssemblyInformationalVersion("1.6.0+6e7dfa70…")`, which is the
  `X.Y.Z+<commit sha>` shape `docs/RELEASE.md` §6 says to expect.
- **No fix cycles used**: the build and the whole suite were green on the first run.
- `dotnet list package --outdated`: recorded in full, as the plan requires, and **nothing taken**.

  ```text
  The given project `Enigma.MarkdownEditor.Core` has no updates given the current sources.

  Project `Enigma.MarkdownEditor.Desktop` has the following updates to its packages
     [net10.0]:
     Top-level Package                   Requested   Resolved   Latest
     > Microsoft.Extensions.Hosting      10.0.11     10.0.11    10.0.12

  Project `Enigma.MarkdownEditor.Core.UnitTests` has the following updates to its packages
     [net10.0]:
     Top-level Package      Requested   Resolved   Latest
     > xunit.v3             3.2.2       3.2.2      4.0.0

  Project `Enigma.MarkdownEditor.Desktop.UnitTests` has the following updates to its packages
     [net10.0]:
     Top-level Package      Requested   Resolved   Latest
     > xunit.v3             3.2.2       3.2.2      4.0.0
  ```

  The six coupled `Avalonia*` pins, `AvaloniaEdit`, `Markdig`, `coverlet.collector` and every Enigma
  package report nothing newer.
- **Acceptance criteria, one by one.**
  - `<Version>` reads `1.6.0` in both csproj files, and nothing tracked outside the permitted places
    still claims 1.5.0 as current ✓ — a `git grep` for `1.5.0` over every **tracked** file outside
    `docs/plan/` and `docs/done/` returns five kinds of hit, all correct: `RELEASENOTES.md` (its own
    1.5.0 section, plus the 1.6.0 section naming 1.5.0 as the version upgraded *from* and downgraded
    *to*), `docs/roadmap.md`'s row for `FEATURE-F6A4`, the 1.5.0 MSI profile (a historical record),
    `docs/RELEASE.md`'s note that `FEATURE-4797` shipped in 1.5.0, and a comment in
    `WorkspacesFileTests` naming the version whose file shape it pins.
  - `Directory.Packages.props` untouched and the `--outdated` report recorded ✓ — `git diff` on the
    file is empty; the report is above.
  - `msiProfiles/…1.6.0.msipkg.json` exists, is valid JSON, keeps the `upgradeCode` byte-for-byte and
    carries a fresh `productId` differing from all seven earlier profiles' ✓ — parsed with
    `json.load`; eight distinct `productId`s across eight profiles, one shared `upgradeCode`; `diff`
    against 1.5.0 is exactly the `version` and `productId` lines.
  - `RELEASENOTES.md` has a 1.6.0 section above 1.5.0 with a *New Features* entry per shipped item,
    and every earlier section is unmodified ✓ — 55 insertions, 0 deletions.
  - The README what's-new callout reads 1.6.0 ✓.
  - Release build 0 warnings, no `AVLN` output, full suite green ✓ — as above.
  - No `.msi`, no `.tar.gz`, no `artifacts/` content produced; no tag created and no push performed ✓
    — `artifacts/` does not exist, a `find` for `*.msi` and `*.tar.gz` outside `.git/` returns
    nothing, `git tag --list` still reads `1.0.0 1.1.0 1.2.0 1.3.0 1.4.0 1.5.0`, and the only git
    actions taken on this item were creating its branch and committing it.
  - Completion doc written, roadmap and plan flipped to `DONE` ✓ — this document; both statuses read
    `DONE`.

## Printed for the user — never run here

After merging the run branch into `main`:

```bash
git switch main && git pull
git tag 1.6.0
git push origin 1.6.0
```

`docs/RELEASE.md` §2 (manual Linux verification), §4 (building the MSI and the Linux tarball) and §5
(the GitHub Release) are **deferred by decision** for 1.6.0 — no artifacts are produced by this item.
§1's manual checks, including the three added by this release, are on the user **before** the tag.
