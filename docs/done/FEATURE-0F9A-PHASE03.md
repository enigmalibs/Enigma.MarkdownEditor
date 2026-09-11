# FEATURE-0F9A-PHASE03 — Settings, logging & crash handling

**Status: DONE** · Branch: `feature/feature-0f9a-phase03-settings-logging` · Plan: `docs/plan/FEATURE-0F9A.md`

This is the **final phase** of FEATURE-0F9A, so the item's own roadmap row flips to `DONE` with it.

## Summary

Gave the shell from PHASE02 a memory, a log and a safety net: an `AppSettings` class persisted as
hand-editable JSON under the per-user application-data folder, a settings window of `SettingsCard`
rows that switches the theme live, an NLog-backed rolling log file behind `ILogger<T>`, and three
crash handlers that log, flush and report.

Two decisions were taken with the user before building (both were the recommended option):

1. **An unreadable settings file is quarantined.** It is moved to `settings.invalid.json`, the editor
   starts on defaults, and a warning InfoBar names the file and lists what was wrong with it.
2. **Crash coverage goes beyond the plan's two handlers.** Avalonia's
   `Dispatcher.UnhandledException` is handled as well, and marked `Handled`, so a bug in one event
   handler no longer takes an editor full of unsaved text down with it.

## Files/modules touched

### Created — `src/Enigma.MarkdownEditor.Core/Settings`

| File | Notes |
|---|---|
| `AppTheme.cs` | `System` / `Dark` / `Light`. Persisted by name, so the numeric values may be reordered but the member names may not. |
| `AppSettings.cs` | The ten planned settings with their defaults, plus DataAnnotations for range/required-ness. The property defaults *are* the application's defaults — a missing or quarantined file falls back to exactly this object, so no default is written down twice. |
| `AppSettingsValidator.cs` | One rule, deliberately: `ImageFolderName` must be a single folder name. Ranges stay on the properties as attributes — repeating them here would give one broken value two messages. |
| `AppPaths.cs` | `settings.json`, `settings.invalid.json` and `logs/app.log`, all under one per-user folder via `ConfigPaths.InApplicationData`, so uninstalling is one folder to delete and a support request is one folder to zip. |
| `SettingsLoader.cs` | Load-or-recover. See *Deviations* 1 for why the load is eager and why recovery cannot be done in place. |
| `SettingsLoadResult.cs` | Always a readable `IConfig<AppSettings>`, plus the failure, the quarantine path, and whether persistence is available at all. |

### Created — `src/Enigma.MarkdownEditor.Desktop`

| File | Notes |
|---|---|
| `Services/IThemeService.cs`, `Services/ThemeService.cs` | Drives `Application.RequestedThemeVariant`. `AppTheme.System` maps to `ThemeVariant.Default`, **not** a snapshot of the current OS setting — that is what keeps the editor following a light/dark switch made while it runs. |
| `Services/StartupNotice.cs` | A message parked for the shell to show. Composition runs before any window exists, so a problem found there has nowhere to go at the moment it is found. |
| `Services/SettingsStartupNotice.cs` | Turns a `SettingsLoadResult` into that message: defaults are in use, where the old file went, and every validation error (capped at five). |
| `Services/CrashReporter.cs` | The three handlers. Each logs then **flushes** through `IFlushableLoggerHandle` — a buffered entry when the process dies is an entry that never existed. Re-entrancy guarded so a fault inside a render loop cannot queue a hundred dialogs. |
| `Services/ISettingsWindowService.cs`, `Services/SettingsWindowService.cs` | Owns the settings window so no ViewModel has to know a `Window` type. Takes three factories rather than instances: a closed window cannot be reshown, and the owner must be resolved late because this service is built while the main window still is. |
| `ViewModels/SettingsWindowViewModel.cs` | One property per setting, over the **live** settings object — no working copy, which is what lets the theme apply as it is set. `SaveAsync` returns a user-facing message instead of throwing. |
| `Views/SettingsWindow.axaml(.cs)` | Four `SettingsCardExpander` groups (Appearance / Editor / Behaviour / Advanced). Code-behind is one Close handler. |

