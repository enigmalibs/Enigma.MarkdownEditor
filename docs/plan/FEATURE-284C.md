# FEATURE-284C — First-run setup

**Status: DONE** · Single phase · Roadmap: `docs/roadmap.md`
Branch at build time: `feature/feature-284c-first-run-setup`.

## Objective

Ask the user, once, on the very first startup, the small number of things that are worth deciding before the editor is used: **vim mode**, **theme**, and **the first workspace folder**. Everything else stays in Settings, where it is discoverable.

The third is the one that changes the most: without a workspace the application opens to an empty tree and a *Manage workspaces* button, so a first run currently ends with the user hunting for the way in.

## Context & constraints

- **`AppSettings` already holds all three values** — `VimEnabled` (default `true`), `Theme` (default `System`), and the workspace list lives in its own `workspaces.json` behind `IWorkspaceService`. Nothing new is persisted, and no schema changes.
- **`SettingsLoader` cannot report that the file was missing.** `ConfigLoadResult<T>` carries `Failure`, `QuarantinedFilePath` and `PersistenceDisabled` — there is no "did the file exist" flag, so the check has to happen in `App` before the loader runs.
- **A corrupt settings file is quarantined by moving it aside**, which leaves `AppPaths.SettingsFile` absent. Checking existence *after* the load would therefore mistake a corruption for a first run and re-onboard someone who has been using the app for months. The check must come **first**.
- **Settings are not written at startup.** Today `settings.json` is only written when the settings window closes (`SettingsWindowService.ShowAsync`), which is what makes its absence a reliable first-run signal — and what makes writing it at the end of the dialog necessary, or the dialog would return on every launch.
- **The ContentDialog host lives in `MainWindow`**, so the dialog can only be shown from `MainWindowViewModel.OnOpenedAsync` — the method that already runs after the window is open, and where the startup InfoBar notice is shown.
- **`ContentDialog` closes on `Escape` or a scrim click, returning `DialogResult.None`.** That is built into the control; the dismissal design below works with it rather than against it.
- Settings applied while the app runs go through existing seams: `IThemeService.Apply` for the theme, and `Documents.OnSettingsChanged()` / `Preview.OnSettingsChanged()` / `FileTree.OnSettingsChangedAsync()` for the rest — the same three calls `OpenSettingsAsync` makes when the settings window closes.
- Workspace creation must reuse `IFolderPicker`, `WorkspaceValidation` and `IWorkspaceService`. No second validation path.
- **No new package.**

## Design

**Detection** — in `App.OnFrameworkInitializationCompleted`, before `SettingsLoader.Load`:

```
bool isFirstRun = !File.Exists(AppPaths.SettingsFile);
```

registered in the container as a small named singleton (a `FirstRunState` record with a `bool IsFirstRun`, rather than a bare `bool`, so the registration is unambiguous and injectable). No property is added to `AppSettings`: the file's absence *is* the signal, so there is nothing to migrate and an existing `settings.json` is untouched and forward-compatible.

**Consequence, accepted deliberately:** a 1.0.0 install that never opened the Settings window has no `settings.json` and will see the dialog once after upgrading. That is the intended behaviour, not a regression — those users have never been asked.

**`FirstRunViewModel`** (`Desktop/ViewModels`, transient) — `ObservableObject` over the live `IConfig<AppSettings>`, following `SettingsWindowViewModel` exactly: no working copy, each property reads and writes the settings object in place so a change is live immediately.

- `VimEnabled` — `bool`, defaults to the current setting (`true`).
- `Theme` — `AppTheme`, applied through `IThemeService` on set, so the dialog itself repaints as it is chosen.
- `WorkspaceFolder` — `string?`, plus `ChooseFolderCommand` (`AsyncRelayCommand`) driving `IFolderPicker`. Displays the chosen path, or "No folder chosen yet".
- `CompleteAsync()` — creates the workspace when a folder was chosen (through `IWorkspaceService`, validated), then persists the settings through the same `IConfig<AppSettings>` save path `SettingsWindowViewModel.SaveAsync` uses, returning a failure string on error.

