# FEATURE-414B — Settings surface

**Status: DONE** · 2 phases · Roadmap: `docs/roadmap.md`
Planned on `develop`; each phase cuts its own branch from `HEAD` at build time.

## Objective

Two deliberate changes to how settings are presented, one per phase: make the Preview group speak
the Editor group's language and lower the editor's default font size; then stop the settings screen
being the last real `Window` in an application whose every other dialog is a `ContentDialog`.

Neither phase changes what any setting *does*. No `AppSettings` property is renamed, no value is
migrated, and nothing new is persisted.

## Context & constraints

- **Two projects, one rule:** `Enigma.MarkdownEditor.Core` never references Avalonia. `AppSettings`
  lives in `Core`; everything the dialog is made of lives in `Desktop`.
- **`TreatWarningsAsErrors` is on solution-wide** and `EnforceCodeStyleInBuild` with it — a
  **0-warning** build, per phase.
- **`AVLN*` diagnostics are not promoted** by `TreatWarningsAsErrors`, so
  `dotnet build -v normal | grep -i AVLN` is part of the check whenever XAML is touched.
- **Compiled bindings are the default.** Every view root and `DataTemplate` carries `x:DataType`, so
  a renamed ViewModel is a build failure rather than a silent runtime one — which is what makes
  PHASE02's rename safe to do wholesale.
- **The `ContentDialog` host is a single control** in `MainWindow.axaml:505-511`, with
  `DialogMinWidth="360" DialogMaxWidth="860" DialogMaxHeight="640"`. Its six size properties are
  **not reset between dialogs**, which is exactly why they live on the host and not in a caller —
  PHASE02 does not touch them.
- **Three dialogs already show the shape to copy:** `AboutDialogService`,
  `WorkspaceManagerDialogService` and `FirstRunDialogService` — `View` + `ViewModel` + a service
  holding `IContentDialogService`, resolved per showing.
- **`FirstRunDialogService` already documents the tail PHASE02 must keep:** the host is one control,
  so a message raised *while* another dialog is open has nowhere to go. Both go through it in turn.

---

## PHASE01 — Preview labels & editor default

**Status: DONE**

### Objective

The Preview group calls its rows "Text font" and "Text size" while the Editor group, two groups up,
calls the same two things "Font family" and "Font size". One vocabulary, and the editor's is the one
that names what the value actually is. Separately, the editor's default font size drops from 18 to
16.

### Design

1. **Preview group labels** (`Views/SettingsWindow.axaml`, the `Preview`
   `SettingsCardExpander`) — headers only, in row order:

   | Today | Becomes |
   |-------------------|--------------------|
   | `Text font` | `Font family` |
   | `Text size` | `Font size` |
   | `Code font` | `Code font family` |
   | `Code font size` | `Code font size` (unchanged) |
   | `Line height` | `Line height` (unchanged) |

   Row descriptions are reworded only where they lean on the old label; what each row *does* is
   unchanged, and so are the `IntEditor` / `DoubleEditor` / `ComboBox` controls and every binding.
2. **No `AppSettings` rename.** `PreviewFontFamily`, `PreviewFontSize`, `PreviewCodeFontFamily` and
   `PreviewCodeFontSize` keep their names: they are the keys in every user's `settings.json`, and a
   label is not a reason to break a file people hand-edit.
3. **`AppSettings.EditorFontSize` default `18` → `16`** (`Core/Settings/AppSettings.cs:75`).
   `[Range(6, 72)]` is unchanged.
4. **No migration.** A settings file that already holds `18` keeps `18`. The default governs a fresh
   install and a file with the key absent — nothing rewrites a value the user can see and edit. The
   XML doc on the property says so, so the next reader does not go looking for the migration.
