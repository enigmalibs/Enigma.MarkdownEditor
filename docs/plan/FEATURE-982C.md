# FEATURE-982C — Uniform About dialog

**Status:** DONE
**Type:** FEATURE · single phase · Roadmap: `docs/roadmap.md` · **Branch:** `feature/feature-982c-uniform-about-dialog`, cut from the run branch at build time.
**Run:** feature/2026-10-29-uniform-enigma-apps

## Objective

The About dialog has the same layout, style and colours as Enigma.GitClient's, which is the
reference for every Enigma app:

- the 96 px icon;
- the product name (18, SemiBold, `EnigmaForegroundBrush`);
- `Version X.Y.Z` (`EnigmaForegroundSecondaryBrush`);
- the build line, when there is one (11, `EnigmaForegroundTertiaryBrush`, the SHA selectable);
- the copyright (11, secondary brush);
- in a content dialog with no title, no icon and a single default `Close` button.

## Context & constraints

- The reference, read from `Enigma.GitClient` at `4bfab63`:
  - `src/Enigma.GitClient.Desktop/Views/Dialogs/AboutView.axaml`;
  - `ViewModels/Dialogs/AboutViewModel.cs`;
  - `Services/AboutDialogService.cs`.

  The GitClient view says it was made from this app's view, "at the same size and in the same style".
- How this app's dialog differs from the reference:
  - **"BUILT WITH" credits box**: the `caption` and `license` styles, `ItemsControl x:Name="Credits"`,
    `AboutViewModel.Credits` / `DefaultCredits`, and `ViewModels/CreditEntry.cs`. The reference has
    none of these.
  - **`productName` foreground**: here it is the inherited default; the reference sets it explicitly
    to `EnigmaForegroundBrush`.
  - **`AboutDialogService`** sets `Title = "About"` and `IconData = Info`. The reference sets
    `Title = null`, no icon, `DefaultButton = DefaultButton.Close` and `ConfigureAwait(true)`.
- `AboutDialogService` here takes a `Func<AboutViewModel>` factory from DI. That is plumbing, not
  layout, so it stays.
- Tests: `ViewModels/AboutViewModelTests.cs` and `Views/AboutViewTests.cs` (Desktop tests).
- The dialog host (`MainWindow.axaml:543`) is `DialogMinWidth="360"`. GitClient uses 380. The view
  itself is 380 wide in both apps, so the card renders at the same width. The host bounds every
  dialog, so it stays as it is.

## Steps

1. `Views/AboutView.axaml`:
   - make the markup the reference's, with the bindings, `x:Name`s and comments it carries;
   - add `Foreground="{DynamicResource EnigmaForegroundBrush}"` to `productName`;
   - remove the credits `Border` and the `caption` and `license` styles.
2. `ViewModels/AboutViewModel.cs`:
   - drop `Credits` and `DefaultCredits`;
   - the constructor becomes `(string version, string? buildSha, string copyright)`;
   - delete `ViewModels/CreditEntry.cs`.
3. `Services/AboutDialogService.cs`:
   - `Title = null`, no `IconData`;
   - `CloseButtonText = "Close"`, `DefaultButton = DefaultButton.Close`;
   - `.ConfigureAwait(true)`;
   - remove the static `Icon` field and its usings.
4. Tests:
   - update `AboutViewModelTests` and `AboutViewTests` for the removed credits (delete those
     assertions);
   - assert the product-name brush key, and that no credits list is present;
   - if a test covers `AboutDialogService`, update it to assert no title, no icon and `Close` as the
     default button; if none does, add one over a fake `IContentDialogService` that captures the
     configured `ContentDialog`.
5. Docs: `README.md` and `docs/RELEASE.md` mentions of the About dialog's credits or title, if any.

## Acceptance criteria

- `AboutView.axaml` matches the reference element for element: the same styles, brushes, sizes and
  spacing. The only differences are the namespace and the `x:Class`.
- The dialog opens with no title and no icon, and has one `Close` button, which is the default.
- No `CreditEntry` or `Credits` remains under `src/` or `tests/`.
- The Release build has zero warnings and no `AVLN` diagnostics, and the full suite is green.

## Out of scope

- The dialog host's size bounds, and every other dialog.
- A third-party notices file (see the follow-up below).

## Decisions taken autonomously

| Question | Chosen | Why | Alternatives rejected |
|---|---|---|---|
| Keep the "BUILT WITH" credits | Remove them | "Same layout as GitClient's" and uniform across apps. The reference has no credits | Keeping them below the copyright (not uniform) |
| Licence attribution once credits go | Follow-up only: suggest a shared third-party notices file for all Enigma apps | The licences are satisfied by shipped notices more than by an About box, and a new file is outside this spec | Adding `THIRD-PARTY-NOTICES.md` in this run |
| Title and icon | None (`Title = null`), as in the reference | The view already shows the icon and the name. A title column beside it would be a different layout | Keeping the "About" title and the Info glyph |
| The DI factory `Func<AboutViewModel>` | Kept | It is plumbing, invisible in the dialog, and existing registrations and tests rely on it | Newing up the ViewModel as GitClient does |
| Host `DialogMinWidth` 360 vs 380 | Unchanged | The view is 380 wide, so the About card renders the same. The host bounds every dialog | Raising it to 380 |
