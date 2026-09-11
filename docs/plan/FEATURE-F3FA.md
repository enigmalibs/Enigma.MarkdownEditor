# FEATURE-F3FA — Tab strip polish

**Status: DONE** · Single phase · Roadmap: `docs/roadmap.md`
Branch at build time: `feature/feature-f3fa-tab-strip`.

## Objective

Make the tab strip behave the way an editor's tab strip is expected to: the close button reachable on a modified tab, the active tab identifiable at a glance, and nothing floating over the tabs you are trying to click.

## Context & constraints

- Everything lives in `src/Enigma.MarkdownEditor.Desktop/Views/EditorPaneView.axaml` and its code-behind. No ViewModel change, no settings, no new package.
- The tab strip is a `ListBox` (`x:Name="Tabs"`) with a horizontal `StackPanel` items panel; one `ListBoxItem` per `DocumentViewModel`. Selection, keyboard navigation and hover/focus states come from the theme.
- Palette keys come from `Enigma.Avalonia.Desktop` (`EnigmaForegroundBrush`, `EnigmaForegroundSecondaryBrush`); no hard-coded colours, so both theme variants follow automatically.

## Design

### 1 — Hover swaps the dirty dot for the close button

Today `Ellipse.dirty` and `Button.tabClose` each set `IsVisible` as a **local binding inside the DataTemplate** (`{Binding HasUnsavedChanges}` / `{Binding !HasUnsavedChanges}`). A local value outranks a style setter in Avalonia, so no `:pointerover` style can override them — the visibility must first be re-expressed as a class:

- Bind `Classes.dirty="{Binding HasUnsavedChanges}"` on the item template's root panel and drop both `IsVisible` bindings.
- Style rules, keyed on that class and the ancestor `ListBoxItem`'s `:pointerover` pseudo-class:
  - dot visible **iff** `.dirty` **and not** `:pointerover`
  - close button visible **iff** **not** `.dirty` **or** `:pointerover`

Hover is the whole tab, not the 8px dot — VS Code's behaviour, and what the existing code comment ("the close button ... whenever the pointer is over the tab") already promised but never implemented. The two share a slot, so the tab does not change width.

### 2 — Selected tab contrast

`TextBlock.tabTitle` gets `Foreground="{DynamicResource EnigmaForegroundSecondaryBrush}"`; a `ListBoxItem:selected` rule raises the selected tab's title to `EnigmaForegroundBrush`. Foreground only — no accent bar (competes with the strip's existing 1px bottom border) and no bold (the selected tab would change width and shift the whole strip under the pointer).

### 3 — Scrollbar no longer covers the tabs

Avalonia's Fluent `ScrollViewer` template overlays its scrollbars on the content by design and expands them on hover; there is no supported "reserve space" switch. So the bar goes:

- `ScrollViewer.HorizontalScrollBarVisibility="Hidden"` on the `Tabs` ListBox.
- A `PointerWheelChanged` handler on the strip translating vertical wheel delta into a horizontal offset on the tab strip's `ScrollViewer`.
- Scrolling the selected tab into view is already `ListBox`'s own behaviour on selection change — verify, do not reimplement.

## Acceptance criteria

- A modified tab shows the dot at rest and the close button under the pointer; an unmodified tab always shows the close button; the tab never changes width between the two states.
- The selected tab's title is visibly darker/lighter than the others in **both** the Dark and Light variants.
- No scrollbar is drawn over the tabs; the mouse wheel scrolls the strip horizontally when tabs overflow; selecting a tab off-screen brings it into view.
- Build clean with zero warnings; the whole suite passes.
- Pointer-driven states are asserted in `Desktop.UnitTests` with `Avalonia.Headless` input simulation where the harness can drive it; where it cannot, the completion doc states plainly that the state was verified by inspection rather than by a test.

## Dependencies

None. Independent of the other three items in this batch.

## Out of scope

Tab reordering by drag · a tab overflow dropdown or chevron buttons · middle-click to close · pinned tabs.
