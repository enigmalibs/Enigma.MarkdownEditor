# FEATURE-E31C-PHASE01 — Early hooks & uniform error dialog

## Summary

Unhandled exceptions are now caught from the first line of `Main`, not only from the moment
`CrashReporter.Install()` used to run after the host was built:

- **`Program.Main`** calls `UnhandledExceptionHooks.Install()` (AppDomain and unobserved tasks).
- **`App.OnFrameworkInitializationCompleted`** calls `InstallDispatcher()` before anything else.
- **The router.** Each hook reports to `UnhandledExceptionHooks.Router`, an
  `UnhandledExceptionRouter`. Until the crash reporter attaches, the router writes each report to
  `Trace` and keeps it (32 at most). It replays them when `CrashReporter` attaches, in `Compose`,
  where `Install()` used to be.
- **Exit.** `Detach()` runs at the start of the exit sequence, before the container is disposed.

`CrashReporter` is now the sink (`ICrashReporter.Report`). It logs and flushes on the reporting
thread. AppDomain reports are logged at Critical and get no dialog. For the other two sources it
shows the uniform error dialog on the UI thread:

- **The look** is Enigma.GitClient's: "Something went wrong", Phosphor `Warning` in `#E8A33D`, a
  single default `Close`.
- **The body** is the type and message, "Enigma Markdown Editor is still running", and the log path,
  in a selectable text block.
- **It waits for an open question** on the host (`AttachHost` + the host's `Closed`), so it never
  takes over another dialog.
- **It waits for the host itself** when the error comes before the host exists.
- **One dialog at a time.** Later errors are only logged.
- **A dialog that fails to show** is logged once, and showing is then turned off.

`ErrorDialog` holds the dialog's configuration, so PHASE02's start-up window can use the same one.

## Files/modules touched

- Created:
  - `src/Enigma.MarkdownEditor.Desktop/Services/UnhandledErrorSource.cs`
  - `src/Enigma.MarkdownEditor.Desktop/Services/ICrashReporter.cs`
  - `src/Enigma.MarkdownEditor.Desktop/Services/UnhandledExceptionRouter.cs`
  - `src/Enigma.MarkdownEditor.Desktop/Services/UnhandledExceptionHooks.cs`
  - `src/Enigma.MarkdownEditor.Desktop/Views/ErrorDialog.cs`
  - `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Services/UnhandledExceptionRouterTests.cs`
  - `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Services/CrashReporterTests.cs`
  - `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Fakes/FakeFlushableLoggerHandle.cs`
- Modified:
  - `src/Enigma.MarkdownEditor.Desktop/Services/CrashReporter.cs`
  - `src/Enigma.MarkdownEditor.Desktop/Program.cs`
  - `src/Enigma.MarkdownEditor.Desktop/App.axaml.cs`
  - `tests/Enigma.MarkdownEditor.Desktop.UnitTests/Fakes/FakeContentDialogService.cs` (`Hold`,
    `Release`, `Failure`, `Attempts`)
  - `docs/roadmap.md`, `docs/plan/FEATURE-E31C.md`

## Deviations & follow-ups

- **The router is an instance class** (`UnhandledExceptionRouter`). The static
  `UnhandledExceptionHooks` only subscribes to the events. The plan's "internal test seam" became
  unnecessary: the project has no `InternalsVisibleTo`, and tests build their own router instead of
  touching process-wide state.
- **The two dialog titles are now one.** The unobserved-task dialog used to read "A background
  operation failed". Both sources now use GitClient's title, as the uniform look requires.
- **Status lines.** The plan's status lines were normalized to `**Status:** X` (with the branch on its
  own line) so `roadmap.py set-status` accepts them. The bold-keyword form is refused as legacy.
- **Not covered by a test:** the three event subscriptions themselves (`Install` /
  `InstallDispatcher`). They are global to the test process and cannot be undone. Each handler is two
  lines.
- **The real app was not launched**, to avoid writing to the user's own `%APPDATA%` session.

## Build/test evidence

- `dotnet build Enigma.MarkdownEditor.slnx -c Release --no-incremental -v normal`: **0 Warning(s),
  0 Error(s)**. `grep -ci AVLN` over the log gives **0**.
- `dotnet test --solution Enigma.MarkdownEditor.slnx -c Release`: **3639 passed, 0 failed, 0 skipped**.
  That is 3621 + 6 router tests + 12 reporter / error-dialog tests.
- Fix cycles: **0**.

## Decisions taken at build time

| Question | Chosen | Why |
|---|---|---|
| How the reporter learns the host's state | `CrashReporter.AttachHost(ContentDialog)`, called right after `RegisterHost` | `IContentDialogService` exposes neither `IsOpen` nor `Closed` |
| Waiting for the question to be answered | Subscribe to the host's `Closed`, then post the retry | The closing dialog is still raising its event, and its awaiting caller has yet to run |
| Dispatching the dialog | `Dispatcher.UIThread.Post`, as before | Keeps `AddAppLogging`'s registration free of `IUiDispatcher` (which `AddShell` registers); headless tests pump with `RunJobs()` |
| Message text | Selectable, and says the app is still running | It can be copied into a bug report, and it says what the user most needs to know |
| Documentation sweep (§7) | No change | No doc describes crash handling |
