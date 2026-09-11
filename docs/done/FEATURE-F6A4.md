# FEATURE-F6A4 — Release 1.5.0

**Status: DONE** · Single phase · Branch: `feature/feature-f6a4-release-1-5-0` (cut from
`feature/feature-4797-phase02-preview-html`)
Plan: `docs/plan/FEATURE-F6A4.md` · Roadmap: `docs/roadmap.md`

## Summary

Version 1.5.0 cut: the version set in both `src/*` csproj files, the 1.5.0 MSI profile cloned and
committed, and the release documentation written. **No artifacts were built** — no MSI, no Linux
tarball, no publish — and no tag was created or pushed; the tag commands are printed for the user to
run.

**A minor release carrying one item.** `FEATURE-4797` and its two phases are the whole of it, so the
version is a SemVer MINOR and the release notes have a *New Features* section and nothing else: no
*Fixes*, no *Changes*, and — as with `FEATURE-6A2F` (1.4.1) — no *Dependencies*, because no dependency
moved. Functionality was added; nothing was removed, deprecated or changed in shape.

**No dependency was touched, and the `--outdated` report was taken anyway.** The plan asks for the
report to be recorded whatever it says, and for nothing to be taken from it; both were honoured. The
only thing it names is `xunit.v3` 3.2.2 → 4.0.0, in the two test projects — the same major
test-framework bump 1.4.0 and 1.4.1 deliberately held, for the same reason they held it.
`Directory.Packages.props` is byte-identical (`git diff` on it is empty), and neither `src/` project
has any update available at all. Full output under *Build/test evidence*.

