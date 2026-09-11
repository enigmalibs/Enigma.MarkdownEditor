# FEATURE-53E4 — Release 1.4.0

**Status: DONE** · Single phase · Branch: `feature/feature-53e4-release-1-4-0`
Plan: `docs/plan/FEATURE-53E4.md`

## Summary

Version 1.4.0 cut: the version set in both `src/*` csproj files, the two agreed dependency groups
refreshed, the 1.4.0 MSI profile cloned and committed, and the release documentation written. **No
artifacts were built** — no MSI, no Linux tarball, no publish — and no tag was created or pushed;
the tag commands are printed for the user to run.

**Both dependency bumps went in clean, and the one risk the plan named did not materialise.** The
plan's fallback was to hold the whole Avalonia set at 12.1.1 if `Avalonia.Headless.XUnit` 12.1.2
raised its `xunit.v3.extensibility.core` floor above the 3.2.2 this repo pins — that would have made
holding `xunit.v3` a downgrade conflict at restore. It did not: 12.1.2's nuspec still names
`xunit.v3.extensibility.core` 3.2.2 exactly, verified in the resolved package
(`~/.nuget/packages/avalonia.headless.xunit/12.1.2/avalonia.headless.xunit.nuspec`), so the comment
in `Directory.Packages.props` asserting that coupling stays true with its number bumped. The whole
set moved together, and `xunit.v3` stayed at 3.2.2 as planned. `coverlet.collector` 10.0.1 needed no
fallback either — the suite runs green under the Microsoft Testing Platform runner with it.

**Every comment naming a version number was bumped with the pin it describes.** Three files carry
one: `Directory.Packages.props` (the `Avalonia.Headless.XUnit` coupling note),
`src/…Desktop.csproj` ("Version-coupled Avalonia set") and
`tests/…Desktop.UnitTests.csproj` ("Part of the version-coupled Avalonia set"). A stale comment
about a version coupling is worse than none, since it is the only place the coupling is written down.

**The MSI profile is a three-field clone, per `docs/RELEASE.md` §4.** `version` → `1.4.0`,
`productId` → a freshly generated GUID (`2193b810-f1fb-4d40-9df4-780a3f1485fd`, from
`[guid]::NewGuid()`, distinct from all four earlier profiles'), and `upgradeCode` kept **byte-for-byte**
from 1.3.0 — `a7b550b9-1338-458f-bf2b-66dcf1d25380`. A `diff` against the 1.3.0 profile shows exactly
those two lines and nothing else.

**The release notes were written from the phases' own completion docs, not from memory.** Each of the
five `FEATURE-724A` phases and `BUG-B549-PHASE02` has one, and each of the five vim phases declined a
`RELEASENOTES.md` edit in its own freshness sweep on the grounds that this item would write it — so
this is the first and only pass over that document for the cycle. The 1.4.0 section names what came
off the README's *Not implemented* list per phase (the WORD family, the visual commands, `.` and the
registers and macros, regex search and `:s`), which is what makes the list mean anything;
`FEATURE-724A-PHASE05` is not credited with shortening it, because its own completion doc does not
claim it did. `BUG-B549-PHASE01` gets no entry at all — a test-file suppression with no user-visible
effect — as the plan directs.

## Files/modules touched

**Created**

- `msiProfiles/Enigma.MarkdownEditor.Desktop.1.4.0.msipkg.json` — the 1.3.0 profile with `version`
  and `productId` changed and nothing else.
- `docs/done/FEATURE-53E4.md` — this document.

**Modified — version**

- `src/Enigma.MarkdownEditor.Desktop/…csproj` — `<Version>` 1.3.0 → 1.4.0; the Avalonia set comment
  reads 12.1.2.
- `src/Enigma.MarkdownEditor.Core/…csproj` — `<Version>` 1.3.0 → 1.4.0.

**Modified — dependencies**

- `Directory.Packages.props` — the six coupled `Avalonia*` pins 12.1.1 → 12.1.2, the
  `Avalonia.Headless.XUnit` coupling comment with them; `coverlet.collector` 6.0.4 → 10.0.1;
  `xunit.v3` untouched at 3.2.2.
