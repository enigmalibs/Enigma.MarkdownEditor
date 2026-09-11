# FEATURE-449F — Release 1.3.0

**Status: DONE** · Single phase · Branch: `feature/feature-449f-release-1-3-0`
Plan: `docs/plan/FEATURE-449F.md`

## Summary

Version 1.3.0 is cut: the version is set in both project files, the versioned MSI profile is
committed, and the release documentation is written. **No artifacts were produced** — no MSI, no
Linux tarball, no publish — and no tag was created or pushed. Those commands are printed for the user
to run.

**Version.** `<Version>1.2.0</Version>` → `<Version>1.3.0</Version>` in the Desktop and Core csproj
files, the two that must move together because `build/Common.ps1` names the artifacts off the first
and the profile mirrors it. Nothing else in the repository still claims 1.2.0 as *current*: the
remaining occurrences are the 1.2.0 MSI profile, the 1.2.0 section of `RELEASENOTES.md`, the
`Release 1.2.0` roadmap row and the plan/done records of earlier work — all of them historical by
nature, and all of them wrong to rewrite.

**MSI profile.** `msiProfiles/Enigma.MarkdownEditor.Desktop.1.3.0.msipkg.json`, cloned from the 1.2.0
profile per the clone rule in `docs/RELEASE.md` §4. A `diff` against its source shows **exactly two
changed lines**: `version` → `1.3.0`, and `productId` → `4dec3add-7de7-44b8-b38a-76b848795f1d`,
generated with `[guid]::NewGuid()` and checked against the three earlier profiles' IDs. The
`upgradeCode` `a7b550b9-1338-458f-bf2b-66dcf1d25380` carries over byte-for-byte — changing it would
make Windows Installer treat 1.3.0 as an unrelated product and accumulate it beside 1.2.0 rather than
upgrade it. Everything else is untouched, the TFM not having moved.

**Release notes.** A `v1.3.0` section prepended above the 1.2.0 one, which — with 1.1.0 and 1.0.0 — is
byte-for-byte unmodified. It covers all six devs shipped this cycle: the two vim additions as the
headline (and says out loud that both were on the engine's published *Not implemented* list since
1.0.0, so the release shortens that list rather than lengthening an open-ended one), Settings moving
into a dialog, the lowered editor default and the preview label vocabulary under *Changes*, and the
tab-strip scrollbar and preview checkbox repairs under *Fixes*. *Dependencies* records that nothing
moved and that the coupled `Avalonia.*` set was held at 12.1.1 deliberately; *Compatibility* records
that no settings key was added, renamed or retyped, so a 1.2.0 `settings.json` needs no migration.

**README.** The what's-new callout reads 1.3.0 and leads with the vim keys. The vim key reference
needed **no change** — see below.

## Files/modules touched

**Created**

- `msiProfiles/Enigma.MarkdownEditor.Desktop.1.3.0.msipkg.json` — the version's committed MSI profile.
- `docs/done/FEATURE-449F.md` — this file.

**Modified**

- `src/Enigma.MarkdownEditor.Desktop/Enigma.MarkdownEditor.Desktop.csproj` — `<Version>` → `1.3.0`.
- `src/Enigma.MarkdownEditor.Core/Enigma.MarkdownEditor.Core.csproj` — `<Version>` → `1.3.0`.
- `RELEASENOTES.md` — the v1.3.0 section prepended; nothing below it touched.
- `README.md` — the what's-new callout only.
- `docs/roadmap.md` — `FEATURE-449F` → `DONE`, and the whole table re-padded (see *Deviations*).
- `docs/plan/FEATURE-449F.md` — header status → `DONE`.
- `docs/RELEASE.md` — §3's tag sentence only, out of the freshness sweep (see *Deviations*).

**Deliberately not modified**

- `docs/RELEASE.md` **as the plan's step 5 defines it** — read in full against the repository as it
  now stands, and nothing the six devs shipped made any of it false, so step 5 produced no edit. The
  one line that did change came from the freshness sweep instead, for a sentence that was already
  wrong before this release cycle began (see *Deviations*).
- The README *Vim key reference* — verified, not re-done, per the plan.
- The README *Screenshots* section — the *Captures pending* note and the commented-out image block
  are exactly as they were.

## Deviations & follow-ups

- **The vim key reference needed no edit.** The plan required verifying rather than redoing it, and
  the verification came out clean against the engine as it now stands: the *Motions* table carries
  `f<char>` `F<char>` `t<char>` `T<char>` and `;` `,`; the *Operators* table carries the
  text-object row; a *Text objects* section documents `iw`/`aw`, the four bracket pairs and the three
  quote characters; and §*Not implemented* no longer names text objects generically, listing instead
  the `W` `B` `E` WORDs with `iW`/`aW`, the sentence, paragraph and tag objects, and `2i(`. Each claim
  was checked against the source, not assumed: `VimMotion` has exactly the six find members,
  `VimTextObject` exactly the sixteen inner/around members and no WORD/sentence/paragraph/tag member,
  and the alias spellings the README advertises (`i)` `ib`, `i]`, `i}` `iB`, `i>`) are the ones
  `KeySequenceParser.cs:295-298` actually accepts. `FEATURE-23AB` PHASE02's own freshness sweep had
  left nothing behind.