**`FirstRunView`** (`Desktop/Views`, `UserControl`, `x:DataType="vm:FirstRunViewModel"`) — a short intro line, then three `controls:SettingsCard` rows in a `StackPanel`, deliberately the same controls the settings window uses so the dialog looks native to the app on day one:

| Row | Control | Icon |
|---|---|---|
| Vim emulation | `ToggleSwitch` | `{ei:IconGeometry Keyboard}` |
| Theme | `ComboBox` over `AppTheme` | `{ei:IconGeometry Moon}` |
| Notes folder | `Button` "Choose folder…" + the chosen path | `{ei:IconGeometry FolderOpen}` |

**`IFirstRunDialogService` / `FirstRunDialogService`** (`Desktop/Services`, singleton) — the `WorkspaceManagerDialogService` shape again: builds the view over a fresh ViewModel and calls `IContentDialogService.ShowAsync` with `Title = "Welcome"`, `PrimaryButtonText = "Get started"`, `CloseButtonText = "Skip"`.

**Dismissal — every exit is terminal.** `Get started`, `Skip`, `Escape` and a scrim click all lead to `CompleteAsync()`, which writes `settings.json`. The dialog therefore fires exactly once and never nags. The cost is accepted knowingly: an accidental `Escape` on the very first launch loses the prompt, and all three values remain one click away in Settings.

**Where it runs** — first in `MainWindowViewModel.OnOpenedAsync`, **before** `RestoreDocumentsAsync()`. On a genuine first run there is no session to restore, and choosing the workspace before the tree first loads avoids reading the folders twice. The existing startup-notice InfoBar keeps its place after the restore.

**After completion**, the same three seams `OpenSettingsAsync` uses are called (`Documents.OnSettingsChanged()`, `Preview.OnSettingsChanged()`, `await FileTree.OnSettingsChangedAsync()`) so vim and the new workspace take effect without a restart. The theme has already been applied live.

**Failure handling** — a failed settings write is reported with `IContentDialogService.ShowMessageAsync`, after the first dialog has closed, exactly as `SettingsWindowService` reports one. An invalid or unreadable chosen folder surfaces through the existing `WorkspaceValidationException` path rather than a new message.

**Logging** — one `Information` entry when the dialog is shown, and one recording what it wrote (vim on/off, theme, whether a workspace was added). This is the only evidence a first run leaves behind.

## Acceptance criteria

- On a machine with no `%APPDATA%\Enigma.MarkdownEditor\settings.json`, the dialog appears once, just after the main window opens.
- Toggling vim, changing the theme and choosing a folder all take effect immediately — theme repaints the dialog itself, and on close the tree shows the chosen folder and the editor honours the vim choice, with no restart.
- `Skip`, `Escape` and a scrim click each write `settings.json`; restarting the application does **not** show the dialog again in any of the three cases.
- A **corrupt** `settings.json` that gets quarantined does **not** trigger the dialog — the existing "settings could not be read" InfoBar appears instead, as it does today.
- Choosing no folder is allowed: the app opens to its existing empty state, and the dialog is still done.
- A settings write failure shows a message and does not take the application down.
- `Core.UnitTests`: nothing new is added to `AppSettings`, so coverage here is limited to confirming the settings round-trip is unchanged.
- `Desktop.UnitTests`: `FirstRunViewModel` writes vim and theme to the live settings object and applies the theme through a fake `IThemeService`; `CompleteAsync` adds the workspace through a fake `IWorkspaceService` when a folder was chosen and adds none when it was not; a write failure returns a message rather than throwing. A headless view test asserts the `FirstRunView` bindings resolve. The first-run gate itself is tested as a pure predicate over a temp path (file present / absent), using the existing `TempSettings` fake.

## Dependencies

Independent of `FEATURE-7FA8` — they touch `MainWindowViewModel` and `App` in different places, but neither needs the other. `FEATURE-6EF0` must come after this.

## Out of scope

Editor font size, "show all files", the image folder name and debug logging in the dialog (all discoverable in Settings, none blocking) · a multi-step wizard · a "don't ask again" checkbox · a welcome or sample note · importing settings from another install · re-onboarding on a future upgrade.