5. **Tests that assert the old numbers move with it:**
   `Core.UnitTests/Settings/AppSettingsTests.cs:23` and
   `AppSettingsRoundTripTests.cs:90` (both assert the default is 18) — note that
   `AppSettingsRoundTripTests.cs:111-125` reads **16 from a file** and must keep asserting 16 for a
   reason that is now "the file said so", not "that is the default".
   `Desktop.UnitTests/Views/SettingsPreviewSectionTests.cs:52-58` asserts the five row headers and
   takes the new list.

### Acceptance criteria

- The Preview group's rows read `Font family`, `Font size`, `Code font family`, `Code font size`,
  `Line height`, in that order, and each still edits the property it edited before.
- A fresh profile (no `settings.json`) starts the editor at 16pt; a profile whose file holds
  `"EditorFontSize": 18` still opens at 18.
- No `AppSettings` property name changes; an existing `settings.json` round-trips unchanged.
- Build clean with zero warnings; no `AVLN` output; the whole suite passes.

### Files

- `src/Enigma.MarkdownEditor.Core/Settings/AppSettings.cs`
- `src/Enigma.MarkdownEditor.Desktop/Views/SettingsWindow.axaml`
- `tests/Enigma.MarkdownEditor.Core.UnitTests/Settings/AppSettingsTests.cs`,
  `AppSettingsRoundTripTests.cs`
- `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Views/SettingsPreviewSectionTests.cs`

---

## PHASE02 — Settings as a content dialog

**Status: DONE**

### Objective

`SettingsWindow` is a real `Window` shown with `ShowDialog`. Nothing about it needs to be: it is
modal over the main window, it is not resized to any purpose, it carries no taskbar entry
(`ShowInTaskbar="False"`), and every other dialog in the application — About, Manage workspaces,
First run — is a `ContentDialog`. It becomes one too, and the names stop saying "window".

### Design

1. **The view becomes a `UserControl`.** `Views/SettingsWindow.axaml` → `Views/SettingsView.axaml`
   with `<UserControl>` as its root, `x:DataType="vm:SettingsViewModel"`, `Width="760"` (the shape
   `WorkspaceManagerView` already uses at 700 × 420), and `Window.Styles` → `UserControl.Styles`.
   The window-only properties go: `Title`, `Icon`, `Height`, `MinWidth`, `MinHeight`,
   `ShowInTaskbar`, `WindowStartupLocation`, `Background`.
   The existing inner `ScrollViewer` stays and is what makes the content fit the host's 640 cap — if
   the host's own template turns out to wrap content in a `ScrollViewer` of its own, the inner one
   goes rather than nesting two, and the completion doc records it.
2. **The footer goes.** The `Border` + `Close` `Button` in row 1 and `OnCloseClick` in the
   code-behind are deleted; the dialog's own `CloseButtonText = "Close"` replaces them. The
   two-row `Grid` collapses to the `ScrollViewer`. `SettingsView.axaml.cs` keeps only its
   constructor — the class was already documented as "deliberately logic-free".
3. **The service becomes a dialog service**, shaped like `AboutDialogService`: it holds
   `Func<SettingsViewModel>` and `IContentDialogService`, builds the view per showing, and calls
   `ShowAsync(dialog => { Title = "Settings"; Content = view; CloseButtonText = "Close"; })`.
   `Func<SettingsWindow>` and `Func<Window> getOwner` are dropped — there is no window and no owner
   to resolve.
   An icon is optional; if one is used it follows `AboutDialogService`'s pattern
   (`PhosphorIconSet.Instance.GetGlyph(PhosphorIcon.Gear).ToGeometry()`, resolved once into a
   `static readonly Geometry`), because the dialog is configured in code and `{ei:IconGeometry}` is
   markup-only.
4. **The save tail is preserved exactly.** `SaveAsync` still runs *after* the dialog has closed, and
   a failure is still reported with `ShowMessageAsync("Settings were not saved", failure)` — a
   second, sequential dialog, because the host is one control and cannot show two at once. The
   comment explaining this is updated: the reason is no longer "a modal child covers the main
   window" but "the host is a single control".