- `tests/Enigma.MarkdownEditor.Desktop.UnitTests/…csproj` — the coupled-set comment reads 12.1.2.

**Modified — prose**

- `RELEASENOTES.md` — a `v1.4.0` section prepended above 1.3.0: *New Features* (one entry per vim
  phase), *Changes* (the settings dialog), *Dependencies*, *Compatibility*, *Version*. Purely
  additive — 111 insertions, 0 deletions, so the 1.3.0, 1.2.0, 1.1.0 and 1.0.0 sections are
  byte-identical. No *Fixes* section: nothing this cycle was a defect fix to a user.
- `README.md` — the what's-new callout rewritten for 1.4.0, leading with the vim work and closing on
  the settings dialog; nine corrections to the vim key reference (see the audit record below).

**Workflow artifacts**

- `docs/roadmap.md` — `FEATURE-53E4` `TODO` → `IN PROGRESS` → `DONE`. The table was re-padded on each
  flip: `IN PROGRESS` is two characters wider than the previous widest Status cell (`ABANDONED`), so
  it invalidates every row's alignment on the way in and again on the way out. Net diff: one row.
- `docs/plan/FEATURE-53E4.md` — the item header's status.

## Deviations & follow-ups

- **`docs/RELEASE.md` needed no change.** The plan expected "a small edit or none" and recorded that
  the likely outcome would be recorded rather than assumed, so: it was read in full against the
  repository as it now stands, and nothing in it is false. §2's vim smoke set was extended by the
  freshness sweeps of `FEATURE-724A` PHASE01, PHASE02, PHASE04 and PHASE05 as each landed, and every
  claim in it still matches the engine — checked line by line against those phases' completion docs.
  Per the plan, the smoke set was **not** grown to cover more of what the engine now does: that is
  `FEATURE-724A`'s business, and only a line that became *wrong* was in scope here.
- **`docs/images/` does not exist at all**, where the plan says it "is empty". Same outcome, and the
  plan's instruction was followed to the letter: the README's *Captures pending* note and the
  commented-out image block are untouched, and `docs/RELEASE.md` §1's screenshots checkbox stays
  **unticked**. It is the one pre-flight item this release does not satisfy, exactly as 1.3.0 shipped.
- **The vim key reference was audited rather than rewritten**, as the plan directs — but the audit
  was not clean, and nine things needed fixing. See the audit record below.
- **Follow-up: a stale code comment in the engine.** `VimEngine.cs:366` still reads "a function key
  or a **Page Down** has no vim meaning" in the comment above the silent-swallow gate. `PageDown` and
  `PageUp` became real commands in `FEATURE-724A-PHASE05` and are read by the parser
  (`KeySequenceParser.cs:464-470`), so the example is now wrong — the gate itself is correct, since
  the page keys never reach it. Left alone deliberately: this is a release item, and editing engine
  source is outside its scope. Worth a one-line fix in the next dev that touches the file.
- **Deferred by decision for 1.4.0**, and not attempted here: `docs/RELEASE.md` §2 (manual Linux
  verification), §4 (building the MSI and the Linux tarball) and §5 (the GitHub Release). No
  artifacts were produced. `artifacts/` still holds only the two tarballs from earlier releases; it
  is git-ignored and nothing new was written to it.
- **No line-ending noise.** `git diff --check` is empty and no diff in this dev carries CRLF↔LF
  churn. What each file stores is LF, as `.gitattributes` (`* text=auto eol=lf`) requires — every
  blob in `HEAD` is LF, and the two created files normalize the same way on commit. The working tree
  itself is CRLF throughout, touched and untouched files alike, because this machine has
  `core.autocrlf=true`; that is a checkout setting, not repository content, and per the workflow it
  is a recommendation only. Nothing was done about it and nothing needs to be.

## Build/test evidence

- `dotnet restore Enigma.MarkdownEditor.slnx`: **succeeded** with both bumps in place — no downgrade
  conflict, which is the gate the plan set on holding `xunit.v3` at 3.2.2.
