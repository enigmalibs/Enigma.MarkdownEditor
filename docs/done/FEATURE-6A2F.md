# FEATURE-6A2F — Release 1.4.1

**Status: DONE** · Single phase · Branch: `feature/feature-6a2f-release-1-4-1`
Plan: `docs/plan/FEATURE-6A2F.md`

## Summary

Version 1.4.1 cut: the version set in both `src/*` csproj files, the 1.4.1 MSI profile cloned and
committed, and the release documentation written. **No artifacts were built** — no MSI, no Linux
tarball, no publish — and no tag was created or pushed; the tag commands are printed for the user to
run.

**A patch release carrying one item.** `BUG-38F5` is the whole of it, so the version is a SemVer
PATCH and the release notes have a *Fixes* section and nothing else: no *New Features*, no *Changes*
and — the one place this differs in shape from `FEATURE-53E4` (1.4.0) — no *Dependencies*, because no
dependency moved.

**No dependency was touched, and the `--outdated` report was taken anyway.** The plan asks for the
report to be recorded whatever it says, and for nothing to be taken from it; both were honoured. The
only thing it names is `xunit.v3` 3.2.2 → 4.0.0, in the two test projects — the same major
test-framework bump 1.4.0 deliberately held, for the same reason it held it. `Directory.Packages.props`
is byte-identical (`git diff` on it is empty), and neither `src/` project has any update available at
all. Full output under *Build/test evidence*.