**The MSI profile is a two-field clone, per `docs/RELEASE.md` §4.** `version` → `1.5.0` and
`productId` → a freshly generated GUID (`2385d51d-4de1-434a-b261-f4d0c7abc62f`, from
`[guid]::NewGuid()`, distinct from all six earlier profiles'), with `upgradeCode` kept
**byte-for-byte** from 1.4.1 — `a7b550b9-1338-458f-bf2b-66dcf1d25380`. A `diff` against the 1.4.1
profile shows exactly those two lines and nothing else, and the file parses with `json.load`.

**The release notes were written from `docs/done/FEATURE-4797-PHASE01.md` and
`docs/done/FEATURE-4797-PHASE02.md`, not from memory.** The *New Features* section leads with the
gesture — `Ctrl+A` then `Ctrl+C`, the header button and the context menu, all three obeying the same
selection-or-whole-note rule — then with what actually lands on the clipboard, and closes on the
second flavour and what it is for. The two rules that took the most work to get right in the phases
are stated because a user can see both: the text is the note *as it reads* (markers and task boxes
kept, fences dropped, table rows tab-separated, `[alt text]` where an image sat), and the HTML is
*unstyled* on purpose, so a note copied out of the dark theme does not paste as pale text on a white
page. Nothing about the internals is in the notes — not `MarkdownPlainText`'s `Blocks` projection,
not the CF_HTML envelope, not `DataFormat.CreateBytesPlatformFormat`; they are not user-facing.

## Files/modules touched

**Created**

- `msiProfiles/Enigma.MarkdownEditor.Desktop.1.5.0.msipkg.json` — the 1.4.1 profile with `version`
  and `productId` changed and nothing else.
- `docs/done/FEATURE-F6A4.md` — this document.

**Modified — version**

- `src/Enigma.MarkdownEditor.Desktop/…csproj:8` — `<Version>` 1.4.1 → 1.5.0.
- `src/Enigma.MarkdownEditor.Core/…csproj:13` — `<Version>` 1.4.1 → 1.5.0.

No comment in either file names a version number that moved this release — the Desktop csproj's
"version-coupled Avalonia set (12.1.2)" note is about a dependency pin, and no pin moved.

**Modified — prose**

- `RELEASENOTES.md` — a `v1.5.0` section prepended above 1.4.1: *New Features* (three entries),
  *Compatibility*, *Version*. Purely additive — **45 insertions, 0 deletions** — so the 1.4.1, 1.4.0,
  1.3.0, 1.2.0, 1.1.0 and 1.0.0 sections are byte-identical.
- `README.md` — the what's-new callout rewritten for 1.5.0 (4 lines for 3), and the *Preview* bullet
  extended with the rich-text flavour, from the documentation freshness sweep. See *Deviations* for
  why that bullet was short of a feature 1.5.0 ships.
- `docs/RELEASE.md` — one pre-flight item added to §1, from the documentation freshness sweep: a
  manual check that the preview copies in both flavours, including the paste into Word that no test
  can stand in for. Nothing in the file was *corrected* — it was read in full and nothing in it is
  false (see *Deviations*); this is an addition, and it sits after the insert-mode dot check and
  before the MSI-profile item, in §1 rather than §2's Linux smoke set because it is
  platform-independent.

**Untouched, deliberately**

- `Directory.Packages.props` — no dependency moves on a feature release.

**Workflow artifacts**

- `docs/roadmap.md` — `FEATURE-F6A4` `TODO` → `IN PROGRESS` → `DONE`. `IN PROGRESS` is the widest
  Status cell the table can hold and it was already the column's width, so the padding was stable
  across both flips and no other row moved. Net diff: one row.
- `docs/plan/FEATURE-F6A4.md` — the item header's status.

## Deviations & follow-ups

- **The `1.4.1` tag does not exist, and the plan assumed it did.** The plan's *Context & constraints*
  reads "`1.0.0` … `1.4.1` exist; `1.5.0` must not". `git tag --list` returns `1.0.0 1.1.0 1.2.0
  1.3.0 1.4.0` — 1.4.1 was cut as a commit (`b5eed77`) and merged, but never tagged. Nothing in this
  item depends on it: what that constraint protects is that `1.5.0` must not already exist, and it
  does not. Recorded rather than acted on, because tagging is the user's step and a retroactive
  `1.4.1` on the right commit is a decision, not a chore. **Worth settling before `1.5.0` goes on**,
  so the tag history does not keep a hole in it.
- **`docs/RELEASE.md` needed no correction.** The plan asked for this to be read and recorded rather
  than assumed, so: read in full, and every claim in it still matches the repository. §1's version,
  notes, callout, build, `AVLN` and profile items are exactly the pre-flight this item ran; §3's
  bare-`X.Y.Z` tag convention is what the printed commands use; §4's clone rule is what the 1.5.0
  profile was made by; §6's `FileVersion` / `ProductVersion` / About-dialog checks are unaffected by
  a minor release. §2's *Preview* line ("a note with headings, a GFM table, a fenced code block, a
  task list and an image renders correctly") describes rendering, which `FEATURE-4797` did not change
  — it added a way to take the rendering out, and the pane draws exactly what it drew in 1.4.1. So
  nothing in §2 became **wrong**; what `FEATURE-4797` leaves is a *gap* rather than staleness. The
  sweep closed it with a new §1 item rather than a §2 one, because the copy and the paste are
  platform-independent — §2 keeps the X11 `text/html` target, which is the part that is not.
- **The README's Preview bullets covered PHASE01 only, and the plan expected them to cover both.**
  The plan's §4 says the Preview bullets and the `Ctrl+` reference table are "already updated by
  `FEATURE-4797` itself; verify rather than repeat". Verified: the `Ctrl+A` / `Ctrl+C` row in the
  shortcut table is there, and the Preview bullet describes the plain-text copy exactly — but neither
  mentions the rich-text flavour, because `FEATURE-4797-PHASE02` declined its own documentation
  sweep. Surfaced in this item's sweep rather than folded in silently, and **accepted**: the bullet
  now carries the second flavour, and why it is deliberately unstyled.
- **The release notes do not mention that the clipboard does not survive the application exiting.**
  `docs/done/FEATURE-4797-PHASE02.md` records it: Avalonia writes through `OleSetClipboard`, which
  delegates rendering to the writing process, and nothing calls `IClipboard.FlushAsync`, so a copy is
  gone from the clipboard once the editor closes. It is **pre-existing** — PHASE01's plain-text write
  and every other copy the application makes go through the same call — and this item's plan
  enumerates the release-notes sections sentence by sentence with no *Known issues* among them, so
  adding one would have been a redesign of the plan rather than an execution of it. **Worth its own
  work item**, as PHASE02 already said; if it gets one, the note belongs in that release's notes.
- **The manual checks the two phases asked for before this release were not performed here.** Both
  completion docs name checks worth doing "before the 1.5.0 release item", and this item did none of
  them, because driving a live desktop window and watching another application render a paste is not
  something this environment can do. All three remain open, and all three are cheap — and as of the
  documentation sweep above, the first and the third are now a standing `docs/RELEASE.md` §1
  pre-flight item, so no future release reaches a tag without someone having looked at them:
  - **Nobody has watched Word render the paste.** PHASE02 proved the bytes on a real Windows
    clipboard end to end — the formats present, the `HGLOBAL` size, UTF-8, the declared fragment
    offsets — but how Word, Outlook and LibreOffice *display* valid markup is a separate question
    from whether the markup is valid.
  - **The X11 `text/html` target has never run on a real Linux session.**
  - **The `PREVIEW` header button and the context menu are compile-verified, not clicked.** Compiled
    bindings, the `x:DataType`, the `Click` handler names and the icon all resolve at build time and
    `AVLN` is clean, but nothing in the suite builds `MainWindow`, so no test exercises those two
    surfaces end to end — including their disabled state with no tab open.
- **`docs/RELEASE.md` §1's insert-mode dirty-dot check was not performed either.** It is a standing
  pre-flight item, added by `FEATURE-6A2F` precisely so a release could not reach a tag without
  someone having looked at it. This item cannot drive keystrokes into a live window, so it stays
  **unticked**, and it is on the user before the tag — as is the `FEATURE-4797` set above.
- **`docs/images/` still does not exist**, so `docs/RELEASE.md` §1's screenshots checkbox stays
  **unticked** — the same pre-flight item 1.3.0, 1.4.0 and 1.4.1 all shipped without. The README's
  *Captures pending* note and its commented-out image block are untouched, per the plan.
- **Deferred by decision for 1.5.0**, and not attempted here: `docs/RELEASE.md` §2 (manual Linux
  verification), §4 (building the MSI and the Linux tarball) and §5 (the GitHub Release). No
  artifacts were produced — `artifacts/` does not exist in the tree at all, and nothing was written
  to it.
- **`xunit.v3` 4.0.0 is available and was not taken**, consistent with 1.4.0's and 1.4.1's stated
  reason: a major test-framework bump is its own piece of work, not a passenger on a release. It is
  the only entry in the whole `--outdated` report.
- **No line-ending noise.** `git diff --check` is empty, and every file this dev touched or created
  is **LF throughout** in the working tree, not just in the blobs, as `.gitattributes`
  (`* text=auto eol=lf`) requires. Nothing was done and nothing needs to be.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`: **succeeded,
  0 Warning(s), 0 Error(s)**, with `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` on
  solution-wide.
- `grep -i AVLN` over that same normal-verbosity log: **no output**, from a `--no-incremental` build
  across all four projects, so the XAML compiler actually ran rather than being skipped as
  up-to-date. `AVLN*` codes come from an MSBuild task and are not promoted by
  `TreatWarningsAsErrors`, so a green build can hide a broken binding; this release changes no XAML,
  but the check is the cheap half of the reason it is in the runbook.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3432 passed, 0 failed, 0
  skipped**. The same 3432 `FEATURE-4797-PHASE02` left green; no test was added, because this item
  ships no behaviour.
- **Version stamped as expected**: both `AssemblyInfo.cs` files under `obj/Release/net10.0/` read
  `AssemblyFileVersion("1.5.0.0")` and `AssemblyInformationalVersion("1.5.0+276c9bb…")`, which is the
  `X.Y.Z+<commit sha>` shape `docs/RELEASE.md` §6 says to expect.
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
  - `<Version>` reads `1.5.0` in both csproj files, and nothing in the repo still claims 1.4.1 as
    current ✓ — a `git grep` for `1.4.1` over every **tracked** file outside `docs/plan/` and
    `docs/done/` returns three kinds of hit, all correct: the 1.4.1 MSI profile (a historical
    record), `RELEASENOTES.md` (its own 1.4.1 section, plus the 1.5.0 section naming 1.4.1 as the
    version upgraded *from*), and `docs/roadmap.md`'s row for `FEATURE-6A2F`, the 1.4.1 release item
    itself. The `bin/` / `obj/` hits a plain repo-wide grep also finds are git-ignored build output,
    and they read 1.5.0 now anyway.
  - `Directory.Packages.props` is untouched and the `--outdated` report is recorded ✓ — `git diff` on
    the file is empty; the report is above.
  - `msiProfiles/Enigma.MarkdownEditor.Desktop.1.5.0.msipkg.json` exists, is valid JSON, keeps the
    `upgradeCode` byte-for-byte and carries a fresh `productId` differing from all six earlier
    profiles' ✓ — parsed with `json.load`; the seven `productId`s across the seven profiles are seven
    distinct values; `diff` against 1.4.1 is exactly the `version` and `productId` lines.
  - `RELEASENOTES.md` has a 1.5.0 section above 1.4.1 with a *New Features* entry for
    `FEATURE-4797`, and every earlier section is unmodified ✓ — 45 insertions, 0 deletions.
  - The README what's-new callout reads 1.5.0 ✓.
  - Release build 0 warnings, no `AVLN` output, full suite green ✓ — as above.
  - No `.msi`, no `.tar.gz`, no `artifacts/` content produced; no tag created and no push performed ✓
    — `artifacts/` does not exist, a `find` for `*.msi` and `*.tar.gz` outside `.git/` returns
    nothing, `git tag --list` still reads `1.0.0 1.1.0 1.2.0 1.3.0 1.4.0`, and the only git action
    taken on this item was creating its branch.
  - Completion doc written, roadmap and plan flipped to `DONE` ✓ — this document; both statuses read
    `DONE`.