- `dotnet build Enigma.MarkdownEditor.slnx -c Release`: **succeeded, 0 errors, 0 warnings**, with
  `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` on solution-wide. This is also the gate on
  both dependency bumps.
- `dotnet build Enigma.MarkdownEditor.slnx -c Release -v normal | grep -i AVLN`: **no output**, from
  a cleaned `obj/Release` so the XAML compiler actually ran. `AVLN*` codes come from an MSBuild task
  and are not promoted by `TreatWarningsAsErrors`, so a green build can hide a broken binding — and
  an Avalonia bump plus `BUG-B549-PHASE02`'s new view root are exactly where that would hide.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3295 passed, 0 failed, 0
  skipped** — `Enigma.MarkdownEditor.Core.UnitTests` and
  `Enigma.MarkdownEditor.Desktop.UnitTests`, over the bumped Avalonia set and the new
  `coverlet.collector`. No test was added: this item ships no behaviour.
- **Acceptance criteria, one by one.**
  - `<Version>` reads `1.4.0` in both csproj files, and nothing in the repo still claims 1.3.0 as
    current ✓ — a repo-wide grep for `1.3.0` and `12.1.1` outside `docs/done/`, `docs/plan/`,
    `RELEASENOTES.md` and the older MSI profiles (all four of which are historical records) returns
    only `docs/roadmap.md`'s row for `FEATURE-449F`, the 1.3.0 release item itself.
  - `Directory.Packages.props` pins all six `Avalonia*` packages at 12.1.2 and `coverlet.collector`
    at 10.0.1, with `xunit.v3` still at 3.2.2, and every comment naming a version reads 12.1.2 ✓ —
    the set did not have to be held back; the reason it did not is recorded above.
  - `msiProfiles/Enigma.MarkdownEditor.Desktop.1.4.0.msipkg.json` exists, is valid JSON, keeps the
    1.3.0 `upgradeCode` byte-for-byte, and carries a fresh `productId` differing from all four
    earlier profiles' ✓ — parsed with `json.load`, and the five `productId`s across the profiles are
    five distinct values.
  - `RELEASENOTES.md` has a 1.4.0 section above 1.3.0 covering the six user-facing devs, with the
    older sections unmodified ✓ — 111 insertions, 0 deletions.
  - The README what's-new callout reads 1.4.0, and the vim key reference is exhaustive against the
    engine including the *Not implemented* list ✓ — after nine corrections; see the audit record
    below.
  - Release build 0 warnings, no `AVLN` output, full suite green ✓ — as above.
  - No `.msi`, no `.tar.gz`, no `artifacts/` content produced; no tag created and no push performed
    ✓ — `git tag --list` reads `1.0.0 1.1.0 1.2.0 1.3.0`, and the only git action taken on this item
    was creating its branch.

## Audit record — the vim key reference

The plan asks for the reference to be **verified, not re-done**: the five `FEATURE-724A` phases each
updated it as they landed, so the job here was to find what their own freshness sweeps missed. The
whole reference was enumerated against `KeySequenceParser.Feed` and its tables and then checked
against the engine, and every finding below was re-verified in the source before the README was
touched. **The audit was not clean.** Two statements were false, six behaviours were undocumented in
a section that advertises itself as exhaustive, and one claim over-promised.

**Two things the reference got wrong**

- **`Y` was documented as `y$`.** The row read ``| `D` `C` `Y` | To the end of the line: delete,
  change, yank |``. `Y` is `yy` — `KeySequenceParser.cs:678` emits `Operate(Yank, WholeLine)`, and
  the comment two lines above it (`:673-675`) says so in as many words: "`D` is `d$`, `C` is `c$`,
  `Y` is `yy`". `VimOperatorTests.cs:47` asserts the linewise yank. This is vim's own irregularity
  (neovim changed it), so the code was right and the documentation was wrong. `Y` now has a row of
  its own that says which of the two it is.
