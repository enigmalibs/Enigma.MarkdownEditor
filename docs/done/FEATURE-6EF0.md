# FEATURE-6EF0 — Release 1.1.0

**Status: DONE** · Single phase · Branch: `feature/feature-6ef0-release-1-1-0`
Plan: `docs/plan/FEATURE-6EF0.md`

## Summary

Version **1.1.0** is set and documented. This item ships documentation and one JSON profile — **no
artifact was built**: no MSI, no Linux tarball, no publish, no tag. `artifacts/` holds only what
`FEATURE-5B28` left there on 2026-08-28.

`<Version>` moved `1.0.0` → `1.1.0` in **both** `src/*` csproj files, which is all the version
plumbing there is: `build/Common.ps1` reads the Desktop csproj to name artifacts, `AppInfo` reads the
`AssemblyInformationalVersionAttribute` the SDK stamps from it, and no script or source file carries a
literal version of its own.

The MSI profile was cloned from 1.0.0's with **exactly two lines changed** — `version` and a
`productId` generated with `[guid]::NewGuid()` (`c749531f-…`). `upgradeCode` is byte-for-byte the
1.0.0 value (`a7b550b9-1338-458f-bf2b-66dcf1d25380`): change it and Windows Installer stops treating
1.1.0 as an upgrade of 1.0.0 and accumulates it alongside instead. A `diff` of the two profiles shows
those two lines and nothing else.

**The release notes cover seven items, not two.** Four of them — the tab strip polish, the editor and
preview typography, and the tickable preview task boxes (`FEATURE-F3FA`, `FEATURE-E340`,
`FEATURE-C8F1`) — landed after 1.0.0's notes were written and had never appeared in any release notes;
they are documented here rather than left unannounced. Sub-sections follow the house order from the
`dotnet-release` skill (*New Features · Dependencies · Compatibility · Version*), which is where the
plan's own bullet list differed from it by one swap.

**No dependency was refreshed**, deliberately, and the notes say so rather than staying silent: the
`Avalonia.*` set is version-coupled and moves as a set, so it stays at 12.1.1.

## Files/modules touched

**Modified**

- `src/Enigma.MarkdownEditor.Desktop/Enigma.MarkdownEditor.Desktop.csproj` — `<Version>` → `1.1.0`.
- `src/Enigma.MarkdownEditor.Core/Enigma.MarkdownEditor.Core.csproj` — `<Version>` → `1.1.0`.
- `RELEASENOTES.md` — a `v1.1.0` section prepended above 1.0.0's, which is untouched (the diff is
  65 insertions, 0 deletions).
- `README.md` — the what's-new callout now reads 1.1.0; a new **Start-up** group in *Features* covers
  the splash, the first-run questions and the About dialog; the *Screenshots* note lost its stale
  "the images land before the 1.0.0 release" clause (see *Deviations*); and, from the documentation
  sweep, the *Vim key reference*'s two version-shaped labels now name the boundary instead of a
  release — `### Not in 1.0` → `### Not implemented`, and "the v1 contract" → "the vim contract".
  Nothing about vim emulation changed in 1.1.0, which is exactly why the version labels had to go.
- `docs/roadmap.md`, `docs/plan/FEATURE-6EF0.md` — status `TODO` → `DONE`.

**Created**

- `msiProfiles/Enigma.MarkdownEditor.Desktop.1.1.0.msipkg.json` — the committed, never-built profile.
- `docs/done/FEATURE-6EF0.md` — this file.

**Deliberately unchanged**

- `docs/RELEASE.md` — see *Deviations*: the plan's step 5 was already done.
- `Directory.Packages.props` — no dependency bump this release.

## Deviations & follow-ups

- **Plan step 5 was already satisfied when the build started.** The plan says `docs/RELEASE.md` §6
  still claims *"The application has no About box…"*; it does not. `FEATURE-7FA8 PHASE02` had already
  rewritten §6 (the ARP / file-version / About triple, lines 191–201) and added the splash and About
  checks to §2 and §6. The acceptance criterion is met with **no edit to that file** — verified by
  reading it, not assumed.