### Modified

| File | Change |
|---|---|
| `App.axaml.cs` | Settings are loaded **before** the host builder, because composition depends on them; then `AddAppSettings` / `AddAppLogging` / `AddEnigmaServices` / `AddShell`, crash handlers installed first, theme applied before the window is shown. |
| `App.axaml` | The `RequestedThemeVariant` comment now describes what actually drives it. |
| `Extensions/ServiceCollectionExtensions.cs` | `AddAppSettings`, `AddAppLogging` (NLog file target + `ClearProviders` + `SetMinimumLevel` + `CrashReporter`), and `AddShell` extended with the theme service, the settings window pair and a `TryAdd` fallback notice. |
| `ViewModels/MainWindowViewModel.cs` | `OpenSettingsCommand`, `OnOpenedAsync` (shows the start-up notice), the Debug-only `ThrowTestExceptionCommand`, and an `ILogger`. See *Deviations* 3. |
| `Views/MainWindow.axaml` | Toolbar split into a `Grid` so the gear (and the Debug-only bug button) sit at the trailing edge; the `.toolbar` style selector became `:is(Button).toolbar` so it reaches `Button` as well as `ToggleButton`. |
| `Views/MainWindow.axaml.cs` | `OnOpened` calls the ViewModel's `OnOpenedAsync`. |
| `Enigma.MarkdownEditor.Core.csproj` | `Enigma.Configuration` reference. |
| `Enigma.MarkdownEditor.Desktop.csproj` | `Enigma.Logging.NLog` reference. |
| `Directory.Packages.props` | Removed PHASE01's stale "not published on nuget.org, PHASE03 is blocked" note — both packages are on nuget.org at 1.0.0 — and moved the `Enigma.Logging.NLog` pin out of the Avalonia coupled group, where it did not belong: it is an NLog/MEL adapter with no Avalonia dependency. |
| `docs/roadmap.md`, `docs/plan/FEATURE-0F9A.md` | PHASE03 → `IN PROGRESS` → `DONE`; the item → `DONE`. The whole roadmap table was re-padded: with no `IN PROGRESS` row left, the Status column shrinks from 11 to 6 characters, which invalidates every row's padding. |

### Created / modified — tests (88 total, was 18)

| File | Notes |
|---|---|
| `Core.UnitTests/Settings/AppSettingsTests.cs` | 2 tests: every default, and that the monospace stack names a family for Windows *and* Linux. |
| `Core.UnitTests/Settings/TempSettingsFolder.cs` | Throw-away folder helper, so the settings tests drive the real read/write/quarantine pipeline rather than a stand-in. |
| `Core.UnitTests/Settings/AppSettingsRoundTripTests.cs` | 4 tests: round-trip through a real file, the enum written by name (the file has to stay hand-editable), a missing file, a partial file. |
| `Core.UnitTests/Settings/SettingsLoaderTests.cs` | 8 tests: four kinds of broken file all quarantined; both validation halves reported together; the recovered config still writes to the *real* path; a second quarantine overwrites the first; an empty file counts as missing; an unknown key is ignored so a downgrade is not bricked. |
| `Core.UnitTests/Settings/AppSettingsValidatorTests.cs` | 10 tests: plain names accepted, paths/drives/`.`/`..` rejected, and an empty value reported **once** by the attribute rather than twice in two vocabularies. |
| `Desktop.UnitTests/Fakes/` | `FakeSettingsWindowService` (with an injectable failure), `FakeInfoBarService`, `FakeThemeService`. |
| `Desktop.UnitTests/Services/SettingsStartupNoticeTests.cs` | 8 tests over the message the user actually reads. |
| `Desktop.UnitTests/Services/ThemeServiceTests.cs` | 5 tests, the interesting one being that `System` stays dynamic. |
| `Desktop.UnitTests/ViewModels/SettingsWindowViewModelTests.cs` | 11 tests against a real settings file: live mutation, live theme apply, no re-apply on a no-op, a cleared numeric editor keeping the old value, a successful save, and a rejected value reported with the file left untouched. |
| `Desktop.UnitTests/ViewModels/MainWindowViewModelTests.cs` | Existing 6 kept, ctor updated, 5 added — including that a failure opening settings is surfaced rather than swallowed (see *Deviations* 3). |
| `Desktop.UnitTests/Extensions/ServiceCollectionExtensionsTests.cs` | Extended to 24: lifetimes for the new registrations, the loaded configuration registered *as the same instance*, the notice surviving `AddShell`'s `TryAdd`, `ILogger<T>` routed through the flushable handle, and the host's default providers cleared. |