**The MSI profile is a two-field clone, per `docs/RELEASE.md` §4.** `version` → `1.4.1` and
`productId` → a freshly generated GUID (`4c91f181-7c56-4a9c-9636-99292fe30d9b`, from
`[guid]::NewGuid()`, distinct from all five earlier profiles'), with `upgradeCode` kept
**byte-for-byte** from 1.4.0 — `a7b550b9-1338-458f-bf2b-66dcf1d25380`. A `diff` against the 1.4.0
profile shows exactly those two lines and nothing else, and the file parses with `json.load`.

**The release notes were written from `docs/done/BUG-38F5.md`, not from memory.** The *Fixes* entry
leads with what a user sees — the dot clearing on every save, including one taken in insert mode, and
staying clear across `Esc` — then names the second hole the same fix closed: an edit landing *during*
the disk write no longer counts as saved. It also states what did **not** change, because that was
the risk the bug's plan was written around: `u` after an insert session still takes the whole session
back in one step, saved mid-way or not. Nothing about the internals (`MarkSaved`, the undo-group
correction, `_editedSinceSave`) is in the notes; they are not user-facing.

## Files/modules touched

**Created**

- `msiProfiles/Enigma.MarkdownEditor.Desktop.1.4.1.msipkg.json` — the 1.4.0 profile with `version`
  and `productId` changed and nothing else.
- `docs/done/FEATURE-6A2F.md` — this document.

**Modified — version**

- `src/Enigma.MarkdownEditor.Desktop/…csproj` — `<Version>` 1.4.0 → 1.4.1.
- `src/Enigma.MarkdownEditor.Core/…csproj` — `<Version>` 1.4.0 → 1.4.1.

No comment in either file names a version number that moved this release — the Desktop csproj's
"version-coupled Avalonia set (12.1.2)" note is about a dependency pin, and no pin moved.

**Modified — prose**

- `RELEASENOTES.md` — a `v1.4.1` section prepended above 1.4.0: *Fixes* (one entry), *Compatibility*,
  *Version*. Purely additive — **35 insertions, 0 deletions** — so the 1.4.0, 1.3.0, 1.2.0, 1.1.0 and
  1.0.0 sections are byte-identical.
- `README.md` — the what's-new callout rewritten for 1.4.1 (3 lines for 3). Nothing else in it
  changed: see *Deviations* on why the editor-features list and the vim key reference needed no edit.
- `docs/RELEASE.md` — one pre-flight item added to §1, from the documentation freshness sweep: a
  manual check that the unsaved dot clears on a save made in insert mode. Nothing in the file was
  *corrected* — it was read in full and nothing in it is false (see *Deviations*); this is an
  addition, and it is the check whose absence let `BUG-38F5` ship in 1.4.0 over a green suite. It
  sits after the test-suite item and before the MSI-profile item, because it is exactly what a green
  suite does not prove, and in §1 rather than §2's Linux smoke set because it is
  platform-independent.

**Untouched, deliberately**

- `Directory.Packages.props` — no dependency moves on a patch release.

**Workflow artifacts**

- `docs/roadmap.md` — `FEATURE-6A2F` `TODO` → `IN PROGRESS` → `DONE`. The table was re-padded on each
  flip: `IN PROGRESS` is two characters wider than the widest Status cell otherwise present
  (`ABANDONED`), so it invalidates every row's alignment on the way in and again on the way out. Net
  diff: one row.
- `docs/plan/FEATURE-6A2F.md` — the item header's status.

## Deviations & follow-ups

- **`docs/RELEASE.md` needed no *correction*, and got one *addition*.** The plan asked for this to be
  read and recorded rather than assumed, so: read in full, and every claim in it still matches the
  repository — nothing was stale and nothing was rewritten. The freshness sweep then added one new §1
  pre-flight item (the insert-mode dot check, above), which is a gap the sweep found rather than
  staleness this dev caused. On the reading itself: §1's version, notes,
  callout, build, `AVLN` and profile items are exactly the pre-flight this item ran; §3's bare-`X.Y.Z`
  tag convention matches `git tag --list`; §4's clone rule is what the 1.4.1 profile was made by;
  §6's `FileVersion` / `ProductVersion` / About-dialog checks are unaffected by a patch. `BUG-38F5`
  changed two view-model files and no behaviour §2 describes — its "type, `Ctrl+S`, and the file on
  disk changes" line was true before the fix and is true after it, since the file was always written.
  Nothing was *added* to §2's smoke set either: per the plan, only a line that had become **wrong**
  was in scope here.
- **The README needed nothing beyond the callout, and this was checked rather than assumed.**
  `BUG-38F5` is the kind of fix that can make prose stale, so the three places the README describes
  the dot were read against it: the *Editor* bullet ("A modified tab carries a dot that becomes its
  close button under the pointer"), the interactive-checkbox bullet ("the tab goes dirty, autosave
  picks it up"), and the vim `:w` `:q` row ("through the same save and unsaved-changes question the
  tab strip uses"). All three describe the behaviour the fix **restored** — the bug violated them
  rather than being documented by them — so all three become true, not stale, and none needed an
  edit.
- **`docs/images/` still does not exist**, so `docs/RELEASE.md` §1's screenshots checkbox stays
  **unticked** — the one pre-flight item this release does not satisfy, exactly as 1.3.0 and 1.4.0
  shipped. The README's *Captures pending* note and its commented-out image block are untouched, per
  the plan.
- **Open follow-up: the `BUG-38F5` manual pass was never performed, and this release does not perform
  it either.** `docs/done/BUG-38F5.md` records its manual verification as not done and says the pass
  "remains available to run before the 1.4.1 release (`FEATURE-6A2F`)" — but this item's plan does not
  ask for it (its §6 pre-flight is build / `AVLN` / test, and §2's manual checklist is deferred by
  decision), and driving keystrokes into a live desktop window and reading a rendered dot back is not
  something this environment can do. So the fix's only proof remains its nine tests, one of which
  (`SavingInInsertMode_HidesTheDotAndEscDoesNotBringItBack`) drives the real `EditorPaneView` in a
  shown window through the real vim engine. **Worth five minutes in the running app before the tag
  goes on:** `i`, type, `Ctrl+S`, check the dot and the file; then `:w`; then autosave; then close the
  tab. Called out here rather than left in the bug's doc, because this is the last item before the
  release — and, as of this dev, written into `docs/RELEASE.md` §1 as a standing pre-flight item, so
  the next release cannot reach a tag without someone having looked at it.
- **Deferred by decision for 1.4.1**, and not attempted here: `docs/RELEASE.md` §2 (manual Linux
  verification), §4 (building the MSI and the Linux tarball) and §5 (the GitHub Release). No artifacts
  were produced — `artifacts/` does not exist in the tree at all, and nothing was written to it.
- **`xunit.v3` 4.0.0 is available and was not taken**, consistent with 1.4.0's stated reason: a major
  test-framework bump is its own piece of work, not a passenger on a release — least of all a patch
  one. It is the only entry in the whole `--outdated` report.
- **No line-ending noise.** `git diff --check` is empty, and every file this dev touched or created is
  **LF throughout** — the working tree included, not just the blobs, as `.gitattributes`
  (`* text=auto eol=lf`) requires. Nothing was done and nothing needs to be. One correction to the
  record while it is in view: `docs/done/FEATURE-53E4.md` states the working tree is "CRLF throughout
  … because this machine has `core.autocrlf=true`". `core.autocrlf` is still `true`, but the tree is
  LF — `.gitattributes`' explicit `eol=lf` overrides it on checkout — so that observation should not
  be carried forward into the next release doc.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release`: **succeeded, 0 Warning(s), 0 Error(s)**, with
  `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` on solution-wide.
- `dotnet build Enigma.MarkdownEditor.slnx -c Release -v normal | grep -i AVLN`: **no output**, from a
  cleaned `obj/Release` **and** `bin/Release` across all four projects, so the XAML compiler actually
  ran rather than being skipped as up-to-date. That run also reported **0 Warning(s), 0 Error(s)**.
  `AVLN*` codes come from an MSBuild task and are not promoted by `TreatWarningsAsErrors`, so a green
  build can hide a broken binding; this release changes no XAML, but the check is the cheap half of
  the reason it is in the runbook.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3304 passed, 0 failed, 0
  skipped** — `Enigma.MarkdownEditor.Core.UnitTests` (5.6 s) and
  `Enigma.MarkdownEditor.Desktop.UnitTests` (23.0 s). The same 3304 `BUG-38F5` left green; no test was
  added, because this item ships no behaviour.
- `dotnet list package --outdated`: recorded in full, as the plan requires, and **nothing taken**.

  ```text
  The given project `Enigma.MarkdownEditor.Core` has no updates given the current sources.
  The given project `Enigma.MarkdownEditor.Desktop` has no updates given the current sources.

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
  - `<Version>` reads `1.4.1` in both csproj files, and nothing in the repo still claims 1.4.0 as
    current ✓ — a grep for `1.4.0` over every **tracked** file outside `docs/plan/` and `docs/done/`
    returns three things, all of them correct: the 1.4.0 MSI profile (a historical record),
    `RELEASENOTES.md` (its own 1.4.0 section, plus the 1.4.1 section naming 1.4.0 as the version
    upgraded *from*), and `docs/roadmap.md`'s row for `FEATURE-53E4`, the 1.4.0 release item itself.
    The `bin/` / `obj/` hits a plain repo-wide grep also finds are git-ignored build output, not
    content.
  - `Directory.Packages.props` is untouched and the `--outdated` report is recorded ✓ — `git diff` on
    the file is empty; the report is above.
  - `msiProfiles/Enigma.MarkdownEditor.Desktop.1.4.1.msipkg.json` exists, is valid JSON, keeps the
    `upgradeCode` byte-for-byte and carries a fresh `productId` differing from all five earlier
    profiles' ✓ — parsed with `json.load`; the six `productId`s across the profiles are six distinct
    values; `diff` against 1.4.0 is exactly the `version` and `productId` lines.
  - `RELEASENOTES.md` has a 1.4.1 section above 1.4.0 with a *Fixes* entry for `BUG-38F5`, and every
    earlier section is unmodified ✓ — 35 insertions, 0 deletions.
  - The README what's-new callout reads 1.4.1 ✓.
  - Release build 0 warnings, no `AVLN` output, full suite green ✓ — as above.
  - No `.msi`, no `.tar.gz`, no `artifacts/` content produced; no tag created and no push performed ✓
    — `artifacts/` does not exist, `git tag --list` still reads `1.0.0 1.1.0 1.2.0 1.3.0 1.4.0`, and
    the only git action taken on this item was creating its branch.
  - Completion doc written, roadmap and plan flipped to `DONE` ✓ — this document; both statuses read
    `DONE`.
