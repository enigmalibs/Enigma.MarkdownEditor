# FEATURE-A225-PHASE02 — Folder colours in the tree

**Status: DONE** · Phase 2 of 3 · Branch: `feature/feature-a225-phase02-tree-colours` (cut from
`vibe/2026-09-10-title-colors-vim-release`)
Plan: `docs/plan/FEATURE-A225.md` · Roadmap: `docs/roadmap.md`

## Summary

A folder's colour can now be set and seen: a *Colour* submenu on the tree's existing context menu,
nine choices with swatches, and a 4px bar at the head of the row of every folder that carries one.
PHASE03 is what puts the same bar on the tabs.

**The palette is C#, and both the ViewModels and the markup read it from there.**
`Controls/FolderColorPalette` exposes the eight brushes as static members plus
`Brush(FolderColor)`; a ViewModel calls the method and XAML reaches the members with `{x:Static}`, so
the colour a user picks in the menu and the colour their row is painted with cannot drift apart —
there is a test that asserts exactly that, by reference. One palette for both theme variants:
mid-tones legible on either ground, which spares a theme dictionary, a variant lookup and a
notification on every theme switch for eight marks nobody reads as text. The yellow is a dark gold
rather than a true yellow, which is the one hue of the eight that cannot be seen on white at its own
brightness.

**`null` is load-bearing.** `Brush` answers `null` for `None`, and the row's colour is a `Border`
whose `Background` is that binding: a border with no background paints nothing and still occupies its
4px, so a coloured row and an uncoloured one are the same shape and the names line up under each
other. Tinting the icon or the label instead was rejected in the plan and the reason is worth
repeating: a `Foreground` bound to `null` draws no glyph at all, so the uncoloured case would need a
brush the ViewModel has no honest way to produce.

**A colour never re-lists the tree.** `FolderColorsChanged` — PHASE01's own event — is wired to
`ApplyColors()`, which walks the nodes already on screen and re-reads their colour off the workspace.
The two pre-existing events stay wired to `PostRefresh()`. That is the difference between picking a
colour and watching every folder you had opened shut under the pointer, and there is a test that opens
a folder, colours it, and asserts the same child node is still there and still expanded.

**Colours are applied where nodes appear, not only where they change.** `ApplyColors()` also runs at
the end of `RefreshAsync` (the roots) and of `LoadChildrenAsync` (a listing), because a folder listed
for the first time is built after the colour was set — a test colours a folder that has never been
expanded and then expands it.

**A root can be coloured, though it cannot be renamed or deleted.** `CanActOnSelection` excludes roots
because renaming one would leave the workspaces file pointing at a folder that is no longer called
that; a colour is *written into* that same file, so it cannot put the two out of step.
`CanColorSelection` is therefore its own property rather than a reuse, and it also declines the
lone-root fallback `TargetDirectory` makes for *New file*: "colour this folder" has to mean the folder
that was right-clicked.

## Files/modules touched

**Created**

- `src/Enigma.MarkdownEditor.Desktop/Controls/FolderColorPalette.cs` — the eight brushes and
  `Brush(FolderColor)`.
- `tests/…/Desktop.UnitTests/Views/FileTreeColourMenuTests.cs` — 5 cases, through the real view with
  the context menu **opened**: the nine headers in order, each item's `CommandParameter` asserted as
  a typed `FolderColor`, each item wired to the command and answering `CanExecute` true, the submenu
  disabled for a file and enabled for a folder, and every swatch painted in the very brush
  `FolderColorPalette.Brush` returns.
- `docs/done/FEATURE-A225-PHASE02.md` — this document.

**Modified**

- `ViewModels/FileTreeNodeViewModel.cs` — `Color` and `ColorBrush`, the first raising the second.
- `ViewModels/FileTreeViewModel.cs` — `SetColorCommand`, `CanColorSelection`, the private
  `ColorTarget` and `SetColorAsync`, the `ApplyColors` pair, the `FolderColorsChanged` subscription,
  and the two call sites where nodes appear. `NotifyOperationsChanged` gained the new command and the
  property notification.
- `Views/FileTreeView.axaml` — two namespaces, the *Colour* submenu (nine items, swatches,
  `{x:Static}` parameters), two styles (`Border.folderColour`, `Border.swatch`), and the row template
  gaining a third column for the bar.
- `tests/…/Desktop.UnitTests/ViewModels/FileTreeNodeViewModelTests.cs` — 2 cases: no colour means no
  brush, and changing the colour notifies both properties.