## Deviations & follow-ups

1. **The plan's `InitializeEnigmaConfigurationAsync` warm-up is not used; settings are loaded eagerly
   and registered as an instance.** Two reasons, and the first is a defect in the plan rather than a
   preference. (a) The plan's own acceptance criterion — *"invalid file → validation error surfaced,
   app still starts on defaults"* — is **not achievable** with the warm-up: `Config<T>` caches nothing
   on a failed load, so catching the warm-up exception leaves a singleton whose every later `Value`
   read re-reads the same broken file and throws again. A deliberate fallback is required, and it has
   to happen while there is still somewhere to go. (b) Composition depends on the settings — the log
   level and the theme are both decided before the first window exists — so the file has to be read
   before the container is built. One eager read, registered as the instance every consumer shares,
   is also what lets the settings window mutate the object the rest of the app is holding.
   `services.AddSingleton<IConfig<T>>(instance)` is a documented registration in the library's own DI
   guide.
2. **Recovery quarantines to a stamped name if the usual one is taken**, and if the file can be read
   neither in place nor moved aside, the configuration falls back to a scratch path with
   `PersistenceDisabled` set, which the notice reports. This triple-fault path is untested by the
   suite — it needs a locked file or an unwritable folder — but it exists so a broken settings file
   can never crash start-up.
3. **`AsyncRelayCommand` swallows exceptions, and that cost real time — worth knowing about.** The
   first gear click did nothing at all and logged nothing: the command parks a faulted task instead of
   letting the exception escape. `OpenSettingsAsync` now catches, logs and surfaces an InfoBar, and
   there is a test for it. Any future async command needs the same treatment.
4. **The "throw test exception" hook lives on the toolbar, not in the settings window.** It was built
   in the settings window first, and verification showed why that is wrong: the `ContentDialog` host is
   a control in the *main* window, so a dialog raised from behind a modal child is logged and flushed
   but never seen. Moving it made the plan's *"and an error dialog"* criterion verifiable. **The
   underlying limitation remains**: any crash raised while the settings window is open is logged and
   flushed but its dialog is invisible. Follow-up worth considering — give the settings window its own
   `ContentDialog` host, or fall back to a native message box when a modal child is open.
5. **`ShowDiagnostics` gates the button, but `ThrowTestExceptionCommand` is compiled in both
   configurations.** Compiled bindings are resolved at build time, so a command the XAML references
   cannot be `#if`-conditional. Nothing in a Release build can reach it — verified: the button is
   absent from the Release toolbar.
6. **Settings are written when the settings window closes, not per keystroke.** Changes apply to the
   live object immediately (which is what makes the theme switch live); the single `WriteAsync` runs
   after the modal is gone, so a rejected value can be reported through a dialog that is actually
   reachable. Consequence: a crash with the settings window open loses that session's changes.
7. **`DebugLogging` takes effect on the next start.** NLog's level rules are built with the provider,
   so honouring it live would mean rebuilding the logging pipeline. The row says so in its description.