- **The README *Screenshots* note was reworded, against the plan's letter.** The plan says leave the
  section exactly as it is; its own acceptance criterion says nothing may still claim 1.0.0 as
  current, and *"the images land before the 1.0.0 release"* claimed exactly that. Asked and decided:
  drop the stale clause, keep the section and the commented-out image block. The captures themselves
  are still pending, so **`docs/RELEASE.md` §1's screenshots checkbox stays unticked for 1.1.0** — a
  known, accepted gap, not an oversight.
- **A pre-existing flaky test — worth its own `BUG` item.**
  `ServiceCollectionExtensionsTests.TheComposedContainer_ResolvesTheShellViewModel` **failed on the
  first Release run of the suite and passed on the second**, with the same working tree. It is
  order-dependent, not random: it is a plain `[Fact]`, and resolving the container reaches
  `services.AddSingleton<IFontCatalog>(_ => FontCatalog.ForCurrentSystem())`
  (`ServiceCollectionExtensions.cs:267`) → `FontManager.Current` (`FontCatalog.cs:121`), which throws
  `InvalidOperationException: Unable to locate 'Avalonia.Platform.IFontManagerImpl'` unless the
  assembly's headless Avalonia session (`[assembly: AvaloniaTestApplication]`, `HeadlessTestApp.cs`)
  has already been started by some `[AvaloniaFact]` in the same assembly. Run in isolation
  (`-method "*ResolvesTheShellViewModel*"`) it fails **every** time. Introduced with the `FontCatalog`
  registration in `FEATURE-E340 PHASE01`, and untouched by this item — the only code change here is
  the `<Version>` string. **Not fixed: out of scope for a release item, and weakening the assertion
  would be the wrong repair.** The fix is one of: make the test an `[AvaloniaFact]`, or register a
  fake `IFontCatalog` after `AddShell` exactly as the test already does for `IThemeService` and
  `IUiDispatcher`.
- **Deferred by decision for 1.1.0**, per the plan's *Out of scope*: `docs/RELEASE.md` §2 (the manual
  Linux verification checklist), §4 (both artifacts and the MSI) and §5 (the GitHub Release). Nothing
  in `artifacts/` was produced or touched by this item.
- **Documentation sweep:** two candidates offered, one accepted (the README vim labels above). The
  other — `docs/RELEASE.md` §3's *"This repository has no tags yet, so the convention starts here"*,
  which stops being true the moment `git tag 1.1.0` runs — was **declined** and left as written.
- **Line endings:** nothing to report — every file this item touched is LF-only, and the diffs carry
  no CRLF churn.

## Build/test evidence

| Check | Result |
|---|---|
| `dotnet build Enigma.MarkdownEditor.slnx -c Release` | **Build succeeded — 0 Warning(s), 0 Error(s)** |
| Forced rebuild (`--no-incremental -v normal`), grepped for `AVLN` | **0 matches**, 0 warnings, 0 errors |
| `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release` | **Passed — 1927/1927**, 0 failed, 0 skipped |

The first `dotnet test` run of this item reported **1 failed / 1926 passed**; the failure is the
order-dependent pre-existing test described above, and the re-run on the identical tree was
1927/1927. Both runs are reported here on purpose — the green number alone would misrepresent the
suite's state.

**No tests were added.** This item changes one version string and four documents; there is no
behaviour to cover, and the plan's acceptance criteria call for none. The MSI profile was verified
instead by `diff` against 1.0.0's (two lines) and by parsing it as JSON to confirm `version`,
`productId` and the carried-over `upgradeCode`.

The first Release build of the item also failed outright (`MSB3027`) because a running 1.0.0-era
instance of the editor held `Enigma.MarkdownEditor.Core.dll`; the user closed it and the build was
re-run. Recorded because it will recur on any release cut from a machine with the app open.
