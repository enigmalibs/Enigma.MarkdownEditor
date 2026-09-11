# FEATURE-A225-PHASE01 — Colours in the workspaces file

**Status: DONE** · Phase 1 of 3 · Branch: `feature/feature-a225-phase01-colour-store` (cut from
`vibe/2026-09-10-title-colors-vim-release`)
Plan: `docs/plan/FEATURE-A225.md` · Roadmap: `docs/roadmap.md`

## Summary

The colour itself, with no UI: the palette as an enum, the per-workspace persistence, the two
resolution questions, and the one service call that writes a colour. Nothing on screen changes in
this phase — PHASE02 and PHASE03 are what draw it.

**Eight colours and `None`, as an enum with `None = 0`.** A fixed set validates where a hex string
cannot, stays readable when the file is edited by hand, and reads as one design where eight
separately-chosen colours would not. `None` being zero is what makes an unset property, and a value
a future build writes that this one does not know, come out as *no colour* rather than as a colour
nobody picked.

**A list of entries, not a path-keyed map.** `Workspace.FolderColors` is a
`List<FolderColorEntry>`, mirroring `RootDirectories` and `Workspaces` — the two shapes this
repository has already proven round-trip through Enigma.Configuration. A dictionary keyed by a
Windows path would have put escaped backslashes in the file's *keys* and would have been the first
dictionary binding here; neither is worth finding out about at release time.

**Two questions, because two surfaces ask different ones.** `FolderColorMap.ColorOf` is exact — "is
*this* folder the one carrying a colour?" — and is what a tree row will show, so the tree shows where
a colour was set rather than tinting a whole subtree. `FolderColorMap.Resolve` walks up — "which
coloured folder does this file belong to?" — and is what a tab will show. The walk goes through
`Path.GetDirectoryName` rather than comparing prefixes, which is the difference between `C:\a\bb`
being a sibling of a coloured `C:\a\b` and being read as its child; and the **deepest** match wins,
so a colour on a sub-folder overrides its parent's on purpose. Neither method asks whether a path
exists, so an unplugged root still answers with its colour and the whole rule is testable without a
file system.

**A new event, and this is the load-bearing decision of the phase.**
`IWorkspaceService.FolderColorsChanged` exists because both of the existing events are wired, in
`FileTreeViewModel`'s constructor, to `PostRefresh()` — a full re-listing of every root. Announcing a
colour through either of them would collapse the user's expanded folders every time they picked one.
A colour changes how the same entries are *drawn* and nothing about what they are, so it gets its own
signal and subscribers repaint in place.

**`Version` stays 1, deliberately.** The key is additive: a file written by 1.5.0 has no
`FolderColors` and picks up the empty default (pinned by a test that spells out the pre-1.6.0 file),
and a file written by 1.6.0 is still read by 1.5.0, which ignores what it does not know. Bumping the
version would have made this build quarantine its own predecessor's file over an added key.

**Rollback needed a real copy.** `Workspace.Clone` now clones the *entries*, not just the list:
`MutateAsync` mutates the live object and restores the snapshot if the write throws, so a snapshot
holding the same `FolderColorEntry` instances would have restored the objects the failed mutation had
already edited. There is a test that fails without it.

## Files/modules touched

**Created**

- `src/Enigma.MarkdownEditor.Core/Workspaces/FolderColor.cs` — the enum.
- `src/Enigma.MarkdownEditor.Core/Workspaces/FolderColorEntry.cs` — one folder and its colour, with
  the two per-property attributes and an `internal Clone()`.
- `src/Enigma.MarkdownEditor.Core/Workspaces/FolderColorMap.cs` — `ColorOf` and `Resolve`.
- `tests/…/Core.UnitTests/Workspaces/FolderColorMapTests.cs` — 15 cases: exact versus inherited,
  inheritance through three levels, deepest-wins, the name-prefix sibling, a trailing separator, a
  relative path, case (asserted against the platform rather than assumed), an entry hand-edited to
  `None`, no path at all, nothing coloured, and the walk terminating at the drive root.
- `docs/done/FEATURE-A225-PHASE01.md` — this document.

**Modified**

- `Workspaces/Workspace.cs` — the `FolderColors` property, and `Clone()` copying its entries.
- `Workspaces/WorkspacesConfigValidator.cs` — one more rule (`HasUsableFolderColors`): no blank path,
  no folder coloured twice. The same two rules the root directories already get.
