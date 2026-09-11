# FEATURE-A225 — Folder & tab colours

**Status: DONE** · 3 phases · Roadmap: `docs/roadmap.md`
**Type:** FEATURE · **Branch:** one per phase, `feature/feature-a225-phaseNN-<slug>`, each cut from the run branch.
**Run:** vibe/2026-09-10-title-colors-vim-release

## Objective

Give a folder a colour, and let every tab opened from inside it carry that colour — so a strip of
eight tabs reads as "three from *Work*, two from *Notes*, three from *Archive*" at a glance instead
of as eight file names.

- **PHASE01** — the colour itself: the palette, the per-workspace persistence, the resolution rule,
  and the service call that writes one. No UI.
- **PHASE02** — the tree: a *Colour* submenu on the existing context menu, and a coloured bar on the
  row of the folder that carries a colour.
- **PHASE03** — the tabs: the same bar on every tab whose file sits under a coloured folder, kept
  current across a colour change, a workspace switch and a *Save as*.

## Context & constraints

Established by reading the code, not assumed.

- **The workspaces file is the natural home.** `Workspace` already holds `RootDirectories`, and
  `WorkspaceService` already has the whole write machinery this needs: `MutateAsync` snapshots the
  list, applies the change, writes the file, and puts the previous state back if the write throws
  (`Core/Workspaces/WorkspaceService.cs`). `WorkspacesConfigValidator` is where a rule spanning more
  than one property goes. Nothing else in the application has that.
- **A `List<T>` of records round-trips; a dictionary is unproven here.** `Workspaces` is a
  `List<Workspace>` and `RootDirectories` a `List<string>`, both bound by Enigma.Configuration and
  covered by `WorkspacesFileTests`. Enums are written **by name** (`AppSettingsRoundTripTests`
  asserts `"Theme": "Light"`), which keeps the file hand-editable. So the colours are a list of
  Path/Color entries, not a dictionary whose Windows keys would carry escaped backslashes and whose
  binding this repo has never exercised.
- **`WorkspacesConfig.Version` stays 1.** A 1.5.0 file simply has no `FolderColors` key and picks up
  the property default, exactly as `APartialFile_KeepsTheDefaultsForEverythingItDoesNotMention`
  proves for the settings file. A version bump would make this build reject its own predecessor's
  file for an additive key.