5. **Dismissal is a close.** Escape and a scrim click resolve as `DialogResult.None`, and that is
   correct here: every edit is applied to the live `AppSettings` as it is made, so there is nothing
   to confirm and nothing to roll back. The result is deliberately not inspected.
6. **Full rename**, since compiled bindings turn every miss into a build error:

   | Today | Becomes |
   |------------------------------|-------------------------------|
   | `Views/SettingsWindow.axaml(.cs)` | `Views/SettingsView.axaml(.cs)` |
   | `ViewModels/SettingsWindowViewModel` | `ViewModels/SettingsViewModel` |
   | `Services/ISettingsWindowService` | `Services/ISettingsDialogService` |
   | `Services/SettingsWindowService` | `Services/SettingsDialogService` |
   | `Fakes/FakeSettingsWindowService` | `Fakes/FakeSettingsDialogService` |

   Callers to follow: `Extensions/ServiceCollectionExtensions.cs:211-212, 305-307` (the
   `AddTransient<SettingsWindow>` registration goes — a `UserControl` is built by the service, not
   resolved), `ViewModels/MainWindowViewModel.cs:22, 64`, and the cross-references in
   `FirstRunDialogService.cs:19` and `FirstRunViewModel.cs:23`.
   `SettingsPreviewSectionTests` shows a real `SettingsWindow` in a window; it shows the
   `UserControl` instead, the way the other view tests do.
   `ServiceCollectionExtensionsTests.cs:52-56, 306` take the new type names and lose the
   `SettingsWindow` transient row.
7. **The host's size properties are not touched.** `DialogMinWidth`/`MaxWidth`/`MaxHeight` stay at
   360 / 860 / 640: they are shared with every other dialog, and raising a ceiling for the tallest
   one raises it for all of them.

### Acceptance criteria

- The gear button opens settings as a `ContentDialog` over the main window — no second OS window,
  no taskbar entry — with the title "Settings" and a single "Close" button.
- Every group and row is present and still edits its setting; the theme still switches live while
  the dialog is open; the content scrolls within the dialog when it does not fit.
- Closing by the button, by Escape and by clicking the scrim all persist the settings, and a failed
  save still reports "Settings were not saved" in a dialog of its own afterwards.
- No type named `SettingsWindow*` remains, and nothing in `Desktop` resolves a `Window` to show
  settings.
- Build clean with zero warnings; no `AVLN` output; the whole suite passes.

### Files

- `src/Enigma.MarkdownEditor.Desktop/Views/SettingsView.axaml(.cs)` (renamed)
- `src/Enigma.MarkdownEditor.Desktop/ViewModels/SettingsViewModel.cs` (renamed)
- `src/Enigma.MarkdownEditor.Desktop/Services/I?SettingsDialogService.cs` (renamed, rewritten body)
- `src/Enigma.MarkdownEditor.Desktop/Extensions/ServiceCollectionExtensions.cs`
- `src/Enigma.MarkdownEditor.Desktop/ViewModels/MainWindowViewModel.cs`,
  `FirstRunViewModel.cs`, `Services/FirstRunDialogService.cs` (references and doc comments)
- `tests/…Desktop.UnitTests/`: `Fakes/FakeSettingsDialogService.cs`,
  `Extensions/ServiceCollectionExtensionsTests.cs`,
  `ViewModels/SettingsWindowViewModelTests.cs` (renamed),
  `Views/SettingsPreviewSectionTests.cs`,
  `ViewModels/DocumentManagerViewModelTests.cs:709` (test name mentions "settings window")

## Dependencies

PHASE01 before PHASE02 — both edit the same view file, and doing the labels first keeps the rename
diff mechanical. Neither depends on `BUG-8F2A`.

## Out of scope

Renaming any `AppSettings` property or `settings.json` key · migrating existing values · changing
which groups are expanded by default · restructuring the settings groups · any release or version
bump.