- **"A key that is not in this reference is turned down by name rather than silently ignored."** Not
  true of every key, and deliberately so: `VimEngine.cs:366-378` swallows `VimSpecialKey.Other` (the
  function keys), `Tab` and the control characters some platforms report as text **with no message**,
  its own comment reading "Swallowed without a word"; `VimEditorBehavior.cs:59-63,270-272` does the
  same for `Delete` and `Insert` in the command modes. The sentence now says what is named (a
  character key, a half-typed sequence) and what is swallowed, with the reason.

**Six behaviours the engine has and the reference did not document**

- **A count on `gg` / `G` names a line rather than multiplying** — `3G` goes to line 3
  (`VimEngine.cs:1007,1011`; `VimRanges.cs:78-79`). The *Counts* section opened "A count in front of
  almost anything … multiplying" and never named the counts that do not; it now has a paragraph
  listing all of them, `2gg`/`3G` included.
- **Over a selection the count on `>` / `<` is a number of shiftwidths**, not of lines: `V3>` moves
  three widths where `3>>` moves three lines by one (`VimEngine.cs:1277` against `:1090`, both
  commented to that effect; `VimShiftTests.cs:76`).
- **A bare `/` or `?` then `Enter` repeats the last pattern**, taking its direction from the key
  (`VimEngine.cs:2475-2489`) — the same reuse the reference documented only for `:s//bar/`.
- **`:substitute` and `:se` are accepted spellings** of `:s` and `:set` (`VimEngine.cs:2232-2238`).
- **`qA` appends to the recording already in `a`** (`KeySequenceParser.cs:735` accepts any ASCII
  letter; `VimRegisters.Record` appends for an uppercase name, `:134-146`), and **`@"` / `@0` play
  the unnamed and yank registers** while **`""` names the unnamed register explicitly**
  (`KeySequenceParser.cs:745-746`; `VimRegisters.IsName`, `:58-59`).
- **`d%` is inclusive of both brackets and `+` `-` `_` are linewise behind an operator**
  (`VimRanges.cs:76-78,89-93`). The section states the operator-range shape for the finds, the
  searches, the paragraph motions and `H`/`M`/`L`, so these four were the gap in a pattern it
  otherwise keeps.

**One claim made more precise**

- **`*` and `#` do not take "the word under the caret".** They take the first keyword **at or after**
  the caret on its line — `*` on the `(` of `(a)` searches for `a` — and a line with no keyword left
  falls back to the run of punctuation there, matched **literally (escaped)** rather than between
  word boundaries (`VimSearch.cs:104-139`, whose own remarks say so).

**What the audit confirmed was already right**

- **The *Not implemented* list is entirely accurate.** All thirteen entries were checked against the
  code and every one is genuinely still absent: `:` over a selection, the `is` / `ip` / `it` objects,
  `2i(` walking outwards, `"+` (and `"*`), vim's regex dialect, the `c` flag of `:s` and `&`,
  highlighted and incremental search, `:wa` `:qa` `:e`, marks, jump lists and folds. This is the
  check the plan singles out, and it needed no change — the four phases that shortened the list each
  shortened it correctly.
- **No phantoms.** Every key in all seven tables resolves in the parser: the modes and visual tables,
  all 32 motion keys and the eight named keys, the five operators with the three `g` case operators
  and their doubled spellings, all 18 text-object spellings including the `b`/`B` mnemonics and the
  closing-delimiter aliases, all 19 editing keys, and every search and ex entry.
- The prose claims spot-checked against the code all hold: the `cw`→`ce` quirk, `dw` stopping at a
  line's end, the exclusive-linewise rule, `;` after a `t` stepping over its match, a find count
  refusing to settle for a nearer match, `Ctrl+D` stopping where the last line reaches the bottom,
  the `z`-family count landing, `recording @a`, `search hit BOTTOM`, the one-second pattern timeout,
  `3 substitutions on 2 lines`, and the four mode labels.

Nothing in the engine was changed to satisfy the audit: every fix was to the documentation, because
in all nine cases the code was doing the right thing and the reference was describing it wrongly or
not at all.