- `Workspaces/IWorkspaceService.cs` — `FolderColorsChanged` and `SetFolderColorAsync`.
- `Workspaces/WorkspaceService.cs` — the implementation: normalize, then add / replace / remove
  through `MutateAsync`, then raise the one event. A call asking for the colour the folder already
  has returns before writing anything.
- `tests/…/Core.UnitTests/Workspaces/WorkspaceServiceTests.cs` — 8 cases: colour and survive a
  restart, stored normalized, replace rather than duplicate, `None` removes the entry, the event
  discipline, the no-op call, the failed write putting the colours back, and an unknown workspace.
- `tests/…/Core.UnitTests/Workspaces/WorkspacesFileTests.cs` — 4 cases: the round trip with the
  colour written **by name**, a pre-1.6.0 file loading with no colours and no failure, a blank path
  rejected by the attribute, and one folder coloured twice rejected by the validator.
- `tests/…/Core.UnitTests/Workspaces/TempWorkspacesFolder.cs` — `BlockWrites()`, which puts a
  directory where the file belongs so a mutation's rollback path can be reached.
- `tests/…/Desktop.UnitTests/Fakes/FakeWorkspaceService.cs` — the two new interface members, with
  the same add/replace/remove and the same event discipline as the real service, so the ViewModel
  tests in PHASE02 and PHASE03 exercise the same contract.

## Deviations & follow-ups

- **The plan's validator message was reworded.** It says the rule reports "must not colour a blank
  folder twice or at all"; the implementation says *"must not colour a blank folder, or one folder
  twice."* The first phrasing does not parse as the two rules it covers. Same rule, clearer sentence.
- **`FolderColorEntry` accepts `FolderColor.None` on the way in, though nothing writes it.** Clearing
  a colour removes the entry, so `None` never reaches the file from this application — but a
  hand-edited file holding one means "no colour", and quarantining a whole workspaces file over a
  harmless value would be the wrong trade. A test pins that such an entry does not shadow a colour
  set above it.
- **`FolderColorEntry.Path` is not required to be under one of the workspace's roots.** A root on a
  removable drive is missing while it is unplugged, and an entry dropped meanwhile is a colour the
  user has to set again. Recorded because it is a rule deliberately *not* added.
- **`BlockWrites()` is a platform-shaped trick.** It reaches the rollback path by putting a directory
  at the file's path, which makes the write throw on Windows and on Linux. If a future platform lets
  that write succeed, the test fails loudly rather than silently passing — which is the right way for
  it to break.
- **Nothing user-facing changed, so the documentation sweep changed nothing.** README, `docs/RELEASE.md`
  and the rest of the prose describe behaviour, and this phase adds none that can be seen; the
  README's tree and tab sections become stale in PHASE02 and PHASE03, and that is where they are
  updated. Scanned rather than assumed.
- **No line-ending noise.** `git diff --check` is empty and every file touched or created is LF
  throughout, as `.gitattributes` requires.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release -v normal`: **succeeded, 0 Warning(s), 0
  Error(s)**, with `TreatWarningsAsErrors` and `EnforceCodeStyleInBuild` on solution-wide.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3479 passed, 0 failed, 0
  skipped** — 3453 before this phase, plus the 26 added here.
- **No fix cycles used**: the build and the whole suite were green on the first run.
- **Acceptance criteria, one by one.**
  - A colour survives a reload; clearing it removes the entry ✓ —
    `SetFolderColorAsync_ColoursAFolderAndSurvivesARestart`,
    `SetFolderColorAsync_WithNone_TakesTheEntryOutAltogether`.
  - The enum is written by name; a blank path and a duplicated folder are reported ✓ —
    `TheFile_RoundTripsFolderColoursWithTheColourWrittenByName`, `AColouredFolderWithNoPath_IsRejected`,
    `OneFolderColouredTwice_IsRejected`.
  - `Resolve` answers the deepest coloured ancestor and never treats a name-prefix sibling as a child
    ✓ — `AColouredSubFolder_OverridesItsColouredParent`,
    `AFolderWhoseNameMerelyStartsTheSameWay_IsNotAChild`.
  - `FolderColorsChanged` is the only event a colour write raises ✓ —
    `SetFolderColorAsync_RaisesTheColourEventAndNeitherOfTheOthers`.
  - Release build 0 warnings, full suite green ✓ — above.
