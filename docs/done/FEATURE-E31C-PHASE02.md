# FEATURE-E31C-PHASE02 — Start-up failure dialog

## Summary

A start-up that throws now leaves the user a window carrying the error dialog. It no longer ends the
process from `Main` with nothing on screen.

- **`App.OnFrameworkInitializationCompleted`** runs the whole start-up through
  `StartupGuard.Run(() => Start(desktop), window => desktop.MainWindow = window, Router,
  AppPaths.LogFile)`. The start-up covers the first-run probe, the three config loads, the theme and
  `Compose`.
- **On a throw:**
  - the exception is reported as the new `UnhandledErrorSource.Startup`. `CrashReporter` logs it at
    Critical and flushes when it is attached, and shows no dialog on the never-shown main window's
    host. Otherwise the router traces it and keeps it;
  - the lifetime gets a `StartupErrorWindow` as its main window. That window is a 640 × 420
    centred window with the app's icon and title and a `ContentDialog` host with the main window's
    bounds. On open it shows `ErrorDialog` (PHASE01's uniform look) and closes itself however the
    dialog is dismissed. As the only window, closing it ends the process.
- **The message** names the log file when the reporter had attached. Otherwise it says the start-up
  failed before logging started, so the details could not be written to the log.
- **`UnhandledExceptionRouter.IsAttached`** tells the two cases apart.

## Files/modules touched

- Created:
  - `src/Enigma.MarkdownEditor.Desktop/Views/StartupErrorWindow.axaml`
  - `src/Enigma.MarkdownEditor.Desktop/Views/StartupErrorWindow.axaml.cs`
  - `src/Enigma.MarkdownEditor.Desktop/Services/StartupGuard.cs`
  - `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Services/StartupGuardTests.cs`
  - `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Views/StartupErrorWindowTests.cs`
  - `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Fakes/RecordingCrashReporter.cs` (moved out of the
    router tests to be shared)
- Modified:
  - `src/Enigma.MarkdownEditor.Desktop/App.axaml.cs`: the start-up goes into `Start(desktop)`, run
    under the guard
  - `src/Enigma.MarkdownEditor.Desktop/Services/UnhandledErrorSource.cs` (`Startup`)
  - `src/Enigma.MarkdownEditor.Desktop/Services/UnhandledExceptionRouter.cs` (`IsAttached`)
  - `src/Enigma.MarkdownEditor.Desktop/Services/CrashReporter.cs` (the `Startup` branch)
  - `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Services/CrashReporterTests.cs`
  - `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Services/UnhandledExceptionRouterTests.cs`
  - `docs/roadmap.md`, `docs/plan/FEATURE-E31C.md`

## Deviations & follow-ups

- **A dedicated `Startup` source**, not the plan's `Dispatcher`. A start-up failure is not a UI-thread
  error the editor survives. Reporting it as one would have logged the wrong message and queued a
  dialog on a host that never appears.
- **The config loads moved inside the desktop-lifetime branch** (into `Start`), so the guard covers
  them too. The only behaviour change is for a non-desktop lifetime (none ships), which no longer
  reads the files.
- **Not exercised end to end in the real app.** `AppPaths` resolves through
  `Environment.GetFolderPath`, which ignores a redirected `APPDATA`, so a forced-failure launch would
  have run against the user's own settings and session. The guard, the window and the reporter are
  each covered by headless tests. A manual check belongs in `docs/RELEASE.md` (`FEATURE-B260`).

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`: **0 Warning(s),
  0 Error(s)**. `grep -ci AVLN` over the log gives **0**, so the window's parameterless constructor
  keeps the XAML loader quiet.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3648 passed, 0 failed, 0 skipped**.
  That is 3639 plus:
  - 2 `Startup` cases in the reporter theories;
  - 1 `IsAttached` test;
  - 4 `StartupGuardTests`;
  - 2 `StartupErrorWindowTests`.
- Fix cycles: **0**.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| How App and the guard meet | `StartupGuard.Run(start, setMainWindow, router, logFile)` | Testable without the real `App` or a fake lifetime |
| The window closes on any dismissal | Yes, in a `finally` | A window with nothing on it would keep a failed process alive for ever |
| Window size | 640 × 420, centred, minimum 480 × 320 | Room for the card at its minimum width with the scrim around it |
| Documentation sweep (§7) | No change | No doc describes start-up failure handling |