- **Sub-section order in the release notes follows the shipped files, not the plan's bullet order.**
  The plan enumerated *Compatibility* before *Dependencies*; every released section in
  `RELEASENOTES.md` puts *Dependencies* first. The plan asked for "the house order" and then listed
  its bullets in a different one, so the file won — the new section reads New Features · Changes ·
  Fixes · Dependencies · Compatibility · Version. *Changes* is new this release and sits between the
  additions and the repairs, which is the only place it reads in sequence.
- **The roadmap table was re-padded whole, so its diff is 57 lines rather than 1.** Flipping the last
  `IN PROGRESS` row to `DONE` left `ABANDONED` as the widest Status cell, narrowing that column from
  11 characters to 9; the workflow requires every row re-padded to the current widest cell on any
  edit. The only *semantic* change in that file is `FEATURE-449F`'s status.
- **`docs/RELEASE.md` §3 carried a pre-existing inaccuracy, fixed through the freshness sweep.** It
  read *"This repository has no tags yet, so the convention starts here"* — untrue since 1.0.0 was
  tagged, and untrue before any of the six devs in this release, so it fell outside the plan's step 5,
  which is scoped to what those six devs made false. Raised in the documentation freshness sweep and
  accepted there: the sentence now reads *"The tag convention is a **bare `X.Y.Z`**, no `v` prefix."*
  One line changed, and the tag commands under it are untouched.
- **The README screenshots are still pending, so `docs/RELEASE.md` §1's screenshot checkbox cannot be
  ticked for 1.3.0.** This is a decision carried forward from 1.0.0, not an oversight: the plan puts
  the captures out of scope and the *Captures pending* note stays. Recording it here is what the plan
  asks for in place of quietly ignoring it. **Follow-up:** capture
  `docs/images/screenshot-dark.png` and `docs/images/screenshot-light.png` and uncomment the image
  block.
- **Deferred by decision for 1.3.0**, and not failures of this item: `docs/RELEASE.md` §2 (the manual
  Linux verification checklist), §4 (building the MSI and the Linux tarball) and §5 (publishing the
  GitHub Release). This item produces no artifacts by design.
- **No line-ending noise.** Every file touched is LF, as `.gitattributes` (`* text=auto eol=lf`)
  requires — checked on `RELEASENOTES.md`, `README.md` and the new profile after writing, and every
  diff is line-scoped with no whole-file churn. Nothing to recommend.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release -v normal`: **Build succeeded, 0 Warning(s),
  0 Error(s)**. The `<Version>` change invalidates every assembly, so this was a full recompile rather
  than an incremental no-op.
- **`AVLN` sweep on that same normal-verbosity log: no output**, as `docs/RELEASE.md` §1 requires.
  `AVLN*` codes come from an MSBuild task and are not promoted by `TreatWarningsAsErrors`, so a green
  build can still hide a broken compiled binding — worth running even though this item touched no
  XAML, because `FEATURE-414B-PHASE02` moved every settings row into a new view.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **2127 passed, 0 failed, 0
  skipped** across `Enigma.MarkdownEditor.Core.UnitTests` and
  `Enigma.MarkdownEditor.Desktop.UnitTests`. No test was added: this item ships no behaviour, and its
  acceptance criteria are verified by inspection and by the release build itself.
- **Acceptance criteria, one by one.**
  - `<Version>` reads 1.3.0 in both csproj files, and nothing in the repo still claims 1.2.0 as
    current ✓ — both files grepped after the edit; every surviving `1.2.0` enumerated above and shown
    to be a historical record.
  - The 1.3.0 MSI profile exists, is valid JSON, keeps the 1.2.0 `upgradeCode` byte-for-byte and
    carries a fresh `productId` ✓ — parsed with a JSON reader (not eyeballed); `diff` against the
    1.2.0 profile shows exactly the two intended lines; the new `productId` differs from
    `02ef086b-…` (1.0.0), `c749531f-…` (1.1.0) and `a20d5fcf-…` (1.2.0).
  - `RELEASENOTES.md` has a 1.3.0 section above 1.2.0 covering all six devs, the older sections
    unmodified ✓ — the four `# Enigma Markdown Editor vX.Y.Z` headings now sit at lines 1, 87, 152 and
    218, and `git diff` on the file is a pure prepend with no line below the new section changed.
  - The README what's-new callout reads 1.3.0, and the vim key reference is exhaustive against the
    engine including the corrected *Not implemented* list ✓ — callout replaced; reference verified
    against `VimMotion`, `VimTextObject` and `KeySequenceParser` as detailed above.
  - Release build 0 warnings, no `AVLN`, full suite green ✓ — as above.
  - No `.msi`, no `.tar.gz`, no `artifacts/` content produced; no tag created, no push performed ✓ —
    `artifacts/` holds only the two tarballs and the publish folder dated 2026-08-28, four days before
    this build, and it is git-ignored; `git tag -l` still lists `1.0.0`, `1.1.0`, `1.2.0` and no
    `1.3.0`. `git status --porcelain` shows exactly the six modified files and the new profile.
