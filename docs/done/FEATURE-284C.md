# FEATURE-284C — First-run setup

**Status: DONE** · Single phase · Branch: `feature/feature-284c-first-run-setup`
Plan: `docs/plan/FEATURE-284C.md`

## Summary

On the very first start the editor now asks, once, about the three things worth deciding before it is
used: **vim emulation**, **the theme**, and **the folder the notes live in**. Everything else stays in
Settings.

The signal is the absence of `settings.json`. It is read in
`App.OnFrameworkInitializationCompleted` **before** `SettingsLoader.Load` — the loader recovers an
unreadable file by moving it aside, so the same question asked one line later would read a corruption
as a first run and re-onboard someone who has been using the editor for months. The answer is parked
in a `FirstRunState` singleton, exactly as a `StartupNotice` is, and read by the shell once it has a
window.

The dialog itself is the settings window's own three rows (`SettingsCard` + `ToggleSwitch` /
`ComboBox` / a *Choose folder…* button) inside the application's one `ContentDialog`, raised from
`MainWindowViewModel.OnOpenedAsync` **before** the session restore. Every way out of it is terminal —
*Get started*, *Skip*, `Escape` and a click on the scrim all run `CompleteAsync()`, which creates the
workspace and writes `settings.json`. Writing that file is what stops the dialog coming back, so it
fires exactly once and never nags. Afterwards the shell calls the same three seams the settings window
is followed by, so the choices reach the editor, the preview and the tree without a restart; the theme
needed no seam, having been applied as it was chosen.

Nothing new is persisted: `AppSettings` is untouched, no schema changed, and an existing
`settings.json` is read and written exactly as before.

## Files/modules touched

**Created — `src/Enigma.MarkdownEditor.Desktop`**

- `Services/FirstRunState.cs` — the gate as a record over one predicate (`Detect(settingsFilePath)`),
  plus the two states `First` / `Returning`.
- `Services/IFirstRunDialogService.cs`, `Services/FirstRunDialogService.cs` — builds the view over a
  fresh ViewModel, shows the dialog, then applies the answers and reports a failed write through
  `IContentDialogService.ShowMessageAsync`, after the dialog is gone.
- `ViewModels/FirstRunViewModel.cs` — `ObservableObject` over the live `IConfig<AppSettings>`, no
  working copy; `Theme`, `VimEnabled`, `WorkspaceFolder` + `ChooseFolderCommand`, `CompleteAsync`.
- `Views/FirstRunView.axaml`, `Views/FirstRunView.axaml.cs` — three `SettingsCard` rows, logic-free
  code-behind.

**Modified — `src/Enigma.MarkdownEditor.Desktop`**

- `App.axaml.cs` — `FirstRunState.Detect(AppPaths.SettingsFile)` before the settings load, threaded
  through `ComposeAndHandOver` into `AddFirstRun`.
- `Extensions/ServiceCollectionExtensions.cs` — new `AddFirstRun(FirstRunState)`; `FirstRunViewModel`
  (transient) and `IFirstRunDialogService` (singleton) in `AddShell`, plus a `TryAddSingleton`
  fallback of `FirstRunState.Returning` beside the existing `StartupNotice.None` one.
- `ViewModels/MainWindowViewModel.cs` — two new dependencies (`IFirstRunDialogService`,
  `FirstRunState`); `OnOpenedAsync` runs `ShowFirstRunAsync()` before `RestoreDocumentsAsync()`.

**Created — `tests/Enigma.MarkdownEditor.Desktop.UnitTests`**

- `Fakes/FakeFirstRunDialogService.cs` — counts the showings, can fail, and carries an `OnShow` hook a
  test edits the live settings through.
- `Services/FirstRunStateTests.cs` — the gate as a predicate over a temp path, including the
  quarantine trap.
- `ViewModels/FirstRunViewModelTests.cs` — 14 tests over a real settings file in a temp folder.
- `Views/FirstRunViewTests.cs` — 6 headless view tests.

**Modified — `tests/Enigma.MarkdownEditor.Desktop.UnitTests`**

- `ViewModels/MainWindowViewModelTests.cs` — the two new constructor arguments, plus four tests for
  the gate, the seams and the failure path.
- `Extensions/ServiceCollectionExtensionsTests.cs` — three new lifetime rows and three tests for
  `AddFirstRun` and its fallback.

Nothing was added to `Enigma.MarkdownEditor.Core` or its test project.

## Deviations & follow-ups

- **`CompleteAsync` writes the settings even when the workspace could not be created.** The plan reads
  "creates the workspace … then persists the settings … returning a failure string on error", which
  taken strictly would skip the write after a workspace failure — and that would bring the dialog back
  at the next start, contradicting the plan's own "the dialog therefore fires exactly once and never
  nags". The two steps are therefore independent: both are attempted, and both failures are reported
  in one message. Covered by
  `CompleteAsync_ReportsAWorkspaceThatCouldNotBeCreatedAndStillWritesTheSettings`.
- **An inline error line was added for a folder picker that cannot open.** The plan's failure handling
  covers a failed settings write and an invalid folder, but not a picker that throws — and an
  `AsyncRelayCommand` parks a faulted task, which would have left the button looking dead on the
  user's very first screen. It is reported inside the dialog, the way the manage-workspaces dialog
  reports its own errors, because a `ContentDialog` cannot stack a second one on top of itself.
- **`ShowFirstRunAsync` catches broadly and surfaces the failure in the InfoBar.** It runs before the
  session restore, so an unguarded throw would have cost the user their tabs as well as the questions.
  Same shape as `OpenSettingsAsync` and `OpenAboutAsync`.
- **The vim row's icon is `Keyboard`, as the plan specifies.** The settings window's own vim row uses
  `Terminal`. Worth aligning one way or the other at some point; cosmetic, and left as planned.
- **Manual verification still worth doing on a real first run.** `App.axaml.cs` has no test in this
  suite (it never has — the registrations are covered by the composition-root tests instead), so these
  plan criteria are asserted at the seam rather than end to end: that the dialog actually appears just
  after the window opens, that the theme repaints the dialog live, and that `Escape` / a scrim click
  reach `CompleteAsync`. Delete `%APPDATA%\Enigma.MarkdownEditor\settings.json` to try it.
- **No line-ending noise seen.** The repository's `.gitattributes` already normalizes to LF, and every
  file added here is LF; no CRLF recommendation to make.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx` (Debug): **succeeded, 0 errors, 0 warnings.**
- `dotnet test Enigma.MarkdownEditor.slnx`: **1927 passed, 0 failed, 0 skipped** across
  `Enigma.MarkdownEditor.Core.UnitTests` and `Enigma.MarkdownEditor.Desktop.UnitTests`. **37 of them
  are new** — 27 in the three `FirstRun*` classes (verified with
  `--filter-class "*FirstRun*"`: 27 total, 27 succeeded), 4 in `MainWindowViewModelTests`, 6 in
  `ServiceCollectionExtensionsTests`.
- A Release build could not be completed here: the running `Enigma.MarkdownEditor.Desktop` process
  holds `bin/Release/net10.0/Enigma.MarkdownEditor.Core.dll` open (MSB3021/MSB3027, a file lock). Not
  a code failure — the same sources build clean in Debug and the suite is green.