- **`WorkspaceValidation.PathComparer`** is the path comparer the rest of the workspace code uses
  (case-insensitive on Windows), and `NormalizeDirectory` is how a path is stored. Both apply here or
  `C:\Notes` and `c:\notes\` would be two different folders.
- **Both events on `IWorkspaceService` rebuild the tree.** `FileTreeViewModel`'s constructor wires
  `ActiveWorkspaceChanged` and `WorkspacesChanged` to `PostRefresh()`, which re-lists every root. A
  colour must therefore **not** be announced through either of them — see *Decisions*.
- **A null brush paints nothing, and that is the safe shape.** `Border.Background` and `Shape.Fill`
  are `IBrush?`; a `TextBlock` or `ei:Icon` `Foreground` bound to null draws no glyph at all. So the
  colour is carried by a fixed-width `Border` whose `Background` is null when there is no colour —
  which also means the row and the tab are the same width coloured or not.
- **The tab template is in `Views/EditorPaneView.axaml`** and its styles are asserted from a shown
  window by `Views/EditorTabStripTests.cs` through the `TabStrip` harness. The strip's own comment
  explains why a *selected*-state accent bar was rejected: it would compete with the 1px border
  under the strip. A per-folder **leading** bar is a different mark and does not.
- **`DocumentManagerViewModel` has no workspace dependency yet** and is built by `EditorHarness` in
  the Desktop tests; adding one means updating that harness and `FakeWorkspaceService`.
- **ViewModels may reference `Desktop.Controls`** — `MarkdownPreviewViewModel` already does — so the
  palette can live there and be read from a ViewModel property.
- **Compiled bindings are on** (`AvaloniaUseCompiledBindingsByDefault`), so every template root needs
  its `x:DataType`, and `dotnet build -v normal | grep -i AVLN` is part of each phase's check:
  `AVLN*` comes from an MSBuild task and is not promoted by `TreatWarningsAsErrors`.
- **No new package.** Avalonia's brushes and the existing `ei:Icon` cover the whole surface.

## PHASE01 — Colours in the workspaces file

**Status:** DONE

### Steps

1. `Core/Workspaces/FolderColor.cs` — `public enum FolderColor { None = 0, Red, Orange, Yellow,
   Green, Teal, Blue, Purple, Pink }`. `None` is the default and means "no colour", so a missing or
   unreadable value degrades to no colour rather than to a colour nobody chose.
2. `Core/Workspaces/FolderColorEntry.cs` — a `sealed class` (bound to JSON, so settable properties,
   like `Workspace`): `[Required(AllowEmptyStrings = false)] string Path` and
   `[EnumDataType(typeof(FolderColor))] FolderColor Color`, plus an `internal Clone()`.
3. `Workspace` — `public List<FolderColorEntry> FolderColors { get; set; } = [];`, copied in
   `Clone()` (a fresh list of fresh entries, or a rollback would restore aliases of the mutated
   objects).
4. `WorkspacesConfigValidator` — one more rule: no blank or white-space `Path`, and no two entries
   naming the same path under `PathComparer`.
5. `Core/Workspaces/FolderColorMap.cs` — the whole resolution rule, as decisions over strings:
   - `public static FolderColor ColorOf(IEnumerable<FolderColorEntry>? entries, string? path)` —
     exact match under `PathComparer`, `None` otherwise. What a tree row shows.
   - `public static FolderColor Resolve(IEnumerable<FolderColorEntry>? entries, string? path)` — the
     colour of the nearest coloured ancestor of `path`, itself included; `None` when there is none.
     Ancestors are walked with `Path.GetDirectoryName` so `C:\a\bb` is never read as a child of
     `C:\a\b`. The **deepest** match wins, so a coloured sub-folder overrides its coloured parent.
     What a tab shows.
6. `IWorkspaceService` and `WorkspaceService`:
   - `event EventHandler? FolderColorsChanged;`
   - `Task SetFolderColorAsync(Guid workspaceId, string folder, FolderColor color,
     CancellationToken cancellationToken = default)` — normalizes the folder the way
     `AddRootDirectoryAsync` does, then through `MutateAsync`: `None` removes the entry, anything
     else adds or replaces it. A call that changes nothing returns without writing. Raises
     `FolderColorsChanged` and neither of the other two events.
7. Tests:
   - `Core.UnitTests/Workspaces/FolderColorMapTests.cs` — exact versus inherited, deepest-wins, a
     sibling prefix (`C:\a\bb` under a coloured `C:\a\b`), a file path under a coloured folder, a
     trailing separator, case (Windows), `None`, an empty list, null.
   - `WorkspaceServiceTests` — set, replace, clear; the file holds it after a reload; a failed write
     rolls the list back and leaves the file alone; `FolderColorsChanged` fires and
     `WorkspacesChanged` / `ActiveWorkspaceChanged` do **not**.
   - `WorkspacesFileTests` — a file written with colours reads back with them, the enum is written by
     name, and a 1.5.0-shaped file (no `FolderColors` key) loads with an empty list and no failure.

### Acceptance criteria

- A colour set through the service survives a reload of the file; clearing it removes the entry.
- The enum is written by name, and a file with two entries for one path, or a blank path, is
  reported by the validator rather than accepted.
- `Resolve` answers the deepest coloured ancestor, and never treats a name-prefix sibling as a child.
- `FolderColorsChanged` is the only event a colour write raises.
- Release build 0 warnings; full suite green.

## PHASE02 — Folder colours in the tree

**Status:** DONE

### Steps

1. `Desktop/Controls/FolderColorPalette.cs` — one theme-independent mid-tone brush per colour, as
   `public static IBrush` members (so XAML can reach them with `{x:Static}`) plus
   `public static IBrush? Brush(FolderColor color)` returning `null` for `None`. The brushes are
   `ImmutableSolidColorBrush`, built once.
2. `FileTreeNodeViewModel` — `public FolderColor Color { get; set => ... SetProperty ... }` raising
   `ColorBrush` with it, and `public IBrush? ColorBrush => FolderColorPalette.Brush(Color)`.
3. `FileTreeViewModel`:
   - `public IReadOnlyList<FolderColorChoice> ColorChoices { get; }` — nine choices (label, colour,
     brush) for the menu, built once. `FolderColorChoice` is a small `sealed record` beside it.
   - `SetColorCommand` — enabled while the selection is a directory (a root included) and there is
     an active workspace; a null parameter is a no-op. Calls `SetFolderColorAsync` through the
     existing `RunAsync` reporting path.
   - `ApplyColors()` — walks the existing nodes and assigns `ColorOf(...)` to each directory. Called
     where nodes are built (roots and listings) and from a `FolderColorsChanged` subscription, so a
     colour change re-tints in place instead of re-listing the tree.
   - `CanActOnSelection`'s notification path already exists (`NotifyOperationsChanged`); the new
     command joins it.
4. `Views/FileTreeView.axaml` — a *Colour* submenu with nine explicit `MenuItem`s (None first, then
   the eight), each carrying `Command="{Binding SetColorCommand}"` and a
   `CommandParameter` naming its `FolderColor` through `{x:Static}`, with a swatch `Border` as its
   `Icon` filled from `{x:Static ...FolderColorPalette....}`; plus a 4px `Border` at the head of the
   row template with `Background="{Binding ColorBrush}"` and a 6px right margin.
5. Tests — `Desktop.UnitTests/ViewModels/FileTreeViewModelTests.cs`: setting a colour on a folder
   writes it through the service, tints that node and no other, and does **not** collapse an expanded
   folder; clearing it removes the tint; the command is disabled on a file, on an informational row
   and with no workspace. `FileTreeNodeViewModelTests`: `ColorBrush` is null for `None` and a brush
   otherwise, and changing `Color` notifies both properties.

### Acceptance criteria

- Right-clicking a folder offers nine colours; picking one tints that row and persists it.
- An expanded folder stays expanded across a colour change.
- The command is unavailable for anything that is not a folder.
- Release build 0 warnings, no `AVLN` output; full suite green.

## PHASE03 — Tab colours

**Status:** DONE

### Steps

1. `DocumentViewModel` — `public FolderColor Color { get; set => ... SetProperty ... }` raising
   `ColorBrush`, and `public IBrush? ColorBrush => FolderColorPalette.Brush(Color)`.
2. `DocumentManagerViewModel` — takes `IWorkspaceService`:
   - resolves a document's colour with `FolderColorMap.Resolve(active.FolderColors, filePath)` when
     the tab is opened;
   - re-resolves every open tab on `FolderColorsChanged` and on `ActiveWorkspaceChanged`;
   - re-resolves one tab when its `FilePath` changes (a *Save as* into another folder), through the
     `OnDocumentPropertyChanged` handler that already exists.
3. `Views/EditorPaneView.axaml` — the same 4px `Border` at the head of the tab template, with a 6px
   right margin, ahead of the title.
4. `Extensions/ServiceCollectionExtensions.cs` — nothing is expected to change; `IWorkspaceService`
   is already a singleton and the container resolves the new constructor parameter itself. Verify
   rather than assume.
5. Tests — `EditorHarness` gains a `FakeWorkspaceService` (which gains the two new members);
   `DocumentManagerViewModelTests`: a tab under a coloured folder is coloured, one outside it is not,
   a colour change re-colours the open tabs, a workspace switch re-resolves them, and a *Save as*
   into an uncoloured folder clears the tab's colour. `EditorTabStripTests` and the `TabStrip`
   harness: the bar is painted for a coloured tab, unpainted for an uncoloured one, and the tab's
   width is the same either way.

### Acceptance criteria

- A tab opened from a coloured folder shows that folder's colour; one from an uncoloured folder shows
  none, and both tabs are the same width.
- Colouring a folder re-colours the tabs already open under it, with no reload.
- Switching workspaces re-resolves every open tab against the new workspace's colours.
- Release build 0 warnings, no `AVLN` output; full suite green.

## Out of scope

Free-form colour picking (a hex field or a colour wheel) · colouring individual **files** · tinting a
coloured folder's descendants in the tree · a colour in the preview pane, the status bar or the
window title · reordering or renaming the palette from the UI · per-workspace palettes · sharing
colours between workspaces (the list is per-workspace by design; two workspaces holding the same
folder each carry their own entry).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Free colours or a palette? | A fixed palette of 8 plus None | It validates, it stays hand-editable in JSON, it reads as one design at 4px, and it needs no picker dialog | A hex string per folder: unbounded, unvalidatable, and half the values are illegible at 4px |
| Where do colours live? | `Workspace.FolderColors` in the workspaces file | Colours are facts about a workspace's folders, and that service already has atomic write, rollback and events | `AppSettings`: a flat scalar file with no list precedent, and a colour is not an application preference |
| List or dictionary? | A `List` of entries | `List<T>` binding is proven in this repo by `Workspaces`; a path-keyed dictionary is not, and its Windows keys are escaped backslashes | A path-keyed dictionary |
| Which event announces a colour? | A new `FolderColorsChanged` | Both existing events are wired to a full tree refresh, which would collapse the user's expansion state on every colour click | Reusing `WorkspacesChanged` |
| Theme-aware palette? | One theme-independent mid-tone set | Eight small accents legible on both grounds, with no `ThemeDictionaries`, no variant lookup and no re-notification on a theme switch | A light/dark pair per colour, resolved from `Application` resources |
| How is the colour shown? | A 4px `Border` at the head of the row and of the tab | A null background paints nothing, so there is no layout shift and no null-brush hazard; one mark in both places is one thing to learn | Tinting the folder icon and the tab title (a null `Foreground` draws nothing at all); tinting the tab background (fights `:selected` and `:pointerover`) |
| Does the tree inherit? | No — exact match on a row, inheritance for tabs only | The tree shows *where* a colour is set; the tab shows *which* folder a file belongs to. Tinting whole subtrees makes the tree noisy and hides where the setting lives | Inheriting down the tree |
| Deepest or shallowest ancestor? | Deepest | A colour on a sub-folder is a deliberate override of its parent's | Shallowest, so the outermost colour always wins |
| Where is the palette? | `Desktop/Controls/FolderColorPalette.cs`, C# only | One source of truth that both the ViewModels and XAML (`{x:Static}`) can read | A merged XAML dictionary plus a C# resource lookup: two sources, and a lookup a ViewModel cannot test |
| Menu shape | Nine explicit `MenuItem`s | A `MenuItem` with `ItemsSource` needs generated-container styling, and `$parent[TreeView]` does not resolve from a `ContextMenu` popup | A data-templated submenu |
| Spelling | "Colour" | The codebase's prose and the README are consistently British ("coloured", "colours") | "Color" |