- `tests/…/Desktop.UnitTests/ViewModels/FileTreeViewModelTests.cs` — 7 cases: the write and the tint
  landing on that row alone, the tree staying as the user opened it, clearing the colour, a colour set
  before a folder was ever listed, a root being colourable but not renameable, colouring offered for
  nothing but a folder, and no workspace meaning nothing to colour.
- `README.md` — one bullet in *Workspaces and the file tree*, from the documentation sweep. See
  *Deviations*.

## Deviations & follow-ups

- **`ColorChoices` and `FolderColorChoice` were not written**, though the plan lists them as step 3.
  They only existed to feed a data-templated submenu, and the plan's own step 4 chose explicit
  `MenuItem`s instead — the two steps contradicted each other, and the collection would have shipped
  as a public property nothing reads. The nine items carry their colour through `{x:Static}` and their
  swatch through the palette's static members, so nothing is duplicated by writing them out.
- **The `MenuItem` disabled state is `IsEnabled="{Binding CanColorSelection}"` on the submenu**, on
  top of the command's own `CanExecute`. Without it the *Colour* row itself stays enabled and opens
  onto nine disabled items, which reads as a bug rather than as a rule.
- **The command's parameter is `FolderColor?`, not `FolderColor`.** The toolkit's
  `AsyncRelayCommand<T>` refuses a null parameter outright when `T` is a non-nullable value type,
  before the predicate runs — so a menu item whose parameter had not been bound yet would be drawn
  disabled with nothing to show why. `FileTreeColourMenuTests` is what pins this down: it is a
  runtime behaviour that compiles either way.
- **A missing root cannot be coloured.** `IsDirectory` is false for a root that is not on disk, so
  the submenu is disabled on it. Deliberate — it lists nothing that could inherit the colour — but it
  is a rule a user could reasonably expect to go the other way, so it is recorded rather than buried.
- **The menu does not mark the folder's current colour.** Showing which of the nine is in force needs
  a check mark bound through a comparison, which is nine more bindings and a converter; the tree's own
  bar already says it. Recorded as a deliberate omission, not an oversight.
- **The README bullet is an addition, not a correction** — the same judgement as
  `docs/done/FEATURE-E64A.md` records, and for the same reason: a user-facing gesture with no line in
  the README is what 1.5.0's release item had to fold in after the fact. The tab half of the sentence
  arrives with PHASE03, which is what ships it.
- **The colour bar's 4px is spent on every row, folder or not.** A file's bar is never painted, but
  its width is still reserved, which is what keeps the names aligned. That is the trade the shape was
  chosen for.
- **No line-ending noise.** `git diff --check` is empty; every file touched or created is LF
  throughout.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release -v normal`: **succeeded, 0 Warning(s), 0
  Error(s)**.
- `grep -i AVLN` over that log: **no output** — which is the check that matters most in this phase,
  since it is the one that touches XAML: `AVLN*` codes come from an MSBuild task and are not promoted
  by `TreatWarningsAsErrors`, so a broken binding or a missing `x:DataType` can hide behind a green
  build.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3493 passed, 0 failed, 0
  skipped** — 3479 before this phase, plus the 14 added here.
- **One fix cycle used of the three available**: `ImmutableSolidColorBrush` needed
  `using Avalonia.Media.Immutable;` (`CS0246`). The build and the whole suite were green on the next
  cycle.
- **Acceptance criteria, one by one.**
  - Right-clicking a folder offers nine colours; picking one tints that row and persists it ✓ —
    `TheMenu_OffersNoneAndTheEightColours`,
    `ColouringAFolder_WritesItThroughTheServiceAndTintsThatRowAlone`.
  - An expanded folder stays expanded across a colour change ✓ —
    `ColouringAFolder_LeavesTheTreeAsTheUserOpenedIt`.
  - The command is unavailable for anything that is not a folder ✓ —
    `ColouringIsOfferedForAFolderAndNothingElse`, `WithNoWorkspace_ThereIsNothingToColour`,
    `WithNothingColourable_TheWholeSubmenuIsDisabled`.
  - Release build 0 warnings, no `AVLN` output, full suite green ✓ — above.
- **Not verified here, and worth a look before the release tag:** nobody has *seen* the bar or the
  swatches. The menu is opened for real in a headless window and every binding, parameter and brush is
  read off the controls the markup produced — but whether 4px at that gold reads as yellow beside a
  folder icon is a judgement only a person at the window can make. The `docs/RELEASE.md` §1
  pre-flight is where that belongs, and the release item is where it gets added.
