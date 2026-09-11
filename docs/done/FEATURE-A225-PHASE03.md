# FEATURE-A225-PHASE03 — Tab colours

**Status: DONE** · Phase 3 of 3 · Branch: `feature/feature-a225-phase03-tab-colours` (cut from
`vibe/2026-09-10-title-colors-vim-release`)
Plan: `docs/plan/FEATURE-A225.md` · Roadmap: `docs/roadmap.md`

## Summary

The half the feature exists for: a tab now carries the colour of the folder its note came from, in
the same 4px bar the tree uses, kept current through a colour change, a workspace switch and a
*Save as*. `FEATURE-A225` is complete with this phase.

**Inherited, not owned.** A tab's colour comes from `FolderColorMap.Resolve` — the nearest coloured
folder at or above the file, deepest wins — where a tree row uses `ColorOf`. That asymmetry is the
whole design: the tree says *where* a colour is set so it can be found and changed, and the tab says
*which* coloured folder this note belongs to. A file is never coloured itself.

**Three things re-resolve a tab, and each is a different question.** Opening one resolves it against
the workspace as it stands. `FolderColorsChanged` re-resolves every open tab against the *same*
workspace — the case a user meets when they colour a folder with notes already open, and the reason
PHASE01 gave colours an event of their own instead of reusing one that re-lists the tree.
`ActiveWorkspaceChanged` re-resolves them against a *different* set of colours, which matters because
the same folder can be in two workspaces with two different colours — or with none. And a
`FilePath` change re-resolves one tab, because *Save as* can move a note into another folder and the
mark follows the file rather than the buffer it started as.

**The bar keeps its width whether it is painted or not**, and that matters more here than in the
tree: `Border.Background` bound to `null` paints nothing while the 4px stays reserved, so picking a
colour does not change any tab's width and the strip does not slide out from under the pointer. There
is a test that measures a tab before and after.

**One dependency added, and nothing else.** `DocumentManagerViewModel` now takes
`IWorkspaceService`, which the container already registers as a singleton — the composition root
needed no edit, verified rather than assumed. `EditorHarness` seeds a workspace over its own temp
folder so every tab the Desktop tests open has a workspace to resolve against.

## Files/modules touched

**Created**

- `docs/done/FEATURE-A225-PHASE03.md` — this document.

**Modified**

- `ViewModels/DocumentViewModel.cs` — `Color` and `ColorBrush`, the first raising the second.
- `ViewModels/DocumentManagerViewModel.cs` — the `IWorkspaceService` dependency and its two
  subscriptions, `ColorFor` and `ApplyColors`, the resolve on open, and the `FilePath` branch of
  `OnDocumentPropertyChanged` gaining a re-resolve.
- `Views/EditorPaneView.axaml` — the `Border.tabColour` style and the bar at the head of the tab
  template.
- `tests/…/Desktop.UnitTests/Fakes/EditorHarness.cs` — a `FakeWorkspaceService`, seeded active over
  the harness's own root.
- `tests/…/Desktop.UnitTests/ViewModels/DocumentManagerViewModelTests.cs` — 4 cases (inherited
  colour, re-colour on a colour change, re-resolve on a workspace switch, re-resolve on a *Save as*),
  a `Write(directory, name)` helper for a file in a sub-folder, and the twelve
  `ItsArguments_AreChecked` constructor calls updated — including a new one for the new argument.
- `tests/…/Desktop.UnitTests/Views/TabStrip.cs` — `ColorBar(index)`, `ColourAsync(directory, colour)`
  and `Root`.
- `tests/…/Desktop.UnitTests/Views/EditorTabStripTests.cs` — 2 cases through a shown window: the bar
  painted in the palette's own brush with the tab's width unchanged, and the same on clearing.
- `README.md` — the colour bullet extended with the tab half, from the documentation sweep.

**Untouched, deliberately**

- `Extensions/ServiceCollectionExtensions.cs` — `IWorkspaceService` is already a singleton and the
  container resolves the new constructor parameter itself. The plan asked for this to be verified
  rather than assumed: verified, and the composition-root tests still pass.

## Deviations & follow-ups

- **`ActiveWorkspaceChanged` is subscribed as well as `FolderColorsChanged`.** The plan lists both, so
  this is not a deviation — but it is worth stating why it is not redundant: the two answer different
  questions (the same colours re-read versus a different set of colours), and dropping the second
  would leave a tab claiming a colour the newly-opened workspace does not set. A test covers exactly
  that.
- **The subscriptions are never released.** Both this ViewModel and the workspace service are
  singletons that live as long as the window, which is the same reasoning `FileTreeViewModel`'s class
  comment already records for its own. `Dispose` therefore does not detach them.
- **A tab keeps its colour when the file is deleted underneath it.** The tab stays open (that is the
  existing design — the buffer is still the user's work) and so does its colour, because the folder
  it belongs to has not changed. Recorded because it is a state nothing asserts either way.
- **The bar sits before the title rather than at the tab's very edge.** It is inside the tab's
  `StackPanel`, so the `ListBoxItem`'s 10px left padding is still in front of it. Flush against the
  edge would need the padding moved into the template, which changes the geometry of every tab for a
  4px gain — not a trade this phase makes.
- **The README bullet is an addition rather than a correction**, the same call `docs/done/FEATURE-E64A.md`
  and `…-PHASE02.md` record. This is the sentence PHASE02 deliberately left half-written.
- **No line-ending noise.** `git diff --check` is empty; every file touched is LF throughout.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release -v normal`: **succeeded, 0 Warning(s), 0
  Error(s)**.
- `grep -i AVLN` over that log: **no output**. This phase touches the tab template, so the check is
  the one that would have caught a binding that stopped resolving — `AVLN*` is an MSBuild-task
  diagnostic and is not promoted by `TreatWarningsAsErrors`.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3499 passed, 0 failed, 0
  skipped** — 3493 before this phase, plus the 6 added here.
- **One fix cycle used of the three available**: `TestContext.Current` is not available inside the
  `TabStrip` helper class (`CS0103`), so `ColourAsync` passes `CancellationToken.None`. Green on the
  next cycle.
- **Acceptance criteria, one by one.**
  - A tab from a coloured folder shows that colour, one from an uncoloured folder shows none, and both
    are the same width ✓ — `ATabUnderAColouredFolder_CarriesTheColourAndKeepsItsWidth`,
    `ATabUnderAColouredFolder_CarriesThatFoldersColour`.
  - Colouring a folder re-colours the tabs already open under it, with no reload ✓ —
    `ColouringAFolder_RecoloursTheTabsAlreadyOpenUnderIt`.
  - Switching workspaces re-resolves every open tab ✓ —
    `OpeningAnotherWorkspace_ReResolvesEveryTabAgainstItsColours`.
  - Release build 0 warnings, no `AVLN` output, full suite green ✓ — above.
- **Not verified here, and worth a look before the release tag:** the whole feature has never been
  *seen*. The bar's brush, its width, the tab's geometry and every re-resolve are asserted through a
  shown headless window, but whether eight tabs with four colours between them actually read at a
  glance — and whether the bar is visible enough at 4px against the selected tab's lighter background
  — is a judgement only a person at the window can make. The 1.6.0 release item is where that becomes
  a `docs/RELEASE.md` §1 pre-flight line.