8. **Eight of the ten settings are shown disabled**, per the plan ("only settings that already have
   consumers are shown enabled"). Each row names the work item that will enable it. Only `Theme` and
   `DebugLogging` have consumers today. Drop the `IsEnabled="False"` as each feature lands.
9. **No "open log folder" button.** The Advanced group shows the settings-file and logs-folder paths as
   selectable text instead. Launching a folder is not in the plan and would pull `TopLevel.Launcher`
   plumbing into a ViewModel; worth adding when the plan calls for it.
10. **`TaskScheduler.UnobservedTaskException` has no test hook.** It fires on garbage collection, so a
    button for it would be non-deterministic. The handler is wired and reviewed but was not exercised
    at run time — unlike the other two paths.
11. **Documentation freshness sweep: offered, declined.** The candidates were a refreshed README status
    block (it still describes only the placeholder shell, so it now understates what works) and a short
    README section naming the per-user folder the app writes to. You chose to skip both, so no prose
    doc was touched in this commit. Still worth doing before the 1.0.0 release item (FEATURE-5B28).
12. **Line endings (recommendation only, no action taken).** The new files are CRLF in the working tree
    while `.editorconfig` says `end_of_line = lf`; `.gitattributes` (`* text=auto eol=lf`) normalizes
    them in the index on commit, so no separate pass is expected. If you want it applied explicitly,
    the git-repo-hygiene command set — **not run here** — is `git add --renormalize .`, `git status`,
    `git commit -m "Normalize all line endings to LF"`.

## Build/test evidence

```
> dotnet build Enigma.MarkdownEditor.slnx -c Release
Build succeeded.
    0 Warning(s)
    0 Error(s)

> dotnet build -c Release -t:Rebuild -v:n     (src/Enigma.MarkdownEditor.Desktop)
    grep -icE "AVLN[0-9]+" over the full normal-verbosity log → 0
    0 Warning(s)

> dotnet test --solution Enigma.MarkdownEditor.slnx
Test run summary: Passed!
  total: 88   failed: 0   succeeded: 88   skipped: 0
```

88 tests = PHASE01's 3 smoke + PHASE02's 15 + 70 added here (24 Core settings, 46 Desktop).

### How the run-time criteria were verified

The machine was unlocked this time, so the **real Windows executable** was driven end to end rather
than a headless probe: UI Automation to locate controls, `PostMessage` for input that does not depend
on which window has focus, and `PrintWindow(PW_RENDERFULLCONTENT)` to capture the window even when
occluded. Screenshots are in the session scratchpad, not the repo.

| Criterion | Result |
|---|---|
| Settings persist across restarts (hand-editable JSON in per-user app-data) | **Met.** Theme switched to `Light` in the running app → on Close, `%APPDATA%\Enigma.MarkdownEditor\settings.json` contained `"Theme": "Light"` with all ten settings, indented, enum by name. Restarted (Release → Debug) and the editor came up Light from the first frame. |
| Invalid file → validation error surfaced, app still starts on defaults | **Met.** Wrote a file broken two ways at once (`EditorFontSize: 9000`, `ImageFolderName: "nested/folder"`). The editor started on defaults; the file was moved to `settings.invalid.json` with its content byte-identical; a warning InfoBar read "Settings could not be read", named the quarantine path, and listed **both** validation errors. |
| Theme setting switches variant live (no restart) | **Met.** Selecting `Light` in the ComboBox repainted the settings window *and* the main window — toolbar, three panes, status bar — with no restart and no rebuild of the visual tree. |
| Log file created; forced test exception produces a logged, flushed entry and an error dialog | **Met.** `logs\app.log` created on first start with the host and "Enigma Markdown Editor started." entries. Pressing the Debug-only bug button logged `ERROR \| Unhandled exception on the UI thread.` with the full stack trace, **readable from disk while the process was still running** (i.e. flushed, not buffered), showed a "Something went wrong" dialog naming the exception and the log path, and **the app stayed alive** — `Handled = true` doing its job. |
| Core.UnitTests: settings defaults + round-trip against a temp path | **Met.** `AppSettingsTests` (defaults), `AppSettingsRoundTripTests` (round-trip, hand-editability, missing and partial files). |

The `%APPDATA%\Enigma.MarkdownEditor` folder created by this verification was deleted afterwards, so
the profile is back to its pre-run state.
